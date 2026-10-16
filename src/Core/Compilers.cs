namespace WorldEdit.Core;

/// <summary>Resolves block state text (<c>minecraft:stone</c>, <c>minecraft:oak_stairs[facing=north]</c>) to a state id. The plugin answers with
/// the engine's parser; tests answer with a table.</summary>
public interface IBlockResolver
{
    /// <summary>The state id of the text, or null when it names no block or state.</summary>
    uint? ParseState(string text);
}

/// <summary>Compiles WorldEdit pattern text into the engine's binary pattern program (the WEPT format).
/// Supported: single blocks with states, weighted lists (<c>50%stone,50%dirt</c>), and random states of a block (<c>*stone</c>). Not yet:
/// <c>#clipboard</c>, <c>hand</c>, <c>pos1</c>, tags, <c>^</c>, expressions.</summary>
public static class PatternCompiler
{
    public const int MaxNodes = 256;
    public const int MaxChildren = 64;

    private enum NodeKind : byte
    {
        Single = 0,
        Weighted = 1,
        RandomState = 2,
    }

    private readonly record struct Term(NodeKind Kind, uint State, uint? Weight);

    public static Result<byte[]> Compile(string text, IBlockResolver blocks)
    {
        var terms = new List<Term>();
        foreach (string raw in Lists.SplitTopLevel(text, ','))
        {
            string entry = raw.Trim();
            uint? weight = null;
            int percent = entry.IndexOf('%');
            if (percent > 0 && uint.TryParse(entry.AsSpan(0, percent), out uint w))
            {
                if (w == 0) return Result<byte[]>.Fail("a pattern weight must be above zero");
                weight = w;
                entry = entry[(percent + 1)..].Trim();
            }
            if (entry.Length == 0) return Result<byte[]>.Fail("an empty pattern entry");

            if (entry[0] == '*')
            {
                string name = entry[1..].Trim();
                uint? state = blocks.ParseState(name);
                if (state is null) return Result<byte[]>.Fail($"unknown block '{name}'");
                terms.Add(new Term(NodeKind.RandomState, state.Value, weight));
                continue;
            }
            if (entry[0] == '#') return Result<byte[]>.Fail($"'{entry}' is not supported in patterns yet");
            if (entry is "hand" or "offhand" or "pos1") return Result<byte[]>.Fail($"'{entry}' is not supported in patterns yet");

            uint? single = blocks.ParseState(entry);
            if (single is null) return Result<byte[]>.Fail($"unknown block '{entry}'");
            terms.Add(new Term(NodeKind.Single, single.Value, weight));
        }
        if (terms.Count == 0) return Result<byte[]>.Fail("the pattern is empty");
        if (terms.Count == 1 && terms[0].Weight is not null) return Result<byte[]>.Fail("a lone weighted pattern entry is invalid: add the other entries");

        var nodes = new List<byte[]>();
        foreach (Term t in terms)
        {
            nodes.Add(t.Kind == NodeKind.Single ? Node(NodeKind.Single, t.State) : Node(NodeKind.RandomState, t.State));
        }
        // with more than one entry, the last node is a weighted root over them; a lone entry is the root itself
        if (terms.Count > 1)
        {
            if (terms.Count > MaxChildren) return Result<byte[]>.Fail($"at most {MaxChildren} entries in a pattern");
            var children = new List<(uint Weight, ushort Index)>();
            for (int i = 0; i < terms.Count; i++) children.Add((terms[i].Weight ?? 1u, (ushort)i));
            nodes.Add(WeightedNode(children));
        }
        if (nodes.Count > MaxNodes) return Result<byte[]>.Fail($"at most {MaxNodes} entries in a pattern");

        var program = new Writer();
        program.Bytes(WireFormat.PatternMagic).Byte(WireFormat.Version).Short((ushort)nodes.Count);
        foreach (byte[] node in nodes) program.Bytes(node);
        return Result<byte[]>.Success(program.ToArray());
    }

    private static byte[] Node(NodeKind kind, uint state) => new Writer().Byte((byte)kind).Uint(state).ToArray();

    private static byte[] WeightedNode(List<(uint Weight, ushort Index)> children)
    {
        var w = new Writer().Byte((byte)NodeKind.Weighted).Short((ushort)children.Count);
        foreach (var (weight, index) in children) w.Uint(weight).Short(index);
        return w.ToArray();
    }
}

