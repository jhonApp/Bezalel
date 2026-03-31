using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Bezalel.Core.DTOs;
using Bezalel.Core.Interfaces;
using System.Text.Json;

namespace Bezalel.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of ICarouselJobRepository.
/// Stores carousel job metadata, slides JSON, and final URLs.
/// </summary>
public class DynamoDbCarouselJobRepository : ICarouselJobRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DynamoDbCarouselJobRepository(IAmazonDynamoDB dynamoDb, string tableName)
    {
        _dynamoDb = dynamoDb;
        _tableName = tableName;
    }

    public async Task SaveAsync(CarouselJob job)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["JobId"]     = new AttributeValue { S = job.JobId },
            ["UserId"]    = new AttributeValue { S = job.UserId },
            ["Tema"]      = new AttributeValue { S = job.Tema },
            ["Status"]    = new AttributeValue { S = job.Status },
            ["CreatedAt"] = new AttributeValue { S = job.CreatedAt.ToString("O") }
        };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task<CarouselJob?> GetByIdAsync(string jobId)
    {
        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobId }
            }
        });

        if (response.Item == null || response.Item.Count == 0)
            return null;

        var item = response.Item;

        item.TryGetValue("Status", out var statusVal);
        item.TryGetValue("UserId", out var userIdVal);
        item.TryGetValue("Tema", out var temaVal);
        item.TryGetValue("BackgroundPrompt", out var bgPromptVal);
        item.TryGetValue("PaletteJson", out var paletteVal);
        item.TryGetValue("SlidesJson", out var slidesVal);
        item.TryGetValue("FinalImageUrls", out var urlsVal);
        item.TryGetValue("CreatedAt", out var createdVal);

        // Deserialize nested JSON fields
        var palette = !string.IsNullOrEmpty(paletteVal?.S)
            ? JsonSerializer.Deserialize<ColorPalette>(paletteVal.S, JsonOptions) ?? new ColorPalette()
            : new ColorPalette();

        var slides = !string.IsNullOrEmpty(slidesVal?.S)
            ? JsonSerializer.Deserialize<List<CarouselSlide>>(slidesVal.S, JsonOptions) ?? new List<CarouselSlide>()
            : new List<CarouselSlide>();

        var finalUrls = urlsVal?.L?.Select(v => v.S).ToList() ?? new List<string>();

        return new CarouselJob
        {
            JobId = jobId,
            UserId = userIdVal?.S ?? string.Empty,
            Tema = temaVal?.S ?? string.Empty,
            BackgroundPrompt = bgPromptVal?.S ?? string.Empty,
            Palette = palette,
            Slides = slides,
            Status = statusVal?.S ?? "UNKNOWN",
            FinalImageUrls = finalUrls,
            CreatedAt = createdVal?.S != null ? DateTime.Parse(createdVal.S) : default
        };
    }

    public async Task UpdateStatusAsync(string jobId, string status, string? slidesJson = null, List<string>? finalImageUrls = null)
    {
        var updateExpr = "SET #st = :status, UpdatedAt = :now";
        var exprNames = new Dictionary<string, string>
        {
            ["#st"] = "Status"
        };
        var exprValues = new Dictionary<string, AttributeValue>
        {
            [":status"] = new AttributeValue { S = status },
            [":now"]    = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
        };

        if (!string.IsNullOrEmpty(slidesJson))
        {
            updateExpr += ", SlidesJson = :slides";
            exprValues[":slides"] = new AttributeValue { S = slidesJson };
        }

        if (finalImageUrls is { Count: > 0 })
        {
            updateExpr += ", FinalImageUrls = :urls";
            exprValues[":urls"] = new AttributeValue
            {
                L = finalImageUrls.Select(u => new AttributeValue { S = u }).ToList()
            };
        }

        await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["JobId"] = new AttributeValue { S = jobId }
            },
            UpdateExpression = updateExpr,
            ExpressionAttributeNames = exprNames,
            ExpressionAttributeValues = exprValues
        });
    }
}
