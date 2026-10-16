namespace WorldEdit.Core;

/// <summary>An integer block position.</summary>
public readonly record struct Point(int X, int Y, int Z);

/// <summary>An inclusive box of block positions, with Min at most Max on every axis.</summary>
public readonly record struct Box(Point Min, Point Max)
{
    public int SizeX => Max.X - Min.X + 1;
    public int SizeY => Max.Y - Min.Y + 1;
    public int SizeZ => Max.Z - Min.Z + 1;

    /// <summary>The number of blocks, saturated at <see cref="ulong.MaxValue"/> (a box of 2^32 blocks a side would wrap otherwise).</summary>
    public ulong Volume
    {
        get
        {
            UInt128 v = (UInt128)(uint)SizeX * (uint)SizeY * (uint)SizeZ;
            return v > ulong.MaxValue ? ulong.MaxValue : (ulong)v;
        }
    }

    public bool IsValid => Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;

    public bool Contains(Point p) => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

    public static Box FromCorners(Point a, Point b) =>
        new(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
}

/// <summary>The six directions of the block grid, with the Minecraft axes: north is -Z, south +Z, east +X, west -X, up +Y, down -Y.</summary>
public enum Direction
{
    Up,
    Down,
    North,
    South,
    East,
    West,
}

public static class Directions
{
    /// <summary>Parses a direction word: the full names, or the single letters u, d, n, s, e, w.</summary>
    public static bool TryParse(string word, out Direction direction)
    {
        switch (word.ToLowerInvariant())
        {
            case "up" or "u": direction = Direction.Up; return true;
            case "down" or "d": direction = Direction.Down; return true;
            case "north" or "n": direction = Direction.North; return true;
            case "south" or "s": direction = Direction.South; return true;
            case "east" or "e": direction = Direction.East; return true;
            case "west" or "w": direction = Direction.West; return true;
            default: direction = default; return false;
        }
    }

    /// <summary>The horizontal direction a player faces, from the yaw the engine reports (0 = south, 90 = west, 180 = north, 270 = east).</summary>
    public static Direction FromYaw(float yaw)
    {
        int quarter = (int)Math.Floor(((yaw % 360 + 360) % 360 + 45) / 90) % 4;
        return quarter switch
        {
            0 => Direction.South,
            1 => Direction.West,
            2 => Direction.North,
            _ => Direction.East,
        };
    }

    /// <summary>Whether moving in the direction increases the coordinate of its axis.</summary>
    public static bool IsPositive(Direction d) => d is Direction.Up or Direction.South or Direction.East;

    /// <summary>The axis of a direction: 0 for x, 1 for y, 2 for z.</summary>
    public static int Axis(Direction d) => d switch
    {
        Direction.East or Direction.West => 0,
        Direction.Up or Direction.Down => 1,
        _ => 2,
    };
}
