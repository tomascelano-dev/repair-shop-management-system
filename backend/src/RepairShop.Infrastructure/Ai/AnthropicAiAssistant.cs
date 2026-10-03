using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Ai;

/// <summary>
/// Repair suggestions from Claude (structured JSON output). Only device data, the reported issue and
/// anonymized similar cases are sent: customer names, phones, emails and long digit sequences
/// (IMEI, DNI, phone numbers) are stripped before the request leaves the server.
/// </summary>
public sealed class AnthropicAiAssistant : IAiAssistant
{
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    private const string SystemPrompt = """
        Sos un técnico senior de un servicio técnico de celulares, tablets y notebooks en Argentina.
        Recibís el equipo, la falla que reportó el cliente, el checklist de recepción y reparaciones
        parecidas que hizo el mismo local (con lo que se cobró). Con eso proponés:
        - diagnosis_hypotheses: de 1 a 4 causas probables, de la más a la menos probable, cada una en
          una oración corta y concreta (qué revisar y por qué).
        - suggested_items: los ítems que irían en el presupuesto (kind "Labor" para mano de obra,
          "Part" para repuestos, "Other" para el resto). estimated_price en la moneda indicada,
          basado en los casos parecidos; usá 0 si no hay datos suficientes para estimarlo.
        - customer_message: un mensaje breve (máximo 3 oraciones), cordial y en español rioplatense,
          para explicarle al cliente qué se le va a revisar, sin prometer precios ni plazos.
        No inventes repuestos que no correspondan al modelo. Si la falla es ambigua, decilo en las hipótesis.
        """;

