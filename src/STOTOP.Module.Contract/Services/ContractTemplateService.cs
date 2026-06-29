using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Entities;
using STOTOP.Module.Contract.Services.Interfaces;

namespace STOTOP.Module.Contract.Services;

public class ContractTemplateService : IContractTemplateService
{
    private readonly IRepository<ConContractTemplate> _repository;

    public ContractTemplateService(IRepository<ConContractTemplate> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ContractTemplateListItemDto>> GetTemplatesAsync(ContractTemplateQueryRequest request)
    {
        var query = _repository.Query().Include(t => t.Type).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(t => t.FTemplateName.Contains(keyword));
        }

        if (request.TypeId.HasValue)
        {
            query = query.Where(t => t.FTypeId == request.TypeId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ContractTemplateListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ContractTemplateDto?> GetTemplateByIdAsync(long id)
    {
        var entity = await _repository.Query()
            .Include(t => t.Type)
            .FirstOrDefaultAsync(t => t.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ContractTemplateDto> CreateTemplateAsync(CreateContractTemplateRequest request)
    {
        var entity = new ConContractTemplate
        {
            FTypeId = request.TypeId,
            FTemplateName = request.TemplateName,
            FTemplateContent = request.TemplateContent,
            FVersion = 1,
            FStatus = 0, // 草稿
            FCreatedTime = DateTime.Now
        };

        await _repository.AddAsync(entity);

        // 重新查询以获取导航属性
        return (await GetTemplateByIdAsync(entity.FID))!;
    }

    public async Task<ContractTemplateDto?> UpdateTemplateAsync(long id, UpdateContractTemplateRequest request)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);

        if (entity == null) return null;

        entity.FTemplateName = request.TemplateName;
        entity.FTemplateContent = request.TemplateContent;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);
        return await GetTemplateByIdAsync(id);
    }

    public async Task<bool> DeleteTemplateAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return false;

        await _repository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> PublishTemplateAsync(long id)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);

        if (entity == null) return false;

        // 停用同类型的其他已发布模板
        var publishedTemplates = await _repository.Query()
            .AsTracking()
            .Where(t => t.FTypeId == entity.FTypeId && t.FStatus == 1 && t.FID != id)
            .ToListAsync();

        foreach (var t in publishedTemplates)
        {
            t.FStatus = 2; // 已停用
            t.FUpdatedTime = DateTime.Now;
            await _repository.UpdateAsync(t);
        }

        entity.FStatus = 1; // 已发布
        entity.FVersion += 1;
        entity.FUpdatedTime = DateTime.Now;
        await _repository.UpdateAsync(entity);
        return true;
    }

    private static ContractTemplateDto MapToDto(ConContractTemplate entity)
    {
        return new ContractTemplateDto
        {
            Id = entity.FID,
            TypeId = entity.FTypeId,
            TypeName = entity.Type?.FName,
            TemplateName = entity.FTemplateName,
            TemplateContent = entity.FTemplateContent,
            Version = entity.FVersion,
            Status = entity.FStatus,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static ContractTemplateListItemDto MapToListItemDto(ConContractTemplate entity)
    {
        return new ContractTemplateListItemDto
        {
            Id = entity.FID,
            TypeId = entity.FTypeId,
            TypeName = entity.Type?.FName,
            TemplateName = entity.FTemplateName,
            Version = entity.FVersion,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }
}
