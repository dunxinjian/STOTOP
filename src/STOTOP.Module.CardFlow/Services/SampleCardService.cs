using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// M5-1 样例卡片（设计 E2）：取 definition 近 30 天卡片采样（上限 20 张），供干跑代入样例字段。
/// 经当前用户可视域过滤（CfCard 走 DbContext 组织/租户全局过滤器，此处不绕）。
/// 脱敏口径：cardSchema 中 Sensitive=true 的字段脱敏，但减去被路由条件引用的字段集
/// （否则代入脱敏值致路由推演失真）——精确差集脱敏。
/// </summary>
public sealed class SampleCardService : ISampleCardService
{
    private const int SampleLimit = 20;
    private const int SampleDays = 30;
    private const string MaskValue = "***";

    private readonly STOTOPDbContext _dbContext;

    public SampleCardService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SampleCardDto>> GetSampleCardsAsync(
        long definitionId,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.Now.AddDays(-SampleDays);
        var query = _dbContext.Set<CfCard>()
            .Where(card => card.FFlowDefinitionId == definitionId && card.FCreatedTime >= cutoff);

        var trimmed = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            query = query.Where(card =>
                (card.FTitle != null && card.FTitle.Contains(trimmed))
                || (card.FCardNumber != null && card.FCardNumber.Contains(trimmed)));
        }

        var cards = await query
            .OrderByDescending(card => card.FCreatedTime)
            .Take(SampleLimit)
            .Select(card => new { card.FID, card.FTitle, card.FCardNumber, card.FDataJson, card.FFlowVersionId })
            .ToListAsync(cancellationToken);

        var result = new List<SampleCardDto>();
        if (cards.Count == 0)
            return result;

        // 敏感字段集：以最近采样卡片的版本 schema 为准（同 definition 卡片 schema 基本一致，取任一有值版本）
        var versionId = cards.Select(card => card.FFlowVersionId).FirstOrDefault(id => id > 0);
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var routeReferencedCardFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (versionId > 0)
        {
            var version = await _dbContext.Set<CfFlowVersion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FID == versionId, cancellationToken);
            foreach (var field in CardSchemaReader.ReadFields(version?.FCardSchemaJson))
            {
                if (field.Sensitive && !string.IsNullOrWhiteSpace(field.Key))
                    sensitiveKeys.Add(field.Key);
            }

            var conditionJsons = await _dbContext.Set<CfStageRouteRule>()
                .Where(route => route.FFlowVersionId == versionId && route.FConditionJson != null)
                .Select(route => route.FConditionJson!)
                .ToListAsync(cancellationToken);
            foreach (var conditionJson in conditionJsons)
                CollectCardFieldReferences(conditionJson, routeReferencedCardFields);
        }

        // 精确差集：敏感字段中被路由引用的保留原值，其余脱敏
        var keysToMask = new HashSet<string>(sensitiveKeys, StringComparer.OrdinalIgnoreCase);
        keysToMask.ExceptWith(routeReferencedCardFields);

        foreach (var card in cards)
        {
            result.Add(new SampleCardDto
            {
                CardId = card.FID,
                Title = string.IsNullOrWhiteSpace(card.FTitle)
                    ? (card.FCardNumber ?? $"#{card.FID}")
                    : card.FTitle!,
                DataJson = MaskDataJson(card.FDataJson, keysToMask)
            });
        }

        return result;
    }

    // 敏感字段脱敏，路由引用字段已从 keysToMask 中剔除故保留原值。非法 JSON 静默返回空对象（不泄露原文）。
    private static string MaskDataJson(string? dataJson, HashSet<string> keysToMask)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return "{}";
        if (keysToMask.Count == 0)
            return dataJson;

        try
        {
            using var document = JsonDocument.Parse(dataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return dataJson;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (keysToMask.Contains(property.Name))
                    {
                        writer.WriteString(property.Name, MaskValue);
                    }
                    else
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    // 递归收集条件 JSON 里引用的 card 字段（root=card/carddata 或无前缀，取首段 key）。
    // 口径对齐 ConditionRuleEvaluator.ResolveField：无点前缀默认 root=card。
    private static void CollectCardFieldReferences(string? conditionJson, HashSet<string> target)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
            return;
        try
        {
            using var document = JsonDocument.Parse(conditionJson);
            WalkConditionElement(document.RootElement, target);
        }
        catch (JsonException)
        {
            // 非法条件忽略
        }
    }

    private static void WalkConditionElement(JsonElement element, HashSet<string> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("field", out var fieldElement)
                    && fieldElement.ValueKind == JsonValueKind.String)
                {
                    var cardKey = ExtractCardFieldKey(fieldElement.GetString());
                    if (!string.IsNullOrWhiteSpace(cardKey))
                        target.Add(cardKey);
                }
                foreach (var property in element.EnumerateObject())
                    WalkConditionElement(property.Value, target);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    WalkConditionElement(item, target);
                break;
        }
    }

    // "card.amount" → "amount"；"amount" → "amount"（无前缀默认 card）；"initiator.id"/"detailSummary.x" → null（非 card 字段）
    private static string? ExtractCardFieldKey(string? field)
    {
        var normalized = field?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var dot = normalized.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
            return normalized; // 无前缀 → root=card，整体即字段 key

        var root = normalized[..dot];
        if (root.Equals("card", StringComparison.OrdinalIgnoreCase)
            || root.Equals("cardData", StringComparison.OrdinalIgnoreCase))
        {
            var key = normalized[(dot + 1)..];
            var nested = key.IndexOf('.', StringComparison.Ordinal);
            return nested < 0 ? key : key[..nested]; // 顶层 card 字段 key
        }

        return null; // 非 card 域引用不参与卡片字段保留
    }
}
