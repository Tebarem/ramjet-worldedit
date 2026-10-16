using System.Text.Json.Serialization;
using WorldEdit.Core;

namespace WorldEdit;

/// <summary>Region editing for Ramjet. The plugin keeps selections and clipboard handles per player and builds requests; the engine runs the
/// edits in slices (Mc.Scripting.Edit) and keeps the undo records and clipboards. Block data never passes through the worker.</summary>
[PluginInfo("WorldEdit", Version = "0.1.0")]
public sealed class WorldEditPlugin : Plugin
{
    /// <summary>How long a player's selection and clipboard handle survive a disconnect (the engine keeps history for the same time).</summary>
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);

    private Config<WorldEditConfig> _config = null!;

    /// <summary>The sessions, keyed by profile id. Only the tick thread touches them.</summary>
    internal SessionStore<Uuid> Sessions { get; } = new();

    internal WorldEditConfig Settings => _config.Value;

    protected override void OnEnable()
    {
        _config = Config.Load(WorldEditJson.Default.WorldEditConfig);
        foreach (var (node, description) in Nodes.All) Permissions.Register(node, PermissionDefault.Op, description);
        new CommandSet(this).RegisterAll();
        new Wand(this).Register();
        Events.On((ref PlayerQuitEvent e) => Sessions.MarkLeft(e.Player.Uuid, DateTimeOffset.UtcNow));
        Scheduler.RunEvery(20 * 60, () => Sessions.Sweep(DateTimeOffset.UtcNow, SessionLifetime));
    }
}

/// <summary>The config file (plugins/worldedit/data/config.json). Written with defaults on first run.</summary>
public sealed class WorldEditConfig
{
    /// <summary>The largest selection a command may edit, in blocks. The engine has its own cap as well.</summary>
    public long MaxRegionVolume { get; set; } = 1_048_576;

    /// <summary>The item that works as the selection wand (left click sets the first corner, right click the second).</summary>
    public string WandItem { get; set; } = "minecraft:wooden_axe";
}

[JsonSerializable(typeof(WorldEditConfig))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class WorldEditJson : JsonSerializerContext
{
}
