using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.System.Services;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 数据质量中心服务实现
/// </summary>
public class QualityCenterService : IQualityCenterService
{
    private const string ErrShopEmpty = "ERR_SHOP_EMPTY";
    private const string ErrShopPendingConfig = "ERR_SHOP_PENDING_CONFIG";
    private const string ErrNetworkPointNotFound = "ERR_NETWORK_POINT_NOT_FOUND";
    private const string ErrNetworkPointMismatch = "ERR_NETWORK_POINT_MISMATCH";

    private readonly IRepository<ExpShop> _shopRepo;
    private readonly IRepository<ExpQuotationShop> _assignmentRepo;
    private readonly IRepository<ExpQuotation> _quotationRepo;
    private readonly IRepository<CfBatchError> _errorRepo;
    private readonly IRepository<CfBatch> _batchRepo;
    private readonly IRepository<ExpNetworkPoint> _networkPointRepo;
    private readonly IRepository<ExpNetworkPointAlias> _aliasRepo;
    private readonly IImportService _importService;
    private readonly IWorkItemService _workItemService;
    private readonly string _connectionString;
    private readonly ILogger<QualityCenterService> _logger;

    public QualityCenterService(
        IRepository<ExpShop> shopRepo,
        IRepository<ExpQuotationShop> assignmentRepo,
        IRepository<ExpQuotation> quotationRepo,
        IRepository<CfBatchError> errorRepo,
        IRepository<CfBatch> batchRepo,
        IRepository<ExpNetworkPoint> networkPointRepo,
        IRepository<ExpNetworkPointAlias> aliasRepo,
        IImportService importService,
        IWorkItemService workItemService,
        IConfiguration configuration,
        ILogger<QualityCenterService> logger)
    {
        _shopRepo = shopRepo;
        _assignmentRepo = assignmentRepo;
        _quotationRepo = quotationRepo;
        _errorRepo = errorRepo;
        _batchRepo = batchRepo;
        _networkPointRepo = networkPointRepo;
        _aliasRepo = aliasRepo;
        _importService = importService;
        _workItemService = workItemService;
        _connectionString = DbConnectionsHelper.GetSystemConnectionString(configuration.GetValue<string>("Security:EncryptionKey"))
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");
        _logger = logger;
    }

    // ========== 概览 ==========

    public async Task<QualityCenterOverviewDto> GetOverviewAsync()
    {
        var pendingShopQuery = _shopRepo.Query().Where(s => s.FNeedsAssignment);
        var pendingShopCount = await pendingShopQuery.CountAsync();
        var autoCreatedPendingCount = await pendingShopQuery.CountAsync(s => s.FIsAutoCreated);

        var emptyErrorQuery = _errorRepo.Query()
            .Where(e => e.FErrorType == ErrShopEmpty && (e.FDispatchStatus == null || e.FDispatchStatus != "Ignored" && e.FDispatchStatus != "Resolved"));
        var emptyShopRowCount = await emptyErrorQuery.CountAsync();

        // 未识别网点运单数统计
        var unrecognizedNetworkPointCount = await _errorRepo.Query()
            .Where(e => e.FErrorType == ErrNetworkPointNotFound
                && e.FDispatchStatus != "Ignored" && e.FDispatchStatus != "Resolved")
            .CountAsync();

        // 网点不一致警告数统计
        var networkPointMismatchCount = await _errorRepo.Query()
            .Where(e => e.FErrorType == ErrNetworkPointMismatch
                && e.FDispatchStatus != "Ignored" && e.FDispatchStatus != "Resolved")
            .CountAsync();

        var blockedBatchIds = await _errorRepo.Query()
            .Where(e => (e.FErrorType == ErrShopEmpty || e.FErrorType == ErrShopPendingConfig || e.FErrorType == ErrNetworkPointNotFound)
                && (e.FDispatchStatus == null || e.FDispatchStatus != "Ignored" && e.FDispatchStatus != "Resolved"))
            .Select(e => e.FBatchId)
            .Distinct()
            .Take(50)
            .ToListAsync();

        return new QualityCenterOverviewDto
        {
            PendingShopCount = pendingShopCount,
            AutoCreatedPendingCount = autoCreatedPendingCount,
            EmptyShopRowCount = emptyShopRowCount,
            UnrecognizedNetworkPointCount = unrecognizedNetworkPointCount,
            NetworkPointMismatchCount = networkPointMismatchCount,
            AffectedBatchCount = blockedBatchIds.Count,
            BlockedBatchIds = blockedBatchIds
        };
    }

