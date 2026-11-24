using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class WalletTests
{
    private static readonly CoinsConfig Config = new() { PerStar = 10, Daily = 20, PerPar = 1, PercentPerScore = 5 };

    private readonly SaveGame save = new();

    private Wallet Wallet => new(save, Config);

    // The fields the wallet doesn't read are anything valid
    private static MapScore Score(int par, int keptMoves, int? previousMoves, bool counted = true) =>
        new(par, keptMoves, 10000, par * 2, 0, previousMoves, counted, keptMoves, 0);

    private static CustomLevel Imported(Guid id, Map? map = null) =>
        new(id, "x", map ?? TestMaps.Example(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Pay_stars_pays_new_stars_and_adds_them()
    {
        var paid = Wallet.PayStars(new LevelFinish(5, 2, null, null));

        Assert.Equal((20L, 20L), (paid, save.Coins));
    }

    [Fact]
    public void Pay_stars_pays_nothing_without_new_stars()
    {
        var paid = Wallet.PayStars(new LevelFinish(5, 3, 5, 3));

        Assert.Equal((0L, 0L), (paid, save.Coins));
    }

    [Fact]
    public void First_counted_finish_pays_the_whole_map()
    {
        Assert.Equal(14L, Wallet.PayMap(Score(6, 8, null), 63));
    }

    [Fact]
    public void Better_retry_pays_the_difference()
    {
        Assert.Equal(18L - 14L, Wallet.PayMap(Score(6, 6, 8), 63));
    }

    [Fact]
    public void Finish_that_does_not_count_pays_nothing()
    {
        Assert.Equal((0L, 0L), (Wallet.PayMap(Score(6, 8, null, counted: false), 63), save.Coins));
    }

    [Fact]
    public void Counted_finish_with_no_higher_payout_pays_nothing()
    {
        // 13 and 14 moves both round down to 8 coins at par 6 and score 63
        Assert.Equal((0L, 0L), (Wallet.PayMap(Score(6, 13, 14), 63), save.Coins));
    }

    [Theory]
    [AutoData]
    public void Own_custom_level_pays_nothing(Guid id)
    {
        var own = new CustomLevel(id, "x", TestMaps.Example(), DateTimeOffset.UnixEpoch);

        Assert.Equal((0L, (long?)null), (Wallet.PayCustom(own, 6, 8, 63), save.CustomPayout(id)));
    }

    [Theory]
    [AutoData]
    public void Imported_level_pays_and_keeps_its_best(Guid id)
    {
        Assert.Equal((14L, (long?)14L), (Wallet.PayCustom(Imported(id), 6, 8, 63), save.CustomPayout(id)));
    }

    [Theory]
    [AutoData]
    public void Better_finish_tops_up_over_the_best(Guid id)
    {
        Wallet.PayCustom(Imported(id), 6, 8, 63);

        Assert.Equal((4L, (long?)18L, 18L), (Wallet.PayCustom(Imported(id), 6, 6, 63), save.CustomPayout(id), save.Coins));
    }

    [Theory]
    [AutoData]
    public void Worse_finish_pays_nothing(Guid id)
    {
        Wallet.PayCustom(Imported(id), 6, 6, 63);

        Assert.Equal((0L, (long?)18L), (Wallet.PayCustom(Imported(id), 6, 8, 63), save.CustomPayout(id)));
    }

    [Theory]
    [AutoData]
    public void New_version_tops_up_over_the_old_best(Guid id)
    {
        Wallet.PayCustom(Imported(id), 6, 6, 63);

        var paid = Wallet.PayCustom(Imported(id, TestMaps.Sketch()), 8, 8, 63);

        Assert.Equal(Config.ForMap(8, 8, 63) - 18, paid);
    }

    [Theory]
    [AutoData]
    public void Zero_payout_stores_nothing(Guid id)
    {
        Assert.Equal((0L, (long?)null), (Wallet.PayCustom(Imported(id), 6, 8, 0), save.CustomPayout(id)));
    }

    [Fact]
    public void Pay_daily_pays_the_config_amount()
    {
        Assert.Equal((20L, 20L), (Wallet.PayDaily(), save.Coins));
    }
}
