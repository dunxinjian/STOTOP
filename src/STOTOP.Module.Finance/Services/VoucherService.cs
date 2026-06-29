using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Events;
using STOTOP.Module.Finance.Services.Auxiliary;
using STOTOP.Module.Finance.Services.Interfaces;
// 消歧义：本文件 IVoucherService 指 Finance 内部接口
using IVoucherService = STOTOP.Module.Finance.Services.Interfaces.IVoucherService;

namespace STOTOP.Module.Finance.Services;

public class VoucherService : IVoucherService
{
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinVoucherEntry> _entryRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly OperationLogService _operationLogService;
    private readonly ChangeTrackingService _changeTrackingService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly STOTOPDbContext _context;

    public VoucherService(
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinVoucherEntry> entryRepository,
        IRepository<FinAccount> accountRepository,
        IRepository<FinAccountPeriod> periodRepository,
        OperationLogService operationLogService,
        ChangeTrackingService changeTrackingService,
        IHttpContextAccessor httpContextAccessor,
        IEventDispatcher eventDispatcher,
        STOTOPDbContext context)
    {
        _voucherRepository = voucherRepository;
        _entryRepository = entryRepository;
        _accountRepository = accountRepository;
        _periodRepository = periodRepository;
        _operationLogService = operationLogService;
        _changeTrackingService = changeTrackingService;
        _httpContextAccessor = httpContextAccessor;
        _eventDispatcher = eventDispatcher;
        _context = context;
    }

