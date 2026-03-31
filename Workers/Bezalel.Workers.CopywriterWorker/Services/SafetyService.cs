using Amazon.Lambda.Core;

namespace Bezalel.Workers.CopywriterWorker.Services;

public interface ISafetyService
{
    bool IsContentSafe(string userText, ILambdaLogger logger);
}

public class LocalSafetyService : ISafetyService
{
    private static readonly string[] BadKeywords =
    {
        "ignore previous instructions",
        "system prompt",
        "dan mode",
        "jailbreak",
        "endoftext"
    };

    public bool IsContentSafe(string userText, ILambdaLogger logger)
    {
        if (string.IsNullOrWhiteSpace(userText)) return true;

        foreach (var keyword in BadKeywords)
        {
            if (userText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning($"[Safety] Potential injection detected: '{keyword}' in user input.");
                return false;
            }
        }

        if (userText.Length > 2000)
        {
             logger.LogWarning($"[Safety] Input text too long ({userText.Length} chars). Possible DoS.");
             return false;
        }

        return true;
    }
}
