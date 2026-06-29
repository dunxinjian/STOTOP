using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Entities;
using STOTOP.Module.Contract.Services.Interfaces;

namespace STOTOP.Module.Contract.Services;

public class ContractService : IContractService
{
    private readonly IRepository<ConContract> _contractRepository;
    private readonly IRepository<ConContractParty> _partyRepository;
    private readonly IRepository<ConContractClause> _clauseRepository;
    private readonly IRepository<ConContractTemplate> _templateRepository;

    public ContractService(
        IRepository<ConContract> contractRepository,
        IRepository<ConContractParty> partyRepository,
        IRepository<ConContractClause> clauseRepository,
        IRepository<ConContractTemplate> templateRepository)
    {
        _contractRepository = contractRepository;
        _partyRepository = partyRepository;
        _clauseRepository = clauseRepository;
        _templateRepository = templateRepository;
    }

    public async Task<PagedResult<ContractListItemDto>> GetContractsAsync(ContractQueryRequest request)
    {
        var query = _contractRepository.Query().Include(c => c.Type).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(c => c.FContractNo.Contains(keyword) || c.FTitle.Contains(keyword));
        }

        if (request.TypeId.HasValue)
        {
            query = query.Where(c => c.FTypeId == request.TypeId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.FStatus == request.Status.Value);
        }

