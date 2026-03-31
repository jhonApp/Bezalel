using Amazon.Lambda.Core;
using System.Text.Json;

namespace Bezalel.Workers.CopywriterWorker.Services;

public interface ITelemetryService
{
    void LogUsage(string jobId, string modelId, int promptTokens, int completionTokens, long latencyMs);
}

public class CloudWatchTelemetryService : ITelemetryService
{
    public void LogUsage(string jobId, string modelId, int promptTokens, int completionTokens, long latencyMs)
    {
        double cost = (promptTokens * 3.0 / 1_000_000.0) + (completionTokens * 15.0 / 1_000_000.0);

        var telemetry = new
        {
            Type = "AI_USAGE",
            JobId = jobId,
            ModelId = modelId,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            EstimatedCostUsd = Math.Round(cost, 6),
            LatencyMs = latencyMs,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        Console.WriteLine(JsonSerializer.Serialize(telemetry));
    }
}
