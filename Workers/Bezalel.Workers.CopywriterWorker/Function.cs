using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Bezalel.Workers.CopywriterWorker.Services;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Bezalel.Workers.CopywriterWorker;

/// <summary>
/// SQS message format received from the CarouselController/CarouselService.
/// </summary>
public record SqsMessageBody(string JobId, string Tema, int SlideCount, string? VisualStyle);

public class Function
{
    private readonly ICopywriterService  _copywriter;
    private readonly IJobRepository      _jobRepository;
    private readonly ISafetyService      _safety;

    public Function()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
        services.AddSingleton<ITelemetryService, CloudWatchTelemetryService>();
        services.AddSingleton<ISafetyService, LocalSafetyService>();

        services.AddHttpClient("Anthropic", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
        });

        services.AddTransient<IJobRepository, DynamoDbJobRepository>();
        services.AddTransient<ICopywriterService>(provider =>
            new CopywriterService(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("Anthropic"),
                provider.GetRequiredService<ITelemetryService>()));

        var provider = services.BuildServiceProvider();
        _copywriter    = provider.GetRequiredService<ICopywriterService>();
        _jobRepository = provider.GetRequiredService<IJobRepository>();
        _safety        = provider.GetRequiredService<ISafetyService>();
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            string jobId = string.Empty;

            try
            {
                var bodyOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedBody = JsonSerializer.Deserialize<SqsMessageBody>(record.Body, bodyOptions)
                    ?? throw new ArgumentException("Failed to parse SQS message body.");

                jobId = parsedBody.JobId;
                context.Logger.LogInformation($"[CopywriterWorker] Starting copywriting for JobId: {jobId}");

                // Safety check on the user prompt
                if (!_safety.IsContentSafe(parsedBody.Tema, context.Logger))
                {
                    context.Logger.LogWarning($"[Audit] Job {jobId} blocked by safety guardrails.");
                    await _jobRepository.UpdateStatusAsync(jobId, "BLOCKED", context.Logger);
                    return;
                }

                // Update status to COPYWRITING
                await _jobRepository.UpdateStatusAsync(jobId, "COPYWRITING", context.Logger);

                // Call Claude to generate the CarouselJob JSON
                var carouselJson = await _copywriter.GenerateCarouselAsync(
                    parsedBody.Tema,
                    parsedBody.SlideCount,
                    parsedBody.VisualStyle,
                    context.Logger,
                    jobId);

                // Save the generated carousel data to DynamoDB
                await _jobRepository.SaveCarouselResultAsync(jobId, carouselJson, context.Logger);

                // Update status to COPYWRITTEN (ready for StudioWorker)
                await _jobRepository.UpdateStatusAsync(jobId, "COPYWRITTEN", context.Logger);

                // TODO: Publish message to StudioWorker SQS queue to start rendering

                context.Logger.LogInformation($"[CopywriterWorker] Job {jobId} completed successfully.");
            }
            catch (Exception ex)
            {
                var idToLog = string.IsNullOrEmpty(jobId) ? record.MessageId : jobId;
                context.Logger.LogError($"[CopywriterWorker] Error processing {idToLog}: {ex.Message}");

                if (!string.IsNullOrEmpty(jobId))
                {
                    try { await _jobRepository.UpdateStatusAsync(jobId, "FAILED", context.Logger); }
                    catch { /* swallow to preserve original exception */ }
                }

                throw; // Rethrow for SQS/Lambda retry mechanisms
            }
        }
    }
}
