using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.HR.Dtos;
using STOTOP.Module.HR.Entities;
using STOTOP.Module.System.Entities;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.HR.Services;

public class EmployeeService : IEmployeeService
{
    private readonly STOTOPDbContext _db;
    private readonly IEventDispatcher _eventDispatcher;

    public EmployeeService(STOTOPDbContext db, IEventDispatcher eventDispatcher)
    {
        _db = db;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<ApiResult<PagedResult<EmployeeDto>>> GetPagedListAsync(EmployeePagedRequest request)
    {
        var query = _db.Set<HrEmployee>()
            .Include(e => e.PaymentAccounts)
            .AsQueryable();

        // 关键字搜索（姓名/手机号/身份证号）
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e =>
                e.FName.Contains(keyword) ||
                (e.FPhone != null && e.FPhone.Contains(keyword)) ||
                (e.FIdCardNumber != null && e.FIdCardNumber.Contains(keyword)));
        }

        // 员工状态筛选
        if (request.EmployeeStatus.HasValue)
        {
            query = query.Where(e => e.FEmployeeStatus == request.EmployeeStatus.Value);
        }

        var total = await query.CountAsync();

        var employees = await query
            .OrderByDescending(e => e.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // Join SysUser 获取用户名和账号
        var userIds = employees.Select(e => e.FUserId).Distinct().ToList();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName, u.FAccount })
            .ToListAsync();
        var userDict = users.ToDictionary(u => u.FID);

        var items = employees.Select(e =>
        {
            var dto = MapToDto(e);
            if (userDict.TryGetValue(e.FUserId, out var user))
            {
                dto.UserName = user.FName;
                dto.UserAccount = user.FAccount;
            }
            return dto;
        }).ToList();

        var result = new PagedResult<EmployeeDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };

        return ApiResult<PagedResult<EmployeeDto>>.Success(result);
    }

    public async Task<ApiResult<EmployeeDto>> GetByIdAsync(long id)
    {
        var employee = await _db.Set<HrEmployee>()
            .Include(e => e.PaymentAccounts)
            .FirstOrDefaultAsync(e => e.FID == id);

        if (employee == null)
            return ApiResult<EmployeeDto>.Fail("员工不存在");

        var dto = MapToDto(employee);

        // 获取关联用户信息
        var user = await _db.Set<SysUser>()
            .Where(u => u.FID == employee.FUserId)
            .Select(u => new { u.FName, u.FAccount })
            .FirstOrDefaultAsync();

        if (user != null)
        {
            dto.UserName = user.FName;
            dto.UserAccount = user.FAccount;
        }

        return ApiResult<EmployeeDto>.Success(dto);
    }

    public async Task<ApiResult<EmployeeDto>> CreateAsync(CreateEmployeeRequest request)
    {
        // 验证 UserId 对应的 SysUser 存在
        var userExists = await _db.Set<SysUser>().AnyAsync(u => u.FID == request.UserId);
        if (!userExists)
            return ApiResult<EmployeeDto>.Fail("关联的用户不存在");

        // 验证该用户尚未关联其他员工记录
        var alreadyLinked = await _db.Set<HrEmployee>().AnyAsync(e => e.FUserId == request.UserId);
        if (alreadyLinked)
            return ApiResult<EmployeeDto>.Fail("该用户已关联其他员工记录");

        var employee = new HrEmployee
        {
            FUserId = request.UserId,
            FName = request.Name,
            FGender = request.Gender,
            FBirthDate = request.BirthDate,
            FIdCardNumber = request.IdCardNumber,
            FPhone = request.Phone,
            FEthnicity = request.Ethnicity,
            FEducation = request.Education,
            FMaritalStatus = request.MaritalStatus,
            FHomeAddress = request.HomeAddress,
            FHouseholdAddress = request.HouseholdAddress,
            FEmergencyContact = request.EmergencyContact,
            FEmergencyContactPhone = request.EmergencyContactPhone,
            FEmergencyContactRelation = request.EmergencyContactRelation,
            FEntryDate = request.EntryDate,
            FRegularDate = request.RegularDate,
            FEmployeeStatus = request.EmployeeStatus,
            FRemark = request.Remark,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        // 处理收款账号
        if (request.PaymentAccounts != null && request.PaymentAccounts.Count > 0)
        {
            EnsureSingleDefault(request.PaymentAccounts);
            foreach (var acc in request.PaymentAccounts)
            {
                employee.PaymentAccounts.Add(new HrEmployeePaymentAccount
                {
                    FAccountType = acc.AccountType,
                    FAccountName = acc.AccountName,
                    FAccountNumber = acc.AccountNumber,
                    FBankName = acc.BankName,
                    FBankBranch = acc.BankBranch,
                    FIsDefault = acc.IsDefault,
                    FRemark = acc.Remark,
                    FCreateTime = DateTime.Now,
                    FUpdateTime = DateTime.Now
                });
            }
        }

        _db.Set<HrEmployee>().Add(employee);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(employee.FID);
    }

    public async Task<ApiResult<EmployeeDto>> UpdateAsync(long id, UpdateEmployeeRequest request)
    {
        var employee = await _db.Set<HrEmployee>()
            .AsTracking()
            .Include(e => e.PaymentAccounts)
            .FirstOrDefaultAsync(e => e.FID == id);

        if (employee == null)
            return ApiResult<EmployeeDto>.Fail("员工不存在");

        var oldName = employee.FName;

        // 更新基本信息
        employee.FName = request.Name;
        employee.FGender = request.Gender;
        employee.FBirthDate = request.BirthDate;
        employee.FIdCardNumber = request.IdCardNumber;
        employee.FPhone = request.Phone;
        employee.FEthnicity = request.Ethnicity;
        employee.FEducation = request.Education;
        employee.FMaritalStatus = request.MaritalStatus;
        employee.FHomeAddress = request.HomeAddress;
        employee.FHouseholdAddress = request.HouseholdAddress;
        employee.FEmergencyContact = request.EmergencyContact;
        employee.FEmergencyContactPhone = request.EmergencyContactPhone;
        employee.FEmergencyContactRelation = request.EmergencyContactRelation;
        employee.FEntryDate = request.EntryDate;
        employee.FRegularDate = request.RegularDate;
        employee.FLeaveDate = request.LeaveDate;
        employee.FEmployeeStatus = request.EmployeeStatus;
        employee.FRemark = request.Remark;
        employee.FUpdateTime = DateTime.Now;

        // 同步收款账号子表
        var incomingAccounts = request.PaymentAccounts ?? new List<SavePaymentAccountRequest>();
        EnsureSingleDefault(incomingAccounts);

        var existingAccounts = employee.PaymentAccounts.ToList();
        var incomingIds = incomingAccounts.Where(a => a.Id.HasValue && a.Id.Value > 0).Select(a => a.Id!.Value).ToHashSet();

        // 删除不在传入列表中的账号
        var toDelete = existingAccounts.Where(a => !incomingIds.Contains(a.FID)).ToList();
        foreach (var del in toDelete)
        {
            _db.Set<HrEmployeePaymentAccount>().Remove(del);
        }

        // 新增或更新
        foreach (var acc in incomingAccounts)
        {
            if (acc.Id.HasValue && acc.Id.Value > 0)
            {
                // 更新现有账号
                var existing = existingAccounts.FirstOrDefault(a => a.FID == acc.Id.Value);
                if (existing != null)
                {
                    existing.FAccountType = acc.AccountType;
                    existing.FAccountName = acc.AccountName;
                    existing.FAccountNumber = acc.AccountNumber;
                    existing.FBankName = acc.BankName;
                    existing.FBankBranch = acc.BankBranch;
                    existing.FIsDefault = acc.IsDefault;
                    existing.FRemark = acc.Remark;
                    existing.FUpdateTime = DateTime.Now;
                }
            }
            else
            {
                // 新增账号
                employee.PaymentAccounts.Add(new HrEmployeePaymentAccount
                {
                    FEmployeeId = employee.FID,
                    FAccountType = acc.AccountType,
                    FAccountName = acc.AccountName,
                    FAccountNumber = acc.AccountNumber,
                    FBankName = acc.BankName,
                    FBankBranch = acc.BankBranch,
                    FIsDefault = acc.IsDefault,
                    FRemark = acc.Remark,
                    FCreateTime = DateTime.Now,
                    FUpdateTime = DateTime.Now
                });
            }
        }

        await _db.SaveChangesAsync();

        // 员工姓名变更时发布辅助核算同步事件（使用关联的UserId作为SourceId）
        if (oldName != request.Name)
        {
            await _eventDispatcher.PublishAsync(new AuxiliarySourceChangedEvent
            {
                SourceType = "SYS用户",
                SourceId = employee.FUserId,
                NewName = request.Name
            });
        }

        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var employee = await _db.Set<HrEmployee>()
            .Include(e => e.PaymentAccounts)
            .FirstOrDefaultAsync(e => e.FID == id);

        if (employee == null)
            return ApiResult<bool>.Fail("员工不存在");

        _db.Set<HrEmployeePaymentAccount>().RemoveRange(employee.PaymentAccounts);
        _db.Set<HrEmployee>().Remove(employee);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    public async Task<ApiResult<EmployeeDto?>> GetByUserIdAsync(long userId)
    {
        var employee = await _db.Set<HrEmployee>()
            .Include(e => e.PaymentAccounts)
            .FirstOrDefaultAsync(e => e.FUserId == userId);

        if (employee == null)
            return ApiResult<EmployeeDto?>.Success(null);

        var dto = MapToDto(employee);

        var user = await _db.Set<SysUser>()
            .Where(u => u.FID == userId)
            .Select(u => new { u.FName, u.FAccount })
            .FirstOrDefaultAsync();

        if (user != null)
        {
            dto.UserName = user.FName;
            dto.UserAccount = user.FAccount;
        }

        return ApiResult<EmployeeDto?>.Success(dto);
    }

    public async Task<ApiResult<List<UserSimpleDto>>> SearchAvailableUsersAsync(string? keyword)
    {
        // 查询已关联员工的用户ID
        var linkedUserIds = await _db.Set<HrEmployee>()
            .Select(e => e.FUserId)
            .ToListAsync();

        var query = _db.Set<SysUser>()
            .Where(u => !linkedUserIds.Contains(u.FID) && u.FStatus == 1);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(u => u.FName.Contains(kw) || u.FAccount.Contains(kw));
        }

        var users = await query
            .OrderBy(u => u.FName)
            .Take(50)
            .Select(u => new UserSimpleDto
            {
                Id = u.FID,
                Name = u.FName,
                Account = u.FAccount
            })
            .ToListAsync();

        return ApiResult<List<UserSimpleDto>>.Success(users);
    }

    private static EmployeeDto MapToDto(HrEmployee e) => new()
    {
        Id = e.FID,
        FUID = e.FUID,
        UserId = e.FUserId,
        Name = e.FName,
        Gender = e.FGender,
        BirthDate = e.FBirthDate,
        IdCardNumber = e.FIdCardNumber,
        Phone = e.FPhone,
        Ethnicity = e.FEthnicity,
        Education = e.FEducation,
        MaritalStatus = e.FMaritalStatus,
        HomeAddress = e.FHomeAddress,
        HouseholdAddress = e.FHouseholdAddress,
        EmergencyContact = e.FEmergencyContact,
        EmergencyContactPhone = e.FEmergencyContactPhone,
        EmergencyContactRelation = e.FEmergencyContactRelation,
        EntryDate = e.FEntryDate,
        RegularDate = e.FRegularDate,
        LeaveDate = e.FLeaveDate,
        EmployeeStatus = e.FEmployeeStatus,
        Remark = e.FRemark,
        CreateTime = e.FCreateTime,
        UpdateTime = e.FUpdateTime,
        PaymentAccounts = e.PaymentAccounts.Select(a => new PaymentAccountDto
        {
            Id = a.FID,
            EmployeeId = a.FEmployeeId,
            AccountType = a.FAccountType,
            AccountName = a.FAccountName,
            AccountNumber = a.FAccountNumber,
            BankName = a.FBankName,
            BankBranch = a.FBankBranch,
            IsDefault = a.FIsDefault,
            Remark = a.FRemark
        }).ToList()
    };

    /// <summary>
    /// 确保最多一个默认账号
    /// </summary>
    private static void EnsureSingleDefault(List<SavePaymentAccountRequest> accounts)
    {
        var defaultAccounts = accounts.Where(a => a.IsDefault == 1).ToList();
        if (defaultAccounts.Count > 1)
        {
            // 只保留最后一个为默认
            for (int i = 0; i < defaultAccounts.Count - 1; i++)
            {
                defaultAccounts[i].IsDefault = 0;
            }
        }
    }
}
