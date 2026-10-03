namespace RepairShop.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"None" (default) or "Anthropic".</summary>
    public string Provider { get; set; } = "None";

    public AnthropicAiOptions Anthropic { get; set; } = new();
}

public sealed class AnthropicAiOptions
{
    /// <summary>Falls back to the ANTHROPIC_API_KEY environment variable when empty.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>low | medium | high. Controls how much the model thinks (latency/cost).</summary>
    public string Effort { get; set; } = "medium";

    public int MaxTokens { get; set; } = 16000;

    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>Retry policy declines on Anthropic's recommended fallback model (server side).</summary>
    public bool UseServerSideFallback { get; set; } = true;
}
