namespace RepairShop.Application.Contracts;

public sealed record SimilarOrderResponse(
    Guid Id,
    string Code,
    string Device,
    string IssueDescription,
    string? Category,
    string Status,
    decimal? Total,
    string? Currency,
    double? HoursToReady,
    double Similarity,
    IReadOnlyList<string> Items);

public sealed record SuggestedItemResponse(string Kind, string Description, int Frequency, decimal? MedianUnitPrice, string? Currency, Guid? InventoryItemId);

public sealed record PriceRangeResponse(decimal Min, decimal Median, decimal Max, string Currency, int Samples);

public sealed record AiSuggestionResponse(IReadOnlyList<string> DiagnosisHypotheses, IReadOnlyList<SuggestedItemResponse> Items, string? CustomerMessage, string Model);

public sealed record RepairSuggestionResponse(
    IReadOnlyList<SimilarOrderResponse> SimilarOrders,
    IReadOnlyList<SuggestedItemResponse> SuggestedItems,
    PriceRangeResponse? PriceRange,
    IReadOnlyList<InventoryItemResponse> CompatibleParts,
    bool AiAvailable,
    AiSuggestionResponse? Ai,
    string? AiError);
