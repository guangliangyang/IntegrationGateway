namespace IntegrationGateway.Application.Common.Models;

/// <summary>
/// Represents the result of an operation with success/failure state and optional data
/// </summary>
public class Result
{
    internal Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; init; }
    public string[] Errors { get; init; }

    public static Result Success() => new(true, Array.Empty<string>());
    
    public static Result<T> Success<T>(T data) => new(data, true, Array.Empty<string>());

    public static Result Failure(IEnumerable<string> errors) => new(false, errors);

    public static Result<T> Failure<T>(IEnumerable<string> errors) => new(default, false, errors);
}

/// <summary>
/// Represents the result of an operation with success/failure state and typed data
/// </summary>
public class Result<T> : Result
{
    internal Result(T? data, bool succeeded, IEnumerable<string> errors) : base(succeeded, errors)
    {
        Data = data;
    }

    public T? Data { get; init; }

    public static implicit operator Result<T>(T data) => Success(data);
}

/// <summary>
/// Extensions for working with Result types
/// </summary>
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> mapper)
    {
        return result.Succeeded && result.Data != null 
            ? Result.Success(mapper(result.Data))
            : Result.Failure<TOut>(result.Errors);
    }

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> mapper)
    {
        var result = await resultTask;
        return result.Map(mapper);
    }
}