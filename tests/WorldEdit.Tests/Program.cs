using WorldEdit.Core;

// A minimal runner: each test is a method; a failed check prints its message and the run exits non-zero.
namespace WorldEdit.Tests;

internal static class Program
{
    private static int _failures;
    private static int _checks;

    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("selection: bounds, volume and the grow and shrink faces", SelectionTests.Bounds),
            ("selection: contract refuses to vanish, outset and inset", SelectionTests.ContractAndPad),
            ("direction: yaw buckets and word parsing", DirectionTests.Yaw),
            ("args: flags, value flags, quotes and negative numbers", ArgsTests.Splitting),
            ("pattern: single, weighted, random and refusals", PatternTests.Compile),
            ("mask: blocks, states, negation, existing and refusals", MaskTests.Compile),
            ("job: request bytes for a set and a paste", JobTests.Bytes),
            ("session: rotation and flips, sweep after the lifetime", SessionTests.RotateFlipSweep),
        };
        foreach (var (name, run) in tests)
        {
            int before = _failures;
            run();
            Console.WriteLine($"{(_failures == before ? "ok  " : "FAIL")} {name}");
        }
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed");
        return _failures == 0 ? 0 : 1;
    }

    internal static void Check(bool condition, string message)
    {
        _checks++;
        if (condition) return;
        _failures++;
        Console.WriteLine($"    check failed: {message}");
    }

    internal static void Equal<T>(T expected, T actual, string message) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected {expected}, got {actual}");
}
