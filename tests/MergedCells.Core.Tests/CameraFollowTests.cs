using System.Numerics;

namespace MergedCells.Core.Tests;

// Glides sampled every 1/60 s. The cases come from the chunk 9 simulation of slides near and into the map's edges,
// on a portrait band like the phone's, so the boards, slides and limits are literals
public class CameraFollowTests
{
    private const float Square = 64;
    private static readonly Vector2 Phone = new(720, 1300);
    private static readonly SlideConfig Slide = new() { SquaresPerSecond = 15, MaxSeconds = 0.4 };

    // (board side, from column, from row, to column, to row)
    public static TheoryData<int, int, int, int, int> NearAnEdge => new()
    {
        { 50, 2, 3, 47, 3 },
        { 50, 10, 10, 40, 10 },
        { 50, 2, 25, 47, 25 },
        { 50, 47, 46, 2, 46 },
        { 50, 3, 2, 3, 47 },
        { 50, 46, 47, 46, 2 },
        { 50, 10, 0, 10, 49 },
        { 100, 5, 12, 95, 12 },
        { 100, 30, 20, 70, 20 },
        { 100, 8, 90, 8, 30 },
        { 100, 5, 0, 60, 0 },
        { 100, 50, 0, 50, 60 },
    };

    public static TheoryData<int, int, int, int, int> IntoAnEdge => new()
    {
        { 100, 50, 40, 50, 0 },
        { 100, 40, 50, 0, 50 },
        { 100, 50, 59, 50, 99 },
        { 100, 59, 50, 99, 50 },
        { 50, 19, 30, 49, 30 },
        { 50, 20, 9, 20, 49 },
    };

    private sealed record Frame(CameraView Planned, CameraView Shown, Vector2 Player);

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    public void Player_stays_on_screen_during_a_long_slide(int columnStep, int rowStep)
    {
        var space = new CameraSpace(
            new BoardRect(0, 0, 100 * Square, 100 * Square), new Vector2(720, 1300), 24, Square, CameraSpaceTests.Config);
        var start = new Vector2(50 * Square + Square / 2, 50 * Square + Square / 2);
        var stop = start + new Vector2(columnStep, rowStep) * 40 * Square;
        var current = space.Centre(start, space.ReadableZoom);
        var slide = SlideTiming.Seconds(40, Slide);
        var glide = space.PlanFollow(current, current, space.ReadableZoom, start, stop, slide);

        Assert.NotNull(glide);
        Assert.NotNull(glide.HalfwayZoom);
        for (var frame = 0; frame / 60.0 <= glide.Delay + glide.Seconds; frame++)
        {
            var time = frame / 60.0;
            var player = Vector2.Lerp(start, stop, (float)Math.Min(time / slide, 1));
            Assert.True(space.ShowsSquare(space.Clamp(glide.Sample(time)), player), $"Player off screen at {time:0.000} s");
        }
    }

    [Theory]
    [MemberData(nameof(NearAnEdge))]
    [MemberData(nameof(IntoAnEdge))]
    public void Planned_glides_never_need_the_clamp(int board, int fromColumn, int fromRow, int toColumn, int toRow)
    {
        var (_, _, frames) = Follow(board, fromColumn, fromRow, toColumn, toRow);

        Assert.All(frames, frame =>
            Assert.True(
                Vector2.Distance(frame.Planned.Focus, frame.Shown.Focus) * frame.Planned.Zoom < 1,
                $"Clamped by {Vector2.Distance(frame.Planned.Focus, frame.Shown.Focus) * frame.Planned.Zoom:0.00} viewport units"));
    }

    [Theory]
    [MemberData(nameof(NearAnEdge))]
    [MemberData(nameof(IntoAnEdge))]
    public void Player_stays_on_screen_near_an_edge(int board, int fromColumn, int fromRow, int toColumn, int toRow)
    {
        var (space, _, frames) = Follow(board, fromColumn, fromRow, toColumn, toRow);

        Assert.All(frames, frame => Assert.True(space.ShowsSquare(frame.Shown, frame.Player)));
    }

    [Theory]
    [MemberData(nameof(NearAnEdge))]
    public void Planned_glide_jolts_no_more_than_chunk_8(int board, int fromColumn, int fromRow, int toColumn, int toRow)
    {
        var alongRow = fromRow == toRow;

        var planned = Jolt(Follow(board, fromColumn, fromRow, toColumn, toRow).Frames, alongRow);
        var chunk8 = Jolt(Follow(board, fromColumn, fromRow, toColumn, toRow, planned: false).Frames, alongRow);

        // 5% allowance: on the 100x100 row-12 slide the view is pinned to the top edge from the first frame, so both paths
        // follow the edge as the zoom-out starts and stops at full speed, and the planned one, sampled at 65 moments,
        // comes out about 3% higher there
        Assert.True(planned <= chunk8 * 1.05f + 0.5f, $"Jolt {planned:0.0} against chunk 8's {chunk8:0.0}");
    }

