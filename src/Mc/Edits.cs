using WorldEdit.Core;

namespace WorldEdit;

/// <summary>The region operations of the commands: build the request from the player's selection and arguments, hand it to the engine, and watch
/// the job. Nothing here touches blocks; the engine applies every change.</summary>
internal sealed class Edits
{
    private readonly WorldEditPlugin _plugin;

    public Edits(WorldEditPlugin plugin) => _plugin = plugin;

    public PlayerSession Session(Player player) => _plugin.Sessions.Get(player.Uuid);

    /// <summary>Sets the first or second corner, at the given position or at the block the player stands in.</summary>
    public int SetCorner(CommandContext ctx, int corner, Point? at)
    {
        Player player = ctx.RequirePlayer();
        Point p = at ?? Coords.BlockOf(player.Position);
        PlayerSession s = Session(player);
        if (corner == 1) s.Selection.SetPos1(p);
        else s.Selection.SetPos2(p);
        string which = corner == 1 ? "First" : "Second";
        ctx.Reply($"{which} position set to ({p.X}, {p.Y}, {p.Z}).{Count(s)}");
        return 1;
    }

    public int Size(CommandContext ctx)
    {
        Player player = ctx.RequirePlayer();
        Box box = Selected(player);
        ctx.Reply($"Selection: {box.SizeX} x {box.SizeY} x {box.SizeZ}, {Coords.Count(box.Volume)} blocks.");
        return 1;
    }

    public int Deselect(CommandContext ctx)
    {
        Player player = ctx.RequirePlayer();
        Session(player).Selection.Clear();
        ctx.Reply("Selection cleared.");
        return 1;
    }

    /// <summary>Expands or contracts the selection on a direction (the facing direction when none is given).</summary>
    public int Modify(CommandContext ctx, int amount, string? direction, bool expand)
    {
        Player player = ctx.RequirePlayer();
        Selected(player);
        Direction d = ResolveDirection(player, direction);
        Selection selection = Session(player).Selection;
        if (expand)
        {
            selection.Expand(d, amount);
        }
        else if (!selection.Contract(d, amount))
        {
            throw new CommandFailure("The selection would vanish: nothing changed.");
        }
        ctx.Reply($"Selection: {Coords.Count(selection.Bounds!.Value.Volume)} blocks.");
        return 1;
    }

    /// <summary>A set, or a walls or faces build, with one pattern.</summary>
    public int Pattern(CommandContext ctx, EditKind kind, string patternText, string label)
    {
        Player player = ctx.RequirePlayer();
        Box box = Selected(player);
        byte[] pattern = CompilePattern(patternText);
        byte[] request = JobWriter.Region(kind, JobFlags.RecordUndo, player.World.Index, 0, box, pattern, null, default);
        return Submit(ctx, player, request, label, null);
    }

    /// <summary>Replace: the mask chooses the blocks, the pattern gives them new states. One word means "replace existing blocks".</summary>
    public int Replace(CommandContext ctx, string text)
    {
        Player player = ctx.RequirePlayer();
        Box box = Selected(player);
        string[] words = CommandArgs.Tokenize(text).ToArray();
        if (words.Length is < 1 or > 2) throw new CommandFailure("Usage: //replace [mask] <pattern>");
        string mask = words.Length == 2 ? words[0] : "#existing";
        string pattern = words[^1];
        Result<byte[]> compiled = MaskCompiler.Compile(mask, EngineBlocks.Instance);
        if (!compiled.Ok) throw new CommandFailure($"Mask: {compiled.Error}");
        byte[] request = JobWriter.Region(EditKind.Replace, JobFlags.RecordUndo, player.World.Index, 0, box, CompilePattern(pattern), compiled.Value, default);
        return Submit(ctx, player, request, "Replace", null);
    }

    /// <summary>Copies the selection into the engine clipboard, with the player's block as the copy origin.</summary>
    public int Copy(CommandContext ctx)
    {
        Player player = ctx.RequirePlayer();
        Box box = Selected(player);
        Point origin = Coords.BlockOf(player.Position);
        PlayerSession s = Session(player);
        int world = player.World.Index;
        byte[] request = JobWriter.Region(EditKind.Copy, JobFlags.None, world, 0, box, null, null, origin);
        return Submit(ctx, player, request, "Copy", info => s.SetClipboard(info.Clipboard, world, origin));
    }

    /// <summary>Cuts the selection: copies it and fills it with the pattern (air by default).</summary>
    public int Cut(CommandContext ctx, string? patternText)
    {
        Player player = ctx.RequirePlayer();
        Box box = Selected(player);
        Point origin = Coords.BlockOf(player.Position);
        PlayerSession s = Session(player);
        int world = player.World.Index;
        byte[] pattern = CompilePattern(string.IsNullOrWhiteSpace(patternText) ? "air" : patternText.Trim());
        byte[] request = JobWriter.Region(EditKind.Cut, JobFlags.RecordUndo, world, 0, box, pattern, null, origin);
        return Submit(ctx, player, request, "Cut", info => s.SetClipboard(info.Clipboard, world, origin));
    }

