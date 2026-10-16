using WorldEdit.Core;
using static WorldEdit.Tests.Program;

namespace WorldEdit.Tests;

/// <summary>Block names for the compiler tests: a table in place of the engine's parser.</summary>
internal sealed class FakeBlocks : IBlockResolver
{
    private static readonly Dictionary<string, uint> States = new()
    {
        ["stone"] = 1,
        ["minecraft:stone"] = 1,
        ["dirt"] = 2,
        ["minecraft:dirt"] = 2,
        ["air"] = 0,
        ["oak_stairs[facing=north]"] = 3,
    };

    public uint? ParseState(string text) => States.TryGetValue(text.Trim(), out uint id) ? id : null;
}

internal static class SelectionTests
{
    public static void Bounds()
    {
        var sel = new Selection();
        Check(!sel.IsComplete, "a new selection is incomplete");
        sel.SetPos1(new Point(5, 70, -3));
        Check(sel.Bounds is null, "one corner is not a box");
        sel.SetPos2(new Point(2, 64, 4));
        Box box = sel.Bounds!.Value;
        Equal(new Point(2, 64, -3), box.Min, "min corner");
        Equal(new Point(5, 70, 4), box.Max, "max corner");
        Equal(4, box.SizeX, "size x");
        Equal(7, box.SizeY, "size y");
        Equal(8, box.SizeZ, "size z");
        Equal(224ul, box.Volume, "volume 4 x 7 x 8");
    }

    public static void ContractAndPad()
    {
        var sel = new Selection();
        sel.SetPos1(new Point(0, 0, 0));
        sel.SetPos2(new Point(9, 9, 9));
        sel.Expand(Direction.North, 3);
        Equal(-3, sel.Bounds!.Value.Min.Z, "expand north grows the minimum z");
        sel.Expand(Direction.Up, 2);
        Equal(11, sel.Bounds!.Value.Max.Y, "expand up grows the maximum y");
        Check(sel.Contract(Direction.South, 1), "contract south is allowed");
        Equal(8, sel.Bounds!.Value.Max.Z, "contract south shrinks the maximum z");
        Check(!sel.Contract(Direction.East, 100), "a contract that would vanish is refused");
        Equal(9, sel.Bounds!.Value.Max.X, "a refused contract changes nothing");

        sel.Outset(1, horizontal: true, vertical: false);
        Equal(-1, sel.Bounds!.Value.Min.X, "horizontal outset moves x");
        Equal(0, sel.Bounds!.Value.Min.Y, "horizontal outset leaves y alone");
        sel.Inset(1, horizontal: true, vertical: true);
        Equal(0, sel.Bounds!.Value.Min.X, "inset takes the outset back");
        Equal(1, sel.Bounds!.Value.Min.Y, "inset on y too");
    }
}

internal static class DirectionTests
{
    public static void Yaw()
    {
        Equal(Direction.South, Directions.FromYaw(0), "yaw 0 faces south");
        Equal(Direction.West, Directions.FromYaw(90), "yaw 90 faces west");
        Equal(Direction.North, Directions.FromYaw(180), "yaw 180 faces north");
        Equal(Direction.East, Directions.FromYaw(270), "yaw 270 faces east");
        Equal(Direction.East, Directions.FromYaw(-90), "a negative yaw wraps");
        Equal(Direction.South, Directions.FromYaw(44), "44 degrees is still south");
        Equal(Direction.West, Directions.FromYaw(46), "46 degrees is west");
        Check(Directions.TryParse("n", out Direction n) && n == Direction.North, "n parses to north");
        Check(Directions.TryParse("UP", out Direction up) && up == Direction.Up, "words are case-insensitive");
        Check(!Directions.TryParse("x", out _), "x is not a direction");
    }
}

internal static class ArgsTests
{
    public static void Splitting()
    {
        CommandArgs a = CommandArgs.Parse("-a -m stone 5 up", "m");
        Check(a.Has('a'), "-a is a flag");
        Equal("stone", a.Value('m'), "-m takes the next word");
        Equal(2, a.Positional.Count, "positional count");
        Equal("5", a.Positional[0], "first positional");

        CommandArgs quoted = CommandArgs.Parse("\"stone:2, dirt\" -o");
        Equal(1, quoted.Positional.Count, "a quoted run is one word");
        Equal("stone:2, dirt", quoted.Positional[0], "quotes keep the spaces");
        Check(quoted.Has('o'), "-o after a quote is a flag");

        CommandArgs negative = CommandArgs.Parse("-5 up");
        Equal("-5", negative.Positional[0], "a minus digit is positional");
        Equal(0, negative.Positional.Count - 2, "two positional words");

        CommandArgs combined = CommandArgs.Parse("-ao");
        Check(combined.Has('a') && combined.Has('o'), "-ao is two flags");

        CommandArgs trailing = CommandArgs.Parse("-m", "m");
        Equal("", trailing.Value('m'), "a value flag at the end has an empty value");
        Check(trailing.Value('x') is null, "an unset value is null");
    }
}