    [Theory]
    [InlineData(100, 30, 20, 70, 20)]
    [InlineData(50, 2, 25, 47, 25)]
    public void Planned_glide_halves_the_jolt(int board, int fromColumn, int fromRow, int toColumn, int toRow)
    {
        var planned = Jolt(Follow(board, fromColumn, fromRow, toColumn, toRow).Frames, alongRow: true);
        var chunk8 = Jolt(Follow(board, fromColumn, fromRow, toColumn, toRow, planned: false).Frames, alongRow: true);

        Assert.True(planned <= chunk8 / 2, $"Jolt {planned:0.0} against chunk 8's {chunk8:0.0}");
    }

    [Fact]
    public void Glide_clear_of_the_edges_has_no_offsets()
    {
        var (_, glide, _) = Follow(100, 30, 50, 70, 50);

        Assert.Null(glide.Offsets);
    }

    [Fact]
    public void Undo_late_in_a_long_slide_shows_the_player_from_halfway()
    {
        var space = Board(100);
        var start = Centre(50, 50);
        var stop = Centre(90, 50);
        var readable = space.ReadableZoom;
        var view = space.Centre(start, readable);
        var follow = space.PlanFollow(view, view, readable, start, stop, SlideTiming.Seconds(40, Slide))!;

        var recentre = space.PlanRecentre(space.Clamp(follow.Sample(0.35)), space.Clamp(follow.To), readable, start);

        Assert.NotNull(recentre);
        for (var time = recentre.Seconds / 2; time <= recentre.Seconds; time += 1 / 60.0)
        {
            Assert.True(space.ShowsSquare(space.Clamp(recentre.Sample(time)), start), $"Player off screen at {time:0.000} s");
        }
    }

    private static CameraSpace Board(int squares) =>
        new(new BoardRect(0, 0, squares * Square, squares * Square), Phone, 24, Square, CameraSpaceTests.Config);

    private static Vector2 Centre(int column, int row) => new((column + 0.5f) * Square, (row + 0.5f) * Square);

    // A slide from the readable zoom centred on its start; planned: false is chunk 8's glide, without the offsets
    private static (CameraSpace Space, CameraGlide Glide, List<Frame> Frames) Follow(
        int board, int fromColumn, int fromRow, int toColumn, int toRow, bool planned = true)
    {
        var space = Board(board);
        var from = Centre(fromColumn, fromRow);
        var to = Centre(toColumn, toRow);
        var slide = SlideTiming.Seconds(Math.Abs(toColumn - fromColumn) + Math.Abs(toRow - fromRow), Slide);
        var current = space.Centre(from, space.ReadableZoom);
        var glide = space.PlanFollow(current, current, space.ReadableZoom, from, to, slide)
            ?? throw new InvalidOperationException("No glide was planned.");
        if (!planned)
        {
            glide = new CameraGlide(glide.From, glide.To, glide.HalfwayZoom, glide.Seconds, glide.Delay);
        }

        var frames = new List<Frame>();
        for (var frame = 0; frame / 60.0 <= glide.Delay + glide.Seconds; frame++)
        {
            var time = frame / 60.0;
            var sample = glide.Sample(time);
            frames.Add(new Frame(sample, space.Clamp(sample), Vector2.Lerp(from, to, (float)Math.Min(time / slide, 1))));
        }
        return (space, glide, frames);
    }

    // The largest change between frames in the player's on-screen speed across the slide, in viewport units. The view
    // is still before and after the glide, so those frames count too: a glide that starts or stops at full speed jolts
    private static float Jolt(List<Frame> frames, bool alongRow)
    {
        float Across(Frame frame)
        {
            var screen = (frame.Player - frame.Shown.Focus) * frame.Shown.Zoom;
            return alongRow ? screen.Y : screen.X;
        }

        var still = frames.Prepend(frames[0]).Append(frames[^1]).Select(Across).ToList();
        var worst = 0f;
        for (var i = 2; i < still.Count; i++)
        {
            worst = Math.Max(worst, Math.Abs(still[i] - 2 * still[i - 1] + still[i - 2]));
        }
        return worst;
    }
}
