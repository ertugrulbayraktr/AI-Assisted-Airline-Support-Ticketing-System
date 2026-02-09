namespace Support.Application.Common;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    public static Result<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, ErrorMessage = error, Errors = new List<string> { error } };
    public static Result<T> Failure(List<string> errors) => new() { IsSuccess = false, Errors = errors, ErrorMessage = string.Join("; ", errors) };
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string error) => new() { IsSuccess = false, ErrorMessage = error, Errors = new List<string> { error } };
    public static Result Failure(List<string> errors) => new() { IsSuccess = false, Errors = errors, ErrorMessage = string.Join("; ", errors) };
}
