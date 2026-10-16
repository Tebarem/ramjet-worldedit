using WorldEdit.Core;

namespace WorldEdit;

/// <summary>The command trees. Names and aliases follow WorldEdit's public documentation: <c>//</c> roots are literal names with two slashes,
/// plain roots have none. Every root has a permission node, so a player without it does not see the command.</summary>
internal sealed class CommandSet
{
    private readonly WorldEditPlugin _plugin;
    private readonly Edits _edits;

    public CommandSet(WorldEditPlugin plugin)
    {
        _plugin = plugin;
        _edits = new Edits(plugin);
    }

    public void RegisterAll()
    {
        Selection();
        Regions();
        Clipboard();
        History();
        Navigation();
        Help();
    }

    private void Selection()
    {
        Register(
            Command.Literal("//pos1").Permission(Nodes.SelectionPos)
                .Executes(ctx => _edits.SetCorner(ctx, 1, null))
                .Then(Command.Argument("position", Arg.BlockPos()).Executes(ctx => _edits.SetCorner(ctx, 1, Coords.ToPoint(ctx.GetBlockPos("position"))))),
            null, "Set the first corner of the selection");

        Register(
            Command.Literal("//pos2").Permission(Nodes.SelectionPos)
                .Executes(ctx => _edits.SetCorner(ctx, 2, null))
                .Then(Command.Argument("position", Arg.BlockPos()).Executes(ctx => _edits.SetCorner(ctx, 2, Coords.ToPoint(ctx.GetBlockPos("position"))))),
            null, "Set the second corner of the selection");

        Register(Command.Literal("//desel").Permission(Nodes.SelectionModify).Executes(ctx => _edits.Deselect(ctx)), null, "Clear the selection");

        Register(Command.Literal("//size").Permission(Nodes.SelectionSize).Executes(ctx => _edits.Size(ctx)), null, "Show the size of the selection");

        Register(GrowOrShrink("//expand", expand: true), null, "Grow the selection on a side");
        Register(GrowOrShrink("//contract", expand: false), null, "Shrink the selection from a side");
    }

    private CommandNode GrowOrShrink(string name, bool expand)
    {
        return Command.Literal(name).Permission(Nodes.SelectionModify)
            .Then(Command.Argument("amount", Arg.Int(1, 4096))
                .Executes(ctx => _edits.Modify(ctx, ctx.GetInt("amount"), null, expand))
                .Then(Command.Argument("direction", Arg.Word())
                    .Executes(ctx => _edits.Modify(ctx, ctx.GetInt("amount"), ctx.GetString("direction"), expand))));
    }

    private void Regions()
    {
        Register(Usage("//set", Nodes.RegionSet, "<pattern>", ctx => _edits.Pattern(ctx, EditKind.Set, ctx.GetString("pattern"), "Set")),
            null, "Fill the selection with a pattern");

        Register(Command.Literal("//replace").Permission(Nodes.RegionReplace)
                .Executes(ctx => throw new CommandFailure("Usage: //replace [mask] <pattern>"))
                .Then(Command.Argument("text", Arg.GreedyString()).Executes(ctx => _edits.Replace(ctx, ctx.GetString("text")))),
            null, "Replace blocks in the selection");

        Register(Usage("//walls", Nodes.RegionWalls, "<pattern>", ctx => _edits.Pattern(ctx, EditKind.Walls, ctx.GetString("pattern"), "Walls")),
            null, "Build the four side walls of the selection");

        Register(Usage("//faces", Nodes.RegionFaces, "<pattern>", ctx => _edits.Pattern(ctx, EditKind.Faces, ctx.GetString("pattern"), "Faces")),
            new[] { "//outline" }, "Build the six faces of the selection");
    }

