using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Bezalel.Workers.StudioWorker.Models;
using System.Text.Json;

namespace Bezalel.Workers.StudioWorker.Services;

/// <summary>
/// Reads carousel job data and updates job status in DynamoDB.
/// </summary>
public sealed class DynamoDbJobRepository : IDynamoDbJobRepository
{
    private readonly string _jobTable = Environment.GetEnvironmentVariable("CAROUSEL_JOBS_TABLE")
                                        ?? "Bezalel_Dev_Job";
    private readonly IAmazonDynamoDB _dynamo;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DynamoDbJobRepository(IAmazonDynamoDB dynamo) => _dynamo = dynamo;

    public async Task<CarouselJobRecord> GetCarouselJobAsync(string carouselJobId, ILambdaLogger logger)
    {
        logger.LogInformation($"[DynamoDbJobRepository] Fetching carousel job: {carouselJobId}");

        var response = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _jobTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = carouselJobId }
            }
        });

        if (response.Item is null || response.Item.Count == 0)
            throw new InvalidOperationException($"CarouselJob '{carouselJobId}' not found in DynamoDB.");

        var item = response.Item;

        // Parse the CarouselJson field (populated by CopywriterWorker)
        var carouselJson = item.TryGetValue("CarouselJson", out var cjVal) ? cjVal.S : null;
        if (string.IsNullOrEmpty(carouselJson))
            throw new InvalidOperationException($"CarouselJob '{carouselJobId}' has no CarouselJson data.");

        var parsed = JsonSerializer.Deserialize<CarouselJobRecord>(carouselJson, JsonOpts)
            ?? throw new InvalidOperationException("Failed to deserialize CarouselJson.");

        // Override JobId from the DynamoDB key (the Claude JSON does not include it)
        var result = parsed with { JobId = carouselJobId };

        logger.LogInformation($"[DynamoDbJobRepository] Carousel job fetched. Slides: {result.Slides.Count}");
        return result;
    }

    public async Task UpdateJobStatusAsync(string jobId, string status, ILambdaLogger logger, string? finalUrl = null)
    {
        logger.LogInformation($"[DynamoDbJobRepository] Updating job {jobId} -> Status: {status}");

        var updateExpr = "SET #st = :status, UpdatedAt = :now";
        var exprNames = new Dictionary<string, string> { ["#st"] = "Status" };
        var exprValues = new Dictionary<string, AttributeValue>
        {
            [":status"] = new AttributeValue { S = status },
            [":now"]    = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
        };

        if (!string.IsNullOrEmpty(finalUrl))
        {
            updateExpr += ", FinalS3Url = :url";
            exprValues[":url"] = new AttributeValue { S = finalUrl };
        }

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _jobTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobId }
            },
            UpdateExpression = updateExpr,
            ExpressionAttributeNames = exprNames,
            ExpressionAttributeValues = exprValues
        });
    }

    public async Task UpdateJobWithSlideUrlsAsync(string jobId, List<string> slideUrls, ILambdaLogger logger)
    {
        logger.LogInformation($"[DynamoDbJobRepository] Saving {slideUrls.Count} slide URLs for job {jobId}");

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _jobTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobId }
            },
            UpdateExpression = "SET FinalImageUrls = :urls, #st = :status, UpdatedAt = :now",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#st"] = "Status" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":urls"]   = new AttributeValue { L = slideUrls.Select(u => new AttributeValue { S = u }).ToList() },
                [":status"] = new AttributeValue { S = "COMPLETED" },
                [":now"]    = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
            }
        });
    }
}