    private static readonly IReadOnlyDictionary<string, JsonElement> Schema = new Dictionary<string, JsonElement>
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["diagnosis_hypotheses"] = new { type = "array", items = new { type = "string" } },
            ["suggested_items"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["kind"] = new { type = "string", @enum = new[] { "Labor", "Part", "Other" } },
                        ["description"] = new { type = "string" },
                        ["estimated_price"] = new { type = "number" },
                    },
                    ["required"] = new[] { "kind", "description", "estimated_price" },
                    ["additionalProperties"] = false,
                },
            },
            ["customer_message"] = new { type = "string" },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "diagnosis_hypotheses", "suggested_items", "customer_message" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    private readonly AnthropicAiOptions _options;
    private readonly ILogger<AnthropicAiAssistant> _logger;
    private readonly Lazy<AnthropicClient> _client;

    public AnthropicAiAssistant(IOptions<AiOptions> options, ILogger<AnthropicAiAssistant> logger)
    {
        _options = options.Value.Anthropic;
        _logger = logger;
        _client = new Lazy<AnthropicClient>(() => new AnthropicClient
        {
            ApiKey = ResolveApiKey(_options),
            Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 600)),
            MaxRetries = 2,
        });
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey(_options));

    public async Task<AiRepairSuggestion?> SuggestRepairAsync(AiRepairRequest request, CancellationToken ct)
    {
        if (!IsConfigured) return null;

        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = Math.Clamp(_options.MaxTokens, 2000, 64000),
            Thinking = new BetaThinkingConfigAdaptive(),
            OutputConfig = new BetaOutputConfig
            {
                Effort = ParseEffort(_options.Effort),
                Format = new BetaJsonOutputFormat { Schema = Schema },
            },
            System = SystemPrompt,
            Messages = [new BetaMessageParam { Role = Role.User, Content = BuildPrompt(request) }],
        };

        if (_options.UseServerSideFallback)
        {
            parameters = parameters with
            {
                Betas = [FallbackBeta],
                Fallbacks = new Default(),
            };
        }

        BetaMessage response;
        try
        {
            response = await _client.Value.Beta.Messages.Create(parameters, ct);
        }
        catch (AnthropicRateLimitException ex)
        {
            _logger.LogWarning(ex, "Claude rate limit reached while suggesting a repair.");
            throw;
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogWarning(ex, "Claude API error while suggesting a repair.");
            throw;
        }

        if (response.StopReason == "refusal")
        {
            _logger.LogWarning("Claude declined the repair suggestion (category {Category}).", response.StopDetails?.Category);
            return null;
        }

        if (response.StopReason == "max_tokens")
        {
            _logger.LogWarning("Claude repair suggestion was truncated (max_tokens {MaxTokens}).", parameters.MaxTokens);
            return null;
        }

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        return Parse(json, response.Model.ToString());
    }

    internal static AiRepairSuggestion? Parse(string json, string model)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var hypotheses = root.TryGetProperty("diagnosis_hypotheses", out var h) && h.ValueKind == JsonValueKind.Array
            ? h.EnumerateArray().Select(x => x.GetString()?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Take(6).ToList()
            : new List<string>();

        var items = new List<AiSuggestedItem>();
        if (root.TryGetProperty("suggested_items", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in arr.EnumerateArray().Take(12))
            {
                var description = it.TryGetProperty("description", out var d) ? d.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(description)) continue;

                var kind = it.TryGetProperty("kind", out var k) ? k.GetString() : null;
                kind = kind is "Labor" or "Part" or "Other" ? kind : "Other";

                decimal? price = null;
                if (it.TryGetProperty("estimated_price", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var value) && value > 0)
                    price = Math.Round(value, 2, MidpointRounding.AwayFromZero);

                items.Add(new AiSuggestedItem(kind, description.Length > 200 ? description[..200] : description, price));
            }
        }

        var message = root.TryGetProperty("customer_message", out var m) ? m.GetString()?.Trim() : null;

        if (hypotheses.Count == 0 && items.Count == 0 && string.IsNullOrWhiteSpace(message)) return null;
        return new AiRepairSuggestion(hypotheses, items, string.IsNullOrWhiteSpace(message) ? null : message, model);
    }

    internal static string BuildPrompt(AiRepairRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Equipo: {Sanitize(request.DeviceBrand)} {Sanitize(request.DeviceModel)}");
        if (!string.IsNullOrWhiteSpace(request.Category)) sb.AppendLine($"Categoría de la falla: {Sanitize(request.Category)}");
        sb.AppendLine($"Falla reportada: {Sanitize(request.IssueDescription)}");
        if (!string.IsNullOrWhiteSpace(request.ReceptionChecklistSummary))
            sb.AppendLine($"Checklist de recepción: {Sanitize(request.ReceptionChecklistSummary)}");
        sb.AppendLine($"Moneda para los precios: {request.Currency}");

        if (request.SimilarCases.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Reparaciones parecidas del mismo local:");
            foreach (var c in request.SimilarCases.Take(5))
            {
                var total = c.Total is null ? "sin precio" : $"{c.Total.Value.ToString("0.##", CultureInfo.InvariantCulture)} {c.Currency}";
                var items = c.Items.Count == 0 ? "sin detalle" : string.Join("; ", c.Items.Take(6).Select(Sanitize));
                sb.AppendLine($"- {Sanitize(c.Device)} | falla: {Sanitize(c.Issue)} | ítems: {items} | total: {total}");
            }
        }

        return sb.ToString();
    }

    private static readonly Regex Email = new(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.Compiled);
    private static readonly Regex LongDigits = new(@"\+?\d[\d\s().-]{5,}\d", RegexOptions.Compiled);

    /// <summary>Removes contact data and identifiers (emails, phones, IMEI/DNI-like numbers).</summary>
    internal static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var value = Email.Replace(text, "[email]");
        value = LongDigits.Replace(value, match => match.Value.Count(char.IsDigit) >= 7 ? "[número]" : match.Value);
        value = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length > 1000 ? value[..1000] : value;
    }

    private static Effort ParseEffort(string? effort) => (effort ?? "").Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        _ => Effort.Medium,
    };

    private static string? ResolveApiKey(AnthropicAiOptions options)
        => string.IsNullOrWhiteSpace(options.ApiKey) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : options.ApiKey;
}

public sealed class NullAiAssistant : IAiAssistant
{
    public bool IsConfigured => false;

    public Task<AiRepairSuggestion?> SuggestRepairAsync(AiRepairRequest request, CancellationToken ct) => Task.FromResult<AiRepairSuggestion?>(null);
}
