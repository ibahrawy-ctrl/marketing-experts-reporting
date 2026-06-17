namespace Reporting.Application.Common;

/// <summary>نتيجة عملية موحّدة بدون استثناءات للتدفق الطبيعي.</summary>
public class Result
{
    public bool Succeeded { get; protected init; }
    public string? Error { get; protected init; }
    public string? ErrorCode { get; protected init; }

    public static Result Success() => new() { Succeeded = true };
    public static Result Failure(string error, string? code = null) =>
        new() { Succeeded = false, Error = error, ErrorCode = code };
}

/// <summary>نتيجة عملية تحمل قيمة.</summary>
public class Result<T> : Result
{
    public T? Value { get; private init; }

    public static Result<T> Success(T value) => new() { Succeeded = true, Value = value };
    public static new Result<T> Failure(string error, string? code = null) =>
        new() { Succeeded = false, Error = error, ErrorCode = code };
}