/// <summary>Compiles WorldEdit mask text into the engine's state mask program (the WEMK format). Supported: block names (every state of the block),
/// exact states (<c>minecraft:oak_stairs[facing=north]</c>), <c>#existing</c>, negation with <c>!</c>, and comma lists, which are unions. Not yet: space
/// intersections, offset and adjacency masks, expressions, noise, tags, regions, lenient state masks.</summary>
public static class MaskCompiler
{
    public const int MaxNodes = PatternCompiler.MaxNodes;
    public const int MaxChildren = PatternCompiler.MaxChildren;
    public const int MaxStateList = 4096;

    private enum NodeKind : byte
    {
        Any = 0,
        States = 1,
        BlockTypes = 2,
        Existing = 3,
        Not = 4,
        And = 5,
        Or = 6,
    }

    public static Result<byte[]> Compile(string text, IBlockResolver blocks)
    {
        var nodes = new List<byte[]>();
        var termIndexes = new List<ushort>();
        foreach (string raw in Lists.SplitTopLevel(text, ','))
        {
            string term = raw.Trim();
            bool negate = term.StartsWith('!');
            if (negate) term = term[1..].Trim();
            if (term.Length == 0) return Result<byte[]>.Fail("an empty mask entry");
            if (term.Contains(' ')) return Result<byte[]>.Fail("intersections of masks are not supported yet: use one entry per mask");

            byte[] node;
            if (term == "#existing")
            {
                node = new Writer().Byte((byte)NodeKind.Existing).ToArray();
            }
            else if (term[0] == '#' || term[0] == '%' || term[0] == '=' || term[0] == '$' || term[0] == '>' || term[0] == '<' || term[0] == '~')
            {
                return Result<byte[]>.Fail($"'{term}' is not supported in masks yet");
            }
            else if (term.Contains('['))
            {
                uint? state = blocks.ParseState(term);
                if (state is null) return Result<byte[]>.Fail($"unknown block state '{term}'");
                node = new Writer().Byte((byte)NodeKind.States).Uint(1).Uint(state.Value).ToArray();
            }
            else
            {
                uint? state = blocks.ParseState(term);
                if (state is null) return Result<byte[]>.Fail($"unknown block '{term}'");
                node = new Writer().Byte((byte)NodeKind.BlockTypes).Uint(1).Uint(state.Value).ToArray();
            }
            nodes.Add(node);
            ushort index = (ushort)(nodes.Count - 1);
            if (negate)
            {
                nodes.Add(new Writer().Byte((byte)NodeKind.Not).Short(index).ToArray());
                index = (ushort)(nodes.Count - 1);
            }
            termIndexes.Add(index);
        }
        if (termIndexes.Count == 0) return Result<byte[]>.Fail("the mask is empty");
        if (termIndexes.Count > MaxChildren) return Result<byte[]>.Fail($"at most {MaxChildren} entries in a mask");

        if (termIndexes.Count > 1)
        {
            var or = new Writer().Byte((byte)NodeKind.Or).Short((ushort)termIndexes.Count);
            foreach (ushort i in termIndexes) or.Short(i);
            nodes.Add(or.ToArray());
        }
        if (nodes.Count > MaxNodes) return Result<byte[]>.Fail($"at most {MaxNodes} nodes in a mask");

        var w = new Writer();
        w.Bytes(WireFormat.MaskMagic).Byte(WireFormat.Version).Short((ushort)nodes.Count);
        foreach (byte[] n in nodes) w.Bytes(n);
        return Result<byte[]>.Success(w.ToArray());
    }
}

/// <summary>Splitting helpers shared by the compilers.</summary>
public static class Lists
{
    /// <summary>Splits on a separator outside brackets, braces and quotes: <c>a[x=1,y=2],b</c> has two parts.</summary>
    public static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        int depth = 0;
        bool quoted = false;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') quoted = !quoted;
            else if (!quoted && (c == '[' || c == '{')) depth++;
            else if (!quoted && (c == ']' || c == '}')) depth--;
            else if (!quoted && depth == 0 && c == separator)
            {
                yield return text[start..i];
                start = i + 1;
            }
        }
        yield return text[start..];
    }
}