internal static class PatternTests
{
    public static void Compile()
    {
        var blocks = new FakeBlocks();

        Result<byte[]> single = PatternCompiler.Compile("stone", blocks);
        Check(single.Ok, "a single block compiles");
        byte[] b = single.Value!;
        Equal(12, b.Length, "WEPT header (7) and one single node (5)");
        Equal((byte)'W', b[0], "magic W");
        Equal((byte)'T', b[3], "magic T");
        Equal((byte)1, b[4], "version");
        Equal((byte)1, b[5], "one node");
        Equal((byte)0, b[7], "single node type");
        Equal((byte)1, b[8], "state 1 (low byte)");

        Result<byte[]> weighted = PatternCompiler.Compile("50%stone,50%dirt", blocks);
        Check(weighted.Ok, "a weighted list compiles");
        Equal((byte)3, weighted.Value![5], "two singles and a weighted root");
        Equal((byte)1, weighted.Value[17], "the root is the weighted node");

        Result<byte[]> random = PatternCompiler.Compile("*stone", blocks);
        Check(random.Ok && random.Value![7] == 2, "*stone is a random-state node");

        Check(!PatternCompiler.Compile("50%stone", blocks).Ok, "a lone weighted entry is refused");
        Check(!PatternCompiler.Compile("0%stone,dirt", blocks).Ok, "a zero weight is refused");
        Check(!PatternCompiler.Compile("stone,", blocks).Ok, "an empty entry is refused");
        Check(!PatternCompiler.Compile("wat", blocks).Ok, "an unknown block is refused");
        Check(!PatternCompiler.Compile("#clipboard", blocks).Ok, "#clipboard is not supported yet");
        Check(!PatternCompiler.Compile("", blocks).Ok, "an empty pattern is refused");
    }
}

internal static class MaskTests
{
    public static void Compile()
    {
        var blocks = new FakeBlocks();

        Result<byte[]> union = MaskCompiler.Compile("stone,dirt", blocks);
        Check(union.Ok, "a union compiles");
        Equal((byte)3, union.Value![5], "two block nodes and an or root");
        Equal((byte)6, union.Value[25], "the root is an or");

        Result<byte[]> negated = MaskCompiler.Compile("!stone", blocks);
        Check(negated.Ok && negated.Value![5] == 2 && negated.Value[16] == 4, "!stone is a not over the block");

        Result<byte[]> existing = MaskCompiler.Compile("#existing", blocks);
        Check(existing.Ok && existing.Value![7] == 3, "#existing is its own node");

        Result<byte[]> state = MaskCompiler.Compile("oak_stairs[facing=north]", blocks);
        Check(state.Ok && state.Value![7] == 1 && state.Value[12] == 3, "a state is an explicit state list");

        Check(!MaskCompiler.Compile("", blocks).Ok, "an empty mask is refused");
        Check(!MaskCompiler.Compile("#solid", blocks).Ok, "#solid is not supported yet");
        Check(!MaskCompiler.Compile("stone dirt", blocks).Ok, "intersections are not supported yet");
    }
}

internal static class JobTests
{
    public static void Bytes()
    {
        byte[] pattern = PatternCompiler.Compile("stone", new FakeBlocks()).Value!;
        byte[] set = JobWriter.Region(EditKind.Set, JobFlags.RecordUndo, 1, 0, new Box(new Point(0, 0, 0), new Point(2, 2, 2)), pattern, null,
            new Point(0, 0, 0));
        Equal((byte)'W', set[0], "job magic");
        Equal((byte)1, set[4], "job version");
        Equal((byte)1, set[5], "kind set");
        Equal((byte)1, set[6], "record undo flag, low byte");
        Equal((byte)1, set[8], "world 1");
        Equal((byte)1, set[20], "cuboid region");
        Equal((byte)2, set[34], "max x is 2");
        Equal(46 + 4 + pattern.Length + 4, set.Length, "header, region, pattern, empty mask");

        byte[] paste = JobWriter.Paste(JobFlags.SkipAir, 1, 77, new Point(10, 64, 10), 1, FlipAxes.X | FlipAxes.Z);
        Equal((byte)22, paste[5], "kind paste");
        Equal((byte)1, paste[^2], "rotation is the second last byte");
        Equal((byte)(FlipAxes.X | FlipAxes.Z), paste[^1], "flip is the last byte");
        Equal((byte)77, paste[^(2 + 12 + 8)], "the clipboard handle starts the parameters");
    }
}

internal static class SessionTests
{
    public static void RotateFlipSweep()
    {
        var s = new PlayerSession();
        s.Rotate(-1);
        Equal(3, s.PasteRotation, "one quarter turn back from zero is three");
        s.Rotate(5);
        Equal(0, s.PasteRotation, "turns wrap modulo four");
        s.Flip(Direction.North);
        Check(s.PasteFlip == FlipAxes.Z, "north flips z");
        s.Flip(Direction.North);
        Check(s.PasteFlip == FlipAxes.None, "a second flip undoes the first");
        s.Flip(Direction.East);
        Check(s.PasteFlip == FlipAxes.X, "east flips x");
        s.SetClipboard(9, 1, new Point(0, 0, 0));
        Check(s.PasteFlip == FlipAxes.None && s.PasteRotation == 0, "a copy resets the paste transform");

        var store = new SessionStore<string>();
        var t0 = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        store.Get("alex");
        store.MarkLeft("alex", t0);
        Equal(0, store.Sweep(t0 + TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)), "a player away five minutes keeps the session");
        Equal(1, store.Sweep(t0 + TimeSpan.FromMinutes(11), TimeSpan.FromMinutes(10)), "after ten minutes the session goes");
        Check(store.Find("alex") is null, "the session is gone");
    }
}
