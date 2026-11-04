using MergedCells.Core;

namespace MergedCells;

// Stand-ins for real levels until chunk 6 or 7 loads them from files
public static class DemoMaps
{
    // The developer's sketch level
    private static readonly string[] SketchRows =
    [
        "SAAAAA",
        "BCCDDD",
        "BCCDDD",
        "BCCEEE",
        "BCCFFF",
        "BGHFFF",
        "IJHKLM",
        "IJHOOO",
        "QQQOOO",
        "PPPPPZ",
    ];

    // Its bottom-right square is 1x1, so a tiled map can end in its far corner
    private static readonly string[] Block =
    [
        "AAAABCCCCD",
        "EFFFBGHHHD",
        "EIIJBGKKLD",
        "EIIJMMKKLN",
        "OOOJPQQQLN",
        "RSTTPUUVVN",
        "RSTTWUUXYY",
        "RZZZWabXcd",
        "eeffWabXcd",
        "ggghhhijjk",
    ];

    public static Map Sketch()
    {
        var regions = new int[SketchRows.Length, SketchRows[0].Length];
        for (var row = 0; row < regions.GetLength(0); row++)
        {
            for (var column = 0; column < regions.GetLength(1); column++)
            {
                regions[row, column] = SketchRows[row][column];
            }
        }
        return Map.Create(regions, new Cell(0, 0), new Cell(9, 5));
    }

    // A square of blocks x blocks copies of the 10x10 block, each copy with its own region IDs
    public static Map Tiled(int blocks)
    {
        var size = blocks * 10;
        var regions = new int[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var copy = row / 10 * blocks + column / 10;
                regions[row, column] = copy * 128 + Block[row % 10][column % 10];
            }
        }
        return Map.Create(regions, new Cell(0, 0), new Cell(size - 1, size - 1));
    }
}
