using System.Numerics;

namespace MergedCells.Core.Tests;

// The camera's geometry depends on its numbers, so the boards, bands and expected views are literals
public class CameraSpaceTests
{
    internal static readonly CameraConfig Config = new()
    {
        ReadableSquareSize = 45,
        ClosestSquareSize = 130,
        FollowZonePercent = 50,
        GlideSeconds = 0.5,
        OpeningSeconds = 0.8,
        OpeningHoldSeconds = 0,
        ZoomStep = 1.25f,
    };

    internal static CameraSpace Space(
        float boardWidth, float boardHeight, float bandX = 720, float bandY = 1000, CameraConfig? config = null) =>
        new(new BoardRect(0, 0, boardWidth, boardHeight), new Vector2(bandX, bandY), 24, 64, config ?? Config);

    internal static CameraView V(float x, float y, float zoom) => new(new Vector2(x, y), zoom);

    internal static void AssertView(CameraView expected, CameraView actual)
    {
        Assert.Equal(expected.Focus.X, actual.Focus.X, 0.01f);
        Assert.Equal(expected.Focus.Y, actual.Focus.Y, 0.01f);
        Assert.Equal(expected.Zoom, actual.Zoom, 0.0001f);
    }

    [Theory]
    [InlineData(640, 640, 720, 1000, 1.05f)]
    [InlineData(640, 640, 1000, 720, 1.05f)]
    [InlineData(640, 1280, 720, 1000, 0.74375f)]
    public void WholeZoom_fits_the_board_in_the_band_with_the_margin(
        float boardWidth, float boardHeight, float bandX, float bandY, float expected)
    {
        Assert.Equal(expected, Space(boardWidth, boardHeight, bandX, bandY).WholeZoom, 0.0001f);
    }

    [Fact]
    public void ClosestZoom_is_the_closest_square_size()
    {
        Assert.Equal(2.03125f, Space(640, 640).ClosestZoom, 0.0001f);
    }

    [Fact]
    public void ClosestZoom_is_never_below_the_whole_map_zoom()
    {
        var space = Space(320, 320);

        Assert.Equal((2.1f, 2.1f), (space.WholeZoom, space.ClosestZoom));
    }

    [Theory]
    [InlineData(3200, 0.703125f, false)]
    [InlineData(640, 1.05f, true)]
    public void Readable_zoom_and_opening(float board, float readable, bool opensWhole)
    {
        var space = Space(board, board);

        Assert.Equal(readable, space.ReadableZoom, 0.0001f);
        Assert.Equal(opensWhole, space.OpensWhole);
    }

    [Fact]
    public void Readable_zoom_is_clamped_to_the_closest_zoom()
    {
        var space = Space(640, 640, config: Config with { ReadableSquareSize = 200 });

        Assert.Equal(space.ClosestZoom, space.ReadableZoom);
    }

    [Fact]
    public void Whole_is_the_centred_whole_map_view()
    {
        AssertView(V(1600, 1600, 0.21f), Space(3200, 3200).Whole);
    }

    [Fact]
    public void Clamp_centres_an_axis_that_fits()
    {
        AssertView(V(320, 320, 1.05f), Space(640, 640).Clamp(V(0, 0, 1.05f)));
    }

    [Theory]
    [InlineData(0, 0, 336, 476)]
    [InlineData(5000, 5000, 2864, 2724)]
    public void Clamp_stops_at_the_board_edge_plus_the_margin(float x, float y, float expectedX, float expectedY)
    {
        AssertView(V(expectedX, expectedY, 1), Space(3200, 3200).Clamp(V(x, y, 1)));
    }

    [Theory]
    [InlineData(100, 2.03125f)]
    [InlineData(0.01f, 0.21f)]
    public void Clamp_keeps_the_zoom_within_the_limits(float zoom, float expected)
    {
        Assert.Equal(expected, Space(3200, 3200).Clamp(V(1600, 1600, zoom)).Zoom, 0.0001f);
    }

