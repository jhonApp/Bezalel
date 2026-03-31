using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Bezalel.Workers.ProcessadorArte.Models;
using Bezalel.Workers.ProcessadorArte.Services;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Bezalel.Workers.ProcessadorArte;

public class Function
{
    private readonly IDynamoDbJobRepository _jobRepository;
    private readonly ISkiaRendererService   _renderer;
    private readonly IS3StorageService      _s3Storage;
    private readonly IFalApiService         _falApi;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Function()
    {
        var services = new ServiceCollection();

        // ── AWS SDK Clients ────────────────────────────────────────────────
        services.AddSingleton<IAmazonS3,       AmazonS3Client>();
        services.AddSingleton<IAmazonDynamoDB,  AmazonDynamoDBClient>();
        services.AddSingleton<ITelemetryService, CloudWatchTelemetryService>();

        // ── HTTP Client (for Fal.ai) ───────────────────────────────────────
        services.AddHttpClient("AI", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // ── Application Services ───────────────────────────────────────────
        services.AddTransient<IDynamoDbJobRepository, DynamoDbJobRepository>();
        services.AddTransient<IS3StorageService,      S3StorageService>();
        services.AddSingleton<ISkiaRendererService,   SkiaRendererService>();
        services.AddTransient<IFalApiService>(provider =>
            new FalApiService(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("AI"),
                provider.GetRequiredService<ITelemetryService>()));

        var provider   = services.BuildServiceProvider();
        _jobRepository = provider.GetRequiredService<IDynamoDbJobRepository>();
        _renderer      = provider.GetRequiredService<ISkiaRendererService>();
        _s3Storage     = provider.GetRequiredService<IS3StorageService>();
        _falApi        = provider.GetRequiredService<IFalApiService>();
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            string jobId = string.Empty;

            try
            {
                // ═══════════════════════════════════════════════════════════
                //  STEP 1 — Deserialize SQS message
                // ═══════════════════════════════════════════════════════════
                var payload = JsonSerializer.Deserialize<SqsJobPayload>(record.Body, JsonOptions)
                    ?? throw new InvalidOperationException("Invalid SQS payload.");

                jobId = payload.JobId;
                context.Logger.LogInformation(
                    $"[ProcessadorArte] Starting rendering for JobId: {jobId}, CarouselJobId: {payload.CarouselJobId}");

                await _jobRepository.UpdateJobStatusAsync(jobId, "RENDERING", context.Logger);

                // ═══════════════════════════════════════════════════════════
                //  STEP 2 — Fetch full carousel job from DynamoDB
                // ═══════════════════════════════════════════════════════════
                var carouselJob = await _jobRepository.GetCarouselJobAsync(payload.CarouselJobId, context.Logger);

                if (carouselJob.Slides.Count == 0)
                    throw new InvalidOperationException($"CarouselJob '{payload.CarouselJobId}' has no slides.");

                // ═══════════════════════════════════════════════════════════
                //  STEP 3 — Render each slide
                // ═══════════════════════════════════════════════════════════
                var slideUrls = new List<string>();

                foreach (var slide in carouselJob.Slides.OrderBy(s => s.Order))
                {
                    context.Logger.LogInformation(
                        $"[ProcessadorArte] Rendering slide {slide.Order}/{carouselJob.Slides.Count}...");

                    // 3a. Generate background via Fal.ai
                    var bgPrompt = !string.IsNullOrWhiteSpace(slide.SlideBackgroundPrompt)
                        ? slide.SlideBackgroundPrompt
                        : carouselJob.BackgroundPrompt;

                    var backgroundBytes = await _falApi.GenerateImageAsync(
                        bgPrompt, context.Logger, $"{jobId}-slide-{slide.Order}");

                    context.Logger.LogInformation(
                        $"[ProcessadorArte] Background generated for slide {slide.Order}: {backgroundBytes.Length} bytes");

                    // 3b. Render slide with SkiaSharp
                    var slideBytes = _renderer.RenderSlide(
                        slide, carouselJob.Palette, backgroundBytes);

                    context.Logger.LogInformation(
                        $"[ProcessadorArte] Slide {slide.Order} rendered: {slideBytes.Length} bytes");

                    // 3c. Upload rendered slide to S3
                    var slideUrl = await _s3Storage.UploadFinalImageAsync(
                        $"{jobId}/slide-{slide.Order:D2}", slideBytes, context.Logger);

                    slideUrls.Add(slideUrl);

                    context.Logger.LogInformation(
                        $"[ProcessadorArte] Slide {slide.Order} uploaded: {slideUrl}");
                }

                // ═══════════════════════════════════════════════════════════
                //  STEP 4 — Update job with all slide URLs → COMPLETED
                // ═══════════════════════════════════════════════════════════
                await _jobRepository.UpdateJobWithSlideUrlsAsync(jobId, slideUrls, context.Logger);

                context.Logger.LogInformation(
                    $"[ProcessadorArte] Pipeline completed. {slideUrls.Count} slides rendered for job {jobId}.");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"[ProcessadorArte] Fatal Error: {ex.Message}");
                context.Logger.LogError($"[ProcessadorArte] StackTrace: {ex.StackTrace}");

                if (!string.IsNullOrEmpty(jobId))
                {
                    try { await _jobRepository.UpdateJobStatusAsync(jobId, "FAILED", context.Logger); }
                    catch { /* swallow */ }
                }

                throw; // Let SQS retry
            }
        }
    }
}