    // ========== 待配置店铺 ==========

    public async Task<PagedResult<PendingShopItemDto>> GetPendingShopsAsync(PendingShopQueryRequest request)
    {
        var query = _shopRepo.Query().Where(s => s.FNeedsAssignment);

        if (request.IsAutoCreated.HasValue)
            query = query.Where(s => s.FIsAutoCreated == request.IsAutoCreated.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(s => s.FName.Contains(kw));
        }

        // 如果指定批次，通过错误明细的 FOriginalValue 反查店铺编码
        if (request.BatchId.HasValue)
        {
            var batchShopCodes = await _errorRepo.Query()
                .Where(e => e.FBatchId == request.BatchId.Value
                    && e.FErrorType == ErrShopPendingConfig
                    && e.FOriginalValue != null)
                .Select(e => e.FOriginalValue!)
                .Distinct()
                .ToListAsync();
            query = query.Where(s => batchShopCodes.Contains(s.FName));
        }

        var total = await query.CountAsync();
        var shopList = await query
            .OrderByDescending(s => s.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 取这些店铺涉及的错误明细（场景B）聚合出受影响运单数和批次
        var shopNames = shopList.Select(s => s.FName).ToList();
        var errorAgg = await _errorRepo.Query()
            .Where(e => e.FErrorType == ErrShopPendingConfig
                && e.FOriginalValue != null
                && shopNames.Contains(e.FOriginalValue))
            .Select(e => new { e.FOriginalValue, e.FBatchId, e.FErrorMessage })
            .ToListAsync();

        var items = shopList.Select(s =>
        {
            var related = errorAgg.Where(x => string.Equals(x.FOriginalValue, s.FName, StringComparison.OrdinalIgnoreCase)).ToList();
            var batchIds = related.Select(x => x.FBatchId).Distinct().Take(5).ToList();
            // 从错误描述中解析 "(N 条运单受影响)"
            int totalAffected = 0;
            foreach (var r in related)
            {
                if (r.FErrorMessage != null)
                {
                    var m = Regex.Match(r.FErrorMessage, @"(\d+)\s*条运单");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                        totalAffected += n;
                }
            }
            return new PendingShopItemDto
            {
                Name = s.FName,
                Platform = s.FPlatform,
                IsAutoCreated = s.FIsAutoCreated,
                NeedsAssignment = s.FNeedsAssignment,
                Status = s.FStatus,
                CreatedTime = s.FCreatedTime,
                Remark = s.FRemark,
                AffectedWaybillCount = totalAffected,
                AffectedBatchIds = batchIds
            };
        }).ToList();

        return new PagedResult<PendingShopItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<CompleteShopConfigResultDto> CompleteShopConfigAsync(CompleteShopConfigRequest request)
    {
        var shop = await _shopRepo.Query().FirstOrDefaultAsync(s => s.FName == request.ShopName)
            ?? throw new InvalidOperationException($"店铺 {request.ShopName} 不存在");
        var quotation = await _quotationRepo.GetByIdAsync(request.PricePlanId)
            ?? throw new InvalidOperationException($"报价方案 {request.PricePlanId} 不存在");

        // 1. 创建归属
        var assignment = new ExpQuotationShop
        {
            FQuotationId = request.PricePlanId,
            FShopName = request.ShopName,
            FCreatedTime = DateTime.Now
        };
        var created = await _assignmentRepo.AddAsync(assignment);

        // 2. 关闭 FNeedsAssignment，启用店铺
        shop.FNeedsAssignment = false;
        if (shop.FStatus == 0) shop.FStatus = 1;
        shop.FUpdatedTime = DateTime.Now;
        await _shopRepo.UpdateAsync(shop);

        // 3. 报价方案校验（软警告）
        string? warning = null;
        if (!request.SkipPricePlanCheck)
        {
            var hasActivePricePlan = await _quotationRepo.Query()
                .AnyAsync(p => p.FID == request.PricePlanId && p.FStatus == 1);
            if (!hasActivePricePlan)
            {
                warning = $"报价方案 [{quotation.FPlanName}] 暂未生效，计费时将按 ERR_NO_PRICE_PLAN 失败。建议前往【报价方案管理】确认方案状态后再重新计费。";
            }
        }

        // 4. 将该店铺相关的 ERR_SHOP_PENDING_CONFIG 错误明细标记为 Resolved
        var relatedErrors = await _errorRepo.Query()
            .Where(e => e.FErrorType == ErrShopPendingConfig
                && e.FOriginalValue != null
                && e.FOriginalValue == shop.FName
                && (e.FDispatchStatus == null || e.FDispatchStatus == "Pending"))
            .ToListAsync();
        foreach (var err in relatedErrors)
        {
            err.FDispatchStatus = "Resolved";
            await _errorRepo.UpdateAsync(err);
        }

        _logger.LogInformation("QualityCenter: 店铺 {ShopName} 已完成归属配置 → QuotationId={QuotationId}, Assignment={AssignmentId}, 处理错误明细 {Count} 条",
            shop.FName, request.PricePlanId, created.FID, relatedErrors.Count);

        // 解决关联的 WorkItem
        await ResolveWorkItemsByBatchAndTypeAsync(relatedErrors, ErrShopPendingConfig);

        return new CompleteShopConfigResultDto
        {
            ShopName = shop.FName,
            AssignmentId = created.FID,
            Completed = true,
            PricePlanWarning = warning
        };
    }

    // ========== 空店铺账号运单 ==========

    public async Task<PagedResult<EmptyShopRowItemDto>> GetEmptyShopRowsAsync(EmptyShopRowQueryRequest request)
    {
        var query = _errorRepo.Query().Where(e => e.FErrorType == ErrShopEmpty);
        if (request.BatchId.HasValue)
            query = query.Where(e => e.FBatchId == request.BatchId.Value);
        if (!string.IsNullOrWhiteSpace(request.DispatchStatus))
            query = query.Where(e => e.FDispatchStatus == request.DispatchStatus);
        else
            query = query.Where(e => e.FDispatchStatus == null || e.FDispatchStatus == "Pending");

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = rows.Select(e => new EmptyShopRowItemDto
        {
            ErrorId = e.FID,
            BatchId = e.FBatchId,
            StagingId = e.FStagingId,
            WaybillNo = ExtractWaybillNoFromMessage(e.FErrorMessage),
            WaybillDate = null,
            ErrorMessage = e.FErrorMessage,
            DispatchStatus = e.FDispatchStatus ?? "Pending",
            CreateTime = e.FCreatedTime
        }).ToList();

        return new PagedResult<EmptyShopRowItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<EmptyShopRowBatchResultDto> FillEmptyShopAccountAsync(FillEmptyShopAccountRequest request)
    {
        if (request.ErrorIds == null || request.ErrorIds.Count == 0)
            return new EmptyShopRowBatchResultDto { AffectedCount = 0, Message = "未选择任何行" };
        if (string.IsNullOrWhiteSpace(request.ShopAccount))
            throw new InvalidOperationException("店铺账号不能为空");

        var shopAccount = request.ShopAccount.Trim();

        // 1. 加载错误明细
        var errors = await _errorRepo.Query()
            .Where(e => request.ErrorIds.Contains(e.FID) && e.FErrorType == ErrShopEmpty)
            .ToListAsync();

        // 2. 按批次分组，查 DcImportBatch 获取实际 STG 表
        var batchIds = errors.Select(e => e.FBatchId).Distinct().ToList();
        var batches = await _batchRepo.Query()
            .Where(b => batchIds.Contains(b.FID) && !b.FIsRevoked)
            .ToDictionaryAsync(b => b.FID, b => b.FActualTargetTable);

        int affected = 0;
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var err in errors)
        {
            if (err.FStagingId == null) continue;
            if (!batches.TryGetValue(err.FBatchId, out var stgTable) || string.IsNullOrWhiteSpace(stgTable)) continue;
            ValidateIdentifier(stgTable);

            var sql = $"UPDATE [{stgTable}] SET [F店铺账号] = @val WHERE [FID] = @id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@val", shopAccount);
            cmd.Parameters.AddWithValue("@id", err.FStagingId.Value);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0)
            {
                affected += rows;
                err.FDispatchStatus = "Resolved";
                await _errorRepo.UpdateAsync(err);
            }
        }

        _logger.LogInformation("QualityCenter: 补填店铺账号 [{Account}]，处理错误明细 {ErrorCount} 条，更新 STG 行 {Affected} 行",
            shopAccount, errors.Count, affected);

        // 解决关联的 WorkItem
        await ResolveWorkItemsByBatchAndTypeAsync(errors, ErrShopEmpty);

        return new EmptyShopRowBatchResultDto
        {
            AffectedCount = affected,
            Message = $"已将 {affected} 行 STG 的 F店铺账号 补填为 [{shopAccount}]，请在批次详情点击“重新计费”触发重算"
        };
    }

