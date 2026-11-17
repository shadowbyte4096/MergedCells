using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class LevelEditsFuzzTests
{
    [Theory]
    [AutoData]
    public void Random_edits_always_give_valid_maps(ulong seed)
    {
        var random = new GameRandom(seed);
        var map = LevelEdits.Blank(random.Next(2, 9), random.Next(2, 9));
        for (var edit = 0; edit < 300; edit++)
        {
            var result = RandomEdit(map, random);

            Assert.Empty(Map.Validate(result.Map.RegionGrid(), result.Map.Start, result.Map.End, result.Map.TileGrid(), result.Map.Teleporters));
            map = result.Map;
        }
    }

    // Strokes as a finger makes them: wandering, doubling back, leaving the board and coming back in
    [Theory]
    [AutoData]
    public void Random_strokes_always_paint_valid_maps(ulong seed)
    {
        var random = new GameRandom(seed);
        var map = LevelEdits.Blank(random.Next(2, 9), random.Next(2, 9));
        for (var edit = 0; edit < 200; edit++)
        {
            var stroke = new EditStroke(0);
            stroke.Press(0, 0, 0, RandomSquare(map, random));
            var steps = random.Next(1, 12);
            for (var step = 1; step <= steps; step++)
            {
                Cell? square = random.Next(5) == 0 ? null : RandomSquare(map, random);
                stroke.Drag(0, step, step, square);
            }
            var end = stroke.Release(0)!;

            var result = end.IsTap ? LevelEdits.Split(map, end.Cells[0]) : LevelEdits.Paint(map, end.Cells);

            Assert.Empty(Map.Validate(result.Map.RegionGrid(), result.Map.Start, result.Map.End, result.Map.TileGrid(), result.Map.Teleporters));
            map = result.Map;
        }
    }

    private static EditResult RandomEdit(Map map, GameRandom random)
    {
        var square = RandomSquare(map, random);
        var line = CellPath.Line(square, RandomSquare(map, random));
        return random.Next(10) switch
        {
            0 => LevelEdits.Paint(map, line),
            1 => LevelEdits.Split(map, square),
            2 => LevelEdits.Erase(map, line.ToArray()),
            3 => LevelEdits.PlaceTile(map, line.ToArray(), Tile.Wall),
            4 => LevelEdits.PlaceTile(map, line.ToArray(), Tile.Trap),
            5 => LevelEdits.AddTeleporters(map, square, RandomSquare(map, random)),
            6 => LevelEdits.MoveStart(map, square),
            7 => LevelEdits.MoveEnd(map, square),
            8 => LevelEdits.Resize(map, random.Next(2, 9), random.Next(2, 9)),
            _ => LevelEdits.Clear(map),
        };
    }

    private static Cell RandomSquare(Map map, GameRandom random) => new(random.Next(map.Rows), random.Next(map.Columns));
}