    /// <summary>Pastes the clipboard with the copy origin on the player's block, or back at the copy origin with <c>-o</c>. <c>-a</c> skips air.</summary>
    public int Paste(CommandContext ctx, string flagText)
    {
        Player player = ctx.RequirePlayer();
        PlayerSession s = Session(player);
        if (s.Clipboard == 0) throw new CommandFailure("Nothing is copied yet: //copy or //cut first.");
        CommandArgs args = CommandArgs.Parse(flagText);
        bool original = args.Has('o');
        if (original && s.ClipboardWorld != player.World.Index) throw new CommandFailure("The clipboard was copied in another world.");
        Point target = original ? s.ClipboardOrigin : Coords.BlockOf(player.Position);
        JobFlags flags = JobFlags.RecordUndo | (args.Has('a') ? JobFlags.SkipAir : JobFlags.None);
        byte[] request = JobWriter.Paste(flags, player.World.Index, s.Clipboard, target, s.PasteRotation, s.PasteFlip);
        return Submit(ctx, player, request, "Paste", null);
    }

    /// <summary>Turns the next paste by a multiple of 90 degrees about the vertical axis.</summary>
    public int Rotate(CommandContext ctx, int degrees)
    {
        Player player = ctx.RequirePlayer();
        if (degrees % 90 != 0) throw new CommandFailure("The rotation must be a multiple of 90 degrees.");
        PlayerSession s = Session(player);
        s.Rotate(degrees / 90);
        ctx.Reply($"The next paste turns {s.PasteRotation * 90} degrees.");
        return 1;
    }

    /// <summary>Flips the next paste across the plane of the direction (the facing direction when none is given).</summary>
    public int Flip(CommandContext ctx, string? direction)
    {
        Player player = ctx.RequirePlayer();
        PlayerSession s = Session(player);
        s.Flip(ResolveDirection(player, direction));
        ctx.Reply($"The next paste is flipped: {s.PasteFlip}.");
        return 1;
    }

    public int ClearClipboard(CommandContext ctx)
    {
        Player player = ctx.RequirePlayer();
        PlayerSession s = Session(player);
        if (s.Clipboard != 0) Edit.ReleaseClipboard(player, s.Clipboard);
        s.SetClipboard(0, 0, default);
        ctx.Reply("Clipboard cleared.");
        return 1;
    }

    public int Undo(CommandContext ctx, int steps, bool redo)
    {
        Player player = ctx.RequirePlayer();
        string label = redo ? "Redo" : "Undo";
        long id = redo ? Edit.Redo(player, steps) : Edit.Undo(player, steps);
        if (id == -2) throw new CommandFailure($"Nothing to {label.ToLowerInvariant()}.");
        if (id <= 0) throw new CommandFailure($"{label} refused: {Coords.Refusal(id)}");
        JobWatch.Start(_plugin.Scheduler, player, id, label);
        return 1;
    }

    public int ClearHistory(CommandContext ctx)
    {
        Player player = ctx.RequirePlayer();
        Edit.ClearHistory(player);
        ctx.Reply("Undo history cleared.");
        return 1;
    }

    /// <summary>The selection of a player, or the failure that says how to make one.</summary>
    public Box Selected(Player player)
    {
        Box? box = Session(player).Selection.Bounds;
        if (box is null) throw new CommandFailure("Make a selection first: //pos1 and //pos2, or the wand.");
        ulong limit = (ulong)Math.Max(0, _plugin.Settings.MaxRegionVolume);
        if (box.Value.Volume > limit)
            throw new CommandFailure($"The selection is {Coords.Count(box.Value.Volume)} blocks; the limit is {Coords.Count(limit)}.");
        return box.Value;
    }

    private int Submit(CommandContext ctx, Player player, byte[] request, string label, Action<EditJobInfo>? onDone)
    {
        long id = Edit.Start(player, request);
        if (id <= 0) throw new CommandFailure($"{label} refused: {Coords.Refusal(id)}");
        ctx.Reply($"{label} queued.");
        JobWatch.Start(_plugin.Scheduler, player, id, label, onDone);
        return 1;
    }

    private static byte[] CompilePattern(string text)
    {
        Result<byte[]> compiled = PatternCompiler.Compile(text, EngineBlocks.Instance);
        if (!compiled.Ok) throw new CommandFailure($"Pattern: {compiled.Error}");
        return compiled.Value!;
    }

    private static Direction ResolveDirection(Player player, string? word)
    {
        if (word is null || word.Length == 0) return Directions.FromYaw(player.Location.Yaw);
        if (Directions.TryParse(word, out Direction d)) return d;
        throw new CommandFailure($"'{word}' is not a direction: north, south, east, west, up or down.");
    }

    private string Count(PlayerSession s) => s.Selection.Bounds is { } b ? $" ({Coords.Count(b.Volume)} blocks)" : string.Empty;
}
