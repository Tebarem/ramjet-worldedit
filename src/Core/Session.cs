namespace WorldEdit.Core;

/// <summary>What one player is working on: the selection, the clipboard handle the engine holds, and the transform the next paste applies.
/// Block data never lives here; the engine owns clipboards and undo records, and this keeps only their handles.</summary>
public sealed class PlayerSession
{
    public Selection Selection { get; } = new();

    /// <summary>The engine clipboard handle of the last copy or cut, 0 for none.</summary>
    public ulong Clipboard { get; set; }

    /// <summary>The world the clipboard was taken in (its paste with <c>-o</c> needs it).</summary>
    public int ClipboardWorld { get; set; }

    /// <summary>The copy origin in world coordinates: <c>-o</c> pastes the clipboard back where it was copied.</summary>
    public Point ClipboardOrigin { get; set; }

    /// <summary>Quarter turns about +Y that the next paste applies (0..3). Set by <c>//rotate</c>; a copy resets it.</summary>
    public int PasteRotation { get; private set; }

    /// <summary>The flips the next paste applies. Set by <c>//flip</c>; a copy resets it.</summary>
    public FlipAxes PasteFlip { get; private set; }

    /// <summary>When the player left, or null while connected. Used by the session sweep.</summary>
    public DateTimeOffset? LeftAt { get; set; }

    /// <summary>Adds quarter turns (any integer, negative for the other way).</summary>
    public void Rotate(int quarterTurns) => PasteRotation = ((PasteRotation + quarterTurns) % 4 + 4) % 4;

    /// <summary>Toggles the flip across the plane that the direction names: north and south flip z, east and west flip x, up and down flip y.</summary>
    public void Flip(Direction direction)
    {
        FlipAxes bit = Directions.Axis(direction) switch
        {
            0 => FlipAxes.X,
            1 => FlipAxes.Y,
            _ => FlipAxes.Z,
        };
        PasteFlip ^= bit;
    }

    /// <summary>Records a finished copy and resets the paste transform: the clipboard is taken as copied.</summary>
    public void SetClipboard(ulong handle, int world, Point origin)
    {
        Clipboard = handle;
        ClipboardWorld = world;
        ClipboardOrigin = origin;
        PasteRotation = 0;
        PasteFlip = FlipAxes.None;
    }
}

/// <summary>The sessions of the players, keyed by the player's profile. A session outlives its player for a while (the documented ten
/// minutes), so that a quick reconnect keeps the selection; <see cref="Sweep"/> drops the ones that stayed away.</summary>
public sealed class SessionStore<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, PlayerSession> _sessions = new();

    public int Count => _sessions.Count;

    /// <summary>The session of a player, created on first use. A player who is back is no longer marked as left.</summary>
    public PlayerSession Get(TKey key)
    {
        if (!_sessions.TryGetValue(key, out PlayerSession? session))
        {
            session = new PlayerSession();
            _sessions.Add(key, session);
        }
        session.LeftAt = null;
        return session;
    }

    /// <summary>The session of a player, or null when there is none (no lookup creates one).</summary>
    public PlayerSession? Find(TKey key) => _sessions.TryGetValue(key, out PlayerSession? session) ? session : null;

    public void MarkLeft(TKey key, DateTimeOffset now)
    {
        if (_sessions.TryGetValue(key, out PlayerSession? session)) session.LeftAt = now;
    }

    /// <summary>Drops the sessions of players who left at least <paramref name="lifetime"/> ago. Returns how many were dropped.</summary>
    public int Sweep(DateTimeOffset now, TimeSpan lifetime)
    {
        var stale = new List<TKey>();
        foreach (var (key, session) in _sessions)
            if (session.LeftAt is { } left && now - left >= lifetime) stale.Add(key);
        foreach (TKey key in stale) _sessions.Remove(key);
        return stale.Count;
    }
}
