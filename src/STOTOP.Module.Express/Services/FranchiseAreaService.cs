using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class FranchiseAreaService : IFranchiseAreaService
{
    private readonly IRepository<ExpFranchiseArea> _repository;

    public FranchiseAreaService(IRepository<ExpFranchiseArea> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<FranchiseAreaDto>> GetListAsync(FranchiseAreaQueryRequest request)
    {
        var query = _repository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e =>
                e.FCode.Contains(keyword) ||
                (e.FContractor != null && e.FContractor.Contains(keyword)) ||
                (e.FCoverageDistrict != null && e.FCoverageDistrict.Contains(keyword)));
        }
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.OrgId.HasValue)
            query = query.Where(e => e.FOrgId == request.OrgId.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<FranchiseAreaDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<FranchiseAreaDto?> GetByIdAsync(string code)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<bool> CheckCodeExistsAsync(string code)
    {
        return await _repository.Query().AnyAsync(e => e.FCode == code);
    }

    public async Task<FranchiseAreaDto> CreateAsync(CreateFranchiseAreaRequest request)
    {
        // 编号必填校验
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("承包区编号不能为空");

        // 编号唯一性校验
        var exists = await _repository.Query().AnyAsync(e => e.FCode == request.Code);
        if (exists)
            throw new InvalidOperationException($"承包区编号 '{request.Code}' 已存在");

        var entity = new ExpFranchiseArea
        {
            FCode = request.Code,
            FOrgId = request.OrgId,
            FContractor = request.Contractor,
            FContractStartDate = request.ContractStartDate,
            FContractEndDate = request.ContractEndDate,
            FCoverageDistrict = request.CoverageDistrict,
            FContractFee = request.ContractFee,
            FContactPhone = request.ContactPhone,
            FAddress = request.Address,
            FStatus = request.Status,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<FranchiseAreaDto?> UpdateAsync(string code, UpdateFranchiseAreaRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        if (entity == null) return null;

        entity.FOrgId = request.OrgId;
        entity.FContractor = request.Contractor;
        entity.FContractStartDate = request.ContractStartDate;
        entity.FContractEndDate = request.ContractEndDate;
        entity.FCoverageDistrict = request.CoverageDistrict;
        entity.FContractFee = request.ContractFee;
        entity.FContactPhone = request.ContactPhone;
        entity.FAddress = request.Address;
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

    private static FranchiseAreaDto MapToDto(ExpFranchiseArea e) => new()
    {
        Id = e.FCode,
        Code = e.FCode,
        OrgId = e.FOrgId,
        Contractor = e.FContractor,
        ContractStartDate = e.FContractStartDate,
        ContractEndDate = e.FContractEndDate,
        CoverageDistrict = e.FCoverageDistrict,
        ContractFee = e.FContractFee,
        ContactPhone = e.FContactPhone,
        Address = e.FAddress,
        Status = e.FStatus,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };
}