    public async Task<EmptyShopRowBatchResultDto> IgnoreEmptyShopRowsAsync(IgnoreEmptyShopRowsRequest request)
    {
        if (request.ErrorIds == null || request.ErrorIds.Count == 0)
            return new EmptyShopRowBatchResultDto { AffectedCount = 0, Message = "未选择任何行" };

        var errors = await _errorRepo.Query()
            .Where(e => request.ErrorIds.Contains(e.FID) && e.FErrorType == ErrShopEmpty)
            .ToListAsync();

        foreach (var err in errors)
        {
            err.FDispatchStatus = "Ignored";
            if (!string.IsNullOrWhiteSpace(request.Reason))
                err.FSuggestedFix = $"用户已忽略: {request.Reason}";
            await _errorRepo.UpdateAsync(err);
        }

        return new EmptyShopRowBatchResultDto
        {
            AffectedCount = errors.Count,
            Message = $"已将 {errors.Count} 条空店铺账号标记为忽略"
        };
    }

    // ========== 重新计费 ==========

    public async Task<RerunBillingResultDto> RerunBillingAsync(RerunBillingRequest request)
    {
        var batch = await _batchRepo.GetByIdAsync(request.BatchId)
            ?? throw new InvalidOperationException($"批次 {request.BatchId} 不存在");

        // 已撤销批次不允许重新计费
        if (batch.FIsRevoked)
            throw new InvalidOperationException("该批次已撤销，不能重新计费");

        // 前置检查：是否还有未解决的 ERR_SHOP_EMPTY（仍为 Pending）
        var remainingEmpty = await _errorRepo.Query()
            .CountAsync(e => e.FBatchId == request.BatchId
                && e.FErrorType == ErrShopEmpty
                && (e.FDispatchStatus == null || e.FDispatchStatus == "Pending"));
        if (remainingEmpty > 0)
            throw new InvalidOperationException($"该批次仍有 {remainingEmpty} 条空店铺账号未处理，请先在「空店铺账号」页面补填或忽略");

        // 取消该批次关联的所有活跃 WorkItem（重新计费会重新派发）
        await CancelActiveWorkItemsForBatchAsync(request.BatchId);

        // 清除因配置问题产生的 ERR_SHOP_PENDING_CONFIG 旧明细（自动建档已处理完成）
        var oldPendingErrors = await _errorRepo.Query()
            .Where(e => e.FBatchId == request.BatchId && e.FErrorType == ErrShopPendingConfig
                && (e.FDispatchStatus == null || e.FDispatchStatus == "Pending" || e.FDispatchStatus == "Resolved"))
            .Select(e => e.FID)
            .ToListAsync();
        foreach (var id in oldPendingErrors)
            await _errorRepo.DeleteAsync(id);

        // 触发管道重跑
        try
        {
            await _importService.RetryBatchAsync(request.BatchId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("只有失败或卡住"))
        {
            return new RerunBillingResultDto
            {
                BatchId = request.BatchId,
                Enqueued = false,
                Message = "批次当前状态不允许重跑（仅失败或卡住的批次可重跑）。如需强制重跑，请先在批次列表中将其标记为失败。"
            };
        }

        _logger.LogInformation("QualityCenter: 批次 {BatchId} 已触发重新计费", request.BatchId);
        return new RerunBillingResultDto
        {
            BatchId = request.BatchId,
            Enqueued = true,
            Message = "批次已重新提交，将在后台重新执行计费管道"
        };
    }

    // ========== 未识别网点 ==========

    public async Task<PagedResult<UnrecognizedNetworkPointItemDto>> GetUnrecognizedNetworkPointsAsync(UnrecognizedNetworkPointQueryRequest request)
    {
        var query = _errorRepo.Query()
            .Where(e => e.FErrorType == ErrNetworkPointNotFound
                && e.FDispatchStatus != "Resolved" && e.FDispatchStatus != "Ignored");

        if (request.BatchId.HasValue)
            query = query.Where(e => e.FBatchId == request.BatchId.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(e => e.FOriginalValue != null && e.FOriginalValue.Contains(kw));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = rows.Select(e => new UnrecognizedNetworkPointItemDto
        {
            ErrorId = e.FID,
            BatchId = e.FBatchId,
            WaybillNo = ExtractWaybillNoFromMessage(e.FErrorMessage),
            NetworkPointName = e.FOriginalValue ?? "",
            DispatchStatus = e.FDispatchStatus ?? "Pending",
            CreateTime = e.FCreatedTime
        }).ToList();

        return new PagedResult<UnrecognizedNetworkPointItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<AssociateNetworkPointResultDto> AssociateNetworkPointAsync(AssociateNetworkPointRequest request)
    {
        // 1. 校验网点编号存在
        var networkPoint = await _networkPointRepo.Query()
            .FirstOrDefaultAsync(np => np.FCode == request.NetworkPointCode);
        if (networkPoint == null)
            throw new InvalidOperationException($"网点编号 [{request.NetworkPointCode}] 不存在，请先新增网点");

        // 2. 添加映射关系（如不存在）
        var existingAlias = await _aliasRepo.Query()
            .FirstOrDefaultAsync(a => a.FName == request.NetworkPointName && a.FOrgId == networkPoint.FOrgId);
        if (existingAlias == null)
        {
            var alias = new ExpNetworkPointAlias
            {
                FName = request.NetworkPointName,
                FNetworkPointCode = request.NetworkPointCode,
                FOrgId = networkPoint.FOrgId
            };
            await _aliasRepo.AddAsync(alias);
            _logger.LogInformation("QualityCenter: 新增网点名称映射 [{Name}] → [{Code}]", request.NetworkPointName, request.NetworkPointCode);
        }

        // 3. 将所有匹配的错误记录标记为 Resolved
        var errorsQuery = _errorRepo.Query()
            .Where(e => e.FErrorType == ErrNetworkPointNotFound
                && e.FOriginalValue == request.NetworkPointName
                && e.FDispatchStatus != "Resolved" && e.FDispatchStatus != "Ignored");
        if (request.BatchId.HasValue)
            errorsQuery = errorsQuery.Where(e => e.FBatchId == request.BatchId.Value);

        var errors = await errorsQuery.ToListAsync();

        // 4. 标记错误为 Resolved 并恢复暂存表记录状态
        var batchIds = errors.Select(e => e.FBatchId).Distinct().ToList();
        var batches = await _batchRepo.Query()
            .Where(b => batchIds.Contains(b.FID))
            .ToDictionaryAsync(b => b.FID, b => b.FActualTargetTable);

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var err in errors)
        {
            err.FDispatchStatus = "Resolved";
            await _errorRepo.UpdateAsync(err);

            // 将对应暂存表记录状态从3恢复为0（待重算）
            if (err.FStagingId.HasValue && batches.TryGetValue(err.FBatchId, out var stgTable) && !string.IsNullOrWhiteSpace(stgTable))
            {
                ValidateIdentifier(stgTable);
                var sql = $"UPDATE [{stgTable}] SET [F计算状态] = 0 WHERE [FID] = @id AND [F计算状态] = 3";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", err.FStagingId.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        _logger.LogInformation("QualityCenter: 关联网点 [{Name}] → [{Code}]，处理错误明细 {Count} 条",
            request.NetworkPointName, request.NetworkPointCode, errors.Count);

        // 解决关联的 WorkItem
        await ResolveWorkItemsByBatchAndTypeAsync(errors, ErrNetworkPointNotFound);

        return new AssociateNetworkPointResultDto
        {
            ResolvedCount = errors.Count,
            Message = $"已将 [{request.NetworkPointName}] 映射到网点 [{request.NetworkPointCode}]，处理 {errors.Count} 条错误记录"
        };
    }

    public async Task<IgnoreNetworkPointErrorsResultDto> IgnoreNetworkPointErrorsAsync(IgnoreNetworkPointErrorsRequest request)
    {
        if (request.ErrorIds == null || request.ErrorIds.Count == 0)
            return new IgnoreNetworkPointErrorsResultDto { AffectedCount = 0, Message = "未选择任何行" };

        var errors = await _errorRepo.Query()
            .Where(e => request.ErrorIds.Contains(e.FID) && e.FErrorType == ErrNetworkPointNotFound)
            .ToListAsync();

        foreach (var err in errors)
        {
            err.FDispatchStatus = "Ignored";
            if (!string.IsNullOrWhiteSpace(request.Reason))
                err.FSuggestedFix = $"用户已忽略: {request.Reason}";
            await _errorRepo.UpdateAsync(err);
        }

        return new IgnoreNetworkPointErrorsResultDto
        {
            AffectedCount = errors.Count,
            Message = $"已将 {errors.Count} 条未识别网点错误标记为忽略"
        };
    }

    // ========== 辅助 ==========

    private static void ValidateIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)
            || !Regex.IsMatch(name, @"^[A-Za-z0-9\u4e00-\u9fff_]+$"))
            throw new InvalidOperationException($"非法标识符: {name}");
    }