    /// <summary>
    /// 把一组写操作包进事务：关系型 provider 且当前无外层事务时，自开事务、失败整体回滚；
    /// 已存在外层事务（如 CardFlow FlowEngineService 审批流在事务内经 Bridge 生成凭证）时，
    /// 直接复用外层事务、由外层统一提交/回滚——避免 EF 不支持的嵌套 BeginTransaction 抛错；
    /// 非关系型(InMemory) provider 不支持事务，退化为直接执行（行为不变）。
    /// </summary>
    private async Task WithTransactionAsync(Func<Task> writes)
    {
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
        {
            await writes();
            return;
        }
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            await writes();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    private long GetCurrentUserId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && long.TryParse(claim.Value, out var userId))
            return userId;
        return 0;
    }

    private long GetCurrentAccountSetId()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers["X-AccountSet-Id"].FirstOrDefault();
        return long.TryParse(header, out var id) ? id : 0;
    }

    /// <summary>
    /// 按 id 取凭证（可含分录），并施加组织/账套归属过滤。无权或不存在均返回 null。
    /// </summary>
    private async Task<FinVoucher?> GetOwnedVoucherAsync(long id, bool includeEntries = false)
    {
        var query = _voucherRepository.Query();
        if (includeEntries) query = query.Include(v => v.Entries);
        var voucher = await query.AsTracking().FirstOrDefaultAsync(v => v.FID == id);
        if (voucher == null) return null;
        var orgId = GetCurrentOrgId();
        var accountSetId = GetCurrentAccountSetId();
        return VoucherPostingRules.IsAccessible(voucher, orgId, accountSetId) ? voucher : null;
    }

    public async Task<VoucherPagedResult> GetPagedListAsync(VoucherQueryRequest request, long accountSetId = 0)
    {
        var currentOrgId = GetCurrentOrgId();
        IQueryable<FinVoucher> query = _voucherRepository.Query()
            .Include(v => v.Entries)
            .Where(v => v.FOrgId == currentOrgId);

        // accountSetId > 0 时按账套过滤，否则显示所有凭证
        if (accountSetId > 0)
            query = query.Where(v => v.FAccountSetId == accountSetId);

        // 按日期区间查询
        if (request.StartDate.HasValue)
            query = query.Where(v => v.FDate >= request.StartDate.Value);
        
        if (request.EndDate.HasValue)
        {
            // endDate 需要包含当天整天，所以取次日零点作为上限
            var endDateExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(v => v.FDate < endDateExclusive);
        }

        // 按单个日期查询
        if (request.Date.HasValue)
        {
            var dateStart = request.Date.Value.Date;
            var dateEnd = dateStart.AddDays(1);
            query = query.Where(v => v.FDate >= dateStart && v.FDate < dateEnd);
        }
        
        if (!string.IsNullOrEmpty(request.VoucherWord))
            query = query.Where(v => v.FVoucherWord == request.VoucherWord);
        
        if (request.PeriodId.HasValue)
            query = query.Where(v => v.FPeriodId == request.PeriodId.Value);

        // 按账期区间查询
        if (request.StartPeriodId.HasValue)
            query = query.Where(v => v.FPeriodId >= request.StartPeriodId.Value);
        
        if (request.EndPeriodId.HasValue)
            query = query.Where(v => v.FPeriodId <= request.EndPeriodId.Value);
        
        // 按来源筛选
        if (!string.IsNullOrEmpty(request.Source))
            query = query.Where(v => v.FSource == request.Source);

        // 关键词搜索
        if (!string.IsNullOrEmpty(request.Keyword) && !string.IsNullOrEmpty(request.SearchField))
        {
            switch (request.SearchField)
            {
                case "summary":
                    query = query.Where(v => v.Entries.Any(e => e.FSummary.Contains(request.Keyword)));
                    break;
                case "remark":
                    query = query.Where(v => v.FRemark != null && v.FRemark.Contains(request.Keyword));
                    break;
                case "voucherNumber":
                    if (int.TryParse(request.Keyword, out var num))
                        query = query.Where(v => v.FVoucherNo == num);
                    break;
                case "account":
                    query = query.Where(v => v.Entries.Any(e => e.FAccountName.Contains(request.Keyword) || e.FAccountCode.Contains(request.Keyword)));
                    break;
            }
        }
        else if (!string.IsNullOrEmpty(request.Keyword))
        {
            // 未指定SearchField时，默认按摘要搜索（兼容旧逻辑）
            query = query.Where(v => v.Entries.Any(e => e.FSummary.Contains(request.Keyword)));
        }

        // baseQuery 是不含状态过滤的查询，用于计算各状态计数
        var baseQuery = query;
        var totalAllCount = await baseQuery.CountAsync();
        var pendingCount = await baseQuery.CountAsync(v => v.FStatus == 1);
        var pendingRecordCount = await baseQuery.CountAsync(v => v.FSource == "费用支出" && v.FRemark != null && v.FRemark.Contains("[待补录]") && v.FStatus == 0);

        // 应用状态过滤
        if (request.Status.HasValue)
            query = query.Where(v => v.FStatus == request.Status.Value);

        var total = await query.CountAsync();
        
        // 动态排序
        query = ApplySorting(query, request.SortField, request.SortOrder);

        var vouchers = await query
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var periods = await _periodRepository.Query().ToListAsync();

        var items = vouchers.Select(v =>
        {
            var period = periods.FirstOrDefault(p => p.FID == v.FPeriodId);
            return new VoucherListDto
            {
                Id = v.FID,
                VoucherWord = v.FVoucherWord,
                VoucherNo = v.FVoucherNo,
                Date = v.FDate,
                PeriodId = v.FPeriodId,
                PeriodName = period != null ? $"{period.FYear}年{period.FPeriodNo}期" : "",
                Creator = v.FCreator,
                Auditor = v.FAuditor,
                Status = v.FStatus,
                TotalAmount = v.Entries.Sum(e => e.FDebitAmount),
                Summary = v.Entries.FirstOrDefault()?.FSummary ?? "",
                Remark = v.FRemark,
                Entries = v.Entries.OrderBy(e => e.FLineNo).Select(e => new VoucherEntryDto
                {
                    Id = e.FID,
                    LineNo = e.FLineNo,
                    Summary = e.FSummary,
                    AccountId = e.FAccountId,
                    AccountCode = e.FAccountCode,
                    AccountName = e.FAccountName,
                    AuxiliaryJson = e.FAuxiliaryJson,
                    DebitAmount = e.FDebitAmount,
                    CreditAmount = e.FCreditAmount
                }).ToList()
            };
        }).ToList();

        return new VoucherPagedResult
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            TotalAllCount = totalAllCount,
            PendingCount = pendingCount,
            PendingRecordCount = pendingRecordCount
        };
    }

    public async Task<VoucherDto?> GetByIdAsync(long id)
    {
        var voucher = await GetOwnedVoucherAsync(id, includeEntries: true);

        if (voucher == null) return null;

        var period = await _periodRepository.GetByIdAsync(voucher.FPeriodId);
        
        return new VoucherDto
        {
            Id = voucher.FID,
            VoucherWord = voucher.FVoucherWord,
            VoucherNo = voucher.FVoucherNo,
            Date = voucher.FDate,
            PeriodId = voucher.FPeriodId,
            PeriodName = period != null ? $"{period.FYear}年{period.FPeriodNo}期" : "",
            AttachmentCount = voucher.FAttachmentCount,
            Creator = voucher.FCreator,
            Auditor = voucher.FAuditor,
            Modifier = voucher.FModifier,
            Status = voucher.FStatus,
            Source = voucher.FSource,
            Remark = voucher.FRemark,
            TotalDebit = voucher.Entries.Sum(e => e.FDebitAmount),
            TotalCredit = voucher.Entries.Sum(e => e.FCreditAmount),
            Entries = voucher.Entries.OrderBy(e => e.FLineNo).Select(e => new VoucherEntryDto
            {
                Id = e.FID,
                LineNo = e.FLineNo,
                Summary = e.FSummary,
                AccountId = e.FAccountId,
                AccountCode = e.FAccountCode,
                AccountName = e.FAccountName,
                AuxiliaryJson = e.FAuxiliaryJson,
                DebitAmount = e.FDebitAmount,
                CreditAmount = e.FCreditAmount
            }).ToList()
        };
    }

    public async Task<VoucherDto> CreateAsync(CreateVoucherRequest request, string creator, long accountSetId = 0, bool enforceAuxContract = false)
    {
        ValidateVoucher(request);
        await ValidateEntriesAsync(request.Entries, enforceAuxContract);

        // 验证辅助核算JSON格式
        foreach (var entry in request.Entries)
            ValidateAuxiliaryJson(entry.AuxiliaryJson);

        // 期间解析 + 结账校验（后端权威：PeriodId<=0 时按日期解析；无论来源，已结账期一律拒绝）
        long periodId = request.PeriodId;
        if (periodId <= 0)
        {
            var period = await ResolvePeriodAsync(request.Date, accountSetId);
            VoucherPostingRules.EnsureOpenForPosting(period);
            periodId = period.FID;
        }
        else
        {
            // 调用方直接传入 periodId（如 CardFlow 桥接/导入）：若该期间存在且已结账，同样拒绝。
            // 期间不存在时不在此处中断（保持对历史/特殊调用方的兼容），由后续落库与外键约束兜底。
            var period = await _periodRepository.GetByIdAsync(periodId);
            if (period != null)
                VoucherPostingRules.EnsureOpenForPosting(period);
        }

        var nextNo = await GetNextNumberAsync(request.VoucherWord, periodId, accountSetId);

        // 确定组织ID：优先从HttpContext获取，后台任务无HttpContext时从账套反查
        long orgId = GetCurrentOrgId();
        if (orgId == 0 && accountSetId > 0)
        {
            var accountSet = await _context.Set<FinAccountSet>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.FID == accountSetId);
            orgId = accountSet?.FOrgId ?? 0;
        }

        var voucher = new FinVoucher
        {
            FVoucherWord = request.VoucherWord,
            FVoucherNo = nextNo,
            FDate = request.Date,
            FPeriodId = periodId,
            FAttachmentCount = request.AttachmentCount,
            FCreator = creator,
            FStatus = 1, // 待审核
            FSource = request.Source,
            FRemark = request.Remark,
            FDataScopeId = request.DataScopeId,
            FAccountSetId = accountSetId,
            FOrgId = orgId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await WithTransactionAsync(async () =>
        {
            await _voucherRepository.AddAsync(voucher);

            foreach (var entryRequest in request.Entries)
            {
                var account = await _accountRepository.GetByIdAsync(entryRequest.AccountId);

                var entry = new FinVoucherEntry
                {
                    FVoucherId = voucher.FID,
                    FLineNo = entryRequest.LineNo,
                    FSummary = entryRequest.Summary,
                    FAccountId = entryRequest.AccountId,
                    FAccountCode = account?.FCode ?? "",
                    FAccountName = account?.FName ?? "",
                    FAuxiliaryJson = entryRequest.AuxiliaryJson,
                    FDebitAmount = entryRequest.DebitAmount,
                    FCreditAmount = entryRequest.CreditAmount,
                    FDataScopeId = request.DataScopeId,
                    FOrgId = orgId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                await _entryRepository.AddAsync(entry);
            }
        });

        // 操作日志为 fire-and-forget（内部吞异常），放到事务提交之后，只记录已提交的操作、不占事务连接/锁
        await _operationLogService.LogAsync(
            accountSetId, "凭证", "新增",
            $"新增凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}",
            voucher.FID, $"{voucher.FVoucherWord}{voucher.FVoucherNo}");

        // 读回与事件发布放到事务提交之后
        var createdVoucher = await GetByIdAsync(voucher.FID) ?? throw new InvalidOperationException("创建凭证失败");

        // 凭证状态为待审核时发布事件
        if (voucher.FStatus == 1)
        {
            var currentUserId = GetCurrentUserId();
            await _eventDispatcher.PublishAsync(new VoucherPendingAuditEvent
            {
                VoucherId = voucher.FID,
                VoucherNo = $"{voucher.FVoucherWord}{voucher.FVoucherNo}",
                Amount = createdVoucher.TotalDebit,
                CreatorId = currentUserId,
                AuditorId = 0, // 创建时尚未指定审核人
                AccountSetId = voucher.FAccountSetId,
                TriggeredByUserId = currentUserId,
                ModuleCode = "finance"
            });
        }

        return createdVoucher;
    }

    public async Task<VoucherDto?> UpdateAsync(long id, CreateVoucherRequest request, string modifier, bool enforceAuxContract = false)
    {
        var voucher = await GetOwnedVoucherAsync(id, includeEntries: true);

        if (voucher == null) return null;
        
        if (voucher.FStatus == 2)
        {
            throw new InvalidOperationException("已审核的凭证不能编辑");
        }

        if (voucher.FStatus == 3)
        {
            throw new InvalidOperationException("该凭证所在期间已结账，不能编辑");
        }

        // 检查期间是否已结账
        var period = await _periodRepository.GetByIdAsync(voucher.FPeriodId);
        if (period != null && period.FIsClosed == 1)
        {
            throw new InvalidOperationException("该凭证所属期间已结账，不能编辑");
        }

        ValidateVoucher(request);
        await ValidateEntriesAsync(request.Entries, enforceAuxContract);

        // 验证辅助核算JSON格式
        foreach (var entry in request.Entries)
            ValidateAuxiliaryJson(entry.AuxiliaryJson);

        // 记录变更前的快照，用于字段级变更追踪
        var oldSnapshot = new FinVoucher
        {
            FDate = voucher.FDate,
            FPeriodId = voucher.FPeriodId,
            FAttachmentCount = voucher.FAttachmentCount,
            FModifier = voucher.FModifier,
            FRemark = voucher.FRemark,
            FVoucherWord = voucher.FVoucherWord,
            FVoucherNo = voucher.FVoucherNo,
            FCreator = voucher.FCreator,
            FAuditor = voucher.FAuditor,
            FStatus = voucher.FStatus,
            FSource = voucher.FSource,
            FAccountSetId = voucher.FAccountSetId
        };

        // 期间随日期重解析（后端权威），并校验目标期间未结账（无论 PeriodId 来源）
        long newPeriodId = request.PeriodId;
        if (newPeriodId <= 0)
        {
            var newPeriod = await ResolvePeriodAsync(request.Date, voucher.FAccountSetId);
            VoucherPostingRules.EnsureOpenForPosting(newPeriod);
            newPeriodId = newPeriod.FID;
        }
        else
        {
            var newPeriod = await _periodRepository.GetByIdAsync(newPeriodId);
            if (newPeriod != null)
                VoucherPostingRules.EnsureOpenForPosting(newPeriod);
        }

        voucher.FDate = request.Date;
        voucher.FPeriodId = newPeriodId;
        voucher.FAttachmentCount = request.AttachmentCount;
        voucher.FModifier = modifier;
        voucher.FRemark = request.Remark;
        voucher.FUpdatedTime = DateTime.Now;

        await WithTransactionAsync(async () =>
        {
            await _voucherRepository.UpdateAsync(voucher);

            // 记录凭证字段级变更
            await _changeTrackingService.TrackChangesAsync("凭证", voucher.FID, oldSnapshot, voucher, voucher.FAccountSetId);

            // 删除旧分录（先快照分录ID，避免删除时修改正在枚举的导航集合）
            foreach (var entryId in voucher.Entries.Select(e => e.FID).ToList())
            {
                await _entryRepository.DeleteAsync(entryId);
            }

            // 添加新分录
            foreach (var entryRequest in request.Entries)
            {
                var account = await _accountRepository.GetByIdAsync(entryRequest.AccountId);

                var entry = new FinVoucherEntry
                {
                    FVoucherId = voucher.FID,
                    FLineNo = entryRequest.LineNo,
                    FSummary = entryRequest.Summary,
                    FAccountId = entryRequest.AccountId,
                    FAccountCode = account?.FCode ?? "",
                    FAccountName = account?.FName ?? "",
                    FAuxiliaryJson = entryRequest.AuxiliaryJson,
                    FDebitAmount = entryRequest.DebitAmount,
                    FCreditAmount = entryRequest.CreditAmount,
                    FOrgId = voucher.FOrgId,
                    FDataScopeId = request.DataScopeId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                await _entryRepository.AddAsync(entry);
            }
        });

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var voucher = await GetOwnedVoucherAsync(id);
        if (voucher == null) return false;
        
        if (voucher.FStatus == 2)
        {
            throw new InvalidOperationException("已审核的凭证不能删除");
        }

        if (voucher.FStatus == 3)
        {
            throw new InvalidOperationException("该凭证所在期间已结账，不能删除");
        }

        // 检查期间是否已结账
        var period = await _periodRepository.GetByIdAsync(voucher.FPeriodId);
        if (period != null && period.FIsClosed == 1)
        {
            throw new InvalidOperationException("该凭证所属期间已结账，不能删除");
        }

        await _voucherRepository.DeleteAsync(id);
        await _operationLogService.LogAsync(
            voucher.FAccountSetId, "凭证", "删除",
            $"删除凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}",
            id, $"{voucher.FVoucherWord}{voucher.FVoucherNo}");
        return true;
    }

    public async Task<bool> AuditAsync(long id, string auditor)
    {
        var voucher = await GetOwnedVoucherAsync(id);
        if (voucher == null) return false;

        voucher.FStatus = 2; // 已审核
        voucher.FAuditor = auditor;
        voucher.FUpdatedTime = DateTime.Now;
        await _voucherRepository.UpdateAsync(voucher);
        await _operationLogService.LogAsync(
            voucher.FAccountSetId, "凭证", "审核",
            $"审核凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}",
            id, $"{voucher.FVoucherWord}{voucher.FVoucherNo}");
        return true;
    }

    public async Task<bool> UnAuditAsync(long id)
    {
        var voucher = await GetOwnedVoucherAsync(id);
        if (voucher == null) return false;

        voucher.FStatus = 1; // 待审核
        voucher.FAuditor = null;
        voucher.FUpdatedTime = DateTime.Now;
        await _voucherRepository.UpdateAsync(voucher);
        return true;
    }

    public async Task<VoucherDto> SaveDraftAsync(CreateVoucherRequest request, string creator, long accountSetId = 0)
    {
        // 草稿按日期解析期间（不做结账硬拒绝，草稿允许暂存）
        long periodId = request.PeriodId;
        if (periodId <= 0)
        {
            var period = await ResolvePeriodAsync(request.Date, accountSetId);
            periodId = period.FID;
        }
        var nextNo = await GetNextNumberAsync(request.VoucherWord, periodId, accountSetId);

        // 确定组织ID：优先从HttpContext获取，后台任务无HttpContext时从账套反查
        long orgId = GetCurrentOrgId();
        if (orgId == 0 && accountSetId > 0)
        {
            var accountSet = await _context.Set<FinAccountSet>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.FID == accountSetId);
            orgId = accountSet?.FOrgId ?? 0;
        }

        var voucher = new FinVoucher
        {
            FVoucherWord = request.VoucherWord,
            FVoucherNo = nextNo,
            FDate = request.Date,
            FPeriodId = periodId,
            FAttachmentCount = request.AttachmentCount,
            FCreator = creator,
            FStatus = 0, // 草稿
            FSource = request.Source,
            FRemark = request.Remark,
            FDataScopeId = request.DataScopeId,
            FAccountSetId = accountSetId,
            FOrgId = orgId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await WithTransactionAsync(async () =>
        {
            await _voucherRepository.AddAsync(voucher);

            foreach (var entryRequest in request.Entries)
            {
                var account = await _accountRepository.GetByIdAsync(entryRequest.AccountId);

                var entry = new FinVoucherEntry
                {
                    FVoucherId = voucher.FID,
                    FLineNo = entryRequest.LineNo,
                    FSummary = entryRequest.Summary,
                    FAccountId = entryRequest.AccountId,
                    FAccountCode = account?.FCode ?? "",
                    FAccountName = account?.FName ?? "",
                    FAuxiliaryJson = entryRequest.AuxiliaryJson,
                    FDebitAmount = entryRequest.DebitAmount,
                    FCreditAmount = entryRequest.CreditAmount,
                    FDataScopeId = request.DataScopeId,
                    FOrgId = orgId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                await _entryRepository.AddAsync(entry);
            }
        });

        return await GetByIdAsync(voucher.FID) ?? throw new InvalidOperationException("保存草稿失败");
    }

    public async Task<List<VoucherListDto>> GetDraftsAsync(long accountSetId = 0)
    {
        var vouchers = await _voucherRepository.Query()
            .Include(v => v.Entries)
            .Where(v => v.FStatus == 0 && v.FAccountSetId == accountSetId)
            .OrderByDescending(v => v.FCreatedTime)
            .ToListAsync();

        var periods = await _periodRepository.Query().ToListAsync();

        return vouchers.Select(v =>
        {
            var period = periods.FirstOrDefault(p => p.FID == v.FPeriodId);
            return new VoucherListDto
            {
                Id = v.FID,
                VoucherWord = v.FVoucherWord,
                VoucherNo = v.FVoucherNo,
                Date = v.FDate,
                PeriodId = v.FPeriodId,
                PeriodName = period != null ? $"{period.FYear}年{period.FPeriodNo}期" : "",
                Creator = v.FCreator,
                Auditor = v.FAuditor,
                Status = v.FStatus,
                TotalAmount = v.Entries.Sum(e => e.FDebitAmount),
                Summary = v.Entries.FirstOrDefault()?.FSummary ?? "",
                Remark = v.FRemark,
                Entries = v.Entries.OrderBy(e => e.FLineNo).Select(e => new VoucherEntryDto
                {
                    Id = e.FID,
                    LineNo = e.FLineNo,
                    Summary = e.FSummary,
                    AccountId = e.FAccountId,
                    AccountCode = e.FAccountCode,
                    AccountName = e.FAccountName,
                    AuxiliaryJson = e.FAuxiliaryJson,
                    DebitAmount = e.FDebitAmount,
                    CreditAmount = e.FCreditAmount
                }).ToList()
            };
        }).ToList();
    }

    public async Task<bool> ReorderNumbersAsync(long periodId, long accountSetId = 0)
    {
        var vouchers = await _voucherRepository.Query()
            .Where(v => v.FPeriodId == periodId && v.FAccountSetId == accountSetId)
            .OrderBy(v => v.FDate)
            .ThenBy(v => v.FID)
            .ToListAsync();

        int no = 1;
        foreach (var voucher in vouchers)
        {
            voucher.FVoucherNo = no++;
            voucher.FUpdatedTime = DateTime.Now;
            await _voucherRepository.UpdateAsync(voucher);
        }

        return true;
    }

    public async Task<int> GetNextNumberAsync(string voucherWord, long periodId, long accountSetId = 0)
    {
        var maxNo = await _voucherRepository.Query()
            .Where(v => v.FVoucherWord == voucherWord && v.FPeriodId == periodId && v.FAccountSetId == accountSetId)
            .MaxAsync(v => (int?)v.FVoucherNo) ?? 0;

        return maxNo + 1;
    }

    /// <summary>
    /// 解析凭证期间：按 日期+账套 定位；查不到抛错。复用 VoucherPostingRules 纯规则。
    /// </summary>
    private async Task<FinAccountPeriod> ResolvePeriodAsync(DateTime date, long accountSetId)
    {
        var candidates = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId)
            .ToListAsync();
        return VoucherPostingRules.ResolvePeriod(candidates, date, accountSetId);
    }

    public async Task<int> GetPendingAuditCountAsync(long accountSetId = 0)
    {
        return await _voucherRepository.Query()
            .CountAsync(v => v.FStatus == 1 && v.FAccountSetId == accountSetId);
    }

    public async Task<ApiResult<object>> CopyAsync(long voucherId)
    {
        var source = await GetOwnedVoucherAsync(voucherId, includeEntries: true);
        if (source == null)
            return ApiResult<object>.Fail("凭证不存在");

        // 复制单落当前开放期：今日无对应期间或已结账则拒绝（转 Fail，避免端点 500）
        FinAccountPeriod targetPeriod;
        try
        {
            targetPeriod = await ResolvePeriodAsync(DateTime.Today, source.FAccountSetId);
            VoucherPostingRules.EnsureOpenForPosting(targetPeriod);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
        var nextNo = await GetNextNumberAsync(source.FVoucherWord, targetPeriod.FID, source.FAccountSetId);

        var newVoucher = new FinVoucher
        {
            FVoucherWord = source.FVoucherWord,
            FVoucherNo = nextNo,
            FDate = DateTime.Today,
            FPeriodId = targetPeriod.FID,
            FAttachmentCount = 0,
            FCreator = source.FCreator,
            FStatus = 1, // 待审核
            FRemark = source.FRemark,
            FSource = $"copy:{voucherId}",
            FAccountSetId = source.FAccountSetId,
            FOrgId = source.FOrgId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await WithTransactionAsync(async () =>
        {
            await _voucherRepository.AddAsync(newVoucher);

            foreach (var entry in source.Entries.OrderBy(e => e.FLineNo))
            {
                var newEntry = new FinVoucherEntry
                {
                    FVoucherId = newVoucher.FID,
                    FLineNo = entry.FLineNo,
                    FSummary = entry.FSummary,
                    FAccountId = entry.FAccountId,
                    FAccountCode = entry.FAccountCode,
                    FAccountName = entry.FAccountName,
                    FAuxiliaryJson = entry.FAuxiliaryJson,
                    FDebitAmount = entry.FDebitAmount,
                    FCreditAmount = entry.FCreditAmount,
                    FOrgId = source.FOrgId,
                    FDataScopeId = entry.FDataScopeId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                await _entryRepository.AddAsync(newEntry);
            }
        });

        // 操作日志 fire-and-forget，放到事务提交之后
        await _operationLogService.LogAsync(
            source.FAccountSetId, "凭证", "复制",
            $"复制凭证 {source.FVoucherWord}{source.FVoucherNo} → {newVoucher.FVoucherWord}{newVoucher.FVoucherNo}",
            newVoucher.FID, $"{newVoucher.FVoucherWord}{newVoucher.FVoucherNo}");

        return ApiResult<object>.Success(new { newVoucherId = newVoucher.FID, voucherNo = newVoucher.FVoucherNo }, "复制成功");
    }

    public async Task<ApiResult<object>> ReverseAsync(long voucherId)
    {
        var source = await GetOwnedVoucherAsync(voucherId, includeEntries: true);
        if (source == null)
            return ApiResult<object>.Fail("凭证不存在");

        // 红字冲销落当前开放期：今日无对应期间或已结账则拒绝（转 Fail，避免端点 500）
        FinAccountPeriod targetPeriod;
        try
        {
            targetPeriod = await ResolvePeriodAsync(DateTime.Today, source.FAccountSetId);
            VoucherPostingRules.EnsureOpenForPosting(targetPeriod);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
        var nextNo = await GetNextNumberAsync(source.FVoucherWord, targetPeriod.FID, source.FAccountSetId);

        var newVoucher = new FinVoucher
        {
            FVoucherWord = source.FVoucherWord,
            FVoucherNo = nextNo,
            FDate = DateTime.Today,
            FPeriodId = targetPeriod.FID,
            FAttachmentCount = 0,
            FCreator = source.FCreator,
            FStatus = 1, // 待审核
            FRemark = source.FRemark,
            FSource = $"reverse:{voucherId}",
            FAccountSetId = source.FAccountSetId,
            FOrgId = source.FOrgId,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await WithTransactionAsync(async () =>
        {
            await _voucherRepository.AddAsync(newVoucher);

            foreach (var entry in source.Entries.OrderBy(e => e.FLineNo))
            {
                var newEntry = new FinVoucherEntry
                {
                    FVoucherId = newVoucher.FID,
                    FLineNo = entry.FLineNo,
                    FSummary = $"冲销{entry.FSummary}",
                    FAccountId = entry.FAccountId,
                    FAccountCode = entry.FAccountCode,
                    FAccountName = entry.FAccountName,
                    FAuxiliaryJson = entry.FAuxiliaryJson,
                    FDebitAmount = -entry.FDebitAmount,
                    FCreditAmount = -entry.FCreditAmount,
                    FOrgId = source.FOrgId,
                    FDataScopeId = entry.FDataScopeId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                await _entryRepository.AddAsync(newEntry);
            }
        });

        // 操作日志 fire-and-forget，放到事务提交之后
        await _operationLogService.LogAsync(
            source.FAccountSetId, "凭证", "冲销",
            $"冲销凭证 {source.FVoucherWord}{source.FVoucherNo} → {newVoucher.FVoucherWord}{newVoucher.FVoucherNo}",
            newVoucher.FID, $"{newVoucher.FVoucherWord}{newVoucher.FVoucherNo}");

        return ApiResult<object>.Success(new { newVoucherId = newVoucher.FID, voucherNo = newVoucher.FVoucherNo }, "冲销凭证已生成");
    }

    public async Task<ApiResult<object>> BatchAuditAsync(List<long> voucherIds, long auditorId, string auditorName)
    {
        int successCount = 0;
        int skipCount = 0;
        var now = DateTime.Now;

        foreach (var id in voucherIds)
        {
            var voucher = await GetOwnedVoucherAsync(id);
            if (voucher == null) continue;

            if (voucher.FStatus == 2)
            {
                skipCount++;
                continue;
            }

            voucher.FStatus = 2;
            voucher.FAuditor = auditorName;
            voucher.FUpdatedTime = now;
            await _voucherRepository.UpdateAsync(voucher);

            await _operationLogService.LogAsync(
                voucher.FAccountSetId, "凭证", "批量审核",
                $"批量审核凭证 {voucher.FVoucherWord}{voucher.FVoucherNo}",
                id, $"{voucher.FVoucherWord}{voucher.FVoucherNo}");

            successCount++;
        }

        return ApiResult<object>.Success(
            new { successCount, skipCount },
            $"成功审核 {successCount} 张，跳过 {skipCount} 张（已审核）");
    }

    public async Task<ApiResult<object>> CheckGapAsync(long accountSetId, int year, int periodNo)
    {
        // 找到对应期间
        var period = await _periodRepository.Query()
            .FirstOrDefaultAsync(p => p.FAccountSetId == accountSetId && p.FYear == year && p.FPeriodNo == periodNo);

        if (period == null)
            return ApiResult<object>.Fail($"未找到 {year}年第{periodNo}期的账期");

        var voucherNos = await _voucherRepository.Query()
            .Where(v => v.FAccountSetId == accountSetId && v.FPeriodId == period.FID)
            .Select(v => v.FVoucherNo)
            .OrderBy(n => n)
            .ToListAsync();

        if (voucherNos.Count == 0)
            return ApiResult<object>.Success(new { missingNos = new List<int>(), totalCount = 0 }, "该期间无凭证");

        var missingNos = new List<int>();
        for (int i = voucherNos[0]; i <= voucherNos[^1]; i++)
        {
            if (!voucherNos.Contains(i))
                missingNos.Add(i);
        }

        var msg = missingNos.Count == 0
            ? "当前期间凭证号连续，无断号"
            : $"发现 {missingNos.Count} 个断号";

        return ApiResult<object>.Success(
            new { missingNos, totalCount = voucherNos.Count, minNo = voucherNos[0], maxNo = voucherNos[^1] },
            msg);
    }

    private async Task ValidateEntriesAsync(List<CreateVoucherEntryRequest> entries, bool enforceAuxContract = false)
    {
        foreach (var entry in entries)
        {
            var account = await _accountRepository.GetByIdAsync(entry.AccountId);
            if (account == null)
                throw new InvalidOperationException($"科目不存在：{entry.AccountId}");
            if (account.FEnableStatus != 1)
                throw new InvalidOperationException($"科目 {account.FCode} {account.FName} 已停用，不能录入凭证");

            // 验证辅助核算JSON格式（兼容 AuxiliaryResult[] 数组和 Object 两种格式）
            if (!string.IsNullOrEmpty(entry.AuxiliaryJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(entry.AuxiliaryJson);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array && doc.RootElement.ValueKind != JsonValueKind.Object)
                        throw new InvalidOperationException($"分录第{entry.LineNo}行的辅助核算数据格式无效");
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException($"分录第{entry.LineNo}行的辅助核算数据格式无效");
                }
            }

            // 方案B 源打标(E2)：科目声明了辅助核算维度则分录必须带齐(标准"科目挂辅助核算即必填"口径)。
            // 仅手动录入/导入入口(enforceAuxContract=true)强校验；自动凭证/迁移/草稿默认 false 不阻断,
            // 其维度由自动凭证引擎按科目契约补齐(批次2-C)。
            if (enforceAuxContract)
            {
                var missing = EntryAuxValidator.GetMissing(account.FAuxiliary, entry.AuxiliaryJson);
                if (missing.Count > 0)
                    throw new InvalidOperationException(
                        $"分录第{entry.LineNo}行科目 {account.FCode} {account.FName} 缺少必填辅助核算：{string.Join("、", missing)}");
            }
        }
    }

    internal static void ValidateVoucher(CreateVoucherRequest request)
    {
        if (request.Entries == null || request.Entries.Count < 2)
        {
            throw new InvalidOperationException("凭证至少需要2条分录");
        }

        var totalDebit = request.Entries.Sum(e => e.DebitAmount);
        var totalCredit = request.Entries.Sum(e => e.CreditAmount);

        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException($"借贷不平衡，借方合计：{totalDebit}，贷方合计：{totalCredit}");
        }

        if (totalDebit == 0)
        {
            throw new InvalidOperationException("凭证金额不能为0");
        }
    }

    /// <summary>
    /// 验证辅助核算JSON格式是否有效（兼容 AuxiliaryResult[] 数组和 Object 两种格式）
    /// </summary>
    private static void ValidateAuxiliaryJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var doc = global::System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != global::System.Text.Json.JsonValueKind.Array
                && doc.RootElement.ValueKind != global::System.Text.Json.JsonValueKind.Object)
                throw new InvalidOperationException("辅助核算JSON必须是数组或对象格式");
        }
        catch (global::System.Text.Json.JsonException)
        {
            throw new InvalidOperationException("辅助核算JSON格式无效");
        }
    }

    private static IQueryable<FinVoucher> ApplySorting(IQueryable<FinVoucher> query, string? sortField, string? sortOrder)
    {
        var isDescending = string.Equals(sortOrder, "descending", StringComparison.OrdinalIgnoreCase);

        var orderedQuery = sortField?.ToLower() switch
        {
            "date" => isDescending
                ? query.OrderByDescending(v => v.FDate)
                : query.OrderBy(v => v.FDate),
            "vouchernumber" or "voucherno" => isDescending
                ? query.OrderByDescending(v => v.FVoucherNo)
                : query.OrderBy(v => v.FVoucherNo),
            "creator" => isDescending
                ? query.OrderByDescending(v => v.FCreator)
                : query.OrderBy(v => v.FCreator),
            "auditor" => isDescending
                ? query.OrderByDescending(v => v.FAuditor)
                : query.OrderBy(v => v.FAuditor),
            _ => query.OrderByDescending(v => v.FDate) // 默认排序
        };

        // 始终追加凭证号作为次要排序，确保稳定排序
        if (sortField?.ToLower() != "vouchernumber" && sortField?.ToLower() != "voucherno")
        {
            orderedQuery = ((IOrderedQueryable<FinVoucher>)orderedQuery).ThenBy(v => v.FVoucherNo);
        }

        return orderedQuery;
    }

    /// <summary>
    /// 完成凭证补录：将草稿凭证提交为待审核状态
    /// </summary>
    public async Task<ApiResult> CompleteRecordAsync(long id)
    {
        var voucher = await GetOwnedVoucherAsync(id, includeEntries: true);

        if (voucher == null)
            return ApiResult.Fail("凭证不存在");

        if (voucher.FStatus != 0)
            return ApiResult.Fail("只有草稿状态的凭证才能执行补录完成操作");

        if (voucher.FRemark == null || !voucher.FRemark.Contains("[待补录]"))
            return ApiResult.Fail("该凭证不是待补录凭证");

        // 检查所有分录的科目是否已填写
        var incompleteEntries = voucher.Entries.Where(e => e.FAccountId == 0).ToList();
        if (incompleteEntries.Any())
            return ApiResult.Fail($"尚有 {incompleteEntries.Count} 条分录未填写科目，请先完善科目信息");

        // 补录提交=过账动作：若草稿创建后所在期间已结账，禁止补录提交（不入已结账期）
        var period = await _periodRepository.GetByIdAsync(voucher.FPeriodId);
        if (period != null && period.FIsClosed == 1)
            return ApiResult.Fail($"{period.FYear}年第{period.FPeriodNo}期已结账，不能补录提交该凭证");

        // 将状态从草稿(0)改为待审核(1)
        voucher.FStatus = 1;
        // 清除备注中的"[待补录]"标记
        voucher.FRemark = voucher.FRemark.Replace("[待补录]", "").Trim();
        if (string.IsNullOrWhiteSpace(voucher.FRemark))
            voucher.FRemark = null;
        voucher.FUpdatedTime = DateTime.Now;

        await _voucherRepository.UpdateAsync(voucher);

        // 更新暂存表中关联行的凭证生成状态：从2(已生成草稿)改为3(已手动补录)
        try
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE [STG费用支出记录] SET F凭证生成状态 = 3 WHERE F凭证ID = @id";
                cmd.Parameters.Add(new SqlParameter("@id", id));
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (_context.Database.CurrentTransaction == null)
                    await conn.CloseAsync();
            }
        }
        catch
        {
            // 暂存表更新失败不影响凭证状态变更（暂存表可能不存在或无匹配行）
        }

        await _operationLogService.LogAsync(
            voucher.FAccountSetId, "凭证", "补录完成",
            $"凭证 {voucher.FVoucherWord}{voucher.FVoucherNo} 补录完成，已提交待审核",
            id, $"{voucher.FVoucherWord}{voucher.FVoucherNo}");

        return ApiResult.Ok("补录完成，凭证已提交待审核");
    }
}
