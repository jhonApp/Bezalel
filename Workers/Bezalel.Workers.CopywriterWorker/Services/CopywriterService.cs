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
        "Voce e um copywriter especialista em redes sociais e marketing digital para pequenos empreendedores brasileiros.\n" +
        "Sua tarefa e receber o tema/prompt do usuario e gerar um carrossel completo para Instagram em formato JSON.\n\n" +
        "REGRAS ESTRITAS:\n" +
        "1. Voce DEVE gerar um carrossel contendo entre 4 e 7 slides (o usuario pode sugerir a quantidade exata).\n" +
        "2. O slide 1 (order: 1) DEVE ser obrigatoriamente uma CAPA com um titulo magnetico (Hook) que prenda a atencao.\n" +
        "3. Os slides intermediarios devem conter conteudo de valor, dicas praticas ou informacoes relevantes.\n" +
        "4. O ULTIMO slide DEVE conter obrigatoriamente uma Call to Action (CTA) clara e persuasiva.\n" +
        "5. Retorne APENAS o JSON puro. Sem Markdown, sem code fences, sem explicacoes.\n" +
        "6. Todos os textos devem ser em portugues brasileiro.\n" +
        "7. O 'backgroundPrompt' deve ser em INGLES e descrever uma atmosfera visual abstrata para um gerador de imagens AI.\n" +
        "   PROIBICAO ABSOLUTA: O backgroundPrompt NUNCA deve conter texto, letras, tipografia ou palavras.\n" +
        "   Termine cada backgroundPrompt com: 'Negative: no text, no letters, no typography, no words, no watermarks.'\n" +
        "8. A paleta de cores deve ter semantica clara: backgroundColor (fundo dominante), primaryTextColor (texto principal legivel), accentColor (destaques e CTAs).\n" +
        "9. As cores devem ser harmonicas e garantir contraste suficiente para legibilidade.\n\n" +
        "SCHEMA DE SAIDA (retorne exatamente esta estrutura):\n" +
        "{\n" +
        "  \"backgroundPrompt\": \"string (English, abstract visual atmosphere)\",\n" +
        "  \"palette\": {\n" +
        "    \"backgroundColor\": \"#hex\",\n" +
        "    \"primaryTextColor\": \"#hex\",\n" +
        "    \"accentColor\": \"#hex\"\n" +
        "  },\n" +
        "  \"slides\": [\n" +
        "    {\n" +
        "      \"order\": 1,\n" +
        "      \"headline\": \"Titulo magnetico da capa\",\n" +
        "      \"body\": null,\n" +
        "      \"callToAction\": null,\n" +
        "      \"slideBackgroundPrompt\": \"string (English, per-slide override or same as global)\",\n" +
        "      \"layout\": {\n" +
        "        \"textPosition\": \"center\",\n" +
        "        \"textAlignment\": \"center\",\n" +
        "        \"headlineFontSize\": 96,\n" +
        "        \"bodyFontSize\": 36,\n" +
        "        \"headlineColor\": \"#FFFFFF\",\n" +
        "        \"bodyColor\": \"#E0E0E0\",\n" +
        "        \"headlineFontWeight\": \"extrabold\"\n" +
        "      }\n" +
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
