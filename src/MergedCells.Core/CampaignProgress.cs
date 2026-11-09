namespace MergedCells.Core;

/// <summary>
/// The player's progress through the campaign, worked out from the saved bests: each level's stars with its chapter's
/// thresholds, the star total, the chapter gates, and which levels are current and next. A snapshot; nothing here is
/// saved. Main thread only, like <see cref="SaveGame"/>.
/// </summary>
public sealed class CampaignProgress
{
    private readonly Campaign campaign;
    private readonly Dictionary<int, int?> starsById;
    private readonly Dictionary<int, int> gateByChapter;

    private CampaignProgress(Campaign campaign, Dictionary<int, int?> starsById, Dictionary<int, int> gateByChapter)
    {
        this.campaign = campaign;
        this.starsById = starsById;
        this.gateByChapter = gateByChapter;
        TotalStars = starsById.Values.Sum(stars => stars ?? 0);
        OpenChapters = campaign.Chapters.Where(chapter => gateByChapter[chapter.Number] <= TotalStars).ToArray();
    }

    public int TotalStars { get; }

    // Always the first few chapters, because gates only grow from one chapter to the next
    public IReadOnlyList<Chapter> OpenChapters { get; }

    public Chapter Furthest => OpenChapters[^1];

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> when the config's gate percentage is outside 0 to 100.</summary>
    public static CampaignProgress Of(Campaign campaign, SaveGame save, GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(config);
        var percent = config.Campaign.GatePercent;
        ArgumentOutOfRangeException.ThrowIfNegative(percent, nameof(config.Campaign.GatePercent));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100, nameof(config.Campaign.GatePercent));

        var starsById = new Dictionary<int, int?>();
        var gateByChapter = new Dictionary<int, int>();
        var earlierLevels = 0;
        foreach (var chapter in campaign.Chapters)
        {
            gateByChapter[chapter.Number] = (percent * 3 * earlierLevels + 99) / 100;
            var stars = config.StarsFor(chapter.Number);
            foreach (var level in chapter.Levels)
            {
                starsById[level.Id] = save.Stars(level, stars);
            }
            earlierLevels += chapter.Levels.Count;
        }
        return new CampaignProgress(campaign, starsById, gateByChapter);
    }

    // Null when the level hasn't been finished as it is now; a 0-star finish is 0
    public int? Stars(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return starsById.TryGetValue(level.Id, out var stars)
            ? stars
            : throw new ArgumentException($"Level {level.Id} isn't in the campaign.", nameof(level));
    }

    public int Gate(Chapter chapter) => gateByChapter[chapter.Number];

    public bool IsOpen(Chapter chapter) => Gate(chapter) <= TotalStars;

    // The first level in the chapter's order without a finish, or null when every level in it is finished
    public Level? Current(Chapter chapter) => chapter.Levels.FirstOrDefault(level => starsById[level.Id] is null);

    // The following level in campaign order when its chapter is open, otherwise null
    public Level? Next(Level level)
    {
        var chapter = campaign.ChapterOf(level);
        var index = campaign.NumberInChapter(level);
        if (index < chapter.Levels.Count)
        {
            return chapter.Levels[index];
        }
        if (chapter.Number == campaign.Chapters.Count)
        {
            return null;
        }
        var following = campaign.Chapters[chapter.Number];
        return IsOpen(following) ? following.Levels[0] : null;
    }
}
