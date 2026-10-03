namespace RepairShop.Application.Contracts;

public sealed record ImportError(int Row, string Message);

public sealed record ImportResult(
    string Entity,
    bool DryRun,
    int TotalRows,
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<ImportError> Errors,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Preview,
    IReadOnlyList<string> DetectedColumns);
