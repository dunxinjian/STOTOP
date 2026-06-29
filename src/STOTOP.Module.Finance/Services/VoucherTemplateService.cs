using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Constants;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Services;

public class VoucherTemplateService
{
    private readonly IRepository<FinVoucherTemplate> _templateRepository;
    private readonly IRepository<FinVoucherTemplateEntry> _entryRepository;
    private readonly IRepository<FinVoucher> _voucherRepository;
    private readonly IRepository<FinVoucherEntry> _voucherEntryRepository;
    private readonly IRepository<FinAccountPeriod> _periodRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<VoucherTemplateService> _logger;

    public VoucherTemplateService(
        IRepository<FinVoucherTemplate> templateRepository,
        IRepository<FinVoucherTemplateEntry> entryRepository,
        IRepository<FinVoucher> voucherRepository,
        IRepository<FinVoucherEntry> voucherEntryRepository,
        IRepository<FinAccountPeriod> periodRepository,
        IHttpContextAccessor httpContextAccessor,
        STOTOPDbContext dbContext,
        ILogger<VoucherTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _entryRepository = entryRepository;
        _voucherRepository = voucherRepository;
        _voucherEntryRepository = voucherEntryRepository;
        _periodRepository = periodRepository;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _logger = logger;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    /// <summary>获取模板列表</summary>
    public async Task<List<VoucherTemplateListDto>> GetListAsync(long accountSetId)
    {
        var templates = await _templateRepository.Query()
            .Where(t => t.FAccountSetId == accountSetId)
            .OrderBy(t => t.FSort)
            .ThenBy(t => t.FCreateTime)
            .ToListAsync();

        var ids = templates.Select(t => t.FID).ToList();
        var countMap = new Dictionary<long, int>();
        if (ids.Count > 0)
        {
            var entryCounts = await _entryRepository.Query()
                .Where(e => ids.Contains(e.FTemplateId))
                .GroupBy(e => e.FTemplateId)
                .Select(g => new { TemplateId = g.Key, Count = g.Count() })
                .ToListAsync();
            countMap = entryCounts.ToDictionary(x => x.TemplateId, x => x.Count);
        }

        return templates.Select(t => new VoucherTemplateListDto
        {
            Id = t.FID,
            Name = t.FName,
            Description = t.FDescription,
            EntryCount = countMap.GetValueOrDefault(t.FID, 0),
            CreateTime = t.FCreateTime
        }).ToList();
    }

    /// <summary>获取模板详情（含分录）</summary>
    public async Task<VoucherTemplateDto?> GetDetailAsync(long id)
    {
        var template = await _templateRepository.Query()
            .Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.FID == id);

        if (template == null) return null;

        return new VoucherTemplateDto
        {
            Id = template.FID,
            Name = template.FName,
            Description = template.FDescription,
            Entries = (template.Entries ?? []).OrderBy(e => e.FSeq).Select(e => new VoucherTemplateEntryDto
            {
                Id = e.FID,
                Summary = e.FSummary,
                AccountId = e.FAccountId,
                AccountCode = e.FAccountCode,
                AccountName = e.FAccountName,
                DebitAmount = e.FDebitAmount,
                CreditAmount = e.FCreditAmount,
                Seq = e.FSeq,
                AuxiliaryJson = e.FAuxiliaryJson
            }).ToList()
        };
    }

    /// <summary>新增模板</summary>
    public async Task<VoucherTemplateDto> CreateAsync(VoucherTemplateCreateRequest request)
    {
        var template = new FinVoucherTemplate
        {
            FAccountSetId = request.AccountSetId,
            FOrgId = GetCurrentOrgId(),
            FName = request.Name,
            FDescription = request.Description,
            FSort = request.Sort,
            FCreateTime = DateTime.Now,
            Entries = request.Entries.Select((e, idx) => new FinVoucherTemplateEntry
            {
                FSummary = e.Summary,
                FAccountId = e.AccountId,
                FAccountCode = e.AccountCode,
                FAccountName = e.AccountName,
                FDebitAmount = e.DebitAmount,
                FCreditAmount = e.CreditAmount,
                FSeq = e.Seq > 0 ? e.Seq : idx + 1,
                FAuxiliaryJson = e.AuxiliaryJson
            }).ToList()
        };

        await _templateRepository.AddAsync(template);

        return new VoucherTemplateDto
        {
            Id = template.FID,
            Name = template.FName,
            Description = template.FDescription,
            Entries = (template.Entries ?? []).Select(e => new VoucherTemplateEntryDto
            {
                Id = e.FID,
                Summary = e.FSummary,
                AccountId = e.FAccountId,
                AccountCode = e.FAccountCode,
                AccountName = e.FAccountName,
                DebitAmount = e.FDebitAmount,
                CreditAmount = e.FCreditAmount,
                Seq = e.FSeq,
                AuxiliaryJson = e.FAuxiliaryJson
            }).ToList()
        };
    }

