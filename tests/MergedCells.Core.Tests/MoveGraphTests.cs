using AutoFixture;

namespace MergedCells.Core.Tests;

public class MoveGraphTests
{
    private const int Up = 0;
    private const int Down = 1;
    private const int Right = 3;

    // s Right to a, which wins Down; s Down to b, which can only slide to c and back
    private static (MoveGraph Graph, Cell S, Cell A, Cell B, Cell C) WinAndPocket()
    {
        var cells = new Fixture().CreateMany<Cell>(4).ToArray();
        var (s, a, b, c) = (cells[0], cells[1], cells[2], cells[3]);
        var rule = new MoveRule()
            .Slide(s, Direction.Right, a)
            .Win(a, Direction.Down)
            .Slide(s, Direction.Down, b)
            .Slide(b, Direction.Right, c)
            .Slide(c, Direction.Left, b);
        return (MoveGraph.Build(s, rule.Apply), s, a, b, c);
    }

    private static int StopOf(MoveGraph graph, Cell cell) =>
        Enumerable.Range(0, graph.StopCount).Single(stop => graph.CellOf(stop) == cell);

    [Fact]
    public void Moves_to_the_end_count_the_fewest_winning_moves()
    {
        var (graph, s, a, _, _) = WinAndPocket();

        Assert.Equal((2, 1), (graph.MovesToEnd(StopOf(graph, s)), graph.MovesToEnd(StopOf(graph, a))));
    }

    [Fact]
    public void A_dead_end_has_no_moves_to_the_end()
    {
        var (graph, _, _, b, c) = WinAndPocket();

        Assert.Equal(
            (MoveGraph.None, MoveGraph.None),
            (graph.MovesToEnd(StopOf(graph, b)), graph.MovesToEnd(StopOf(graph, c))));
    }

    [Fact]
    public void The_start_is_stop_0()
    {
        var (graph, s, _, _, _) = WinAndPocket();

        Assert.Equal(s, graph.CellOf(0));
    }

    [Fact]
    public void Moves_hold_each_direction_s_stop_or_win()
    {
        var (graph, _, a, _, _) = WinAndPocket();
        var stopOfA = StopOf(graph, a);

        Assert.Equal((stopOfA, MoveGraph.None, MoveGraph.Win), (graph.Move(0, Right), graph.Move(0, Up), graph.Move(stopOfA, Down)));
    }

    [Fact]
    public void Looping_slides_are_counted_once_and_are_no_move()
    {
        var start = new Fixture().Create<Cell>();
        var rule = new MoveRule().Loop(start, Direction.Up).Win(start, Direction.Right);

        var graph = MoveGraph.Build(start, rule.Apply);

        Assert.Equal((1, MoveGraph.None), (graph.LoopingSlides, graph.Move(0, Up)));
    }
}
