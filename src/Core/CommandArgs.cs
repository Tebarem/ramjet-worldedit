using System.Text;

namespace WorldEdit.Core;

/// <summary>The words of a WorldEdit command line: flags (<c>-a</c>, or <c>-ao</c> for two), value flags that take the next word (<c>-m mask</c>),
/// and the positional words. A quoted run keeps its spaces (<c>"stone:2, dirt"</c>). A word that starts with a minus and a digit is positional, so
/// <c>//expand -5 up</c> works.</summary>
public sealed class CommandArgs
{
    private readonly HashSet<char> _flags = new();
    private readonly Dictionary<char, string> _values = new();

    private CommandArgs() { }

    public IReadOnlyList<string> Positional { get; private set; } = Array.Empty<string>();

    /// <summary>Whether the flag letter was given.</summary>
    public bool Has(char flag) => _flags.Contains(flag);

    /// <summary>The value of a value flag, or null when it was not given. A value flag at the end of the line has an empty value.</summary>
    public string? Value(char flag) => _values.TryGetValue(flag, out string? v) ? v : null;

    /// <summary>Splits a raw argument text. <paramref name="valueFlags"/> lists the flag letters that take the next word as their value.</summary>
    public static CommandArgs Parse(string raw, string valueFlags = "")
    {
        string[] words = Tokenize(raw).ToArray();
        var positional = new List<string>();
        var args = new CommandArgs();
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (!IsFlagWord(word))
            {
                positional.Add(word);
                continue;
            }
            foreach (char letter in word.AsSpan(1))
            {
                if (valueFlags.Contains(letter))
                {
                    args._values[letter] = i + 1 < words.Length ? words[++i] : string.Empty;
                }
                else
                {
                    args._flags.Add(letter);
                }
            }
        }
        args.Positional = positional;
        return args;
    }

    /// <summary>The words of a raw text, with quoted runs kept whole. The quote characters are dropped.</summary>
    public static IEnumerable<string> Tokenize(string raw)
    {
        var word = new StringBuilder();
        bool quoted = false;
        bool started = false;
        foreach (char c in raw)
        {
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
                continue;
            }
            if (!quoted && char.IsWhiteSpace(c))
            {
                if (started) yield return word.ToString();
                word.Clear();
                started = false;
                continue;
            }
            word.Append(c);
            started = true;
        }
        if (started) yield return word.ToString();
    }

    // "-a", "-ao": a minus followed by a letter. "-5" and a lone "-" are positional.
    private static bool IsFlagWord(string word) => word.Length >= 2 && word[0] == '-' && char.IsLetter(word[1]);
}
