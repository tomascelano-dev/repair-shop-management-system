namespace RepairShop.Infrastructure.Persistence;

internal static class Like
{
    /// <summary>"%term%" with LIKE wildcards escaped (PostgreSQL default escape char is backslash).</summary>
    public static string Contains(string term)
        => "%" + Escape(term.Trim()) + "%";

    public static string StartsWith(string term)
        => Escape(term.Trim()) + "%";

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
