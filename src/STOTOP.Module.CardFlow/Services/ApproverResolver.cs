using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.CardFlow.Services;

public sealed class ApproverResolver : IApproverResolver
{
    private readonly STOTOPDbContext _dbContext;

    public ApproverResolver(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveAsync(
        CfStageDefinition stageDefinition,
        CfCard card,
        IReadOnlyDictionary<string, object?> cardData,
        long flowOrgId,
        long initiatorId,
        string? flowSettingsJson = null,
        CancellationToken cancellationToken = default)
    {
        var config = ParseObject(stageDefinition.FAssigneeConfigJson);
        var strategy = NormalizeStrategy(stageDefinition.FAssigneeStrategy);
        var result = strategy switch
        {
            "fixedUsers" => ResolveConfiguredUsers(config),
            "role" => await ResolveRoleUsersAsync(config, flowOrgId, cancellationToken),
            "fieldUsers" => await ResolveFieldUsersAsync(config, cardData, cancellationToken),
            "orgChain" => await ResolveOrgChainAsync(config, flowOrgId, cancellationToken),
            "amountMatrix" => await ResolveAmountMatrixAsync(config, cardData, flowOrgId, cancellationToken),
            "feeTypeBp" => await ResolveFeeTypeBpAsync(config, cardData, flowOrgId, cancellationToken),
            "initiator" => await ResolveUserIdsAsync(new[] { initiatorId }, "initiator", cancellationToken),
            _ => new ApproverResolveResult { ErrorMessage = $"不支持的处理人策略：{stageDefinition.FAssigneeStrategy}" }
        };

        if (result.Approvers.Count > 0 || !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return result;
        }

        return await ApplyFallbackAsync(config, flowSettingsJson, cardData, flowOrgId, cancellationToken);
    }

    private ApproverResolveResult ResolveConfiguredUsers(JsonElement? config)
    {
        var result = new ApproverResolveResult();
        if (!TryGetProperty(config, "users", out var users) || users.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var sortOrder = 1;
        foreach (var user in users.EnumerateArray())
        {
            if (!TryGetLong(user, "userId", out var userId)
                && !TryGetLong(user, "id", out userId)
                && !TryGetLong(user, "FID", out userId))
            {
                continue;
            }

            result.Approvers.Add(new ResolvedApprover
            {
                UserId = userId,
                UserName = GetString(user, "userName") ?? GetString(user, "name") ?? string.Empty,
                Source = "fixedUsers",
                SortOrder = sortOrder++
            });
        }

        return result;
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveRoleUsersAsync(
        JsonElement? config,
        long flowOrgId,
        CancellationToken cancellationToken)
    {
        var roleCode = TryGetProperty(config, "roleCode", out var roleCodeValue)
            ? ReadString(roleCodeValue)
            : null;
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return new ApproverResolveResult { ErrorMessage = "角色处理人策略缺少 roleCode" };
        }

        var roleIds = await _dbContext.Set<SysRole>()
            .Where(role => role.FStatus == 1 && role.FCode == roleCode)
            .Select(role => role.FID)
            .ToListAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return new ApproverResolveResult();
        }

        var orgScoped = !TryGetProperty(config, "orgScoped", out var orgScopedValue) || ReadBool(orgScopedValue, true);
        var userRoleRows = await _dbContext.Set<SysUserRole>()
            .Where(userRole => roleIds.Contains(userRole.FRoleId))
            .Select(userRole => new { userRole.FUserId, userRole.FOrgId })
            .ToListAsync(cancellationToken);

        if (orgScoped)
        {
            var currentOrgUserIds = await _dbContext.Set<SysUserOrganization>()
                .Where(userOrg => userOrg.FStatus == 1
                    && userOrg.F是否当前
                    && userOrg.FOrgId == flowOrgId)
                .Select(userOrg => userOrg.FUserId)
                .ToListAsync(cancellationToken);
            var currentOrgUserIdSet = currentOrgUserIds.ToHashSet();
            userRoleRows = userRoleRows
                .Where(row => row.FOrgId == flowOrgId || (row.FOrgId == null && currentOrgUserIdSet.Contains(row.FUserId)))
                .ToList();
        }

        var userIds = userRoleRows
            .Select(row => row.FUserId)
            .Distinct()
            .ToList();

        return await ResolveUserIdsAsync(userIds, "role", cancellationToken);
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveFieldUsersAsync(
        JsonElement? config,
        IReadOnlyDictionary<string, object?> cardData,
        CancellationToken cancellationToken)
    {
        var fieldKey = TryGetProperty(config, "fieldKey", out var fieldKeyValue)
            ? ReadString(fieldKeyValue)
            : null;
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            return new ApproverResolveResult { ErrorMessage = "字段取人策略缺少 fieldKey" };
        }

        if (!cardData.TryGetValue(fieldKey, out var value))
        {
            return new ApproverResolveResult();
        }

        var userIds = NormalizeUserIds(value).ToList();
        return await ResolveUserIdsAsync(userIds, "fieldUsers", cancellationToken);
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveOrgChainAsync(
        JsonElement? config,
        long flowOrgId,
        CancellationToken cancellationToken)
    {
        var startOrgId = TryGetProperty(config, "startOrgId", out var startOrgValue) && TryReadLong(startOrgValue, out var parsedStartOrgId)
            ? parsedStartOrgId
            : flowOrgId;
        if (startOrgId <= 0)
        {
            return new ApproverResolveResult { ErrorMessage = "组织链审批策略缺少起始组织" };
        }

        var stopOrgId = TryGetProperty(config, "stopOrgId", out var stopOrgValue) && TryReadLong(stopOrgValue, out var parsedStopOrgId)
            ? parsedStopOrgId
            : (long?)null;
        var stopOrgCode = TryGetProperty(config, "stopOrgCode", out var stopOrgCodeValue)
            ? ReadString(stopOrgCodeValue)
            : null;
        var maxLevels = TryGetProperty(config, "maxLevels", out var maxLevelsValue) && TryReadLong(maxLevelsValue, out var parsedMaxLevels)
            ? Math.Clamp((int)parsedMaxLevels, 1, 20)
            : 20;

        var orgs = await _dbContext.Set<SysOrganization>()
            .Where(org => org.FStatus == 1)
            .Select(org => new { org.FID, org.FParentId, org.FCode, org.FManagerId })
            .ToListAsync(cancellationToken);
        var orgById = orgs.ToDictionary(org => org.FID);
        var managerIds = new List<long>();
        var visited = new HashSet<long>();
        var currentOrgId = startOrgId;

        for (var level = 0; level < maxLevels && currentOrgId > 0 && visited.Add(currentOrgId); level++)
        {
            if (!orgById.TryGetValue(currentOrgId, out var org))
            {
                break;
            }

            if (org.FManagerId.HasValue && org.FManagerId.Value > 0)
            {
                managerIds.Add(org.FManagerId.Value);
            }

            var reachedStop = stopOrgId.HasValue && org.FID == stopOrgId.Value;
            if (!reachedStop && !string.IsNullOrWhiteSpace(stopOrgCode))
            {
                reachedStop = string.Equals(org.FCode, stopOrgCode, StringComparison.OrdinalIgnoreCase);
            }
            if (reachedStop)
            {
                break;
            }

            currentOrgId = org.FParentId;
        }

        return await ResolveUserIdsAsync(managerIds, "orgChain", cancellationToken);
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveAmountMatrixAsync(
        JsonElement? config,
        IReadOnlyDictionary<string, object?> cardData,
        long flowOrgId,
        CancellationToken cancellationToken)
    {
        var amountField = TryGetProperty(config, "amountField", out var amountFieldValue)
            ? ReadString(amountFieldValue)
            : "amount";
        if (string.IsNullOrWhiteSpace(amountField) || !TryReadDecimal(cardData, amountField, out var amount))
        {
            return new ApproverResolveResult();
        }

        if (!TryGetProperty(config, "ranges", out var ranges) || ranges.ValueKind != JsonValueKind.Array)
        {
            return new ApproverResolveResult { ErrorMessage = "金额矩阵策略缺少 ranges" };
        }

        foreach (var range in ranges.EnumerateArray())
        {
            if (range.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var hasMin = TryGetDecimal(range, "min", out var min);
            var hasMax = TryGetDecimal(range, "max", out var max);
            if ((hasMin && amount < min) || (hasMax && amount > max))
            {
                continue;
            }

            var result = await ResolveUsersFromStrategyConfigAsync(range, "amountMatrix", flowOrgId, cancellationToken);
            return result;
        }

        return new ApproverResolveResult();
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveFeeTypeBpAsync(
        JsonElement? config,
        IReadOnlyDictionary<string, object?> cardData,
        long flowOrgId,
        CancellationToken cancellationToken)
    {
        var fieldKey = TryGetProperty(config, "fieldKey", out var fieldKeyValue)
            ? ReadString(fieldKeyValue)
            : "feeType";
        if (string.IsNullOrWhiteSpace(fieldKey) || !TryReadString(cardData, fieldKey, out var feeType))
        {
            return new ApproverResolveResult();
        }

        JsonElement? matchedConfig = null;
        if (TryGetProperty(config, "mapping", out var mapping))
        {
            if (mapping.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in mapping.EnumerateObject())
                {
                    if (string.Equals(property.Name, feeType, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedConfig = property.Value;
                        break;
                    }
                }
            }
            else if (mapping.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in mapping.EnumerateArray())
                {
                    var itemFeeType = GetString(item, "feeType") ?? GetString(item, "value");
                    if (string.Equals(itemFeeType, feeType, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedConfig = item;
                        break;
                    }
                }
            }
        }

        if (!matchedConfig.HasValue)
        {
            return new ApproverResolveResult();
        }

        var result = await ResolveUsersFromStrategyConfigAsync(matchedConfig.Value, "feeTypeBp", flowOrgId, cancellationToken);
        return result;
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ApplyFallbackAsync(
        JsonElement? config,
        string? flowSettingsJson,
        IReadOnlyDictionary<string, object?> cardData,
        long flowOrgId,
        CancellationToken cancellationToken)
    {
        var fallbackType = "failSubmit";
        JsonElement? fallbackConfig = null;
        if (TryGetProperty(config, "fallback", out var fallback))
        {
            fallbackConfig = fallback;
            fallbackType = GetString(fallback, "type") ?? fallbackType;
        }

        if (fallbackConfig.HasValue
            && (string.Equals(fallbackType, "fixedUsers", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fallbackType, "fixed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fallbackType, "specified", StringComparison.OrdinalIgnoreCase)))
        {
            var result = ResolveConfiguredUsers(fallbackConfig);
            result.FallbackReason = "fixedUsers";
            if (result.Approvers.Count == 0)
            {
                result.ErrorMessage = "固定人员兜底未解析到有效处理人";
            }

            return result;
        }

        if (fallbackConfig.HasValue && string.Equals(fallbackType, "role", StringComparison.OrdinalIgnoreCase))
        {
            var result = await ResolveRoleUsersAsync(fallbackConfig, flowOrgId, cancellationToken);
            result.FallbackReason = "role";
            if (result.Approvers.Count == 0 && string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                result.ErrorMessage = "角色兜底未解析到有效处理人";
            }

            return result;
        }

        if (fallbackConfig.HasValue && string.Equals(fallbackType, "fieldUsers", StringComparison.OrdinalIgnoreCase))
        {
            var result = await ResolveFieldUsersAsync(fallbackConfig, cardData, cancellationToken);
            result.FallbackReason = "fieldUsers";
            if (result.Approvers.Count == 0 && string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                result.ErrorMessage = "字段人员兜底未解析到有效处理人";
            }

            return result;
        }

        if (string.Equals(fallbackType, "flowAdmin", StringComparison.OrdinalIgnoreCase))
        {
            var settings = ParseObject(flowSettingsJson);
            var adminUserIds = TryGetProperty(settings, "approvalAdminUserIds", out var adminIds)
                ? ReadLongArray(adminIds)
                : Enumerable.Empty<long>();
            var result = await ResolveUserIdsAsync(adminUserIds, "flowAdmin", cancellationToken);
            result.FallbackReason = "flowAdmin";
            if (result.Approvers.Count == 0)
            {
                result.ErrorMessage = "流程管理员兜底未解析到有效处理人";
            }

            return result;
        }

        return new ApproverResolveResult { ErrorMessage = "未解析到有效处理人" };
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveUserIdsAsync(
        IEnumerable<long> userIds,
        string source,
        CancellationToken cancellationToken)
    {
        var orderedIds = userIds
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();
        if (orderedIds.Count == 0)
        {
            return new ApproverResolveResult();
        }

        var users = await _dbContext.Set<SysUser>()
            .Where(user => user.FStatus == 1 && orderedIds.Contains(user.FID))
            .Select(user => new { user.FID, user.FName })
            .ToListAsync(cancellationToken);
        var usersById = users.ToDictionary(user => user.FID, user => user.FName);

        var result = new ApproverResolveResult();
        var sortOrder = 1;
        foreach (var userId in orderedIds)
        {
            if (!usersById.TryGetValue(userId, out var userName))
            {
                continue;
            }

            result.Approvers.Add(new ResolvedApprover
            {
                UserId = userId,
                UserName = userName,
                Source = source,
                SortOrder = sortOrder++
            });
        }

        return result;
    }

    private static string NormalizeStrategy(string? strategy)
    {
        return strategy?.Trim() switch
        {
            "specified" => "fixedUsers",
            "fixed" => "fixedUsers",
            "fixedUsers" => "fixedUsers",
            "role" => "role",
            "fieldUsers" => "fieldUsers",
            "orgChain" => "orgChain",
            "amountMatrix" => "amountMatrix",
            "feeTypeBp" => "feeTypeBp",
            "initiator" => "initiator",
            null or "" => "initiator",
            var value => value
        };
    }

    private static JsonElement? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement? element, string propertyName, out JsonElement value)
    {
        if (element.HasValue && element.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.Value.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var propertyValue)
            ? ReadString(propertyValue)
            : null;
    }

    private static string? ReadString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var value) ? value.ToString() : null,
            _ => null
        };
    }

    private static bool ReadBool(JsonElement element, bool defaultValue)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(element.GetString(), out var value) => value,
            _ => defaultValue
        };
    }

    private static bool TryGetLong(JsonElement element, string propertyName, out long value)
    {
        if (TryGetProperty(element, propertyName, out var propertyValue))
        {
            return TryReadLong(propertyValue, out value);
        }

        value = 0;
        return false;
    }

    private static bool TryReadLong(JsonElement element, out long value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static IEnumerable<long> ReadLongArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (TryReadLong(item, out var value))
            {
                yield return value;
            }
        }
    }

    private async global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveUsersFromStrategyConfigAsync(
        JsonElement config,
        string source,
        long flowOrgId,
        CancellationToken cancellationToken)
    {
        var userIds = ReadConfiguredUserIds(config).ToList();
        if (userIds.Count > 0)
        {
            return await ResolveUserIdsAsync(userIds, source, cancellationToken);
        }

        if (TryGetProperty(config, "roleCode", out var roleCode) && !string.IsNullOrWhiteSpace(ReadString(roleCode)))
        {
            var roleResult = await ResolveRoleUsersAsync(config, flowOrgId, cancellationToken);
            foreach (var approver in roleResult.Approvers)
            {
                approver.Source = source;
            }

            return roleResult;
        }

        return new ApproverResolveResult();
    }

    private static IEnumerable<long> ReadConfiguredUserIds(JsonElement element)
    {
        if (TryGetProperty(element, "users", out var users))
        {
            foreach (var userId in NormalizeJsonUserIds(users))
            {
                yield return userId;
            }
        }

        if (TryGetProperty(element, "userIds", out var userIds))
        {
            foreach (var userId in NormalizeJsonUserIds(userIds))
            {
                yield return userId;
            }
        }

        if (TryGetProperty(element, "approvers", out var approvers))
        {
            foreach (var userId in NormalizeJsonUserIds(approvers))
            {
                yield return userId;
            }
        }
    }

    private static bool TryReadDecimal(IReadOnlyDictionary<string, object?> cardData, string fieldPath, out decimal value)
    {
        value = 0;
        var normalizedPath = fieldPath.StartsWith("card.", StringComparison.OrdinalIgnoreCase)
            ? fieldPath["card.".Length..]
            : fieldPath;
        if (!cardData.TryGetValue(normalizedPath, out var rawValue) || rawValue == null)
        {
            return false;
        }

        return rawValue switch
        {
            decimal decimalValue => SetDecimal(decimalValue, out value),
            double doubleValue => SetDecimal((decimal)doubleValue, out value),
            float floatValue => SetDecimal((decimal)floatValue, out value),
            int intValue => SetDecimal(intValue, out value),
            long longValue => SetDecimal(longValue, out value),
            string stringValue when decimal.TryParse(stringValue, out var parsed) => SetDecimal(parsed, out value),
            JsonElement jsonElement when TryReadJsonDecimal(jsonElement, out var parsed) => SetDecimal(parsed, out value),
            _ => false
        };

        static bool SetDecimal(decimal source, out decimal target)
        {
            target = source;
            return true;
        }
    }

    private static bool TryReadString(IReadOnlyDictionary<string, object?> cardData, string fieldPath, out string value)
    {
        value = string.Empty;
        var normalizedPath = fieldPath.StartsWith("card.", StringComparison.OrdinalIgnoreCase)
            ? fieldPath["card.".Length..]
            : fieldPath;
        if (!cardData.TryGetValue(normalizedPath, out var rawValue) || rawValue == null)
        {
            return false;
        }

        value = rawValue switch
        {
            string stringValue => stringValue,
            JsonElement jsonElement => ReadString(jsonElement) ?? string.Empty,
            _ => rawValue.ToString() ?? string.Empty
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0;
        if (!TryGetProperty(element, propertyName, out var propertyValue))
        {
            return false;
        }

        return TryReadJsonDecimal(propertyValue, out value);
    }

    private static bool TryReadJsonDecimal(JsonElement element, out decimal value)
    {
        value = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => SetDecimal(decimalValue, out value),
            JsonValueKind.String when decimal.TryParse(element.GetString(), out var decimalValue) => SetDecimal(decimalValue, out value),
            _ => false
        };

        static bool SetDecimal(decimal source, out decimal target)
        {
            target = source;
            return true;
        }
    }

    private static IEnumerable<long> NormalizeUserIds(object? value)
    {
        switch (value)
        {
            case null:
                yield break;
            case long longValue:
                yield return longValue;
                yield break;
            case int intValue:
                yield return intValue;
                yield break;
            case string stringValue when long.TryParse(stringValue, out var userId):
                yield return userId;
                yield break;
            case JsonElement jsonElement:
                foreach (var normalizedUserId in NormalizeJsonUserIds(jsonElement))
                {
                    yield return normalizedUserId;
                }
                yield break;
            case IReadOnlyDictionary<string, object?> dictionary:
                if (TryReadDictionaryId(dictionary, out var dictionaryUserId))
                {
                    yield return dictionaryUserId;
                }
                yield break;
            case IDictionary<string, object?> mutableDictionary:
                if (TryReadDictionaryId(new Dictionary<string, object?>(mutableDictionary), out var mutableDictionaryUserId))
                {
                    yield return mutableDictionaryUserId;
                }
                yield break;
            case IEnumerable<object?> items:
                foreach (var item in items)
                {
                    foreach (var normalizedUserId in NormalizeUserIds(item))
                    {
                        yield return normalizedUserId;
                    }
                }
                yield break;
        }
    }

    private static IEnumerable<long> NormalizeJsonUserIds(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var numberValue))
                {
                    yield return numberValue;
                }
                yield break;
            case JsonValueKind.String:
                if (long.TryParse(element.GetString(), out var stringValue))
                {
                    yield return stringValue;
                }
                yield break;
            case JsonValueKind.Object:
                foreach (var propertyName in new[] { "id", "userId", "FID" })
                {
                    if (TryGetProperty(element, propertyName, out var idProperty)
                        && TryReadLong(idProperty, out var objectValue))
                    {
                        yield return objectValue;
                        yield break;
                    }
                }
                yield break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var normalizedUserId in NormalizeJsonUserIds(item))
                    {
                        yield return normalizedUserId;
                    }
                }
                yield break;
            default:
                yield break;
        }
    }

    private static bool TryReadDictionaryId(IReadOnlyDictionary<string, object?> dictionary, out long userId)
    {
        foreach (var key in new[] { "id", "userId", "FID" })
        {
            var match = dictionary.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key) && TryReadObjectLong(match.Value, out userId))
            {
                return true;
            }
        }

        userId = 0;
        return false;
    }

    private static bool TryReadObjectLong(object? value, out long userId)
    {
        switch (value)
        {
            case long longValue:
                userId = longValue;
                return true;
            case int intValue:
                userId = intValue;
                return true;
            case string stringValue when long.TryParse(stringValue, out userId):
                return true;
            default:
                userId = 0;
                return false;
        }
    }
}
