using WorldEdit.Core;

namespace WorldEdit;

/// <summary>Permission nodes. Names follow WorldEdit's public documentation so that permission files port; every node defaults to operators.</summary>
internal static class Nodes
{
    public const string SelectionPos = "worldedit.selection.pos";
    public const string SelectionModify = "worldedit.selection.modify";
    public const string SelectionSize = "worldedit.selection.size";
    public const string Wand = "worldedit.wand";
    public const string RegionSet = "worldedit.region.set";
    public const string RegionReplace = "worldedit.region.replace";
    public const string RegionWalls = "worldedit.region.walls";
    public const string RegionFaces = "worldedit.region.faces";
    public const string ClipboardCopy = "worldedit.clipboard.copy";
    public const string ClipboardCut = "worldedit.clipboard.cut";
    public const string ClipboardPaste = "worldedit.clipboard.paste";
    public const string ClipboardTransform = "worldedit.clipboard.transform";
    public const string ClipboardClear = "worldedit.clipboard.clear";
    public const string HistoryUndo = "worldedit.history.undo";
    public const string HistoryRedo = "worldedit.history.redo";
    public const string HistoryClear = "worldedit.history.clear";
    public const string Navigation = "worldedit.navigation";

    public static readonly (string Node, string Description)[] All =
    [
        (SelectionPos, "Set the selection corners with //pos1, //pos2 and the wand"),
        (SelectionModify, "Expand, contract and clear the selection"),
        (SelectionSize, "Show the size of the selection"),
        (Wand, "Use the selection wand"),
        (RegionSet, "Fill a selection with a pattern (//set)"),
        (RegionReplace, "Replace blocks in a selection (//replace)"),
        (RegionWalls, "Build the four side walls of a selection (//walls)"),
        (RegionFaces, "Build the six faces of a selection (//faces, //outline)"),
        (ClipboardCopy, "Copy a selection to the clipboard"),
        (ClipboardCut, "Cut a selection to the clipboard"),
        (ClipboardPaste, "Paste the clipboard"),
        (ClipboardTransform, "Rotate and flip the next paste"),
        (ClipboardClear, "Clear the clipboard"),
        (HistoryUndo, "Undo edits"),
        (HistoryRedo, "Redo undone edits"),
        (HistoryClear, "Clear the undo history"),
        (Navigation, "Move up, ascend and descend through blocks"),
    ];
}

/// <summary>Block state text to engine state ids, with the engine's parser. A bare name gets the minecraft namespace.</summary>
internal sealed class EngineBlocks : IBlockResolver
{
    public uint? ParseState(string text)
    {
        string t = text.Trim();
        if (t.Length == 0) return null;
        int bracket = t.IndexOf('[');
        string name = bracket < 0 ? t : t[..bracket];
        if (!name.Contains(':')) t = "minecraft:" + t;
        return BlockState.TryParse(t, out BlockState state) ? state.Id : null;
    }

    public static readonly EngineBlocks Instance = new();
}

/// <summary>Conversions between the engine's positions and the core's, and the messages of the engine's refusals.</summary>
internal static class Coords
{
    public static Point ToPoint(BlockPos p) => new(p.X, p.Y, p.Z);

    public static BlockPos ToBlockPos(Point p) => new(p.X, p.Y, p.Z);

    /// <summary>The block a position stands in (floor on each axis).</summary>
    public static Point BlockOf(Vec3d v) => new((int)Math.Floor(v.X), (int)Math.Floor(v.Y), (int)Math.Floor(v.Z));

    public static string Refusal(long code) => code switch
    {
        -1 => "the edit queue is full (one running edit and two waiting per player), or the clipboard budget is used up",
        -2 => "the engine refused the request as malformed (or the clipboard is not yours any more)",
        -3 => "the edit is over a limit (the volume, or the number of pattern or mask entries)",
        -4 => "the world or the player is unknown",
        _ => $"the engine refused the edit ({code})",
    };

    /// <summary>A block count with thousands separators.</summary>
    public static string Count(ulong blocks) => blocks.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
