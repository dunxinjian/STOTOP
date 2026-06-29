using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;
using STOTOP.Module.Insurance.Entities;
using STOTOP.Module.Insurance.Services.Interfaces;

namespace STOTOP.Module.Insurance.Services;

public class InsCompanyService : IInsCompanyService
{
    private readonly IRepository<InsCompany> _companyRepository;

    public InsCompanyService(IRepository<InsCompany> companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<PagedResult<InsCompanyListItemDto>> GetListAsync(InsCompanyQueryRequest request)
    {
        var query = _companyRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(c => c.FCompanyName.Contains(keyword) || c.FCompanyCode.Contains(keyword));
        }

        if (request.CompanyType.HasValue)
        {
            query = query.Where(c => c.FCompanyType == request.CompanyType.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<InsCompanyListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<InsCompanyDto?> GetByIdAsync(long id)
    {
        var entity = await _companyRepository.Query()
            .FirstOrDefaultAsync(c => c.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<InsCompanyDto> CreateAsync(CreateInsCompanyRequest request)
    {
        // 验证编码唯一性
        var exists = await _companyRepository.Query()
            .AnyAsync(c => c.FCompanyCode == request.CompanyCode);
        if (exists)
        {
            throw new InvalidOperationException($"保险公司编码 {request.CompanyCode} 已存在");
        }

        var entity = new InsCompany
        {
            FUID = Guid.NewGuid().ToString("N"),
            FCompanyName = request.CompanyName,
            FCompanyCode = request.CompanyCode,
            FCompanyType = request.CompanyType,
            FContactPerson = request.ContactPerson,
            FContactPhone = request.ContactPhone,
            FAddress = request.Address,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _companyRepository.AddAsync(entity);

        return (await GetByIdAsync(entity.FID))!;
    }

    public async Task<InsCompanyDto?> UpdateAsync(long id, UpdateInsCompanyRequest request)
    {
        var entity = await _companyRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return null;

        // 验证编码唯一性（排除自身）
        var codeExists = await _companyRepository.Query()
            .AnyAsync(c => c.FCompanyCode == request.CompanyCode && c.FID != id);
        if (codeExists)
        {
            throw new InvalidOperationException($"保险公司编码 {request.CompanyCode} 已存在");
        }

        entity.FCompanyName = request.CompanyName;
        entity.FCompanyCode = request.CompanyCode;
        entity.FCompanyType = request.CompanyType;
        entity.FContactPerson = request.ContactPerson;
        entity.FContactPhone = request.ContactPhone;
        entity.FAddress = request.Address;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _companyRepository.UpdateAsync(entity);

        return await GetByIdAsync(id);
    }

    public async Task<bool> ToggleStatusAsync(long id)
    {
        var entity = await _companyRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return false;

        entity.FStatus = entity.FStatus == 1 ? 0 : 1;
        entity.FUpdatedTime = DateTime.Now;

        await _companyRepository.UpdateAsync(entity);

        return true;
    }

    #region Mapping

    private static InsCompanyDto MapToDto(InsCompany entity)
    {
        return new InsCompanyDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            CompanyName = entity.FCompanyName,
            CompanyCode = entity.FCompanyCode,
            CompanyType = entity.FCompanyType,
            ContactPerson = entity.FContactPerson,
            ContactPhone = entity.FContactPhone,
            Address = entity.FAddress,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static InsCompanyListItemDto MapToListItemDto(InsCompany entity)
    {
        return new InsCompanyListItemDto
        {
            Id = entity.FID,
            CompanyName = entity.FCompanyName,
            CompanyCode = entity.FCompanyCode,
            CompanyType = entity.FCompanyType,
            ContactPerson = entity.FContactPerson,
            ContactPhone = entity.FContactPhone,
            Status = entity.FStatus
        };
    }

    #endregion
}
