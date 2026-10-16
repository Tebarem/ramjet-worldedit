using WorldEdit.Core;

namespace WorldEdit;

/// <summary>The selection wand: a left click on a block sets the first corner, a right click sets the second. The click is cancelled, so the block
/// is not broken or used. Only the main hand counts, and only for players who hold the <c>worldedit.wand</c> node.</summary>
internal sealed class Wand
{
    private readonly WorldEditPlugin _plugin;

    public Wand(WorldEditPlugin plugin) => _plugin = plugin;

    public void Register() => _plugin.Events.On((ref PlayerInteractEvent e) => OnInteract(ref e));

    private void OnInteract(ref PlayerInteractEvent e)
    {
        if (!e.HasBlock || e.Hand != Hand.MainHand) return;
        if (e.Action is not (InteractAction.LeftClickBlock or InteractAction.RightClickBlock)) return;
        if (!string.Equals(e.Item.Key, _plugin.Settings.WandItem, StringComparison.Ordinal)) return;
        Player player = e.Player;
        if (!player.HasPermission(Nodes.Wand)) return;

        PlayerSession s = _plugin.Sessions.Get(player.Uuid);
        Point p = Coords.ToPoint(e.Pos);
        if (e.Action == InteractAction.LeftClickBlock)
        {
            s.Selection.SetPos1(p);
            player.SendMessage($"First position set to ({p.X}, {p.Y}, {p.Z}).");
        }
        else
        {
            s.Selection.SetPos2(p);
            player.SendMessage($"Second position set to ({p.X}, {p.Y}, {p.Z}).");
        }
        e.Cancelled = true;
    }
}
