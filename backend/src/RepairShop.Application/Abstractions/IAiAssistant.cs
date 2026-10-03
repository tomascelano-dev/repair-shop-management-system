namespace RepairShop.Application.Abstractions;

/// <summary>Optional LLM assistant (disabled unless configured). Never receives customer personal data.</summary>
public interface IAiAssistant
{
    bool IsConfigured { get; }
    Task<AiRepairSuggestion?> SuggestRepairAsync(AiRepairRequest request, CancellationToken ct);
}

public sealed record AiSimilarCase(string Device, string Issue, string? Category, IReadOnlyList<string> Items, decimal? Total, string? Currency);

public sealed record AiRepairRequest(
    string DeviceBrand,
    string DeviceModel,
    string IssueDescription,
    string? Category,
    string Currency,
    string? ReceptionChecklistSummary,
    IReadOnlyList<AiSimilarCase> SimilarCases);

public sealed record AiSuggestedItem(string Kind, string Description, decimal? EstimatedPrice);

public sealed record AiRepairSuggestion(
    IReadOnlyList<string> DiagnosisHypotheses,
    IReadOnlyList<AiSuggestedItem> SuggestedItems,
    string? CustomerMessage,
    string Model);
