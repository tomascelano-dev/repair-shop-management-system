namespace RepairShop.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);

public static class Paging
{
    public static (int Skip, int Take) Normalize(int skip, int take, int maxTake = 200)
        => (Math.Max(0, skip), Math.Clamp(take, 1, maxTake));
}
