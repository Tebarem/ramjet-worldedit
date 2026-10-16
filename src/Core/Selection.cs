namespace WorldEdit.Core;

/// <summary>A cuboid selection made of two corners. Shape extensions (ellipsoids, polygons, convex hulls) are not part of this version.</summary>
public sealed class Selection
{
    public Point? Pos1 { get; private set; }
    public Point? Pos2 { get; private set; }

    public bool IsComplete => Pos1 is not null && Pos2 is not null;

    /// <summary>The selected box. Null until both corners are set.</summary>
    public Box? Bounds => IsComplete ? Box.FromCorners(Pos1!.Value, Pos2!.Value) : null;

    public void SetPos1(Point p) => Pos1 = p;

    public void SetPos2(Point p) => Pos2 = p;

    public void Clear()
    {
        Pos1 = null;
        Pos2 = null;
    }

    /// <summary>Grows the box by <paramref name="amount"/> blocks on the side of <paramref name="direction"/>.</summary>
    public void Expand(Direction direction, int amount) => Set(Grow(RequireBounds(), direction, amount));

    /// <summary>Shrinks the box by <paramref name="amount"/> blocks at the face on the side of <paramref name="direction"/>. Returns false, and
    /// changes nothing, when the box would vanish.</summary>
    public bool Contract(Direction direction, int amount) => TrySet(Grow(RequireBounds(), direction, -amount));

    /// <summary>Grows the box on every face: the sides only when <paramref name="horizontal"/>, the top and bottom only when <paramref name="vertical"/>.</summary>
    public void Outset(int amount, bool horizontal, bool vertical) => Set(Pad(RequireBounds(), amount, horizontal, vertical));

    /// <summary>Shrinks the box on every face (same flags as <see cref="Outset"/>). Returns false, and changes nothing, when the box would vanish.</summary>
    public bool Inset(int amount, bool horizontal, bool vertical) => TrySet(Pad(RequireBounds(), -amount, horizontal, vertical));

    private Box RequireBounds() => Bounds ?? throw new InvalidOperationException("the selection needs two corners");

    private void Set(Box box)
    {
        Pos1 = box.Min;
        Pos2 = box.Max;
    }

    private bool TrySet(Box box)
    {
        if (!box.IsValid) return false;
        Set(box);
        return true;
    }

    // Moves the face on the side of `direction` by `delta`: positive directions move the max face, negative ones the min face.
    private static Box Grow(Box box, Direction direction, int delta)
    {
        Point min = box.Min;
        Point max = box.Max;
        int axis = Directions.Axis(direction);
        if (Directions.IsPositive(direction))
        {
            max = axis switch
            {
                0 => max with { X = max.X + delta },
                1 => max with { Y = max.Y + delta },
                _ => max with { Z = max.Z + delta },
            };
        }
        else
        {
            min = axis switch
            {
                0 => min with { X = min.X - delta },
                1 => min with { Y = min.Y - delta },
                _ => min with { Z = min.Z - delta },
            };
        }
        return new Box(min, max);
    }

    private static Box Pad(Box box, int delta, bool horizontal, bool vertical)
    {
        Point min = box.Min;
        Point max = box.Max;
        if (horizontal)
        {
            min = min with { X = min.X - delta, Z = min.Z - delta };
            max = max with { X = max.X + delta, Z = max.Z + delta };
        }
        if (vertical)
        {
            min = min with { Y = min.Y - delta };
            max = max with { Y = max.Y + delta };
        }
        return new Box(min, max);
    }
}
