using Microsoft.Extensions.Logging;
using Polly.Retry;
using Polly;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Bezalel.Workers.CopywriterWorker.Services;

public class AnthropicService : IAnthropicService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    
    // Atualizado para a versão mais recente do Claude 3.5 Sonnet
    private const string AnthropicModel = "claude-3-5-sonnet-20241022"; 
    private const string AnthropicVersion = "2023-06-01";
    private const string AnthropicApiKeyEnv = "ANTHROPIC_KEY";

    private const string SystemPrompt = 
        "Você é um Copywriter Sênior especialista em marketing de atração e redes sociais para pequenos empreendedores.\n" +
        "Sua missão é criar um roteiro de carrossel (estruturado em JSON) magnético, focado em alta conversão e engajamento.\n\n" +
        "DIRETRIZES DE COPY:\n" +
        "1. Hook (Slide 1): O título deve gerar curiosidade imediata abordando uma dor ou desejo.\n" +
        "2. Corpo (Slides 2-5): Entregue valor prático, acionável e demonstre autoridade no nicho especificado.\n" +
        "3. CTA (Último Slide): Faça uma chamada para ação irresistível e direta (ex: comentar uma palavra-chave, chamar no direct).\n" +
        "4. A linguagem deve agradar o público-alvo estabelecido, soando natural e não engessada.\n" +
        "5. backgroundPrompt: gere um prompt EM INGLÊS (curto, atmosférico, abstrato) focado no nicho para um gerador de IA.\n\n" +
        "REGRAS TÉCNICAS ESTRITAS:\n" +
        "1. Retorne EXCLUSIVAMENTE o conteúdo JSON. Sem Markdown fences (```json), sem explicações adicionais textuais.\n" +
        "2. Deve respeitar o Schema abaixo:\n\n" +
        "{\n" +
        "  \"backgroundPrompt\": \"string (prompt em inglês para background image AI)\",\n" +
        "  \"palette\": {\n" +
        "    \"backgroundColor\": \"#hex\",\n" +
        "    \"primaryTextColor\": \"#hex\",\n" +
        "    \"accentColor\": \"#hex\"\n" +
        "  },\n" +
        "  \"slides\": [\n" +
        "    {\n" +
        "      \"order\": 1,\n" +
        "      \"headline\": \"Título Magnético\",\n" +
        "      \"body\": null,\n" +
        "      \"callToAction\": null,\n" +
        "      \"slideBackgroundPrompt\": \"prompt customizado (opcional)\",\n" +
        "      \"layout\": {\n" +
        "        \"textPosition\": \"center\",\n" +
        "        \"textAlignment\": \"center\",\n" +
        "        \"headlineFontSize\": 80,\n" +
        "        \"bodyFontSize\": 32,\n" +
        "        \"headlineColor\": \"#FFFFFF\",\n" +
        "        \"bodyColor\": \"#F2F2F2\",\n" +
        "        \"headlineFontWeight\": \"bold\"\n" +
        "      }\n" +
        "    }\n" +
        "  ]\n" +
        "}";

    private readonly HttpClient _httpClient;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public AnthropicService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        // Política de resiliência (Polly) para intermitência de rede e Rate Limiting (429)
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public async Task<string> GenerateCarouselCopyAsync(
        string nichoNegocio, 
        string temaProposto, 
        string publicoAlvo, 
        string jobId, 
        ILogger logger)
    {
        logger.LogInformation($"[AnthropicService] Iniciando geração Claude 3.5 Sonnet para o Job: {jobId}");
        
        var apiKey = Environment.GetEnvironmentVariable(AnthropicApiKeyEnv)
            ?? throw new InvalidOperationException($"A variável de ambiente {AnthropicApiKeyEnv} não está configurada.");

        var userPrompt = 
            $"Nicho do Negócio: {nichoNegocio}\n" +
            $"Tema/Conteúdo Proposto: {temaProposto}\n" +
            $"Público-Alvo: {publicoAlvo}\n\n" +
            "Gere o JSON do carrossel agora.";

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

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);

        var sw = Stopwatch.StartNew();
        var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(AnthropicApiUrl, httpContent));
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            logger.LogError($"[AnthropicService] Falha na API do Claude (Status: {response.StatusCode}): {errorBody}");
            throw new HttpRequestException($"Anthropic API Error {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(responseJson);
        
        var contentArray = jsonDoc.RootElement.GetProperty("content");
        if (contentArray.GetArrayLength() == 0)
            throw new InvalidOperationException("API do Anthropic retornou um 'content' vazio.");

        var generatedText = contentArray[0].GetProperty("text").GetString() 
            ?? throw new InvalidOperationException("Falha ao extrair o texto de resposta ('text' property was null).");

        // Tratamento robusto para remover markdown code fences perdidos
        var sanitizedJson = SanitizeJsonResponse(generatedText);

        logger.LogInformation($"[AnthropicService] Geração concluída com sucesso em {sw.ElapsedMilliseconds}ms para o JobId: {jobId}");

        return sanitizedJson;
    }

    private static string SanitizeJsonResponse(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) 
            raw = raw[7..];
        else if (raw.StartsWith("```")) 
            raw = raw[3..];
        
        if (raw.EndsWith("```")) 
            raw = raw[..^3];
            
        return raw.Trim();
    }
}
