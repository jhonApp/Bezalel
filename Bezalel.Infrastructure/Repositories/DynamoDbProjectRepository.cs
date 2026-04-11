using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Bezalel.Core.Entities;
using Bezalel.Core.Interfaces;

namespace Bezalel.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of <see cref="IProjectRepository"/>.
/// Uses the low-level SDK to have full control over projections and partial updates.
/// 
/// Table schema:
///   PK = Id (String)
///   GSI "UserIdUpdatedAtIndex": PK = UserId, SK = UpdatedAt (String ISO-8601)
/// </summary>
public class DynamoDbProjectRepository : IProjectRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    /// <summary>GSI name for querying by UserId, sorted by UpdatedAt descending.</summary>
    private const string UserIdGsiName = "UserIdUpdatedAtIndex";

    public DynamoDbProjectRepository(IAmazonDynamoDB dynamoDb, string tableName)
    {
        _dynamoDb = dynamoDb;
        _tableName = tableName;
    }

    public async Task SaveAsync(Project project)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"]        = new() { S = project.Id },
            ["UserId"]    = new() { S = project.UserId },
            ["Title"]     = new() { S = project.Title },
            ["Format"]    = new() { S = project.Format },
            ["Status"]    = new() { S = project.Status },
            ["CreatedAt"] = new() { S = project.CreatedAt.ToString("O") },
            ["UpdatedAt"] = new() { S = project.UpdatedAt.ToString("O") }
        };

        if (!string.IsNullOrEmpty(project.ThumbnailUrl))
            item["ThumbnailUrl"] = new AttributeValue { S = project.ThumbnailUrl };

        if (!string.IsNullOrEmpty(project.EditorState))
            item["EditorState"] = new AttributeValue { S = project.EditorState };

        if (!string.IsNullOrEmpty(project.OriginJobId))
            item["OriginJobId"] = new AttributeValue { S = project.OriginJobId };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task<Project?> GetByIdAsync(string projectId)
    {
        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = projectId }
            }
        });

        if (response.Item is null || response.Item.Count == 0)
            return null;

        return MapFromItem(response.Item);
    }

    public async Task<IReadOnlyList<Project>> GetRecentByUserIdAsync(string userId, int take = 4)
    {
        // Query the GSI sorted by UpdatedAt descending.
        // ProjectionExpression excludes EditorState to keep the response lean.
        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = UserIdGsiName,
            KeyConditionExpression = "UserId = :uid",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":uid"] = new() { S = userId }
            },
            ProjectionExpression = "Id, Title, #fmt, #st, ThumbnailUrl, UpdatedAt",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                // "Format" and "Status" are DynamoDB reserved words
                ["#fmt"] = "Format",
                ["#st"]  = "Status"
            },
            ScanIndexForward = false, // Descending by UpdatedAt
            Limit = take
        });

        return response.Items
            .Select(MapFromItem)
            .ToList()
            .AsReadOnly();
    }

    public async Task UpdateEditorStateAsync(string projectId, string editorState)
    {
        await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = projectId }
            },
            UpdateExpression = "SET EditorState = :es, UpdatedAt = :now",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":es"]  = new() { S = editorState },
                [":now"] = new() { S = DateTime.UtcNow.ToString("O") }
            }
        });
    }

    public async Task DeleteAsync(string projectId)
    {
        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = projectId }
            }
        });
    }

    // ── Item Mapper ──────────────────────────────────────────────────

    private static Project MapFromItem(Dictionary<string, AttributeValue> item)
    {
        item.TryGetValue("Id", out var idVal);
        item.TryGetValue("UserId", out var userIdVal);
        item.TryGetValue("Title", out var titleVal);
        item.TryGetValue("Format", out var formatVal);
        item.TryGetValue("Status", out var statusVal);
        item.TryGetValue("ThumbnailUrl", out var thumbVal);
        item.TryGetValue("EditorState", out var editorVal);
        item.TryGetValue("OriginJobId", out var originVal);
        item.TryGetValue("CreatedAt", out var createdVal);
        item.TryGetValue("UpdatedAt", out var updatedVal);

        return new Project
        {
            Id = idVal?.S ?? string.Empty,
            UserId = userIdVal?.S ?? string.Empty,
            Title = titleVal?.S ?? string.Empty,
            Format = formatVal?.S ?? string.Empty,
            Status = statusVal?.S ?? "Draft",
            ThumbnailUrl = thumbVal?.S,
            EditorState = editorVal?.S,
            OriginJobId = originVal?.S,
            CreatedAt = createdVal?.S is not null ? DateTime.Parse(createdVal.S) : default,
            UpdatedAt = updatedVal?.S is not null ? DateTime.Parse(updatedVal.S) : default
        };
    }
}
