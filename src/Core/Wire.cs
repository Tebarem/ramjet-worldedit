namespace WorldEdit.Core;

/// <summary>The byte formats the engine's edit module reads (the WEPT, WEMK and WEJB formats). Everything is little-endian.</summary>
public static class WireFormat
{
    public static readonly byte[] JobMagic = "WEJB"u8.ToArray();
    public static readonly byte[] PatternMagic = "WEPT"u8.ToArray();
    public static readonly byte[] MaskMagic = "WEMK"u8.ToArray();
    public const byte Version = 1;

    /// <summary>The region kind of a cuboid, the only one this version sends.</summary>
    public const byte CuboidRegion = 1;
}

/// <summary>Writes little-endian fields into a growing buffer.</summary>
public sealed class Writer
{
    private readonly List<byte> _bytes = new();

    public Writer Byte(byte v)
    {
        _bytes.Add(v);
        return this;
    }

    public Writer Short(ushort v)
    {
        _bytes.Add((byte)v);
        _bytes.Add((byte)(v >> 8));
        return this;
    }

    public Writer Uint(uint v)
    {
        for (int i = 0; i < 4; i++) _bytes.Add((byte)(v >> (8 * i)));
        return this;
    }

    public Writer Int(int v) => Uint(unchecked((uint)v));

    public Writer Ulong(ulong v)
    {
        for (int i = 0; i < 8; i++) _bytes.Add((byte)(v >> (8 * i)));
        return this;
    }

    public Writer Bytes(byte[] v)
    {
        _bytes.AddRange(v);
        return this;
    }

    /// <summary>A coordinate triple, x then y then z.</summary>
    public Writer Cell(Point p) => Int(p.X).Int(p.Y).Int(p.Z);

    public byte[] ToArray() => _bytes.ToArray();
}

/// <summary>The kinds of bulk edit a job request asks for (wire values: never renumber).</summary>
public enum EditKind : byte
{
    Set = 1,
    Replace = 2,
    Walls = 4,
    Faces = 5,
    Copy = 21,
    Paste = 22,
    Cut = 23,
}

/// <summary>Request flags of a job (bit set).</summary>
[Flags]
public enum JobFlags : ushort
{
    None = 0,
    RecordUndo = 1 << 0,
    Physics = 1 << 1,
    SkipAir = 1 << 4,
}

/// <summary>Paste flips, one bit per axis.</summary>
[Flags]
public enum FlipAxes : byte
{
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2,
}

/// <summary>Builds the job request of an edit (the WEJB format). The engine validates every field again; this writer only lays the bytes out.</summary>
public static class JobWriter
{
    /// <summary>A region job: set, replace, walls, faces, cut (pattern, optional mask) or copy (optional mask, origin).</summary>
    public static byte[] Region(EditKind kind, JobFlags flags, int world, ulong maxChanges, Box box, byte[]? pattern, byte[]? mask, Point origin)
    {
        var w = Header(kind, flags, world, maxChanges, box);
        Program(w, pattern);
        Program(w, mask);
        if (kind is EditKind.Copy or EditKind.Cut) w.Cell(origin);
        return w.ToArray();
    }

    /// <summary>A paste of an engine clipboard. The destination box is the clipboard's, placed so that its copy origin lands on
    /// <paramref name="target"/>; the region written here is a placeholder the engine ignores.</summary>
    public static byte[] Paste(JobFlags flags, int world, ulong clipboard, Point target, int rotation, FlipAxes flip)
    {
        var w = Header(EditKind.Paste, flags, world, 0, new Box(new Point(0, 0, 0), new Point(0, 0, 0)));
        Program(w, null);
        Program(w, null);
        w.Ulong(clipboard).Cell(target).Byte((byte)rotation).Byte((byte)flip);
        return w.ToArray();
    }

    private static Writer Header(EditKind kind, JobFlags flags, int world, ulong maxChanges, Box box)
    {
        var w = new Writer();
        w.Bytes(WireFormat.JobMagic).Byte(WireFormat.Version).Byte((byte)kind).Short((ushort)flags).Int(world).Ulong(maxChanges);
        w.Byte(WireFormat.CuboidRegion).Byte(0).Cell(box.Min).Cell(box.Max);
        return w;
    }

    // a program with its length (0 when absent)
    private static void Program(Writer w, byte[]? program)
    {
        if (program is null)
        {
            w.Uint(0);
            return;
        }
        w.Uint((uint)program.Length).Bytes(program);
    }
}
