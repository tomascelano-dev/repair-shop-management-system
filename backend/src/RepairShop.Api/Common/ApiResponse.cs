namespace RepairShop.Api.Common;

public sealed record ApiResponse<T>(T Data);

public static class Envelope
{
    public static ApiResponse<T> Ok<T>(T data) => new(data);
}
