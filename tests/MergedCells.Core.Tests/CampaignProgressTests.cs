namespace MergedCells.Core.Tests;

// Par 3 with the shipped thresholds: 3 moves earn 3 stars, 4–5 earn 2, 6–7 earn 1, 8 or more earn 0
public class CampaignProgressTests
{
    private static readonly StarsConfig ShippedStars = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 50,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 100,
    };

    private static GameConfig Config(int gatePercent = 55, IReadOnlyDictionary<int, StarsConfig>? chapterStars = null) =>
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json"))) with
        {
            Stars = ShippedStars,
            Campaign = new CampaignConfig { GatePercent = gatePercent, ChapterStars = chapterStars ?? new Dictionary<int, StarsConfig>() },
        };

    private static Level LevelWith(int id, int chapter, int par = 3) => new(id, $"Level {id}", chapter, TestMaps.Example(), par);

    // Chapters of 2, 2 and 1 levels: IDs 1–2, 3–4 and 5
    private static Campaign Campaign(int firstPar = 3) => MergedCells.Core.Campaign.Parse(
        """{"chapters":[{"name":"A","levels":[1,2]},{"name":"B","levels":[3,4]},{"name":"C","levels":[5]}]}""",
        [LevelWith(1, 1, firstPar), LevelWith(2, 1), LevelWith(3, 2), LevelWith(4, 2), LevelWith(5, 3)]);

    private static Level Level(Campaign campaign, int id) => campaign.Levels.Single(level => level.Id == id);

    private static SaveGame Finished(Campaign campaign, params (int Id, int Moves)[] finishes)
    {
        var save = new SaveGame();
        foreach (var (id, moves) in finishes)
        {
            save.RecordFinish(Level(campaign, id), moves, ShippedStars);
        }
        return save;
    }

    private static string Numbers(IEnumerable<Chapter> chapters) => string.Join(",", chapters.Select(chapter => chapter.Number));

    [Fact]
    public void Fresh_save_has_no_stars()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, new SaveGame(), Config());

        Assert.Equal((0, (int?)null), (progress.TotalStars, progress.Stars(Level(campaign, 1))));
    }

    [Fact]
    public void Fresh_save_opens_only_chapter_1()
    {
        var progress = CampaignProgress.Of(Campaign(), new SaveGame(), Config());

        Assert.Equal(("1", 1), (Numbers(progress.OpenChapters), progress.Furthest.Number));
    }

    [Fact]
    public void Total_sums_every_level_s_stars()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (1, 3), (2, 5), (3, 8)), Config());

        Assert.Equal(5, progress.TotalStars);
    }

    [Theory]
    [InlineData(55, 2, 4)]
    [InlineData(55, 3, 7)]
    [InlineData(100, 3, 12)]
    [InlineData(0, 3, 0)]
    public void Gate_is_the_rounded_up_percentage_of_earlier_stars(int percent, int chapter, int expected)
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, new SaveGame(), Config(percent));

        Assert.Equal(expected, progress.Gate(campaign.Chapters[chapter - 1]));
    }

    [Fact]
    public void Chapter_1_s_gate_is_0()
    {
        var campaign = Campaign();

        Assert.Equal(0, CampaignProgress.Of(campaign, new SaveGame(), Config(100)).Gate(campaign.Chapters[0]));
    }

    [Fact]
    public void Chapter_opens_when_the_total_reaches_its_gate()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (1, 3), (2, 6)), Config());

        Assert.Equal((4, true, 2), (progress.TotalStars, progress.IsOpen(campaign.Chapters[1]), progress.Furthest.Number));
    }

    [Fact]
    public void Chapter_stays_locked_one_star_short()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (1, 3), (2, 8)), Config());

        Assert.Equal((3, false), (progress.TotalStars, progress.IsOpen(campaign.Chapters[1])));
    }

    [Fact]
    public void A_zero_star_finish_counts_as_played()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (1, 8)), Config());

        Assert.Equal(((int?)0, 2), (progress.Stars(Level(campaign, 1)), progress.Current(campaign.Chapters[0])?.Id));
    }

    [Fact]
    public void Chapter_thresholds_change_a_level_s_stars()
    {
        var campaign = Campaign();
        // A 2-star limit of 3 and a 1-star limit of 5 for chapter 1, so 5 moves earn 1 star instead of 2
        var strict = new StarsConfig { TwoStarMinExtraMoves = 0, TwoStarExtraParPercent = 0, OneStarMinExtraMoves = 2, OneStarExtraParPercent = 0 };

        var progress = CampaignProgress.Of(
            campaign, Finished(campaign, (1, 5)), Config(chapterStars: new Dictionary<int, StarsConfig> { [1] = strict }));

        Assert.Equal((int?)1, progress.Stars(Level(campaign, 1)));
    }

    [Fact]
    public void Current_is_the_first_unfinished_level_in_order()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (1, 3)), Config());

        Assert.Equal(2, progress.Current(campaign.Chapters[0])?.Id);
    }

    [Fact]
    public void Current_skips_finished_levels_after_an_unfinished_one()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (2, 3)), Config());

        Assert.Equal(1, progress.Current(campaign.Chapters[0])?.Id);
    }

    [Fact]
    public void Current_is_null_when_every_level_is_finished()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (1, 3), (2, 9)), Config());

        Assert.Null(progress.Current(campaign.Chapters[0]));
    }

    [Fact]
    public void Next_stays_within_a_chapter()
    {
        var campaign = Campaign();

        Assert.Equal(2, CampaignProgress.Of(campaign, new SaveGame(), Config()).Next(Level(campaign, 1))?.Id);
    }

    [Fact]
    public void Next_crosses_into_an_open_chapter()
    {
        var campaign = Campaign();

        var progress = CampaignProgress.Of(campaign, Finished(campaign, (1, 3), (2, 5)), Config());

        Assert.Equal(3, progress.Next(Level(campaign, 2))?.Id);
    }

    [Fact]
    public void Next_is_null_into_a_locked_chapter()
    {
        var campaign = Campaign();

        Assert.Null(CampaignProgress.Of(campaign, new SaveGame(), Config()).Next(Level(campaign, 2)));
    }

    [Fact]
    public void Next_is_null_after_the_last_level()
    {
        var campaign = Campaign();

        Assert.Null(CampaignProgress.Of(campaign, new SaveGame(), Config(0)).Next(Level(campaign, 5)));
    }

    [Fact]
    public void Stale_best_counts_as_unplayed()
    {
        var save = Finished(Campaign(), (1, 3));
        // The same ID with a different par, so the saved fingerprint no longer matches
        var changed = Campaign(firstPar: 4);

        Assert.Null(CampaignProgress.Of(changed, save, Config()).Stars(Level(changed, 1)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Gate_percentage_out_of_range_throws(int percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignProgress.Of(Campaign(), new SaveGame(), Config(percent)));
    }
}