    [Fact]
    public void ZoomAt_keeps_the_board_point_under_the_band_point()
    {
        var zoomed = Space(3200, 3200).ZoomAt(V(1600, 1600, 1), 1.5f, new Vector2(540, 250));

        AssertView(V(1660, 1516.667f, 1.5f), zoomed);
    }

    [Theory]
    [InlineData(100, 2.03125f)]
    [InlineData(0.001f, 0.21f)]
    public void ZoomAt_stops_at_the_zoom_limits(float factor, float expected)
    {
        Assert.Equal(expected, Space(3200, 3200).ZoomAt(V(1600, 1600, 1), factor, new Vector2(360, 500)).Zoom, 0.0001f);
    }

    [Theory]
    [InlineData(1, 1.25f)]
    [InlineData(-1, 0.8f)]
    [InlineData(0.5f, 1.118034f)]
    public void ZoomFactor_raises_the_step_to_the_notches(float notches, float expected)
    {
        Assert.Equal(expected, Space(3200, 3200).ZoomFactor(notches), 0.0001f);
    }

    [Theory]
    [InlineData(1780, 1600, true)]
    [InlineData(1781, 1600, false)]
    [InlineData(1600, 1850, true)]
    [InlineData(1600, 1851, false)]
    [InlineData(1420, 1350, true)]
    [InlineData(1419, 1600, false)]
    public void InFollowZone_checks_the_middle_area_on_each_axis(float x, float y, bool expected)
    {
        Assert.Equal(expected, Space(3200, 3200).InFollowZone(V(1600, 1600, 1), new Vector2(x, y)));
    }

    [Theory]
    [InlineData(1928, 1600, true)]
    [InlineData(1929, 1600, false)]
    [InlineData(1600, 2068, true)]
    [InlineData(1600, 2069, false)]
    public void ShowsSquare_needs_the_whole_square_in_the_band(float x, float y, bool expected)
    {
        Assert.Equal(expected, Space(3200, 3200).ShowsSquare(V(1600, 1600, 1), new Vector2(x, y)));
    }

    [Fact]
    public void Follow_does_nothing_for_a_stop_inside_the_middle_area()
    {
        var view = V(1600, 1600, 1);

        Assert.Null(Space(3200, 3200).PlanFollow(view, view, 1, new Vector2(1600, 1600), new Vector2(1700, 1650)));
    }

    [Fact]
    public void Follow_glides_without_a_zoom_out_to_a_stop_still_on_screen()
    {
        var view = V(1600, 1600, 1);

        var glide = Space(3200, 3200).PlanFollow(view, view, 1, new Vector2(1600, 1600), new Vector2(1900, 1600));

        Assert.NotNull(glide);
        AssertView(V(1900, 1600, 1), glide.To);
        Assert.Equal((view, (float?)null, 0.5), (glide.From, glide.HalfwayZoom, glide.Seconds));
    }

    [Fact]
    public void Follow_zooms_out_for_a_stop_off_screen()
    {
        var view = V(1600, 1600, 1);

        var glide = Space(3200, 3200).PlanFollow(view, view, 1, new Vector2(1600, 1600), new Vector2(2600, 1600));

        Assert.NotNull(glide);
        AssertView(V(2600, 1600, 1), glide.To);
        Assert.Equal(672f / 1064, glide.HalfwayZoom!.Value, 0.0001f);
    }

    [Fact]
    public void Follow_zoom_out_never_goes_past_the_whole_map()
    {
        var view = V(1600, 1600, 1);

        var glide = Space(3200, 3200).PlanFollow(view, view, 1, new Vector2(100, 1600), new Vector2(3100, 1600));

        Assert.Equal(0.21f, glide!.HalfwayZoom!.Value, 0.0001f);
    }

    [Fact]
    public void Follow_ends_on_the_player_zoom()
    {
        var space = Space(3200, 3200);
        var view = V(1600, 1600, 1);

        var glide = space.PlanFollow(view, view, 1.5f, new Vector2(1600, 1600), new Vector2(2600, 1600));

        AssertView(space.Centre(new Vector2(2600, 1600), 1.5f), glide!.To);
    }

