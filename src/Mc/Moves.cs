namespace WorldEdit;

/// <summary>Navigation through blocks: <c>/up</c>, <c>/ascend</c> and <c>/descend</c>. Each reads a handful of blocks in the player's column and then
/// teleports once; nothing is written except the glass under <c>/up</c>.</summary>
internal static class Moves
{
    /// <summary>Walks straight up, through nothing: every block of the column must be air. Puts glass under the new position.</summary>
    public static int Up(CommandContext ctx, int distance)
    {
        Player player = ctx.RequirePlayer();
        World world = player.World;
        BlockPos at = Coords.ToBlockPos(Coords.BlockOf(player.Position));
        int top = at.Y + distance;
        if (distance <= 0 || top + 1 >= world.MaxY) throw new CommandFailure("There is no room that high.");
        for (int y = at.Y + 1; y <= top + 1; y++)
            if (!world.GetBlock(new BlockPos(at.X, y, at.Z)).IsAir) throw new CommandFailure("A block is in the way.");
        if (world.GetBlock(new BlockPos(at.X, top - 1, at.Z)).IsAir) world.SetBlock(new BlockPos(at.X, top - 1, at.Z), "minecraft:glass");
        player.Teleport(new Vec3d(at.X + 0.5, top, at.Z + 0.5));
        return 1;
    }

    /// <summary>Goes up to the floor of the next room (a solid block below, two air blocks for the body), <paramref name="levels"/> times.</summary>
    public static int Ascend(CommandContext ctx, int levels) => Travel(ctx, levels, up: true);

    /// <summary>Goes down to the floor of the next room below, <paramref name="levels"/> times.</summary>
    public static int Descend(CommandContext ctx, int levels) => Travel(ctx, levels, up: false);

    private static int Travel(CommandContext ctx, int levels, bool up)
    {
        Player player = ctx.RequirePlayer();
        World world = player.World;
        BlockPos at = Coords.ToBlockPos(Coords.BlockOf(player.Position));
        int found = 0;
        int target = -1;
        if (up)
        {
            for (int y = at.Y + 1; y + 1 < world.MaxY && found < levels; y++)
            {
                if (IsFloorAt(world, at.X, y, at.Z))
                {
                    found++;
                    target = y;
                }
            }
        }
        else
        {
            for (int y = at.Y - 1; y > world.MinY && found < levels; y--)
            {
                if (IsFloorAt(world, at.X, y, at.Z))
                {
                    found++;
                    target = y;
                }
            }
        }
        if (target < 0) throw new CommandFailure(up ? "No room above." : "No room below.");
        if (found < levels) throw new CommandFailure($"Only {found} of {levels} levels found.");
        player.Teleport(new Vec3d(at.X + 0.5, target, at.Z + 0.5));
        return 1;
    }

    // A player can stand at y when the block below is solid and the two blocks of the body are air.
    private static bool IsFloorAt(World world, int x, int y, int z) =>
        !world.GetBlock(new BlockPos(x, y - 1, z)).IsAir &&
        world.GetBlock(new BlockPos(x, y, z)).IsAir &&
        world.GetBlock(new BlockPos(x, y + 1, z)).IsAir;
}
