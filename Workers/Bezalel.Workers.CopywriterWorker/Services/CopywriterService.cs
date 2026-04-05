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
        "Você é o Diretor de Arte Chefe e Copywriter de um SaaS de geração de carrosséis.\n" +
        "Sua função é gerar um JSON estruturado com o conteúdo textual e os prompts de geração de imagem para cada slide.\n\n" +

        "=== REGRAS DE COPY ===\n" +
        "1. Gere entre 4 e 7 slides (respeite a quantidade solicitada pelo usuário).\n" +
        "2. Todos os textos (Headline, Body) DEVEM ser escritos em Português Brasileiro (pt-BR). Linguagem direta, impactante, sem clichês.\n" +
        "3. Slide 1 (Capa): Write a high-impact headline that provokes curiosity or challenges the reader. Body can be a short subtitle or null.\n" +
        "4. Middle slides: Deliver real value — data points, insights, or revelations about the topic. Body max 2–3 lines.\n" +
        "5. Last slide: Body must contain a clear, persuasive CTA written in Brazilian Portuguese (e.g. 'Salva esse post', 'Me chama no direct', 'Qual foi sua maior sacada?').\n\n" +

        "=== CINEMATIC PHOTOGRAPHY RULES (ImagePrompt) ===\n" +
        "ABSOLUTE PROHIBITION: Never use terms like 'abstract', 'gradient', 'shapes', 'geometric', 'concept', 'metaphor', ou 'illustration'.\n" +
        "You MUST request REAL PEOPLE and REAL LOCATIONS in your ImagePrompts. Your core visual philosophy is to prioritize real human connection, raw authenticity, and highly detailed real-world environments over abstract concepts. Describe concrete, high-quality photographic scenes (e.g., modern clinic, manufacturing floor, bustling cafe) with focus on textures, materials, and lighting to ground the scene in reality.\n" +
        "O 'imagePrompt' de TODOS OS SLIDES deve ser escrito APENAS em INGLÊS.\n\n" +

        "=== REGRAS DE MAPEAMENTO OBRIGATÓRIAS (ASPECT RATIO E SUFIXO) ===\n" +
        "Siga ESTRITAMENTE o mapeamento de 'aspectRatio' baseado no campo 'order' do slide.\n\n" +

        "-> SE \"order\": 1 (Slide de Capa):\n" +
        "A chave 'layoutType' DEVE ser 'CoverFullImage'.\n" +
        "O 'aspectRatio' DEVE ser '1:1'.\n" +
        "O 'imagePrompt' DEVE terminar com a frase exata: \", square format, 1:1 aspect ratio, perfectly centered composition, symmetric, cinematic documentary photography, hyper-realistic, dramatic lighting, shot on 35mm lens, 8k resolution, editorial style, highly detailed. Negative: text, cartoon, abstract, rendering, concept, vector, flat, watermarks.\"\n\n" +

        "-> SE \"order\" for 2, 3, 4, 5... (Slides de Conteúdo):\n" +
        "A chave 'layoutType' DEVE ser 'EditorialArticle'.\n" +
        "O 'aspectRatio' DEVE ser '16:9'.\n" +
        "O 'imagePrompt' DEVE terminar com a frase exata: \", landscape format, 16:9 aspect ratio, wide cinematic shot, negative space for text overlay, uncropped, cinematic documentary photography, hyper-realistic, dramatic lighting, shot on 35mm lens, 8k resolution, editorial style, highly detailed. Negative: text, cartoon, abstract, rendering, concept, vector, flat, watermarks.\"\n\n" +

        "=== HIGHLIGHT WORDS RULES ===\n" +
        "Para cada slide, extraia 1 a 3 palavras da Headline que carreguem o maior impacto.\n" +
        "Copie essas palavras EXATAMENTE igual aparecem na Headline dentro do array 'highlightWords'. NÃO inclua pontuações (vírgulas, pontos).\n\n" +

        "=== EXEMPLO ESTRUTURAL OBRIGATÓRIO (SIGA ESTE PADRÃO RIGOROSAMENTE) ===\n" +
        "Retorne APENAS o JSON válido. Não inclua explicações antes ou depois do JSON.\n" +
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
        "      \"aspectRatio\": \"1:1\",\n" +
        "      \"headline\": \"O Fim do Algoritmo Tradicional\",\n" +
        "      \"highlightWords\": [\"Fim\", \"Algoritmo\"],\n" +
        "      \"body\": \"Porque ter milhões de seguidores pode ser um péssimo negócio.\",\n" +
        "      \"imagePrompt\": \"A close-up of a smiling professional looking at a smartphone in a dark room, square format, 1:1 aspect ratio, perfectly centered composition, symmetric, cinematic documentary photography, hyper-realistic, dramatic lighting, shot on 35mm lens, 8k resolution, editorial style, highly detailed. Negative: text, cartoon, abstract, rendering, concept, vector, flat, watermarks.\"\n" +
        "    },\n" +
        "    {\n" +
        "      \"order\": 2,\n" +
        "      \"layoutType\": \"EditorialArticle\",\n" +
        "      \"aspectRatio\": \"16:9\",\n" +
        "      \"headline\": \"A Nova Métrica de Ouro\",\n" +
        "      \"highlightWords\": [\"Métrica\"],\n" +
        "      \"body\": \"As marcas não compram mais alcance, elas compram atenção retida.\",\n" +
        "      \"imagePrompt\": \"A wide shot of a modern brightly lit glass office... landscape format, 16:9 aspect ratio, wide cinematic shot, negative space for text overlay, uncropped, cinematic documentary photography, hyper-realistic, dramatic lighting, shot on 35mm lens, 8k resolution, editorial style, highly detailed. Negative: text, cartoon, abstract, rendering, concept, vector, flat, watermarks.\"\n" +
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