    private static string? ExtractWaybillNoFromMessage(string? msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return null;
        var m = Regex.Match(msg, @"运单\s*([A-Za-z0-9]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractQuotationNpCodeFromMessage(string? msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return null;
        var m = Regex.Match(msg, @"与报价网点\s*\[([^\]]+)\]");
        return m.Success ? m.Groups[1].Value : null;
    }

    // ========== 网点不一致 ==========

    public async Task<PagedResult<NetworkPointMismatchItemDto>> GetNetworkPointMismatchesAsync(NetworkPointMismatchQueryDto request)
    {
        var query = _errorRepo.Query()
            .Where(e => e.FErrorType == ErrNetworkPointMismatch
                && e.FDispatchStatus != "Resolved" && e.FDispatchStatus != "Ignored");

        if (request.BatchId.HasValue)
            query = query.Where(e => e.FBatchId == request.BatchId.Value);
        if (!string.IsNullOrWhiteSpace(request.WaybillNo))
        {
            var kw = request.WaybillNo.Trim();
            query = query.Where(e => e.FErrorMessage != null && e.FErrorMessage.Contains(kw));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = rows.Select(e => new NetworkPointMismatchItemDto
        {
            ErrorId = e.FID,
            BatchId = e.FBatchId,
            WaybillNo = ExtractWaybillNoFromMessage(e.FErrorMessage) ?? "",
            MappedNpCode = e.FOriginalValue ?? "",
            QuotationNpCode = ExtractQuotationNpCodeFromMessage(e.FErrorMessage) ?? "",
            DispatchStatus = e.FDispatchStatus ?? "Pending",
            CreateTime = e.FCreatedTime
        }).ToList();

        return new PagedResult<NetworkPointMismatchItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<int> IgnoreMismatchErrorsAsync(List<long> errorIds)
    {
        if (errorIds == null || errorIds.Count == 0)
            return 0;

        var errors = await _errorRepo.Query()
            .Where(e => errorIds.Contains(e.FID) && e.FErrorType == ErrNetworkPointMismatch)
            .ToListAsync();

        foreach (var err in errors)
        {
            err.FDispatchStatus = "Ignored";
            await _errorRepo.UpdateAsync(err);
        }

        return errors.Count;
    }

    // ========== WorkItem 生命周期集成 ==========

    /// <summary>
    /// 根据已解决的错误明细，查找并解决关联的 WorkItem
    /// </summary>
    private async Task ResolveWorkItemsByBatchAndTypeAsync(IEnumerable<CfBatchError> resolvedErrors, string errorType)
    {
        try
        {
            var batchIds = resolvedErrors.Select(e => e.FBatchId).Distinct().ToList();
            foreach (var batchId in batchIds)
            {
                // 检查该批次是否还有未解决的同类错误
                var remainingCount = await _errorRepo.Query()
                    .CountAsync(e => e.FBatchId == batchId
                        && e.FErrorType == errorType
                        && (e.FDispatchStatus == null || e.FDispatchStatus == "Pending"));

                if (remainingCount == 0)
                {
                    // 该批次该类型错误已全部解决，关闭 WorkItem
                    var activeItems = await GetActiveWorkItemIdsAsync(batchId, errorType);
                    foreach (var itemId in activeItems)
                    {
                        await _workItemService.ResolveAsync(itemId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QualityCenter: 解决关联 WorkItem 失败，错误类型={ErrorType}", errorType);
        }
    }

    /// <summary>
    /// 取消指定批次关联的所有活跃 WorkItem
    /// </summary>
    private async Task CancelActiveWorkItemsForBatchAsync(long batchId)
    {
        try
        {
            // 查找该批次关联的所有活跃 WorkItem
            var errorTypes = new[] { ErrShopEmpty, ErrShopPendingConfig, ErrNetworkPointNotFound, ErrNetworkPointMismatch };
            foreach (var errorType in errorTypes)
            {
                var activeItems = await GetActiveWorkItemIdsAsync(batchId, errorType);
                foreach (var itemId in activeItems)
                {
                    await _workItemService.CancelBySystemAsync(itemId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QualityCenter: 取消批次 {BatchId} 关联 WorkItem 失败", batchId);
        }
    }

    /// <summary>
    /// 查找指定 bizId + bizType 的活跃 WorkItem ID 列表
    /// </summary>
    private async Task<List<long>> GetActiveWorkItemIdsAsync(long batchId, string errorType)
    {
        return await _workItemService.GetActiveWorkItemIdsAsync(batchId, errorType);
    }
}