    /// <summary>更新模板</summary>
    public async Task<VoucherTemplateDto?> UpdateAsync(long id, VoucherTemplateCreateRequest request)
    {
        _logger.LogInformation("更新凭证模板 {TemplateId}", id);

        var template = await _templateRepository.Query()
            .Include(t => t.Entries)
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);

        if (template == null) return null;

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            template.FName = request.Name;
            template.FDescription = request.Description;
            template.FSort = request.Sort;

            // 删除旧分录，重新插入
            var oldEntries = (template.Entries ?? []).ToList();
            foreach (var entry in oldEntries)
            {
                await _entryRepository.DeleteAsync(entry.FID);
            }

            template.Entries = request.Entries.Select((e, idx) => new FinVoucherTemplateEntry
            {
                FTemplateId = id,
                FSummary = e.Summary,
                FAccountId = e.AccountId,
                FAccountCode = e.AccountCode,
                FAccountName = e.AccountName,
                FDebitAmount = e.DebitAmount,
                FCreditAmount = e.CreditAmount,
                FSeq = e.Seq > 0 ? e.Seq : idx + 1,
                FAuxiliaryJson = e.AuxiliaryJson
            }).ToList();

            await _templateRepository.UpdateAsync(template);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "更新凭证模板 {TemplateId} 失败", id);
            throw;
        }

        _logger.LogInformation("凭证模板 {TemplateId} 更新成功，分录数: {Count}", id, request.Entries.Count);

        return new VoucherTemplateDto
        {
            Id = template.FID,
            Name = template.FName,
            Description = template.FDescription,
            Entries = (template.Entries ?? []).Select(e => new VoucherTemplateEntryDto
            {
                Id = e.FID,
                Summary = e.FSummary,
                AccountId = e.FAccountId,
                AccountCode = e.FAccountCode,
                AccountName = e.FAccountName,
                DebitAmount = e.FDebitAmount,
                CreditAmount = e.FCreditAmount,
                Seq = e.FSeq,
                AuxiliaryJson = e.FAuxiliaryJson
            }).ToList()
        };
    }

    /// <summary>删除模板</summary>
    public async Task<bool> DeleteAsync(long id)
    {
        _logger.LogInformation("删除凭证模板 {TemplateId}", id);

        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null) return false;

        await _templateRepository.DeleteAsync(template.FID);
        _logger.LogInformation("凭证模板 {TemplateId} 删除成功", id);
        return true;
    }

    /// <summary>从模板生成凭证</summary>
    public async Task<long> CreateVoucherFromTemplateAsync(long templateId, DateTime date, string creator, long accountSetId)
    {
        var template = await _templateRepository.Query()
            .Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.FID == templateId);

        if (template == null)
            throw new InvalidOperationException($"模板 {templateId} 不存在");

        // 查找对应期间
        var period = await _periodRepository.Query()
            .Where(p => p.FAccountSetId == accountSetId && p.FStartDate <= date && p.FEndDate >= date)
            .FirstOrDefaultAsync();

        long periodId = period?.FID ?? 0;

        // 计算凭证号
        int nextNo = 1;
        var lastVoucher = await _voucherRepository.Query()
            .Where(v => v.FAccountSetId == accountSetId && v.FVoucherWord == VoucherWord.Ji && (periodId == 0 || v.FPeriodId == periodId))
            .OrderByDescending(v => v.FVoucherNo)
            .FirstOrDefaultAsync();
        if (lastVoucher != null) nextNo = lastVoucher.FVoucherNo + 1;

        var voucher = new FinVoucher
        {
            FVoucherWord = VoucherWord.Ji,
            FVoucherNo = nextNo,
            FDate = date,
            FPeriodId = periodId,
            FAttachmentCount = 0,
            FCreator = creator,
            FStatus = 1,
            FSource = $"模板:{template.FName}",
            FAccountSetId = accountSetId,
            FOrgId = GetCurrentOrgId(),
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now,
            Entries = (template.Entries ?? []).OrderBy(e => e.FSeq).Select((e, idx) => new FinVoucherEntry
            {
                FLineNo = idx + 1,
                FSummary = e.FSummary ?? string.Empty,
                FAccountId = e.FAccountId,
                FAccountCode = e.FAccountCode,
                FAccountName = e.FAccountName,
                FDebitAmount = e.FDebitAmount,
                FCreditAmount = e.FCreditAmount,
                FAuxiliaryJson = e.FAuxiliaryJson,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            }).ToList()
        };

        await _voucherRepository.AddAsync(voucher);
        return voucher.FID;
    }
}
