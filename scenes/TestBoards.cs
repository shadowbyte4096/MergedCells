using System;
using System.Collections.Generic;
using MergedCells.Core;

namespace MergedCells;

// Large boards for checking the camera and the drawing cost, played after the level files until chunk 11 replaces Main
public static class TestBoards
{
    // Chunk 4's 10x10 block of rectangles. Its bottom-right square is 1x1, so a tiled map can end in its far corner
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

    // L-shapes, two zigzags (R and U) and a ring (A round k), ending on a 1x1 z
    private static readonly string[] IrregularBlock =
    [
        "AAABCCCDDE",
        "AkABCFFDGE",
        "AAABCFHHGE",
        "IIJJKKHLGE",
        "IMMJKNNLLO",
        "IMPPKNQQRO",
        "SSPTTUQRRO",
        "SVVTWUUXYO",
        "ZZVWWaUXYb",
        "ccddeeffgz",
    ];

    // The lanes board is first: the tiled boards' regions are too short for a slide to leave the screen
    public static IReadOnlyList<(string Title, Func<Map> Build)> All { get; } =
    [
        ("Test · 50×50 lanes", Lanes),
        ("Test · 50×50 tiled", () => Tiled(Block, 5)),
        ("Test · 100×100 tiled", () => Tiled(Block, 10)),
        ("Test · 100×100 irregular", () => Tiled(IrregularBlock, 10)),
    ];

    // Two full-height lanes (columns 10 and 30) cut every row into three strips, so slides run up to 49 squares
    private static Map Lanes()
    {
        const int size = 50;
        var regions = new int[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                regions[row, column] = column switch
                {
                    10 => -1,
                    30 => -2,
                    < 10 => row * 3,
                    < 30 => row * 3 + 1,
                    _ => row * 3 + 2,
                };
            }
        }
        regions[size - 1, size - 1] = 999;
        return Map.Create(regions, new Cell(0, 0), new Cell(size - 1, size - 1));
    }

    // A square of blocks x blocks copies of a 10x10 block, each copy with its own region IDs
    private static Map Tiled(string[] block, int blocks)
    {
        var size = blocks * 10;
        var regions = new int[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var copy = row / 10 * blocks + column / 10;
                regions[row, column] = copy * 128 + block[row % 10][column % 10];
            }
        }
        return Map.Create(regions, new Cell(0, 0), new Cell(size - 1, size - 1));
    }
}
