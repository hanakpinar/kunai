using System.Diagnostics.CodeAnalysis;

namespace Kunai.Api.Common.Results;

/// <summary>The outcome of an operation that returns no value.</summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public static Result Success { get; } = new(null);

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static implicit operator Result(Error error) => new(error);

    /// <summary>Wraps a value explicitly; needed for interface types, which have no implicit conversion.</summary>
    public static Result<T> Ok<T>(T value) => new(value);
}

/// <summary>The outcome of an operation that returns a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value)
        : base(null) => _value = value;

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>The value; only valid when <see cref="Result.IsSuccess"/> is true.</summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