    private void Clipboard()
    {
        Register(Command.Literal("//copy").Permission(Nodes.ClipboardCopy).Executes(ctx => _edits.Copy(ctx)), null, "Copy the selection to the clipboard");

        Register(
            Command.Literal("//cut").Permission(Nodes.ClipboardCut)
                .Executes(ctx => _edits.Cut(ctx, null))
                .Then(Command.Argument("pattern", Arg.GreedyString()).Executes(ctx => _edits.Cut(ctx, ctx.GetString("pattern")))),
            null, "Cut the selection to the clipboard (air or a pattern is left behind)");

        Register(
            Command.Literal("//paste").Permission(Nodes.ClipboardPaste)
                .Executes(ctx => _edits.Paste(ctx, string.Empty))
                .Then(Command.Argument("flags", Arg.GreedyString()).Executes(ctx => _edits.Paste(ctx, ctx.GetString("flags")))),
            null, "Paste the clipboard on your block (-a skips air, -o pastes at the copy origin)");

        Register(
            Command.Literal("//rotate").Permission(Nodes.ClipboardTransform)
                .Then(Command.Argument("degrees", Arg.Int(-360, 360)).Executes(ctx => _edits.Rotate(ctx, ctx.GetInt("degrees")))),
            null, "Turn the next paste about the vertical axis, in steps of 90 degrees");

        Register(
            Command.Literal("//flip").Permission(Nodes.ClipboardTransform)
                .Executes(ctx => _edits.Flip(ctx, null))
                .Then(Command.Argument("direction", Arg.Word()).Executes(ctx => _edits.Flip(ctx, ctx.GetString("direction")))),
            null, "Flip the next paste across a plane");

        Register(Command.Literal("clearclipboard").Permission(Nodes.ClipboardClear).Executes(ctx => _edits.ClearClipboard(ctx)), null,
            "Clear the clipboard");
    }

    private void History()
    {
        Register(
            Command.Literal("//undo").Permission(Nodes.HistoryUndo)
                .Executes(ctx => _edits.Undo(ctx, 1, redo: false))
                .Then(Command.Argument("steps", Arg.Int(1, 200)).Executes(ctx => _edits.Undo(ctx, ctx.GetInt("steps"), redo: false))),
            null, "Undo your last edits");

        Register(
            Command.Literal("//redo").Permission(Nodes.HistoryRedo)
                .Executes(ctx => _edits.Undo(ctx, 1, redo: true))
                .Then(Command.Argument("steps", Arg.Int(1, 200)).Executes(ctx => _edits.Undo(ctx, ctx.GetInt("steps"), redo: true))),
            null, "Redo your undone edits");

        Register(Command.Literal("//clearhistory").Permission(Nodes.HistoryClear).Executes(ctx => _edits.ClearHistory(ctx)), null,
            "Clear your undo history");
    }

    private void Navigation()
    {
        Register(
            Command.Literal("up").Permission(Nodes.Navigation)
                .Then(Command.Argument("distance", Arg.Int(1, 384)).Executes(ctx => Moves.Up(ctx, ctx.GetInt("distance")))),
            null, "Go up through the air, with glass under you");

        Register(
            Command.Literal("ascend").Permission(Nodes.Navigation)
                .Executes(ctx => Moves.Ascend(ctx, 1))
                .Then(Command.Argument("levels", Arg.Int(1, 64)).Executes(ctx => Moves.Ascend(ctx, ctx.GetInt("levels")))),
            null, "Go up to the floor of the next room");

        Register(
            Command.Literal("descend").Permission(Nodes.Navigation)
                .Executes(ctx => Moves.Descend(ctx, 1))
                .Then(Command.Argument("levels", Arg.Int(1, 64)).Executes(ctx => Moves.Descend(ctx, ctx.GetInt("levels")))),
            null, "Go down to the floor of the next room");
    }

    private void Help()
    {
        Register(
            Command.Literal("worldedit").Permission(Nodes.SelectionSize)
                .Executes(ctx => ctx.Reply($"WorldEdit {Version}: selections (//pos1, //pos2, //size), regions (//set, //replace, //walls, //faces), clipboard (//copy, //cut, //paste, //rotate, //flip), history (//undo, //redo)."))
                .Then(Command.Literal("version").Executes(ctx => ctx.Reply($"WorldEdit {Version} for Ramjet."))),
            new[] { "we" }, "WorldEdit help and version");
    }

    private const string Version = "0.1.0";

    // A root whose only argument is the rest of the line, with a usage line when it is empty.
    private static CommandNode Usage(string name, string node, string argument, Func<CommandContext, int> run)
    {
        return Command.Literal(name).Permission(node)
            .Executes(ctx => throw new CommandFailure($"Usage: {name} {argument}"))
            .Then(Command.Argument("pattern", Arg.GreedyString()).Executes(ctx => run(ctx)));
    }

    private void Register(CommandNode root, string[]? aliases, string description) =>
        _plugin.Commands.Register(root, aliases, description);
}
