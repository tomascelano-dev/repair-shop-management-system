namespace RepairShop.Domain.Billing;

public enum PlanId
{
    Basic = 1,
    Standard = 2,
    Pro = 3
}

/// <summary>Features that only some plans include. Everything else (orders, POS, cash, stock, portal, invoicing) is in every plan.</summary>
public static class PlanModules
{
    public const string Purchasing = "purchasing";   // suppliers and purchase orders
    public const string Reports = "reports";         // detailed reports and Excel export
    public const string Transfers = "transfers";     // stock transfers between branches
    public const string Audit = "audit";             // audit log
    public const string Ai = "ai";                   // AI diagnosis hints

    public static readonly IReadOnlyList<string> All = new[] { Purchasing, Reports, Transfers, Audit, Ai };
}

public sealed record PlanDefinition(PlanId Id, string Name, int MaxBranches, IReadOnlyList<string> Modules)
{
    public bool Includes(string module) => Modules.Contains(module, StringComparer.Ordinal);
}

/// <summary>Plan catalog. Prices live in configuration (they differ per currency and provider); limits live here.</summary>
public static class Plans
{
    public static readonly PlanDefinition Basic = new(PlanId.Basic, "Básico", 1, Array.Empty<string>());
    public static readonly PlanDefinition Standard = new(PlanId.Standard, "Estándar", 2, new[] { PlanModules.Purchasing, PlanModules.Reports });
    public static readonly PlanDefinition Pro = new(PlanId.Pro, "Profesional", 10, PlanModules.All);

    public static readonly IReadOnlyList<PlanDefinition> All = new[] { Basic, Standard, Pro };

    public static PlanDefinition Get(PlanId id) => id switch
    {
        PlanId.Basic => Basic,
        PlanId.Standard => Standard,
        PlanId.Pro => Pro,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Plan desconocido.")
    };

    public static bool TryParse(string? value, out PlanId plan)
        => Enum.TryParse(value?.Trim(), ignoreCase: true, out plan) && Enum.IsDefined(plan);
}
