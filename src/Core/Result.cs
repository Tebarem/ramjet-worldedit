namespace WorldEdit.Core;

/// <summary>The outcome of a compile or parse: a value, or the message that explains why there is none. Messages are shown to players as they are.</summary>
public readonly record struct Result<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Fail(string error) => new(default, error);
}
