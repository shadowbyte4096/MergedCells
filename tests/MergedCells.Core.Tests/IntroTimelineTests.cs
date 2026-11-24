using System.Numerics;
using AutoFixture;

namespace MergedCells.Core.Tests;

public class IntroTimelineTests
{
    private static readonly Vector2 Phone = new(720, 1280);

    private static readonly LaunchCircle Launch = new(new Vector2(360, 640), 280);

    private static readonly TitleSpot MenuTitle = new(new Vector2(360, 457), 440, 64);

    public static TheoryData<float, float> Viewports => new()
    {
        { 720, 1280 },
        { 720, 1600 },
        { 1280, 720 },
    };

    // Any positive times will do: the tests check how the parts relate, not how long they take
    private static IntroConfig Times()
    {
        var fixture = new Fixture();
        double Seconds() => (fixture.Create<byte>() % 20 + 1) / 10.0;
        return new IntroConfig { OpenSeconds = Seconds(), TitleSeconds = Seconds(), HoldSeconds = Seconds(), HandoffSeconds = Seconds() };
    }

    private static IntroTimeline Timeline(IntroConfig times) => new(times, IconArtTests.Shipped(), Launch);

    private static Vector2 ArtCentre(IntroFrame frame) => frame.ArtOrigin + new Vector2(50 * frame.ArtScale);

    [Fact]
    public void At_the_start_the_frame_is_the_launch_frame()
    {
        var art = IconArtTests.Shipped();

        var frame = Timeline(Times()).At(0, Phone, MenuTitle);

        Assert.Equal(
            (Launch.Centre, Launch.Diameter / 2, Launch.Diameter / 100, Launch.Centre - new Vector2(Launch.Diameter / 2),
                (float)art.LeftBlock.X, (float)(art.RightBlock.X + art.RightBlock.Width), (float)art.Player.TrailStart, 0f, 1f, 1f, false),
            (frame.CircleCentre, frame.CircleRadius, frame.ArtScale, frame.ArtOrigin,
                frame.LeftBlockLeft, frame.RightBlockRight, frame.TrailStart, frame.TitleAlpha, frame.SceneAlpha, frame.BackgroundAlpha, frame.Done));
    }

    [Theory]
    [MemberData(nameof(Viewports))]
    public void Once_open_the_circle_covers_the_viewport(float width, float height)
    {
        var times = Times();

        var frame = Timeline(times).At(times.OpenSeconds, new Vector2(width, height), MenuTitle);

        Vector2[] corners = [new(0, 0), new(width, 0), new(0, height), new(width, height)];
        Assert.All(corners, corner => Assert.True(Vector2.Distance(corner, frame.CircleCentre) < frame.CircleRadius, $"Corner {corner}"));
    }

    [Theory]
    [MemberData(nameof(Viewports))]
    public void Once_open_the_blocks_and_trail_reach_past_both_edges(float width, float height)
    {
        var times = Times();

        var frame = Timeline(times).At(times.OpenSeconds, new Vector2(width, height), MenuTitle);

        Assert.True(frame.ArtOrigin.X + frame.LeftBlockLeft * frame.ArtScale < 0, "left block");
        Assert.True(frame.ArtOrigin.X + frame.TrailStart * frame.ArtScale < 0, "trail");
        Assert.True(frame.ArtOrigin.X + frame.RightBlockRight * frame.ArtScale > width, "right block");
    }

    [Fact]
    public void Once_open_the_art_is_at_the_open_scale_below_the_centre()
    {
        var times = Times();

        var frame = Timeline(times).At(times.OpenSeconds, Phone, MenuTitle);

        Assert.Equal(IntroTimeline.OpenScale, frame.ArtScale, 0.001f);
        Assert.True(Vector2.Distance(Phone / 2 + new Vector2(0, IntroTimeline.OpenDrop), ArtCentre(frame)) < 0.01f, $"Centre {ArtCentre(frame)}");
    }

    [Fact]
    public void The_title_is_hidden_until_it_starts()
    {
        var timeline = Timeline(Times());

        Assert.Equal(0, timeline.At(timeline.TitleStart, Phone, MenuTitle).TitleAlpha);
    }

    [Fact]
    public void The_title_is_fully_in_by_the_hold()
    {
        var timeline = Timeline(Times());

        var frame = timeline.At(timeline.HoldStart, Phone, MenuTitle);

        Assert.Equal((1f, IntroTimeline.SceneTitle(Phone)), (frame.TitleAlpha, frame.Title));
    }

    [Fact]
    public void The_background_stays_until_the_last_part_of_the_handoff()
    {
        var times = Times();
        var timeline = Timeline(times);

        Assert.Equal(1, timeline.At(timeline.HandoffStart + times.HandoffSeconds * 0.4, Phone, MenuTitle).BackgroundAlpha, 0.0001f);
    }

    [Fact]
    public void The_scene_has_dropped_and_faded_by_the_middle_of_the_handoff()
    {
        var times = Times();
        var timeline = Timeline(times);

        var before = timeline.At(timeline.HandoffStart, Phone, MenuTitle);
        var middle = timeline.At(timeline.HandoffStart + times.HandoffSeconds / 2, Phone, MenuTitle);

        Assert.Equal(0, middle.SceneAlpha, 0.0001f);
        Assert.Equal(before.ArtOrigin.Y + IntroTimeline.HandoffDrop, middle.ArtOrigin.Y, 0.001f);
    }

    [Fact]
    public void At_the_end_the_title_is_on_the_menu_title_and_the_layer_is_clear()
    {
        var timeline = Timeline(Times());

        var frame = timeline.At(timeline.TotalSeconds, Phone, MenuTitle);

        Assert.True(Vector2.Distance(MenuTitle.Centre, frame.Title.Centre) < 0.01f, $"Centre {frame.Title.Centre}");
        Assert.Equal(
            (MenuTitle.Width, MenuTitle.FontSize, 1f, 0f),
            (frame.Title.Width, frame.Title.FontSize, frame.TitleColourMix, frame.BackgroundAlpha));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-0.001, false)]
    public void It_is_done_at_the_total_and_not_before(double offset, bool done)
    {
        var timeline = Timeline(Times());

        Assert.Equal(done, timeline.At(timeline.TotalSeconds + offset, Phone, MenuTitle).Done);
    }

    [Fact]
    public void TotalSeconds_is_the_four_times_less_a_third_of_the_title()
    {
        var times = new IntroConfig { OpenSeconds = 0.6, TitleSeconds = 0.3, HoldSeconds = 0.5, HandoffSeconds = 0.5 };

        Assert.Equal(1.8, Timeline(times).TotalSeconds, 9);
    }

    // A title over three times as long as the opening would otherwise start before the intro
    [Fact]
    public void The_title_never_starts_before_the_intro()
    {
        var times = new IntroConfig { OpenSeconds = 0.1, TitleSeconds = 2, HoldSeconds = 0.5, HandoffSeconds = 0.5 };

        var timeline = Timeline(times);

        Assert.Equal((0.0, 0f, 3.0), (timeline.TitleStart, timeline.At(0, Phone, MenuTitle).TitleAlpha, timeline.TotalSeconds));
    }

    [Fact]
    public void The_timeline_rejects_times_that_are_out_of_range()
    {
        var times = Times() with { HoldSeconds = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => Timeline(times));
    }
}
