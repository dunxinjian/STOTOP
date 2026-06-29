using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.HR.Entities;

namespace STOTOP.Module.Express.Services;

public class SalesmanService : ISalesmanService
{
    private readonly IRepository<ExpSalesman> _repository;
    private readonly IRepository<HrEmployee> _hrEmployeeRepository;

    public SalesmanService(
        IRepository<ExpSalesman> repository,
        IRepository<HrEmployee> hrEmployeeRepository)
    {
        _repository = repository;
        _hrEmployeeRepository = hrEmployeeRepository;
    }

    public async Task<PagedResult<SalesmanDto>> GetListAsync(SalesmanQueryRequest request)
    {
        var query = _repository.Query();

        if (!string.IsNullOrWhiteSpace(request.NetworkPointCode))
            query = query.Where(e => e.FNetworkPointCode == request.NetworkPointCode);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e =>
                e.FEmployeeNo.Contains(keyword) ||
                e.FName.Contains(keyword) ||
                (e.FPhone != null && e.FPhone.Contains(keyword)));
        }

        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<SalesmanDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<SalesmanDto?> GetByNoAsync(string employeeNo)
    {
        var entity = await _repository.Query()
            .FirstOrDefaultAsync(e => e.FEmployeeNo == employeeNo);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<SalesmanDto> CreateAsync(CreateSalesmanRequest request)
    {
        // 从 HR 员工表查找信息
        var hrEmployee = await _hrEmployeeRepository.GetByIdAsync(request.EmployeeId);
        if (hrEmployee == null)
            throw new InvalidOperationException($"HR员工ID '{request.EmployeeId}' 不存在");

        // 生成工号：使用 HR 员工的 FUID 作为工号
        var employeeNo = hrEmployee.FUID;

        // 检查是否已存在
        var exists = await _repository.Query()
            .AnyAsync(e => e.FEmployeeNo == employeeNo);
        if (exists)
            throw new InvalidOperationException($"业务员工号 '{employeeNo}' 已存在");

        var entity = new ExpSalesman
        {
            FEmployeeNo = employeeNo,
            FNetworkPointCode = request.NetworkPointCode,
            FEmployeeId = request.EmployeeId,
            FName = hrEmployee.FName,
            FPhone = hrEmployee.FPhone,
            FDepartment = null, // TODO: 待 HR 模块扩展部门字段后完善
            FHireDate = hrEmployee.FEntryDate.HasValue ? DateOnly.FromDateTime(hrEmployee.FEntryDate.Value) : null,
            FStatus = 1,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<SalesmanDto?> UpdateAsync(string employeeNo, UpdateSalesmanRequest request)
    {
        var entity = await _repository.Query()
            .FirstOrDefaultAsync(e => e.FEmployeeNo == employeeNo);
        if (entity == null) return null;

        if (request.Phone != null)
            entity.FPhone = request.Phone;
        if (request.Remark != null)
            entity.FRemark = request.Remark;
        if (request.Status.HasValue)
            entity.FStatus = request.Status.Value;

        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string employeeNo)
    {
        var count = await _repository.Query()
            .Where(e => e.FEmployeeNo == employeeNo)
            .ExecuteDeleteAsync();
        return count > 0;
    }

    /// <summary>
    /// 获取可选的HR员工候选人列表
    /// TODO: 当前简化实现，返回所有在职HR员工。
    /// 待HR模块扩展岗位/网点字段后，可按网点和岗位="业务员"筛选。
    /// </summary>
    public async Task<List<HrEmployeeCandidateDto>> GetCandidatesAsync(string networkPointCode)
    {
        // 查询在职的HR员工（排除已在业务员表中的）
        var existingEmployeeIds = await _repository.Query()
            .Select(s => s.FEmployeeId)
            .ToListAsync();

        var candidates = await _hrEmployeeRepository.Query()
            .Where(e => e.FEmployeeStatus == 1) // 在职
            .Where(e => !existingEmployeeIds.Contains(e.FID))
            .OrderBy(e => e.FName)
            .Take(100)
            .ToListAsync();

        return candidates.Select(e => new HrEmployeeCandidateDto
        {
            EmployeeId = e.FID,
            EmployeeNo = e.FUID,
            Name = e.FName,
            Department = null, // TODO: 待HR模块扩展部门字段后完善
            Phone = e.FPhone
        }).ToList();
    }

    private static SalesmanDto MapToDto(ExpSalesman e) => new()
    {
        EmployeeNo = e.FEmployeeNo,
        NetworkPointCode = e.FNetworkPointCode,
        EmployeeId = e.FEmployeeId,
        Name = e.FName,
        Phone = e.FPhone,
        Department = e.FDepartment,
        HireDate = e.FHireDate,
        Status = e.FStatus,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };
}
