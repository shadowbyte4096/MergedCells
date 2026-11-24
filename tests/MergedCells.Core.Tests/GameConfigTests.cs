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

    private const string CapsGroup =
        """{"awayMoves":1,"greedyGap":1,"decoys":1,"nearOptimalRoutes":1,"bruteForceExtraMoves":1}""";

    private const string WeightsGroup =
        """{"awayMoves":1,"greedyGap":1,"decoys":1,"nearOptimalRoutes":1,"bruteForceExtraMoves":1}""";

    private const string DifficultyGroup =
        """{"nearOptimalExtraMoves":1,"bruteForceLookahead":1,"caps":""" + CapsGroup + ""","weights":""" + WeightsGroup + ""","modeWeights":{}}""";

    private const string ClimbWeightsGroup =
        "\"splitWeight\":1,\"mergeWeight\":1,\"moveSquareWeight\":1,\"moveStartWeight\":1,\"moveTwistWeight\":1";

    private const string ClimbGroup = """{"maxSteps":1,"stallSteps":1,""" + ClimbWeightsGroup + "}";

    private const string BandsGroup = """[{"name":"A","minScore":0}]""";

    private const string CustomLevelsGroup =
        """{"newSide":2,"minSide":2,"maxSide":2,"maxNameLength":1,"undoLimit":1,"bands":""" + BandsGroup + "}";

    private const string InfiniteEntry =
        """{"minScore":0,"maxScore":100,"plain":{"minSide":2,"maxSide":2},"twists":{"minSide":2,"maxSide":2}}""";

    private const string InfiniteEntries =
        "[" + InfiniteEntry + "," + InfiniteEntry + "," + InfiniteEntry + "," + InfiniteEntry + "," + InfiniteEntry + ","
        + InfiniteEntry + "," + InfiniteEntry + "," + InfiniteEntry + "," + InfiniteEntry + "," + InfiniteEntry + "]";

    private const string ScoringGroup = """{"pointsPerPar":1,"streakBonusPercent":0,"streakBonusCapPercent":0,"efficiencyWindow":1}""";

    // Passes the range check with the generator group above, so a case can break one value of it
    private const string InfiniteGroup =
        """{"minSide":2,"maxSide":2,"startDifficulty":1,"startSide":2,"minPar":1,"maxPar":1,"climbSteps":1,"stallSteps":1,"wallPercent":0,"trapPercent":0,"teleporterPercent":0,"loadingDelaySeconds":0,"scoring":""" + ScoringGroup + ""","difficulties":"""
        + InfiniteEntries + "}";
    private const string ServerGroup =
        """{"url":"https://example.test","publishableKey":"k","timeoutSeconds":1,"retrySeconds":1,"refreshMarginSeconds":1,"scoresPerRequest":1,"recordsPerRequest":1,"recordBytesPerRequest":1024,"recordMaxBytes":1024}""";

    private const string PrivacyGroup = """{"askGuardSeconds":0}""";

    private const string LeaderboardGroup = """{"columns":2}""";

    private const string LibraryGroup = """{"pageSize":1,"sizeSteps":[1]}""";

    private const string IntroGroup = """{"openSeconds":1,"titleSeconds":1,"holdSeconds":1,"handoffSeconds":1}""";

    private const string Valid =
        """{"input":{"swipeThreshold":1},"slide":""" + SlideGroup + ""","stars":{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1},"camera":"""
        + CameraGroup + ""","campaign":""" + CampaignGroup + ""","generator":""" + GeneratorGroup + ""","difficulty":""" + DifficultyGroup + ""","climb":""" + ClimbGroup + ""","customLevels":""" + CustomLevelsGroup + ""","infinite":""" + InfiniteGroup + ""","server":""" + ServerGroup + ""","privacy":""" + PrivacyGroup + ""","leaderboard":""" + LeaderboardGroup + ""","library":""" + LibraryGroup + ""","intro":""" + IntroGroup + "}";

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
        int maxWallRun = 1,
        int nearOptimalExtraMoves = 1,
        int bruteForceLookahead = 1,
        int decoysCap = 1,
        string weights = WeightsGroup,
        string modeWeights = "{}",
        int maxSteps = 1,
        int stallSteps = 1,
        string climbWeights = ClimbWeightsGroup,
        int newSide = 3,
        int minSide = 2,
        int maxSide = 4,
        int maxNameLength = 1,
        int customUndoLimit = 0,
        string bands = BandsGroup,
        string infinite = InfiniteGroup,
        string server = ServerGroup,
        string privacy = PrivacyGroup,
        string leaderboard = LeaderboardGroup,
        string library = LibraryGroup,
        string intro = IntroGroup) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$$"""{"input":{"swipeThreshold":{{{threshold}}}},"slide":{"squaresPerSecond":{{{squaresPerSecond}}},"maxSeconds":{{{maxSeconds}}},"trapShrinkSeconds":{{{trapShrink}}},"trapPauseSeconds":{{{trapPause}}},"trapGrowSeconds":{{{trapGrow}}},"teleportShrinkSeconds":{{{teleportShrink}}},"teleportGrowSeconds":{{{teleportGrow}}}},"stars":{"twoStarMinExtraMoves":{{{twoStarMin}}},"twoStarExtraParPercent":{{{twoStarPercent}}},"oneStarMinExtraMoves":{{{oneStarMin}}},"oneStarExtraParPercent":{{{oneStarPercent}}}},"camera":{"readableSquareSize":{{{readable}}},"closestSquareSize":{{{closest}}},"followZonePercent":{{{zonePercent}}},"glideSeconds":{{{glide}}},"openingSeconds":{{{opening}}},"openingHoldSeconds":{{{hold}}},"zoomStep":{{{zoomStep}}},"gestureSlop":{{{slop}}}},"campaign":{"gatePercent":{{{gatePercent}}},"chapterGatePercent":{{{chapterGatePercent}}},"chapterStars":{{{chapterStars}}},"levelStars":{{{levelStars}}},"undoLimit":{{{undoLimit}}}},"generator":{"acrossWeight":1,"downWeight":1,"singleWeight":1,"blockWeight":1,"maxStripPercent":1,"maxBlockSide":{{{maxBlockSide}}},"bendPercent":1,"maxBendSquares":3,"cornerPercent":1,"minDistancePercent":1,"maxAttempts":{{{maxAttempts}}},"maxWallRun":{{{maxWallRun}}}},"difficulty":{"nearOptimalExtraMoves":{{{nearOptimalExtraMoves}}},"bruteForceLookahead":{{{bruteForceLookahead}}},"caps":{"awayMoves":1,"greedyGap":1,"decoys":{{{decoysCap}}},"nearOptimalRoutes":1,"bruteForceExtraMoves":1},"weights":{{{weights}}},"modeWeights":{{{modeWeights}}}},"climb":{"maxSteps":{{{maxSteps}}},"stallSteps":{{{stallSteps}}},{{{climbWeights}}}},"customLevels":{"newSide":{{{newSide}}},"minSide":{{{minSide}}},"maxSide":{{{maxSide}}},"maxNameLength":{{{maxNameLength}}},"undoLimit":{{{customUndoLimit}}},"bands":{{{bands}}}},"infinite":{{{infinite}}},"server":{{{server}}},"privacy":{{{privacy}}},"leaderboard":{{{leaderboard}}},"library":{{{library}}},"intro":{{{intro}}}}""");

    // The server group with every value valid unless given
    private static string Server(
        string url = "https://example.test", string key = "k", double timeout = 1, double retry = 1, double margin = 1, int scores = 1,
        int records = 1, int bytes = 1024, int maxBytes = 1024) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"url":"{{url}}","publishableKey":"{{key}}","timeoutSeconds":{{timeout}},"retrySeconds":{{retry}},"refreshMarginSeconds":{{margin}},"scoresPerRequest":{{scores}},"recordsPerRequest":{{records}},"recordBytesPerRequest":{{bytes}},"recordMaxBytes":{{maxBytes}}}""");

    private static string Privacy(double guard) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"askGuardSeconds":{{guard}}}""");

    // A full set of five weights, each 1 unless given
    private static string Weights(int awayMoves = 1, int greedyGap = 1, int decoys = 1, int nearOptimalRoutes = 1, int bruteForceExtraMoves = 1) =>
        $$"""{"awayMoves":{{awayMoves}},"greedyGap":{{greedyGap}},"decoys":{{decoys}},"nearOptimalRoutes":{{nearOptimalRoutes}},"bruteForceExtraMoves":{{bruteForceExtraMoves}}}""";

    // The climb group's five edit weights, each 1 unless given
    private static string ClimbWeights(int split = 1, int merge = 1, int moveSquare = 1, int moveStart = 1, int moveTwist = 1) =>
        $"\"splitWeight\":{split},\"mergeWeight\":{merge},\"moveSquareWeight\":{moveSquare},\"moveStartWeight\":{moveStart},\"moveTwistWeight\":{moveTwist}";

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
        Valid.Replace(""","difficulty":""" + DifficultyGroup, ""),
        Valid.Replace(DifficultyGroup, "null"),
        Valid.Replace("\"caps\":" + CapsGroup, "\"caps\":null"),
        Valid.Replace("\"weights\":" + WeightsGroup, "\"weights\":null"),
        Valid.Replace("\"modeWeights\":{}", "\"modeWeights\":null"),
        Valid.Replace("\"weights\":" + WeightsGroup, "\"weights\":" + WeightsGroup.Replace(",\"decoys\":1", "")),
        Valid.Replace("\"caps\":" + CapsGroup, "\"caps\":" + CapsGroup.Replace("\"decoys\":1", "\"decoys\":1,\"extra\":1")),
        Valid.Replace("\"modeWeights\":{}", "\"modeWeights\":{\"versus\":" + WeightsGroup + "}"),
        Valid.Replace("\"modeWeights\":{}", "\"modeWeights\":{\"Daily\":" + WeightsGroup + "}"),
        Valid.Replace("\"modeWeights\":{}", "\"modeWeights\":{\"daily\":null}"),
        Valid.Replace(""","climb":""" + ClimbGroup, ""),
        Valid.Replace(ClimbGroup, "null"),
        Valid.Replace(""","stallSteps":1""", ""),
        Valid.Replace("\"maxSteps\":1", "\"maxSteps\":1,\"extra\":1"),
        Valid.Replace(""","customLevels":""" + CustomLevelsGroup, ""),
        Valid.Replace(""","maxNameLength":1""", ""),
        Valid.Replace("\"maxNameLength\":1", "\"maxNameLength\":1,\"extra\":1"),
        Valid.Replace(BandsGroup, """[{"name":"A","minScore":0,"extra":1}]"""),
        Valid.Replace(""","infinite":""" + InfiniteGroup, ""),
        Valid.Replace("\"teleporterPercent\":0", "\"teleporterPercent\":0,\"extra\":1"),
        Valid.Replace(""","startSide":2""", ""),
        Valid.Replace(""","loadingDelaySeconds":0""", ""),
        Valid.Replace(""","scoring":""" + ScoringGroup, ""),
        Valid.Replace(""","efficiencyWindow":1""", ""),
        Valid.Replace("\"efficiencyWindow\":1", "\"efficiencyWindow\":1,\"extra\":1"),
        Valid.Replace(InfiniteEntries, "[" + InfiniteEntry.Replace("\"maxScore\":100", "\"maxScore\":100,\"extra\":1") + "]"),
        Valid.Replace(""","server":""" + ServerGroup, ""),
        Valid.Replace(""","privacy":""" + PrivacyGroup, ""),
        Valid.Replace(",\"recordBytesPerRequest\":1024", ""),
        Valid.Replace("\"recordBytesPerRequest\":1024", "\"recordBytesPerRequest\":1024,\"extra\":1"),
        Valid.Replace(",\"recordMaxBytes\":1024", ""),
        Valid.Replace(PrivacyGroup, "{}"),
        Valid.Replace(""","leaderboard":""" + LeaderboardGroup, ""),
        Valid.Replace(LeaderboardGroup, "{}"),
        Valid.Replace(LeaderboardGroup, """{"columns":2,"extra":1}"""),
        Valid.Replace(""","library":""" + LibraryGroup, ""),
        Valid.Replace(LibraryGroup, """{"pageSize":1}"""),
        Valid.Replace(LibraryGroup, """{"pageSize":1,"sizeSteps":[1],"extra":1}"""),
        Valid.Replace(""","intro":""" + IntroGroup, ""),
        Valid.Replace(IntroGroup, IntroGroup.Replace("}", ",\"extra\":1}")),
        Valid.Replace(IntroGroup, IntroGroup.Replace("\"openSeconds\":1,", "")),
        Valid.Replace(IntroGroup, IntroGroup.Replace("\"titleSeconds\":1,", "")),
        Valid.Replace(IntroGroup, IntroGroup.Replace("\"holdSeconds\":1,", "")),
        Valid.Replace(IntroGroup, IntroGroup.Replace(",\"handoffSeconds\":1", "")),
    };

    public static TheoryData<string> NullServerTexts => new()
    {
        Valid.Replace(ServerGroup, "null"),
        Valid.Replace(PrivacyGroup, "null"),
        Valid.Replace(LeaderboardGroup, "null"),
        Valid.Replace(LibraryGroup, "null"),
        Valid.Replace(IntroGroup, "null"),
        Valid.Replace("\"sizeSteps\":[1]", "\"sizeSteps\":null"),
        Valid.Replace("\"url\":\"https://example.test\"", "\"url\":null"),
        Valid.Replace("\"publishableKey\":\"k\"", "\"publishableKey\":null"),
    };

    [Theory]
    [MemberData(nameof(NullServerTexts))]
    public void Null_server_and_privacy_parts_are_rejected(string text)
    {
        Assert.Throws<JsonException>(() => GameConfig.Parse(text));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_intro_group(byte open, byte title, byte hold, byte handoff)
    {
        var config = GameConfig.Parse(ConfigText(intro: string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"openSeconds":{{open / 10.0}},"titleSeconds":{{title / 10.0}},"holdSeconds":{{hold / 10.0}},"handoffSeconds":{{handoff / 10.0}}}""")));

        Assert.Equal(
            (open / 10.0, title / 10.0, hold / 10.0, handoff / 10.0),
            (config.Intro.OpenSeconds, config.Intro.TitleSeconds, config.Intro.HoldSeconds, config.Intro.HandoffSeconds));
    }

    public static TheoryData<string, double> BadIntroTimes()
    {
        var data = new TheoryData<string, double>();
        foreach (var key in new[] { "openSeconds", "titleSeconds", "holdSeconds", "handoffSeconds" })
        {
            foreach (var value in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            {
                data.Add(key, value);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(BadIntroTimes))]
    public void Intro_times_that_are_not_positive_and_finite_are_rejected(string key, double value)
    {
        var intro = new IntroConfig
        {
            OpenSeconds = key == "openSeconds" ? value : 1,
            TitleSeconds = key == "titleSeconds" ? value : 1,
            HoldSeconds = key == "holdSeconds" ? value : 1,
            HandoffSeconds = key == "handoffSeconds" ? value : 1,
        };

        Assert.Equal("intro." + key, Assert.Throws<ArgumentOutOfRangeException>(intro.CheckRanges).ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_bad_intro_time_in_the_file_stops_the_start(double open)
    {
        var config = GameConfig.Parse(ConfigText(intro: string.Create(
            CultureInfo.InvariantCulture, $$"""{"openSeconds":{{open}},"titleSeconds":1,"holdSeconds":1,"handoffSeconds":1}""")));

        Assert.Throws<ArgumentOutOfRangeException>(() => config.CheckRanges([]));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_leaderboard_group(byte columns)
    {
        var config = GameConfig.Parse(ConfigText(leaderboard: $$"""{"columns":{{columns}}}"""));

        Assert.Equal(columns, config.Leaderboard.Columns);
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_library_group(byte pageSize, byte firstStep, byte secondStep)
    {
        var config = GameConfig.Parse(ConfigText(library: $$"""{"pageSize":{{pageSize}},"sizeSteps":[{{firstStep}},{{secondStep}}]}"""));

        Assert.Equal((pageSize, $"{firstStep},{secondStep}"), (config.Library.PageSize, string.Join(",", config.Library.SizeSteps)));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_server_and_privacy_groups(byte scores, byte records, ushort bytes, ushort maxBytes, byte guardTenths)
    {
        var config = GameConfig.Parse(ConfigText(server: Server(scores: scores, records: records, bytes: bytes, maxBytes: maxBytes), privacy: Privacy(guardTenths / 10.0)));

        Assert.Equal(
            ((int)scores, (int)records, (int)bytes, (int)maxBytes, guardTenths / 10.0),
            (config.Server.ScoresPerRequest, config.Server.RecordsPerRequest, config.Server.RecordBytesPerRequest, config.Server.RecordMaxBytes, config.Privacy.AskGuardSeconds));
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("https://example.test", "k", true)]
    public void Server_is_on_only_with_a_url(string url, string key, bool on)
    {
        Assert.Equal(on, GameConfig.Parse(ConfigText(server: Server(url: url, key: key))).Server.IsOn);
    }

    public static TheoryData<string> NullInfiniteTexts => new()
    {
        Valid.Replace(InfiniteGroup, "null"),
        Valid.Replace(ScoringGroup, "null"),
        Valid.Replace(InfiniteEntries, "null"),
        Valid.Replace(InfiniteEntries, "[null]"),
        Valid.Replace(InfiniteEntries, "[" + InfiniteEntry.Replace("\"plain\":{\"minSide\":2,\"maxSide\":2}", "\"plain\":null") + "]"),
        Valid.Replace(InfiniteEntries, "[" + InfiniteEntry.Replace("\"twists\":{\"minSide\":2,\"maxSide\":2}", "\"twists\":null") + "]"),
    };

    [Theory]
    [MemberData(nameof(NullInfiniteTexts))]
    public void Null_infinite_parts_are_rejected(string text)
    {
        Assert.Throws<JsonException>(() => GameConfig.Parse(text));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_infinite_group(byte climbSteps, byte wallPercent)
    {
        var text = ConfigText(infinite: InfiniteGroup.Replace("\"climbSteps\":1", $"\"climbSteps\":{climbSteps}").Replace("\"wallPercent\":0", $"\"wallPercent\":{wallPercent}"));

        var infinite = GameConfig.Parse(text).Infinite;

        Assert.Equal((climbSteps, wallPercent, InfiniteConfig.DifficultyCount), (infinite.ClimbSteps, infinite.WallPercent, infinite.Difficulties.Count));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_loading_delay(byte tenths)
    {
        var seconds = tenths / 10.0;
        var text = ConfigText(infinite: InfiniteGroup.Replace("\"loadingDelaySeconds\":0", $"\"loadingDelaySeconds\":{seconds.ToString(CultureInfo.InvariantCulture)}"));

        Assert.Equal(seconds, GameConfig.Parse(text).Infinite.LoadingDelaySeconds);
    }

    public static TheoryData<string> NullCustomLevelsTexts => new()
    {
        Valid.Replace(CustomLevelsGroup, "null"),
        Valid.Replace(BandsGroup, "null"),
        Valid.Replace(BandsGroup, "[null]"),
        Valid.Replace(BandsGroup, """[{"name":null,"minScore":0}]"""),
    };

    [Theory]
    [MemberData(nameof(NullCustomLevelsTexts))]
    public void Null_custom_levels_parts_are_rejected(string text)
    {
        Assert.Throws<JsonException>(() => GameConfig.Parse(text));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_custom_levels_group(byte maxNameLength, byte undoLimit)
    {
        var custom = GameConfig.Parse(ConfigText(maxNameLength: maxNameLength, customUndoLimit: undoLimit)).CustomLevels;

        Assert.Equal((maxNameLength, undoLimit, "A"), (custom.MaxNameLength, custom.UndoLimit, custom.Bands.Single().Name));
    }

    // Three bands starting at 0, 20 and 80, so each row lands on, inside or past a boundary
    [Theory]
    [InlineData(0, "A")]
    [InlineData(19, "A")]
    [InlineData(20, "B")]
    [InlineData(79, "B")]
    [InlineData(80, "C")]
    [InlineData(100, "C")]
    public void Band_for_a_score_is_the_last_band_starting_at_or_below_it(int score, string band)
    {
        var bands = """[{"name":"A","minScore":0},{"name":"B","minScore":20},{"name":"C","minScore":80}]""";

        Assert.Equal(band, GameConfig.Parse(ConfigText(bands: bands)).CustomLevels.BandFor(score).Name);
    }

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

    [Theory]
    [AutoData]
    public void Parse_reads_a_mode_override(int decoys)
    {
        var config = GameConfig.Parse(ConfigText(modeWeights: $$"""{"daily":{{Weights(decoys: decoys)}}}"""));

        Assert.Equal(decoys, config.Difficulty.ModeWeights["daily"].Decoys);
    }

    [Theory]
    [AutoData]
    public void Parse_reads_a_custom_mode_override(int decoys)
    {
        var config = GameConfig.Parse(ConfigText(modeWeights: $$"""{"custom":{{Weights(decoys: decoys)}}}"""));

        Assert.Equal(decoys, config.Difficulty.ModeWeights["custom"].Decoys);
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
        ConfigText(nearOptimalExtraMoves: 0),
        ConfigText(nearOptimalExtraMoves: 11),
        ConfigText(bruteForceLookahead: 0),
        ConfigText(bruteForceLookahead: 5),
        ConfigText(decoysCap: 0),
        ConfigText(weights: Weights(decoys: -1)),
        ConfigText(weights: Weights(decoys: 10_001)),
        ConfigText(weights: Weights(0, 0, 0, 0, 0)),
        ConfigText(modeWeights: $$"""{"daily":{{Weights(decoys: 10_001)}}}"""),
        ConfigText(modeWeights: $$"""{"daily":{{Weights(0, 0, 0, 0, 0)}}}"""),
        ConfigText(maxSteps: 0),
        ConfigText(maxSteps: 10_000_001),
        ConfigText(stallSteps: 0),
        ConfigText(maxSteps: 1, stallSteps: 2),
        ConfigText(climbWeights: ClimbWeights(split: -1)),
        ConfigText(climbWeights: ClimbWeights(0, 0, 0, 0, 0)),
        ConfigText(climbWeights: ClimbWeights(split: int.MaxValue)),
        ConfigText(minSide: 1, newSide: 3),
        ConfigText(newSide: 1),
        ConfigText(maxSide: 2),
        ConfigText(maxSide: 101),
        ConfigText(maxNameLength: 0),
        ConfigText(customUndoLimit: -1),
        ConfigText(bands: "[]"),
        ConfigText(bands: """[{"name":"A","minScore":5}]"""),
        ConfigText(bands: """[{"name":"A","minScore":0},{"name":"B","minScore":0}]"""),
        ConfigText(bands: """[{"name":"A","minScore":0},{"name":"B","minScore":101}]"""),
        ConfigText(bands: """[{"name":" ","minScore":0}]"""),
        ConfigText(infinite: InfiniteGroup.Replace("\"climbSteps\":1,\"stallSteps\":1", "\"climbSteps\":0,\"stallSteps\":0")),
        ConfigText(infinite: InfiniteGroup.Replace("\"loadingDelaySeconds\":0", "\"loadingDelaySeconds\":-1")),
        ConfigText(infinite: InfiniteGroup.Replace("\"efficiencyWindow\":1", "\"efficiencyWindow\":0")),
        ConfigText(server: Server(url: "http://example.test")),
        ConfigText(server: Server(url: "https://example.test/")),
        ConfigText(server: Server(url: "https://")),
        ConfigText(server: Server(url: "", key: "k")),
        ConfigText(server: Server(key: "")),
        ConfigText(server: Server(timeout: 0)),
        ConfigText(server: Server(retry: 0)),
        ConfigText(server: Server(margin: 0)),
        ConfigText(server: Server(scores: 0)),
        ConfigText(server: Server(scores: ServerConfig.MaxScoresPerRequest + 1)),
        ConfigText(server: Server(records: 0)),
        ConfigText(server: Server(records: ServerConfig.MaxRecordsPerRequest + 1)),
        ConfigText(server: Server(bytes: 1023)),
        ConfigText(server: Server(maxBytes: 1023)),
        ConfigText(server: Server(maxBytes: ServerConfig.MaxRecordBytes + 1)),
        ConfigText(privacy: Privacy(-1)),
        ConfigText(leaderboard: """{"columns":1}"""),
        ConfigText(leaderboard: """{"columns":11}"""),
        ConfigText(library: """{"pageSize":0,"sizeSteps":[1]}"""),
        ConfigText(library: """{"pageSize":1,"sizeSteps":[4]}"""),
    };

    [Theory]
    [AutoData]
    public void Parse_reads_the_climb_group(int maxSteps, int stallSteps, byte split, byte merge, byte moveSquare, byte moveStart, byte moveTwist)
    {
        var text = ConfigText(maxSteps: maxSteps, stallSteps: stallSteps, climbWeights: ClimbWeights(split, merge, moveSquare, moveStart, moveTwist));

        Assert.Equal(ClimbConfigs.With(maxSteps, stallSteps, split, merge, moveSquare, moveStart, moveTwist), GameConfig.Parse(text).Climb);
    }

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
