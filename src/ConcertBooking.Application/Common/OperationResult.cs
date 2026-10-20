namespace ConcertBooking.Application.Common;

public sealed record OperationResult<T>(T? Value, OperationFailure Failure, string? Error)
{
    public bool IsSuccess => Failure == OperationFailure.None;
    public static OperationResult<T> Success(T value) => new(value, OperationFailure.None, null);
    public static OperationResult<T> Reject(OperationFailure failure, string error) => new(default, failure, error);
}
