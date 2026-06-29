using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class AgentService : IAgentService
{
    private readonly IRepository<ExpAgent> _repository;
    private readonly IRepository<ExpQuotation> _quotationRepository;

    public AgentService(IRepository<ExpAgent> repository, IRepository<ExpQuotation> quotationRepository)
    {
        _repository = repository;
        _quotationRepository = quotationRepository;
    }

    public async Task<PagedResult<AgentDto>> GetListAsync(AgentQueryRequest request)
    {
        var query = _repository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e =>
                e.FCode.Contains(keyword) ||
                e.FName.Contains(keyword) ||
                (e.FContactName != null && e.FContactName.Contains(keyword)));
        }
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.AgentLevel.HasValue)
            query = query.Where(e => e.FAgentLevel == request.AgentLevel.Value);

        var total = await query.CountAsync();

        var joinQuery = from agent in query
                        join q in _quotationRepository.Query().Where(q => q.FClientType == "DL")
                            on agent.FCode equals q.FBusinessObjectId into gc
                        from q in gc.DefaultIfEmpty()
                        orderby agent.FCreatedTime descending
                        select new { Agent = agent, ServiceObjectId = q != null ? (long?)q.FID : null };

        var items = await joinQuery
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<AgentDto>
        {
            Items = items.Select(x => { var dto = MapToDto(x.Agent); dto.ServiceObjectId = x.ServiceObjectId; return dto; }).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<AgentDto?> GetByIdAsync(string code)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<AgentDto> CreateAsync(CreateAgentRequest request)
    {
        // 检查编码唯一性
        var exists = await _repository.Query().AnyAsync(e => e.FCode == request.Code);
        if (exists)
            throw new InvalidOperationException($"业务代理编码 '{request.Code}' 已存在");

        var entity = new ExpAgent
        {
            FCode = request.Code,
            FName = request.Name,
            FAgentLevel = request.AgentLevel,
            FAgentRegion = request.AgentRegion,
            FContactName = request.ContactName,
            FContactPhone = request.ContactPhone,
            FAddress = request.Address,
            FCooperationStartDate = request.CooperationStartDate,
            FStatus = request.Status,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<AgentDto?> UpdateAsync(string code, UpdateAgentRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        if (entity == null) return null;

        if (request.Code != code)
        {
            var codeExists = await _repository.Query().AnyAsync(e => e.FCode == request.Code);
            if (codeExists)
                throw new InvalidOperationException($"业务代理编码 '{request.Code}' 已存在");
        }

        entity.FName = request.Name;
        entity.FAgentLevel = request.AgentLevel;
        entity.FAgentRegion = request.AgentRegion;
        entity.FContactName = request.ContactName;
        entity.FContactPhone = request.ContactPhone;
        entity.FAddress = request.Address;
        entity.FCooperationStartDate = request.CooperationStartDate;
        entity.FStatus = request.Status;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var count = await _repository.Query()
            .Where(e => e.FCode == code)
            .ExecuteDeleteAsync();
        return count > 0;
    }

    private static AgentDto MapToDto(ExpAgent e) => new()
    {
        Id = e.FCode,
        Code = e.FCode,
        Name = e.FName,
        AgentLevel = e.FAgentLevel,
        AgentRegion = e.FAgentRegion,
        ContactName = e.FContactName,
        ContactPhone = e.FContactPhone,
        Address = e.FAddress,
        CooperationStartDate = e.FCooperationStartDate,
        Status = e.FStatus,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };
}
