using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;

namespace Bezalel.Workers.CopywriterWorker.Services;

/// <summary>
/// DynamoDB implementation for persisting carousel job status and results.
/// </summary>
public class DynamoDbJobRepository : IJobRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDbJobRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
        _tableName = Environment.GetEnvironmentVariable("CAROUSEL_JOBS_TABLE")
                     ?? "Bezalel_Dev_CarouselJobs";
    }

    public async Task UpdateStatusAsync(string jobId, string status, ILambdaLogger logger)
    {
        logger.LogInformation($"[DynamoDbJobRepository] Updating JobId={jobId} to Status={status}");

        await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobId }
            },
            UpdateExpression = "SET #st = :status, UpdatedAt = :now",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#st"] = "Status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":status"] = new AttributeValue { S = status },
                [":now"]    = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
            }
        });
    }

    public async Task SaveCarouselResultAsync(string jobId, string carouselJson, ILambdaLogger logger)
    {
        logger.LogInformation($"[DynamoDbJobRepository] Saving carousel JSON for JobId={jobId} ({carouselJson.Length} chars)");

        await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobId }
            },
            UpdateExpression = "SET CarouselJson = :json, UpdatedAt = :now",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":json"] = new AttributeValue { S = carouselJson },
                [":now"]  = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
            }
        });
    }
}
