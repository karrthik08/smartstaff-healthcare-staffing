namespace SmartStaff.Application.Common;

public sealed class OperationResult<T>
{
    private OperationResult(bool succeeded, T? value, int statusCode, string title, string detail)
    {
        Succeeded = succeeded;
        Value = value;
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public int StatusCode { get; }

    public string Title { get; }

    public string Detail { get; }

    public static OperationResult<T> Ok(T value) => new(true, value, 200, "", "");

    public static OperationResult<T> Fail(int statusCode, string title, string detail) =>
        new(false, default, statusCode, title, detail);
}
