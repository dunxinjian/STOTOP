using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Entities;
using STOTOP.Module.Vehicle.Services.Interfaces;
// 消歧义：本文件 IVoucherService 指 Finance 内部接口
using IVoucherService = STOTOP.Module.Finance.Services.Interfaces.IVoucherService;

namespace STOTOP.Module.Vehicle.Services;

public class RentalChargeService : IRentalChargeService
{
    private readonly IRepository<VehRentalCharge> _chargeRepository;
    private readonly IRepository<VehAssignment> _assignmentRepository;
    private readonly IRepository<VehRentalStandard> _standardRepository;
    private readonly IVoucherService _voucherService;
    private readonly ILogger<RentalChargeService> _logger;

    public RentalChargeService(
        IRepository<VehRentalCharge> chargeRepository,
        IRepository<VehAssignment> assignmentRepository,
        IRepository<VehRentalStandard> standardRepository,
        IVoucherService voucherService,
        ILogger<RentalChargeService> logger)
    {
        _chargeRepository = chargeRepository;
        _assignmentRepository = assignmentRepository;
        _standardRepository = standardRepository;
        _voucherService = voucherService;
        _logger = logger;
    }

    public async Task<PagedResult<RentalChargeListItemDto>> GetChargesAsync(RentalChargeQueryRequest request)
    {
        var baseQuery = _chargeRepository.Query();

        if (request.VehicleId.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.FVehicleId == request.VehicleId.Value);
        }

