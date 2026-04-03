using Microsoft.Extensions.Logging;

namespace Bezalel.Workers.CopywriterWorker.Services;

/// <summary>
/// Interface para comunicação com a API da Anthropic (Claude 3.5 Sonnet).
/// Responsável por gerar roteiros de carrosséis de marketing.
/// </summary>
public interface IAnthropicService
{
    /// <summary>
    /// Envia os dados do negócio para o Claude 3.5 Sonnet com um prompt de sistema
    /// especialista em copy de marketing e retorna a estrutura do carrossel em JSON.
    /// </summary>
    /// <param name="nichoNegocio">O nicho de atuação do cliente (ex: "Odontologia")</param>
    /// <param name="temaProposto">O tema escolhido para o post (ex: "Mitos sobre clareamento")</param>
    /// <param name="publicoAlvo">O público que o post visa atingir</param>
    /// <param name="jobId">O identificador de rastreamento do Job</param>
    /// <param name="logger">Logger da infraestrutura (Lambda Context)</param>
    /// <returns>O JSON gerado pela IA estruturado</returns>
    Task<string> GenerateCarouselCopyAsync(
        string nichoNegocio, 
        string temaProposto, 
        string publicoAlvo, 
        string jobId, 
        ILogger logger);
}
