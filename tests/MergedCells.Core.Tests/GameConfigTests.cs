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

    private const string CampaignGroup = """{"gatePercent":1,"chapterStars":{}}""";

    private const string Valid =
        """{"input":{"swipeThreshold":1},"slide":{"squaresPerSecond":1,"maxSeconds":1},"stars":{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1},"camera":"""
        + CameraGroup + ""","campaign":""" + CampaignGroup + "}";

    private const string ChapterThreeStars =
        """{"3":{"twoStarMinExtraMoves":9,"twoStarExtraParPercent":9,"oneStarMinExtraMoves":9,"oneStarExtraParPercent":9}}""";

    private static string ConfigText(
        float threshold = 1,
        double squaresPerSecond = 1,
        double maxSeconds = 1,
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
        float zoomStep = 1,
        float slop = 1,
        int gatePercent = 1,
        string chapterStars = "{}") =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$$"""{"input":{"swipeThreshold":{{{threshold}}}},"slide":{"squaresPerSecond":{{{squaresPerSecond}}},"maxSeconds":{{{maxSeconds}}}},"stars":{"twoStarMinExtraMoves":{{{twoStarMin}}},"twoStarExtraParPercent":{{{twoStarPercent}}},"oneStarMinExtraMoves":{{{oneStarMin}}},"oneStarExtraParPercent":{{{oneStarPercent}}}},"camera":{"readableSquareSize":{{{readable}}},"closestSquareSize":{{{closest}}},"followZonePercent":{{{zonePercent}}},"glideSeconds":{{{glide}}},"openingSeconds":{{{opening}}},"openingHoldSeconds":{{{hold}}},"zoomStep":{{{zoomStep}}},"gestureSlop":{{{slop}}}},"campaign":{"gatePercent":{{{gatePercent}}},"chapterStars":{{{chapterStars}}}}}""");

    private static GameConfig Shipped() =>
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

    public static TheoryData<string> InvalidTexts => new()
    {
        "{}",
        Valid.Replace("""{"input":{"swipeThreshold":1},""", "{"),
        Valid.Replace(""","slide":{"squaresPerSecond":1,"maxSeconds":1}""", ""),
        Valid.Replace(""","stars":{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}""", ""),
        Valid.Replace("""{"swipeThreshold":1}""", "null"),
        Valid.Replace("""{"squaresPerSecond":1,"maxSeconds":1}""", "null"),
        Valid.Replace("""{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}""", "null"),
        Valid.Replace("""{"swipeThreshold":1}""", "{}"),
        Valid.Replace("""{"squaresPerSecond":1,""", "{"),
        Valid.Replace(""","maxSeconds":1""", ""),
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
        "null",
        "not json",
        "",
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

    [Fact]
    public void StarsFor_uses_the_top_level_stars_for_an_unlisted_chapter()
    {
        var config = GameConfig.Parse(ConfigText(chapterStars: ChapterThreeStars));

        Assert.Equal(config.Stars, config.StarsFor(1));
    }

    [Fact]
    public void StarsFor_uses_a_chapter_s_own_thresholds()
    {
        var config = GameConfig.Parse(ConfigText(chapterStars: ChapterThreeStars));

        Assert.Equal(
            new StarsConfig { TwoStarMinExtraMoves = 9, TwoStarExtraParPercent = 9, OneStarMinExtraMoves = 9, OneStarExtraParPercent = 9 },
            config.StarsFor(3));
    }

    [Fact]
    public void Shipped_gate_percentage_is_55()
    {
        Assert.Equal(55, Shipped().Campaign.GatePercent);
    }

    [Fact]
    public void Shipped_config_parses_and_its_threshold_is_accepted()
    {
        var config = Shipped();

        Assert.Null(Record.Exception(() => new SwipeDetector(config.Input.SwipeThreshold)));
    }

    [Fact]
    public void Shipped_star_thresholds_are_accepted()
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
}