        if (request.EmployeeId.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.FEmployeeId == request.EmployeeId.Value);
        }

        if (request.ChargeStatus.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.FChargeStatus == request.ChargeStatus.Value);
        }

        if (request.StartDate.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.FChargePeriodStart >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.FChargePeriodEnd <= request.EndDate.Value);
        }

        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .Include(c => c.Assignment)
            .ThenInclude(a => a.Vehicle)
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<RentalChargeListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<RentalChargeDto?> GetChargeByIdAsync(long id)
    {
        var charge = await _chargeRepository.Query()
            .Include(c => c.Assignment)
            .ThenInclude(a => a.Vehicle)
            .FirstOrDefaultAsync(c => c.FID == id);

        return charge == null ? null : MapToDto(charge);
    }

    public async Task<int> GenerateChargesAsync(GenerateChargesRequest request)
    {
        // 计算该月份的起止日期
        var periodStart = new DateTime(request.Year, request.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        // 查找所有租赁中（分配状态=使用中 且 分配类型=租赁）的分配记录
        var assignments = await _assignmentRepository.Query()
            .Include(a => a.Vehicle)
            .Where(a => a.FAssignmentStatus == 1 && a.FAssignmentType == 2)
            .ToListAsync();

        var generatedCount = 0;

        foreach (var assignment in assignments)
        {
            // 检查该分配在该月份是否已有收费记录
            var exists = await _chargeRepository.Query()
                .AnyAsync(c => c.FAssignmentId == assignment.FID &&
                               c.FChargePeriodStart.Year == request.Year &&
                               c.FChargePeriodStart.Month == request.Month);

            if (exists) continue;

            // 查找匹配的费用标准（状态=启用且在有效期内）
            var standard = await _standardRepository.Query()
                .Where(s => s.FStatus == 1 &&
                           s.FEffectiveDate <= periodEnd &&
                           (s.FExpiryDate == null || s.FExpiryDate >= periodStart))
                .OrderByDescending(s => s.FEffectiveDate)
                .FirstOrDefaultAsync();

            var charge = new VehRentalCharge
            {
                FUID = Guid.NewGuid().ToString("N"),
                FVehicleId = assignment.FVehicleId,
                FAssignmentId = assignment.FID,
                FEmployeeId = assignment.FEmployeeId,
                FEmployeeName = assignment.FEmployeeName,
                FRentalStandardId = standard?.FID,
                FChargePeriodStart = periodStart,
                FChargePeriodEnd = periodEnd,
                FAmountDue = standard?.FAmount ?? 0,
                FChargeStatus = 1,  // 待收
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            await _chargeRepository.AddAsync(charge);
            generatedCount++;
        }

        return generatedCount;
    }

    public async Task<RentalChargeDto?> ConfirmChargeAsync(long id, ConfirmChargeRequest request)
    {
        var charge = await _chargeRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (charge == null) return null;

        // 更新收费状态
        charge.FChargeStatus = 2;  // 已收
        charge.FAmountPaid = request.AmountPaid;
        charge.FChargeDate = DateTime.Now;
        charge.FRemark = request.Remark;
        charge.FUpdatedTime = DateTime.Now;

        // 如果需要同步到财务系统
        if (request.SyncToFinance)
        {
            try
            {
                // 创建财务凭证
                var voucherRequest = new CreateVoucherRequest
                {
                    VoucherWord = "记",
                    Date = DateTime.Now,
                    PeriodId = 0,  // 由财务模块根据日期确定
                    AttachmentCount = 0,
                    Remark = $"车辆租赁收费 - {charge.FEmployeeName} - {charge.FChargePeriodStart:yyyy-MM}",
                    Entries = new List<CreateVoucherEntryRequest>
                    {
                        new()
                        {
                            LineNo = 1,
                            Summary = $"车辆租赁收费 - {charge.FEmployeeName}",
                            AccountId = 0,  // 需要根据实际会计科目设置
                            DebitAmount = request.AmountPaid,
                            CreditAmount = 0
                        },
                        new()
                        {
                            LineNo = 2,
                            Summary = $"车辆租赁收费 - {charge.FEmployeeName}",
                            AccountId = 0,  // 需要根据实际会计科目设置
                            DebitAmount = 0,
                            CreditAmount = request.AmountPaid
                        }
                    }
                };

                var voucher = await _voucherService.CreateAsync(voucherRequest, "系统", 0);
                charge.FVoucherId = voucher.Id;
            }
            catch (Exception ex)
            {
                // 财务对接失败不影响主流程，仅记录日志
                _logger.LogWarning(ex, "同步到财务系统失败，收费记录ID: {ChargeId}", id);
            }
        }

        await _chargeRepository.UpdateAsync(charge);

        return await GetChargeByIdAsync(id);
    }

    public async Task<RentalChargeDto?> WaiveChargeAsync(long id, WaiveChargeRequest request)
    {
        var charge = await _chargeRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (charge == null) return null;

        charge.FChargeStatus = 4;  // 减免
        charge.FRemark = string.IsNullOrEmpty(request.Remark)
            ? "已减免"
            : $"减免原因: {request.Remark}";
        charge.FUpdatedTime = DateTime.Now;

        await _chargeRepository.UpdateAsync(charge);

        return await GetChargeByIdAsync(id);
    }

    #region Mapping

    private static RentalChargeDto MapToDto(VehRentalCharge entity)
    {
        return new RentalChargeDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            VehicleId = entity.FVehicleId,
            AssignmentId = entity.FAssignmentId,
            EmployeeId = entity.FEmployeeId,
            EmployeeName = entity.FEmployeeName,
            RentalStandardId = entity.FRentalStandardId,
            ChargePeriodStart = entity.FChargePeriodStart,
            ChargePeriodEnd = entity.FChargePeriodEnd,
            AmountDue = entity.FAmountDue,
            AmountPaid = entity.FAmountPaid,
            ChargeStatus = entity.FChargeStatus,
            ChargeDate = entity.FChargeDate,
            VoucherId = entity.FVoucherId,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static RentalChargeListItemDto MapToListItemDto(VehRentalCharge entity)
    {
        return new RentalChargeListItemDto
        {
            Id = entity.FID,
            VehicleId = entity.FVehicleId,
            VehicleCode = entity.Assignment?.Vehicle?.FCode,
            EmployeeId = entity.FEmployeeId,
            EmployeeName = entity.FEmployeeName,
            ChargePeriodStart = entity.FChargePeriodStart,
            ChargePeriodEnd = entity.FChargePeriodEnd,
            AmountDue = entity.FAmountDue,
            AmountPaid = entity.FAmountPaid,
            ChargeStatus = entity.FChargeStatus,
            ChargeDate = entity.FChargeDate
        };
    }

    #endregion
}
