using Amazon.Lambda.Core;
using Polly;
using Polly.Retry;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Bezalel.Workers.CopywriterWorker.Services;

/// <summary>
/// Sends the user's creative theme to Anthropic Claude and returns a structured
/// CarouselJob JSON with slides, color palette, and layout instructions.
/// </summary>
public sealed class CopywriterService : ICopywriterService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicModel = "claude-sonnet-4-6";
    private const string AnthropicApiKeyEnv = "ANTHROPIC_KEY";
    private const string AnthropicVersion = "2023-06-01";

    private const string SystemPrompt =
        "You are a Senior Editorial Art Director and Master Copywriter specializing in high-impact viral business content, in the style of Forbes, Brands Decoded, and documentary storytelling.\n" +
        "Your mission is to receive a topic and generate a complete editorial carousel for Instagram in pure JSON format. Your core visual philosophy is to prioritize real human connection, raw authenticity, and highly detailed real-world environments over abstract concepts.\n\n" +

        "=== COPY RULES ===\n" +
        "1. Generate between 4 and 7 slides (respect the quantity requested by the user).\n" +
        "2. All text fields (headline, body) MUST be written in Brazilian Portuguese. Language must be direct, impactful, and cliché-free.\n" +
        "3. Slide 1 (Cover): Write a high-impact headline that provokes curiosity or challenges the reader. Body can be a short subtitle or null.\n" +
        "4. Middle slides: Deliver real value — data points, insights, or revelations about the topic. Body max 2–3 lines.\n" +
        "5. Last slide: Body must contain a clear, persuasive CTA written in Brazilian Portuguese (e.g. 'Salva esse post', 'Me chama no direct', 'Qual foi sua maior sacada?').\n\n" +

        "=== LAYOUT RULES (MANDATORY) ===\n" +
        "- Slide with order=1: layoutType MUST be 'CoverFullImage'. The image fills the entire screen. Headline is large and bold, overlaid on top of the photo.\n" +
        "- All other slides: layoutType MUST be 'EditorialArticle'. Solid background (white or light grey). A 16:9 horizontal photo centered on the page in the style of a newspaper article. Headline above the photo, Body below.\n\n" +

        "=== CINEMATIC PHOTOGRAPHY RULES (ImagePrompt) ===\n" +
        "ABSOLUTE PROHIBITION: Never use terms like 'abstract', 'gradient', 'shapes', 'geometric', 'concept', 'metaphor', or 'illustration'.\n" +
        "You MUST request REAL PEOPLE and REAL LOCATIONS. Each ImagePrompt must:\n" +
        "  a) Describe a concrete, high-quality photographic scene that includes the human element (e.g., a specific professional, an executive, an artisan, a customer, or a generic 'subject').\n" +
        "  b) Describe a highly detailed real-world context (e.g., a modern dental clinic office, a manufacturing floor, a bustling coffee shop, a clean laboratory, an industrial kitchen). Focus on details like textures, materials, and lighting to ground the scene in reality.\n" +
        "  c) Always end with: ', cinematic documentary photography, hyper-realistic, dramatic lighting, shot on 35mm lens, 8k resolution, editorial style, highly detailed. Negative: no text, no letters, no words, no watermarks, abstract, rendering, concept, vector, flat, cartoon.'\n" +
        "  d) Be written entirely in English.\n\n" +

        "=== HIGHLIGHT WORDS RULES ===\n" +
        "For each slide, identify 1 to 3 words from the Headline that carry the strongest emotional or conceptual impact.\n" +
        "Copy those words EXACTLY as they appear in the Headline into the 'highlightWords' array. DO NOT include punctuation marks (like , . ? !) in the highlight words array.\n\n" +

        "=== OUTPUT SCHEMA (return EXACTLY this JSON structure — no Markdown, no code fences) ===\n" +
        "{\n" +
        "  \"tema\": \"string\",\n" +
        "  \"paletaCores\": {\n" +
        "    \"corFundoGeral\": \"#F3F4F6\",\n" +
        "    \"corTextoPrincipal\": \"#111827\",\n" +
        "    \"corDestaque\": \"#EA580C\"\n" +
        "  },\n" +
        "  \"slides\": [\n" +
        "    {\n" +
        "      \"order\": 1,\n" +
        "      \"layoutType\": \"CoverFullImage\",\n" +
        "      \"headline\": \"O Fim do Algoritmo Tradicional\",\n" +
        "      \"highlightWords\": [\"Fim\", \"Algoritmo\"],\n" +
        "      \"body\": \"Porque ter milhões de seguidores pode ser um péssimo negócio.\",\n" +
        "      \"imagePrompt\": \"A stressed influencer looking at a smartphone in a dark room, cinematic documentary photography, hyper-realistic, dramatic lighting, shot on 35mm lens, 8k resolution, editorial style, highly detailed. Negative: no text, no letters, no words, no watermarks.\"\n" +
        "    },\n" +
        "    {\n" +
        "      \"order\": 2,\n" +
        "      \"layoutType\": \"EditorialArticle\",\n" +
        "      \"headline\": \"A Nova Métrica de Ouro\",\n" +
        "      \"highlightWords\": [\"Métrica\"],\n" +
        "      \"body\": \"As marcas não compram mais alcance, elas compram atenção retida.\",\n" +
        "      \"imagePrompt\": \"A close-up of a business executive analyzing graphs on a modern tablet in a brightly lit glass office, cinematic documentary photography, hyper-realistic, dramatic lighting, shot on 35mm lens, 8k resolution, editorial style, highly detailed. Negative: no text, no letters, no words, no watermarks.\"\n" +
        "    },\n" +
        "    {\n" +
        "      \"order\": \"...continue generating the remaining slides up to the requested amount...\"\n" +
        "    }\n" +
        "  ]\n" +
        "}";    

    private readonly HttpClient _httpClient;
    private readonly ITelemetryService _telemetry;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public CopywriterService(HttpClient httpClient, ITelemetryService telemetry)
    {
        _httpClient = httpClient;
        _telemetry = telemetry;

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public async Task<string> GenerateCarouselAsync(
        string tema, int slideCount, string? visualStyle, ILambdaLogger logger, string jobId)
    {
        logger.LogInformation($"[CopywriterService] Sending theme to Claude (JobId: {jobId})...");

        var apiKey = Environment.GetEnvironmentVariable(AnthropicApiKeyEnv)
            ?? throw new InvalidOperationException($"Environment variable '{AnthropicApiKeyEnv}' is not set.");

        var userPrompt = $"Tema: {tema}\nQuantidade de slides: {slideCount}";
        if (!string.IsNullOrWhiteSpace(visualStyle))
            userPrompt += $"\nEstilo visual desejado: {visualStyle}";

        userPrompt += "\n\nGere o JSON do carrossel completo seguindo o schema.";

        var requestBody = new
        {
            model = AnthropicModel,
            max_tokens = 4096,
            system = SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);

        var sw = Stopwatch.StartNew();
        var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(AnthropicApiUrl, httpContent));
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Anthropic returned {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseJson);

        int inputTokens = 0, outputTokens = 0;
        if (document.RootElement.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.GetProperty("input_tokens").GetInt32();
            outputTokens = usage.GetProperty("output_tokens").GetInt32();
        }

        _telemetry.LogUsage(jobId, AnthropicModel, inputTokens, outputTokens, sw.ElapsedMilliseconds);

        var content = document.RootElement.GetProperty("content")[0].GetProperty("text").GetString()
            ?? throw new InvalidOperationException("Anthropic returned empty content.");

        var sanitized = SanitizeJsonResponse(content);

        // Validate that the response is valid JSON
        try
        {
            using var _ = JsonDocument.Parse(sanitized);
        }
        catch (JsonException ex)
        {
            logger.LogError($"[CopywriterService] Claude returned invalid JSON: {ex.Message}");
            throw new InvalidOperationException("Claude returned invalid JSON for carousel generation.", ex);
        }

        logger.LogInformation($"[CopywriterService] Carousel JSON generated successfully ({sanitized.Length} chars, {sw.ElapsedMilliseconds}ms).");

        return sanitized;
    }

    private static string SanitizeJsonResponse(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) raw = raw[7..];
        else if (raw.StartsWith("```")) raw = raw[3..];
        if (raw.EndsWith("```")) raw = raw[..^3];
        return raw.Trim();
    }
}
