using System.Globalization;
using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class GameConfigTests
{
    // Every group with a value of 1, so each invalid case below breaks exactly one thing
    private const string CameraGroup =
        """{"readableSquareSize":1,"closestSquareSize":1,"followZonePercent":1,"glideSeconds":1,"openingSeconds":1,"openingHoldSeconds":1,"zoomStep":1,"gestureSlop":1}""";

    private const string StarsGroup =
        """{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}""";

    private const string SlideGroup = """{"squaresPerSecond":1,"maxSeconds":1,"trapShrinkSeconds":1,"trapPauseSeconds":1,"trapGrowSeconds":1,"teleportShrinkSeconds":1,"teleportGrowSeconds":1}""";

    private const string CampaignGroup = """{"gatePercent":1,"chapterGatePercent":{},"chapterStars":{},"levelStars":{},"undoLimit":1}""";

    private const string GeneratorGroup =
        """{"acrossWeight":1,"downWeight":1,"singleWeight":1,"blockWeight":1,"maxStripPercent":1,"maxBlockSide":2,"bendPercent":1,"maxBendSquares":3,"cornerPercent":1,"minDistancePercent":1,"maxAttempts":1,"maxWallRun":1}""";

    private const string Valid =
        """{"input":{"swipeThreshold":1},"slide":""" + SlideGroup + ""","stars":{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1},"camera":"""
        + CameraGroup + ""","campaign":""" + CampaignGroup + ""","generator":""" + GeneratorGroup + "}";

    private const string ChapterThreeStars =
        """{"3":{"twoStarMinExtraMoves":9,"twoStarExtraParPercent":9,"oneStarMinExtraMoves":9,"oneStarExtraParPercent":9}}""";

    private static string ConfigText(
        float threshold = 1,
        double squaresPerSecond = 1,
        double maxSeconds = 1,
        double trapShrink = 1,
        double trapPause = 1,
        double trapGrow = 1,
        double teleportShrink = 1,
        double teleportGrow = 1,
        int twoStarMin = 1,
        int twoStarPercent = 1,
        int oneStarMin = 1,
        int oneStarPercent = 1,
        float readable = 1,
        float closest = 1,
        float zonePercent = 1,
        double glide = 1,
        double opening = 1,
        double hold = 1,
        float zoomStep = 2,
        float slop = 1,
        int gatePercent = 1,
        string chapterStars = "{}",
        string chapterGatePercent = "{}",
        string levelStars = "{}",
        int undoLimit = 1,
        int maxBlockSide = 2,
        int maxAttempts = 1,
        int maxWallRun = 1) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$$"""{"input":{"swipeThreshold":{{{threshold}}}},"slide":{"squaresPerSecond":{{{squaresPerSecond}}},"maxSeconds":{{{maxSeconds}}},"trapShrinkSeconds":{{{trapShrink}}},"trapPauseSeconds":{{{trapPause}}},"trapGrowSeconds":{{{trapGrow}}},"teleportShrinkSeconds":{{{teleportShrink}}},"teleportGrowSeconds":{{{teleportGrow}}}},"stars":{"twoStarMinExtraMoves":{{{twoStarMin}}},"twoStarExtraParPercent":{{{twoStarPercent}}},"oneStarMinExtraMoves":{{{oneStarMin}}},"oneStarExtraParPercent":{{{oneStarPercent}}}},"camera":{"readableSquareSize":{{{readable}}},"closestSquareSize":{{{closest}}},"followZonePercent":{{{zonePercent}}},"glideSeconds":{{{glide}}},"openingSeconds":{{{opening}}},"openingHoldSeconds":{{{hold}}},"zoomStep":{{{zoomStep}}},"gestureSlop":{{{slop}}}},"campaign":{"gatePercent":{{{gatePercent}}},"chapterGatePercent":{{{chapterGatePercent}}},"chapterStars":{{{chapterStars}}},"levelStars":{{{levelStars}}},"undoLimit":{{{undoLimit}}}},"generator":{"acrossWeight":1,"downWeight":1,"singleWeight":1,"blockWeight":1,"maxStripPercent":1,"maxBlockSide":{{{maxBlockSide}}},"bendPercent":1,"maxBendSquares":3,"cornerPercent":1,"minDistancePercent":1,"maxAttempts":{{{maxAttempts}}},"maxWallRun":{{{maxWallRun}}}}}""");

    private static GameConfig Shipped() =>
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

    public static TheoryData<string> InvalidTexts => new()
    {
        "{}",
        Valid.Replace("""{"input":{"swipeThreshold":1},""", "{"),
        Valid.Replace(""","slide":""" + SlideGroup, ""),
        Valid.Replace(""","stars":{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}""", ""),
        Valid.Replace("""{"swipeThreshold":1}""", "null"),
        Valid.Replace(SlideGroup, "null"),
        Valid.Replace("""{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}""", "null"),
        Valid.Replace("""{"swipeThreshold":1}""", "{}"),
        Valid.Replace("""{"squaresPerSecond":1,""", "{"),
        Valid.Replace(""","maxSeconds":1""", ""),
        Valid.Replace(""","trapShrinkSeconds":1""", ""),
        Valid.Replace(""","trapPauseSeconds":1""", ""),
        Valid.Replace(""","trapGrowSeconds":1""", ""),
        Valid.Replace(""","teleportShrinkSeconds":1""", ""),
        Valid.Replace(""","teleportGrowSeconds":1""", ""),
        Valid.Replace("""{"twoStarMinExtraMoves":1,""", "{"),
        Valid.Replace(""","twoStarExtraParPercent":1""", ""),
        Valid.Replace(""","oneStarMinExtraMoves":1""", ""),
        Valid.Replace(""","oneStarExtraParPercent":1""", ""),
        Valid[..^1] + ",\"extra\":1}",
        Valid.Replace("\"swipeThreshold\":1", "\"swipeThreshold\":1,\"extra\":1"),
        Valid.Replace("\"maxSeconds\":1", "\"maxSeconds\":1,\"extra\":1"),
        Valid.Replace("\"twoStarExtraParPercent\":1", "\"twoStarExtraParPercent\":1,\"extra\":1"),
        Valid.Replace("\"swipeThreshold\":1", "\"swipeThreshold\":\"1\""),
        Valid.Replace("\"maxSeconds\":1", "\"maxSeconds\":\"1\""),
        Valid.Replace("\"twoStarMinExtraMoves\":1", "\"twoStarMinExtraMoves\":2.5"),
        Valid.Replace(""","camera":""" + CameraGroup, ""),
        Valid.Replace(CameraGroup, "null"),
        Valid.Replace("""{"readableSquareSize":1,""", "{"),
        Valid.Replace(""","closestSquareSize":1""", ""),
        Valid.Replace(""","followZonePercent":1""", ""),
        Valid.Replace(""","glideSeconds":1""", ""),
        Valid.Replace(""","openingSeconds":1""", ""),
        Valid.Replace(""","openingHoldSeconds":1""", ""),
        Valid.Replace(""","zoomStep":1""", ""),
        Valid.Replace(""","gestureSlop":1""", ""),
        Valid.Replace("\"zoomStep\":1", "\"zoomStep\":1,\"extra\":1"),
        Valid.Replace("\"zoomStep\":1", "\"zoomStep\":\"1\""),
        Valid.Replace(""","campaign":""" + CampaignGroup, ""),
        Valid.Replace(CampaignGroup, "null"),
        Valid.Replace("\"gatePercent\":1,", ""),
        Valid.Replace(",\"chapterStars\":{}", ""),
        Valid.Replace("\"chapterStars\":{}", "\"chapterStars\":null"),
        Valid.Replace("\"chapterStars\":{}", "\"chapterStars\":{\"2\":null}"),
        Valid.Replace("\"chapterStars\":{}", "\"chapterStars\":{\"0\":" + StarsGroup + "}"),
        Valid.Replace("\"chapterStars\":{}", "\"chapterStars\":{\"2\":" + StarsGroup.Replace(",\"oneStarExtraParPercent\":1", "") + "}"),
        Valid.Replace("\"gatePercent\":1", "\"gatePercent\":1,\"extra\":1"),
        Valid.Replace(",\"chapterGatePercent\":{}", ""),
        Valid.Replace("\"chapterGatePercent\":{}", "\"chapterGatePercent\":null"),
        Valid.Replace("\"chapterGatePercent\":{}", "\"chapterGatePercent\":{\"0\":50}"),
        Valid.Replace("\"chapterGatePercent\":{}", "\"chapterGatePercent\":{\"2\":null}"),
        Valid.Replace(",\"levelStars\":{}", ""),
        Valid.Replace("\"levelStars\":{}", "\"levelStars\":null"),
        Valid.Replace("\"levelStars\":{}", "\"levelStars\":{\"2\":null}"),
        Valid.Replace("\"levelStars\":{}", "\"levelStars\":{\"0\":" + StarsGroup + "}"),
        Valid.Replace("\"levelStars\":{}", "\"levelStars\":{\"2\":" + StarsGroup.Replace(",\"oneStarExtraParPercent\":1", "") + "}"),
        Valid.Replace(",\"undoLimit\":1", ""),
        Valid.Replace("\"undoLimit\":1", "\"undoLimit\":1.5"),
        "null",
        Valid.Replace("\"levelStars\":{}", "\"levelStars\":{\"2\":" + StarsGroup.Replace("}", ",\"extra\":1}") + "}"),
        Valid.Replace("\"undoLimit\":1", "\"undoLimit\":2,\"undoLimit\":1"),
        Valid.Replace("\"levelStars\":{}", "\"levelStars\":{\"2\":" + StarsGroup + ",\"02\":" + StarsGroup + "}"),
        Valid.Replace(""","generator":""" + GeneratorGroup, ""),
        Valid.Replace(GeneratorGroup, "null"),
        Valid.Replace(""","maxAttempts":1""", ""),
        Valid.Replace("\"maxAttempts\":1", "\"maxAttempts\":1,\"extra\":1"),
        Valid.Replace("\"bendPercent\":1", "\"bendPercent\":2.5"),
    };

    [Theory]
    [AutoData]
    public void Parse_reads_the_swipe_threshold(float threshold)
    {
        Assert.Equal(threshold, GameConfig.Parse(ConfigText(threshold: threshold)).Input.SwipeThreshold);
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_slide_group(double squaresPerSecond, double maxSeconds)
    {
        var slide = GameConfig.Parse(ConfigText(squaresPerSecond: squaresPerSecond, maxSeconds: maxSeconds)).Slide;

        Assert.Equal((squaresPerSecond, maxSeconds), (slide.SquaresPerSecond, slide.MaxSeconds));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_trap_times(double shrink, double pause, double grow)
    {
        var slide = GameConfig.Parse(ConfigText(trapShrink: shrink, trapPause: pause, trapGrow: grow)).Slide;

        Assert.Equal((shrink, pause, grow), (slide.TrapShrinkSeconds, slide.TrapPauseSeconds, slide.TrapGrowSeconds));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_teleport_times(double shrink, double grow)
    {
        var slide = GameConfig.Parse(ConfigText(teleportShrink: shrink, teleportGrow: grow)).Slide;

        Assert.Equal((shrink, grow), (slide.TeleportShrinkSeconds, slide.TeleportGrowSeconds));
    }

    [Fact]
    public void Shipped_teleport_times_are_the_mockup_s()
    {
        var slide = Shipped().Slide;

        Assert.Equal((0.12, 0.12), (slide.TeleportShrinkSeconds, slide.TeleportGrowSeconds));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_stars_group(int twoStarMin, int twoStarPercent, int oneStarMin, int oneStarPercent)
    {
        var stars = GameConfig.Parse(ConfigText(
            twoStarMin: twoStarMin, twoStarPercent: twoStarPercent, oneStarMin: oneStarMin, oneStarPercent: oneStarPercent)).Stars;

        Assert.Equal(
            (twoStarMin, twoStarPercent, oneStarMin, oneStarPercent),
            (stars.TwoStarMinExtraMoves, stars.TwoStarExtraParPercent, stars.OneStarMinExtraMoves, stars.OneStarExtraParPercent));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_camera_group(
        float readable, float closest, float zonePercent, double glide, double opening, double hold, float zoomStep, float slop)
    {
        var camera = GameConfig.Parse(ConfigText(
            readable: readable,
            closest: closest,
            zonePercent: zonePercent,
            glide: glide,
            opening: opening,
            hold: hold,
            zoomStep: zoomStep,
            slop: slop)).Camera;

        Assert.Equal(
            (readable, closest, zonePercent, glide, opening, hold, zoomStep, slop),
            (camera.ReadableSquareSize, camera.ClosestSquareSize, camera.FollowZonePercent, camera.GlideSeconds,
                camera.OpeningSeconds, camera.OpeningHoldSeconds, camera.ZoomStep, camera.GestureSlop));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_gate_percentage(int percent)
    {
        Assert.Equal(percent, GameConfig.Parse(ConfigText(gatePercent: percent)).Campaign.GatePercent);
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_undo_limit_and_chapter_gate_percentages(int undoLimit, int percent)
    {
        var campaign = GameConfig.Parse(ConfigText(undoLimit: undoLimit, chapterGatePercent: $"{{\"2\":{percent}}}")).Campaign;

        Assert.Equal((undoLimit, percent), (campaign.UndoLimit, campaign.ChapterGatePercent[2]));
    }

    private const string LevelFourStars =
        """{"4":{"twoStarMinExtraMoves":8,"twoStarExtraParPercent":8,"oneStarMinExtraMoves":8,"oneStarExtraParPercent":8}}""";

    [Theory]
    [InlineData(true, true, 8)]
    [InlineData(true, false, 8)]
    [InlineData(false, true, 9)]
    [InlineData(false, false, 1)]
    public void StarsFor_takes_the_level_s_then_the_chapter_s_then_the_default(bool levelHasOwn, bool chapterHasOwn, int expected)
    {
        var config = GameConfig.Parse(ConfigText(
            levelStars: levelHasOwn ? LevelFourStars : "{}",
            chapterStars: chapterHasOwn ? ChapterThreeStars : "{}"));
        var level = new Level(4, "Four", 3, TestMaps.Example(), 3);

        Assert.Equal(
            new StarsConfig
            {
                TwoStarMinExtraMoves = expected,
                TwoStarExtraParPercent = expected,
                OneStarMinExtraMoves = expected,
                OneStarExtraParPercent = expected,
            },
            config.StarsFor(level));
    }

    [Fact]
    public void Shipped_campaign_values()
    {
        var campaign = Shipped().Campaign;

        Assert.Equal(
            (66, 5, 0),
            (campaign.GatePercent, campaign.UndoLimit, campaign.ChapterGatePercent.Count));
    }

    [Fact]
    public void Shipped_generator_numbers()
    {
        Assert.Equal(
            new GeneratorConfig
            {
                AcrossWeight = 40,
                DownWeight = 40,
                SingleWeight = 10,
                BlockWeight = 10,
                MaxStripPercent = 100,
                MaxBlockSide = 3,
                BendPercent = 35,
                MaxBendSquares = 8,
                CornerPercent = 40,
                MinDistancePercent = 50,
                MaxAttempts = 200,
                MaxWallRun = 3,
            },
            Shipped().Generator);
    }

    [Fact]
    public void Shipped_config_parses_and_its_threshold_is_accepted()
    {
        var config = Shipped();

        Assert.Null(Record.Exception(() => new SwipeDetector(config.Input.SwipeThreshold)));
    }

    [Fact]
    public void Shipped_default_star_thresholds_are_accepted()
    {
        var config = Shipped();

        // Earned checks all four threshold values
        Assert.Null(Record.Exception(() => Stars.Earned(1, 1, config.Stars)));
    }

    [Fact]
    public void Shipped_slide_timing_is_accepted()
    {
        var config = Shipped();

        Assert.Null(Record.Exception(() => SlideTiming.Seconds(1, config.Slide)));
    }

    [Fact]
    public void Shipped_camera_config_is_accepted()
    {
        var config = Shipped();

        Assert.Null(Record.Exception(() =>
            new CameraSpace(new BoardRect(0, 0, 64, 64), new System.Numerics.Vector2(720, 1280), 24, 64, config.Camera)));
    }

    [Fact]
    public void Shipped_gesture_dead_zone_is_accepted()
    {
        var config = Shipped();

        Assert.Null(Record.Exception(() =>
            (new PinchTracker(config.Camera.GestureSlop), new DragTracker(config.Camera.GestureSlop))));
    }

    [Fact]
    public void Shipped_config_passes_the_range_check()
    {
        Assert.Null(Record.Exception(() => Shipped().CheckRanges(ShippedCampaignTests.Campaign().Levels)));
    }

    // The base the out-of-range cases change one value of: if it failed, every case below would pass for the wrong reason
    [Fact]
    public void Default_test_config_passes_the_range_check()
    {
        Assert.Null(Record.Exception(() => GameConfig.Parse(ConfigText()).CheckRanges([])));
    }

    // Each is the nearest value its user rejects
    public static TheoryData<string> OutOfRangeTexts => new()
    {
        ConfigText(threshold: 0),
        ConfigText(squaresPerSecond: 0),
        ConfigText(maxSeconds: 0),
        ConfigText(trapShrink: 0),
        ConfigText(trapPause: -1),
        ConfigText(trapGrow: 0),
        ConfigText(teleportShrink: 0),
        ConfigText(teleportGrow: 0),
        ConfigText(twoStarMin: -1),
        ConfigText(oneStarPercent: -1),
        ConfigText(readable: 0),
        ConfigText(zonePercent: 101),
        ConfigText(glide: 0),
        ConfigText(hold: -1),
        ConfigText(zoomStep: 1),
        ConfigText(slop: -1),
        ConfigText(undoLimit: -1),
        ConfigText(maxBlockSide: 1),
        ConfigText(maxAttempts: 0),
        ConfigText(maxWallRun: 0),
        ConfigText(maxWallRun: 101),
    };

    [Theory]
    [MemberData(nameof(OutOfRangeTexts))]
    public void A_value_out_of_range_fails_the_range_check(string text)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfig.Parse(text).CheckRanges([]));
    }

    [Theory]
    [AutoData]
    public void A_level_s_own_thresholds_are_range_checked(int id)
    {
        var config = GameConfig.Parse(ConfigText(levelStars: $$"""{"{{id}}":{{StarsGroup.Replace("\"oneStarMinExtraMoves\":1", "\"oneStarMinExtraMoves\":-1")}}}"""));

        Assert.Throws<ArgumentOutOfRangeException>(() => config.CheckRanges([new Level(id, "Level", 1, TestMaps.Example(), 3)]));
    }

    [Theory]
    [AutoData]
    public void Parse_accepts_a_leading_byte_order_mark(float threshold)
    {
        Assert.Equal(threshold, GameConfig.Parse((char)0xFEFF + ConfigText(threshold: threshold)).Input.SwipeThreshold);
    }

    [Fact]
    public void Valid_text_used_by_the_invalid_cases_parses()
    {
        Assert.Null(Record.Exception(() => GameConfig.Parse(Valid)));
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_config_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => GameConfig.Parse(text));
    }

    // A case that broke the JSON itself would throw without testing the rule it was written for
    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Every_invalid_case_is_still_JSON(string text)
    {
        Assert.Null(Record.Exception(() => JsonDocument.Parse(text).Dispose()));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public void Text_that_is_not_JSON_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => GameConfig.Parse(text));
    }

    [Fact]
    public void A_repeated_key_is_named_in_the_error()
    {
        var text = Valid.Replace("\"undoLimit\":1", "\"undoLimit\":2,\"undoLimit\":1");

        Assert.Contains("\"undoLimit\"", Assert.Throws<JsonException>(() => GameConfig.Parse(text)).Message, StringComparison.Ordinal);
    }

    // Swapped numbers: 1 star would be impossible to earn
    [Theory]
    [AutoData]
    public void A_one_star_limit_below_the_two_star_limit_fails_the_range_check(int id)
    {
        var swapped = """{"twoStarMinExtraMoves":4,"twoStarExtraParPercent":0,"oneStarMinExtraMoves":2,"oneStarExtraParPercent":0}""";
        var config = GameConfig.Parse(ConfigText(levelStars: $$"""{"{{id}}":{{swapped}}}"""));

        Assert.Throws<ArgumentOutOfRangeException>(() => config.CheckRanges([new Level(id, "Level", 1, TestMaps.Example(), 5)]));
    }

    [Fact]
    public void A_top_level_one_star_limit_below_the_two_star_limit_fails_the_range_check()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfig.Parse(ConfigText(twoStarMin: 4, oneStarMin: 2)).CheckRanges([]));
    }
}