    [Fact]
    public void Follow_does_nothing_when_the_clamped_target_is_where_it_is_heading()
    {
        var view = V(336, 1600, 1);

        Assert.Null(Space(3200, 3200).PlanFollow(view, view, 1, new Vector2(336, 1600), new Vector2(10, 1600)));
    }

    [Theory]
    [InlineData(1650, true)]
    [InlineData(2650, false)]
    public void Follow_checks_the_view_it_is_heading_to(float stopX, bool glides)
    {
        var glide = Space(3200, 3200).PlanFollow(
            V(1600, 1600, 1), V(2600, 1600, 1), 1, new Vector2(1600, 1600), new Vector2(stopX, 1600));

        Assert.Equal(glides, glide is not null);
    }

    [Fact]
    public void Recentre_never_zooms_out()
    {
        var view = V(1600, 1600, 1);

        var glide = Space(3200, 3200).PlanRecentre(view, view, 1, new Vector2(100, 1600));

        Assert.Null(glide!.HalfwayZoom);
    }

    [Fact]
    public void Recentre_after_an_undo_mid_glide_turns_back_to_the_player()
    {
        var current = V(1700, 1600, 0.8f);

        var glide = Space(3200, 3200).PlanRecentre(current, V(2600, 1600, 1), 1, new Vector2(1600, 1600));

        Assert.NotNull(glide);
        AssertView(V(1600, 1600, 1), glide.To);
        Assert.Equal(current, glide.From);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.3)]
    public void Opening_zooms_from_the_whole_map_to_the_readable_zoom_on_the_start(double hold)
    {
        var space = Space(3200, 3200, config: Config with { OpeningHoldSeconds = hold });

        var glide = space.PlanOpening(new Vector2(32, 32));

        Assert.Equal(space.Whole, glide.From);
        AssertView(V(477.87f, 676.98f, 0.703125f), glide.To);
        Assert.Equal((0.8, hold), (glide.Seconds, glide.Delay));
    }

    [Fact]
    public void Resized_keeps_a_whole_map_view_whole()
    {
        var wider = Space(640, 640, 960, 1000);
        var space = Space(640, 640);

        AssertView(space.Whole, space.Resized(wider.Whole, wider));
    }

    [Fact]
    public void Resized_clamps_any_other_view()
    {
        var previous = Space(3200, 3200, 960, 1000);
        var space = Space(3200, 3200);
        var view = V(100, 1600, 1);

        AssertView(space.Clamp(view), space.Resized(view, previous));
    }

    [Fact]
    public void Resized_without_a_previous_space_clamps()
    {
        var space = Space(640, 640);

        AssertView(space.Clamp(V(0, 0, 0.01f)), space.Resized(V(0, 0, 0.01f), null));
    }

    public static TheoryData<CameraConfig> InvalidConfigs => new()
    {
        Config with { ReadableSquareSize = 0 },
        Config with { ReadableSquareSize = -1 },
        Config with { ReadableSquareSize = float.NaN },
        Config with { ReadableSquareSize = float.PositiveInfinity },
        Config with { ClosestSquareSize = 0 },
        Config with { FollowZonePercent = 0 },
        Config with { FollowZonePercent = 101 },
        Config with { GlideSeconds = 0 },
        Config with { OpeningSeconds = 0 },
        Config with { OpeningHoldSeconds = -1 },
        Config with { OpeningHoldSeconds = double.NaN },
        Config with { ZoomStep = 1 },
        Config with { ZoomStep = 0.5f },
        Config with { ZoomStep = float.PositiveInfinity },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public void Invalid_config_throws(CameraConfig config)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Space(640, 640, config: config));
    }

    [Theory]
    [InlineData(48, 1000)]
    [InlineData(720, 48)]
    public void Band_no_bigger_than_the_margins_throws(float bandX, float bandY)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Space(640, 640, bandX, bandY));
    }
}