        if (request.ContractNature.HasValue)
        {
            query = query.Where(c => c.FContractNature == request.ContractNature.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ContractListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ContractStatisticsDto> GetStatisticsAsync()
    {
        var grouped = await _contractRepository.Query()
            .GroupBy(c => c.FStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var statusNames = new[] { "草稿", "审批中", "待签署", "已生效", "已到期", "已终止" };
        var byStatus = new List<ContractStatusGroupDto>();
        for (var s = 0; s < statusNames.Length; s++)
        {
            byStatus.Add(new ContractStatusGroupDto
            {
                Status = s,
                StatusName = statusNames[s],
                Count = grouped.FirstOrDefault(x => x.Status == s)?.Count ?? 0
            });
        }

        return new ContractStatisticsDto
        {
            TotalCount = byStatus.Sum(b => b.Count),
            ByStatus = byStatus
        };
    }

    public async Task<ContractDto?> GetContractByIdAsync(long id)
    {
        var entity = await _contractRepository.Query()
            .Include(c => c.Type)
            .Include(c => c.Template)
            .Include(c => c.Parties)
            .Include(c => c.Clauses)
            .Include(c => c.Reminders)
            .Include(c => c.ESignRecords)
            .Include(c => c.RelatedContract)
            .FirstOrDefaultAsync(c => c.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ContractDto> CreateContractAsync(CreateContractRequest request)
    {
        // 验证合同号唯一性
        var exists = await _contractRepository.Query()
            .AnyAsync(c => c.FContractNo == request.ContractNo);
        if (exists)
        {
            throw new InvalidOperationException($"合同号 {request.ContractNo} 已存在");
        }

        var contract = new ConContract
        {
            FContractNo = request.ContractNo,
            FTitle = request.Title,
            FTypeId = request.TypeId,
            FTemplateId = request.TemplateId,
            FAmount = request.Amount,
            FStartDate = request.StartDate,
            FEndDate = request.EndDate,
            FRelatedContractId = request.RelatedContractId,
            FContractNature = request.ContractNature,
            FStatus = 0, // 草稿
            FCreatedTime = DateTime.Now
        };

        await _contractRepository.AddAsync(contract);

        // 创建合同方
        if (request.Parties != null && request.Parties.Count > 0)
        {
            foreach (var partyReq in request.Parties)
            {
                var party = new ConContractParty
                {
                    FContractId = contract.FID,
                    FPartyRole = partyReq.PartyRole,
                    FRelatedBusinessType = partyReq.RelatedBusinessType,
                    FRelatedBusinessId = partyReq.RelatedBusinessId,
                    FPartyName = partyReq.PartyName,
                    FContact = partyReq.Contact,
                    FPhone = partyReq.Phone,
                    FAddress = partyReq.Address,
                    FCreatedTime = DateTime.Now
                };
                await _partyRepository.AddAsync(party);
            }
        }

        // 创建合同条款（从模板或手动）
        if (request.Clauses != null && request.Clauses.Count > 0)
        {
            foreach (var clauseReq in request.Clauses)
            {
                var clause = new ConContractClause
                {
                    FContractId = contract.FID,
                    FClauseOrder = clauseReq.ClauseOrder,
                    FClauseTitle = clauseReq.ClauseTitle,
                    FClauseContent = clauseReq.ClauseContent,
                    FIsKeyClause = clauseReq.IsKeyClause,
                    FCreatedTime = DateTime.Now
                };
                await _clauseRepository.AddAsync(clause);
            }
        }
        else if (request.TemplateId.HasValue)
        {
            // 从模板自动填充条款（模板内容作为单条款）
            var template = await _templateRepository.GetByIdAsync(request.TemplateId.Value);
            if (template != null && !string.IsNullOrEmpty(template.FTemplateContent))
            {
                var clause = new ConContractClause
                {
                    FContractId = contract.FID,
                    FClauseOrder = 1,
                    FClauseTitle = "模板条款",
                    FClauseContent = template.FTemplateContent,
                    FIsKeyClause = false,
                    FCreatedTime = DateTime.Now
                };
                await _clauseRepository.AddAsync(clause);
            }
        }

        return (await GetContractByIdAsync(contract.FID))!;
    }

    public async Task<ContractDto?> UpdateContractAsync(long id, UpdateContractRequest request)
    {
        var contract = await _contractRepository.Query()
            .AsTracking()
            .Include(c => c.Parties)
            .Include(c => c.Clauses)
            .FirstOrDefaultAsync(c => c.FID == id);

        if (contract == null) return null;

        contract.FTitle = request.Title;
        contract.FTypeId = request.TypeId;
        contract.FAmount = request.Amount;
        contract.FStartDate = request.StartDate;
        contract.FEndDate = request.EndDate;
        contract.FUpdatedTime = DateTime.Now;

        await _contractRepository.UpdateAsync(contract);

        // 更新合同方：删除旧的，创建新的
        if (request.Parties != null)
        {
            foreach (var existing in contract.Parties)
            {
                await _partyRepository.DeleteAsync(existing.FID);
            }

            foreach (var partyReq in request.Parties)
            {
                var party = new ConContractParty
                {
                    FContractId = id,
                    FPartyRole = partyReq.PartyRole,
                    FRelatedBusinessType = partyReq.RelatedBusinessType,
                    FRelatedBusinessId = partyReq.RelatedBusinessId,
                    FPartyName = partyReq.PartyName,
                    FContact = partyReq.Contact,
                    FPhone = partyReq.Phone,
                    FAddress = partyReq.Address,
                    FCreatedTime = DateTime.Now
                };
                await _partyRepository.AddAsync(party);
            }
        }

        // 更新合同条款：删除旧的，创建新的
        if (request.Clauses != null)
        {
            foreach (var existing in contract.Clauses)
            {
                await _clauseRepository.DeleteAsync(existing.FID);
            }

            foreach (var clauseReq in request.Clauses)
            {
                var clause = new ConContractClause
                {
                    FContractId = id,
                    FClauseOrder = clauseReq.ClauseOrder,
                    FClauseTitle = clauseReq.ClauseTitle,
                    FClauseContent = clauseReq.ClauseContent,
                    FIsKeyClause = clauseReq.IsKeyClause,
                    FCreatedTime = DateTime.Now
                };
                await _clauseRepository.AddAsync(clause);
            }
        }

        return await GetContractByIdAsync(id);
    }

    public async Task<bool> DeleteContractAsync(long id)
    {
        var entity = await _contractRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await _contractRepository.DeleteAsync(id);
        return true;
    }

    // 合法的状态流转规则：0=草稿, 1=审批中, 2=待签署, 3=已生效, 4=已到期, 5=已终止
    private static readonly Dictionary<int, int[]> AllowedStatusTransitions = new()
    {
        { 0, new[] { 1 } },         // 草稿 → 审批中
        { 1, new[] { 0, 2 } },      // 审批中 → 草稿(退回) 或 待签署
        { 2, new[] { 3 } },         // 待签署 → 已生效
        { 3, new[] { 4, 5 } },      // 已生效 → 已到期 或 已终止
        { 4, new[] { 5 } },         // 已到期 → 已终止
        { 5, Array.Empty<int>() },  // 已终止（终态）
    };

    private static readonly Dictionary<int, string> StatusNames = new()
    {
        { 0, "草稿" }, { 1, "审批中" }, { 2, "待签署" },
        { 3, "已生效" }, { 4, "已到期" }, { 5, "已终止" },
    };

    public async Task<bool> UpdateStatusAsync(long id, int status)
    {
        var entity = await _contractRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return false;

        // 验证目标状态值合法
        if (!StatusNames.ContainsKey(status))
        {
            throw new InvalidOperationException($"无效的合同状态值: {status}");
        }

        // 验证状态流转合法性
        var currentStatus = entity.FStatus;
        if (!AllowedStatusTransitions.TryGetValue(currentStatus, out var allowedTargets)
            || !allowedTargets.Contains(status))
        {
            var currentName = StatusNames.GetValueOrDefault(currentStatus, $"未知({currentStatus})");
            var targetName = StatusNames[status];
            throw new InvalidOperationException(
                $"不允许从\"{currentName}\"状态变更为\"{targetName}\"状态");
        }

        entity.FStatus = status;
        entity.FUpdatedTime = DateTime.Now;
        await _contractRepository.UpdateAsync(entity);
        return true;
    }

    public async Task<ContractDto> RenewContractAsync(long originalContractId, CreateContractRequest request)
    {
        // 验证原合同存在性
        var originalContract = await _contractRepository.Query()
            .FirstOrDefaultAsync(c => c.FID == originalContractId);
        if (originalContract == null)
        {
            throw new InvalidOperationException($"原合同(ID={originalContractId})不存在");
        }

        // 验证原合同状态：只有已生效(3)或已到期(4)的合同才能续签
        if (originalContract.FStatus != 3 && originalContract.FStatus != 4)
        {
            throw new InvalidOperationException("只有已生效或已到期的合同才能续签");
        }

        // 设置续签关联和合同性质
        request.RelatedContractId = originalContractId;
        request.ContractNature = 2; // 续签

        return await CreateContractAsync(request);
    }

    #region Mapping

    private static ContractDto MapToDto(ConContract entity)
    {
        return new ContractDto
        {
            Id = entity.FID,
            ContractNo = entity.FContractNo,
            Title = entity.FTitle,
            TypeId = entity.FTypeId,
            TypeName = entity.Type?.FName,
            TemplateId = entity.FTemplateId,
            TemplateName = entity.Template?.FTemplateName,
            Amount = entity.FAmount,
            StartDate = entity.FStartDate,
            EndDate = entity.FEndDate,
            RelatedContractId = entity.FRelatedContractId,
            RelatedContractNo = entity.RelatedContract?.FContractNo,
            ContractNature = entity.FContractNature,
            Status = entity.FStatus,
            OaProcessInstanceId = entity.FOaProcessInstanceId,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime,
            Parties = entity.Parties.Select(MapPartyToDto).ToList(),
            Clauses = entity.Clauses.OrderBy(c => c.FClauseOrder).Select(MapClauseToDto).ToList(),
            Reminders = entity.Reminders.Select(r => new ContractReminderDto
            {
                Id = r.FID,
                ContractId = r.FContractId,
                ReminderType = r.FReminderType,
                ReminderDate = r.FReminderDate,
                RecipientId = r.FRecipientId,
                IsHandled = r.FIsHandled,
                Remark = r.FRemark,
                CreatorName = r.FCreatorName,
                CreatedTime = r.FCreatedTime
            }).ToList(),
            ESignRecords = entity.ESignRecords.Select(e => new ESignRecordDto
            {
                Id = e.FID,
                ContractId = e.FContractId,
                Signer = e.FSigner,
                SignerRole = e.FSignerRole,
                SignMethod = e.FSignMethod,
                SignStatus = e.FSignStatus,
                SignedTime = e.FSignedTime,
                ThirdPartyNo = e.FThirdPartyNo,
                SignedFilePath = e.FSignedFilePath,
                CreatorName = e.FCreatorName,
                CreatedTime = e.FCreatedTime
            }).ToList()
        };
    }

    private static ContractListItemDto MapToListItemDto(ConContract entity)
    {
        return new ContractListItemDto
        {
            Id = entity.FID,
            ContractNo = entity.FContractNo,
            Title = entity.FTitle,
            TypeId = entity.FTypeId,
            TypeName = entity.Type?.FName,
            Amount = entity.FAmount,
            StartDate = entity.FStartDate,
            EndDate = entity.FEndDate,
            ContractNature = entity.FContractNature,
            Status = entity.FStatus,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static ContractPartyDto MapPartyToDto(ConContractParty entity)
    {
        return new ContractPartyDto
        {
            Id = entity.FID,
            ContractId = entity.FContractId,
            PartyRole = entity.FPartyRole,
            RelatedBusinessType = entity.FRelatedBusinessType,
            RelatedBusinessId = entity.FRelatedBusinessId,
            PartyName = entity.FPartyName,
            Contact = entity.FContact,
            Phone = entity.FPhone,
            Address = entity.FAddress
        };
    }

    private static ContractClauseDto MapClauseToDto(ConContractClause entity)
    {
        return new ContractClauseDto
        {
            Id = entity.FID,
            ContractId = entity.FContractId,
            ClauseOrder = entity.FClauseOrder,
            ClauseTitle = entity.FClauseTitle,
            ClauseContent = entity.FClauseContent,
            IsKeyClause = entity.FIsKeyClause
        };
    }

    #endregion
}
