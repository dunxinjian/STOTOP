using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.CRM.Services;

public class CustomerService : ICustomerService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IRepository<CrmCustomer> _customerRepository;
    private readonly IRepository<CrmCustomerContact> _contactRepository;
    private readonly IRepository<CrmCustomerTransfer> _transferRepository;
    private readonly IRepository<CrmVisitRecord> _visitRepository;
    private readonly IRepository<CrmServiceOrder> _orderRepository;
    private readonly IRepository<CrmServiceFeedback> _feedbackRepository;
    private readonly IRepository<CrmReferral> _referralRepository;
    private readonly ILogger<CustomerService> _logger;
    private readonly IEventDispatcher _eventDispatcher;

    public CustomerService(
        STOTOPDbContext dbContext,
        IRepository<CrmCustomer> customerRepository,
        IRepository<CrmCustomerContact> contactRepository,
        IRepository<CrmCustomerTransfer> transferRepository,
        IRepository<CrmVisitRecord> visitRepository,
        IRepository<CrmServiceOrder> orderRepository,
        IRepository<CrmServiceFeedback> feedbackRepository,
        IRepository<CrmReferral> referralRepository,
        ILogger<CustomerService> logger,
        IEventDispatcher eventDispatcher)
    {
        _dbContext = dbContext;
        _customerRepository = customerRepository;
        _contactRepository = contactRepository;
        _transferRepository = transferRepository;
        _visitRepository = visitRepository;
        _orderRepository = orderRepository;
        _feedbackRepository = feedbackRepository;
        _referralRepository = referralRepository;
        _logger = logger;
        _eventDispatcher = eventDispatcher;
    }

    #region Customer CRUD

    public async Task<PagedResult<CustomerListItemDto>> GetCustomersAsync(CustomerQueryRequest request)
    {
        var query = _customerRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(c => c.FShortName.Contains(keyword) || (c.FContact != null && c.FContact.Contains(keyword)) || (c.FPhone != null && c.FPhone.Contains(keyword)));
        }

        if (request.Status.HasValue)
            query = query.Where(c => c.FStatus == request.Status.Value);

        if (request.OrgId.HasValue)
            query = query.Where(c => c.FOrgId == request.OrgId.Value);

        if (request.BdEmployeeId.HasValue)
            query = query.Where(c => c.FBdEmployeeId == request.BdEmployeeId.Value);

        if (!string.IsNullOrWhiteSpace(request.Industry))
            query = query.Where(c => c.FIndustry == request.Industry);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<CustomerListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<CustomerStatisticsDto> GetStatisticsAsync()
    {
        var grouped = await _customerRepository.Query()
            .GroupBy(c => c.FStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var statusNames = new[] { "潜在", "活跃", "流失" };
        var byStatus = new List<CustomerStatusGroupDto>();
        for (var s = 0; s < statusNames.Length; s++)
        {
            byStatus.Add(new CustomerStatusGroupDto
            {
                Status = s,
                StatusName = statusNames[s],
                Count = grouped.FirstOrDefault(x => x.Status == s)?.Count ?? 0
            });
        }
        return new CustomerStatisticsDto { TotalCount = byStatus.Sum(b => b.Count), ByStatus = byStatus };
    }

    public async Task<CustomerDto?> GetCustomerByCodeAsync(string code)
    {
        var customer = await _customerRepository.Query()
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.FCode == code);

        return customer == null ? null : MapToDto(customer);
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request)
    {
        // 编码唯一性检查
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var exists = await _customerRepository.Query().AnyAsync(c => c.FCode == request.Code);
            if (exists)
                throw new InvalidOperationException($"编号 '{request.Code}' 已存在");
        }

        var customer = new CrmCustomer
        {
            FCode = request.Code ?? string.Empty,
            FShortName = request.ShortName,
            FFullName = request.FullName,
            FContact = request.Contact,
            FPhone = request.Phone,
            FIndustry = request.Industry,
            FScale = request.Scale,
            FOrgId = request.OrgId ?? 0,
            FBdEmployeeId = request.BdEmployeeId,
            FMaintenanceEmployeeId = request.MaintenanceEmployeeId,
            FStatus = 0,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _customerRepository.AddAsync(customer);

        // 创建联系人
        if (request.Contacts != null && request.Contacts.Count > 0)
        {
            foreach (var contactReq in request.Contacts)
            {
                var contact = new CrmCustomerContact
                {
                    FCustomerId = customer.FCode,
                    FName = contactReq.Name,
                    FPhone = contactReq.Phone,
                    FPosition = contactReq.Position,
                    FRoleTag = contactReq.RoleTag,
                    FIsPrimary = contactReq.IsPrimary,
                    FRemark = contactReq.Remark,
                    FCreatedTime = DateTime.Now
                };
                await _contactRepository.AddAsync(contact);
            }
        }

        return (await GetCustomerByCodeAsync(customer.FCode))!;
    }

    public async Task<CustomerDto?> UpdateCustomerAsync(string code, UpdateCustomerRequest request)
    {
        var customer = await _customerRepository.Query()
            .AsTracking()
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.FCode == code);

        if (customer == null) return null;

        var oldName = customer.FShortName;

        // 检测归属变更，创建流转记录
        if (customer.FBdEmployeeId != request.BdEmployeeId || customer.FOrgId != request.OrgId)
        {
            var transfer = new CrmCustomerTransfer
            {
                FCustomerId = code,
                FTransferType = customer.FOrgId != request.OrgId ? 1 : 2,
                FOriginalOrgId = customer.FOrgId,
                FNewOrgId = request.OrgId,
                FOriginalBdEmployeeId = customer.FBdEmployeeId,
                FNewBdEmployeeId = request.BdEmployeeId,
                FCreatedTime = DateTime.Now
            };
            await _transferRepository.AddAsync(transfer);
        }

        customer.FShortName = request.ShortName;
        customer.FFullName = request.FullName;
        customer.FContact = request.Contact;
        customer.FPhone = request.Phone;
        customer.FIndustry = request.Industry;
        customer.FScale = request.Scale;
        customer.FOrgId = request.OrgId ?? 0;
        customer.FBdEmployeeId = request.BdEmployeeId;
        customer.FMaintenanceEmployeeId = request.MaintenanceEmployeeId;
        customer.FUpdatedTime = DateTime.Now;

        await _customerRepository.UpdateAsync(customer);

        // 名称变更时发布辅助核算同步事件
        if (oldName != request.ShortName)
        {
            await _eventDispatcher.PublishAsync(new AuxiliarySourceChangedEvent
            {
                SourceType = "CRM客户",
                SourceId = 0, // 主键已改为编号，此处传0兼容
                NewName = request.ShortName
            });
        }

        // 重建联系人：删除旧的，创建新的
        if (request.Contacts != null)
        {
            foreach (var existing in customer.Contacts)
            {
                await _contactRepository.DeleteAsync(existing.FID);
            }

            foreach (var contactReq in request.Contacts)
            {
                var contact = new CrmCustomerContact
                {
                    FCustomerId = code,
                    FName = contactReq.Name,
                    FPhone = contactReq.Phone,
                    FPosition = contactReq.Position,
                    FRoleTag = contactReq.RoleTag,
                    FIsPrimary = contactReq.IsPrimary,
                    FRemark = contactReq.Remark,
                    FCreatedTime = DateTime.Now
                };
                await _contactRepository.AddAsync(contact);
            }
        }

        return await GetCustomerByCodeAsync(code);
    }

    public async Task<bool> DeleteCustomerAsync(string code)
    {
        var customer = await _customerRepository.Query()
            .FirstOrDefaultAsync(c => c.FCode == code);
        if (customer == null) return false;

        _dbContext.Set<CrmCustomer>().Remove(customer);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(string code, int status)
    {
        var customer = await _customerRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FCode == code);

        if (customer == null) return false;

        var oldStatus = customer.FStatus;
        customer.FStatus = status;
        customer.FUpdatedTime = DateTime.Now;
        await _customerRepository.UpdateAsync(customer);

        // 记录状态变更流转
        var transfer = new CrmCustomerTransfer
        {
            FCustomerId = code,
            FTransferType = 3, // 状态变更
            FOriginalStatus = oldStatus,
            FNewStatus = status,
            FCreatedTime = DateTime.Now
        };
        await _transferRepository.AddAsync(transfer);

        return true;
    }

    #endregion

    #region Transfer

    public async Task<bool> TransferCustomerAsync(string code, TransferCustomerRequest request)
    {
        var customer = await _customerRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FCode == code);

        if (customer == null) return false;

        // 1. 验证目标归属人存在（通过CRM角色映射表验证）
        if (request.NewBdEmployeeId.HasValue)
        {
            var targetExists = await _dbContext.Set<CrmRoleMapping>()
                .AnyAsync(r => r.FEmployeeId == request.NewBdEmployeeId.Value);
            if (!targetExists)
            {
                _logger.LogWarning("客户转移失败：目标BD员工 {EmployeeId} 不存在于CRM角色映射中", request.NewBdEmployeeId.Value);
                return false;
            }
        }

        // 2. 检查关联数据完整性
        var pendingOrders = await _orderRepository.Query()
            .Where(o => o.FCustomerId == code && o.FStatus < 3)
            .CountAsync();

        var visitRecords = await _visitRepository.Query()
            .Where(v => v.FCustomerId == code)
            .CountAsync();

        var referralRecords = await _referralRepository.Query()
            .Where(r => r.FCustomerId == code)
            .CountAsync();

        var pendingFeedbacks = await _feedbackRepository.Query()
            .Where(f => f.FCustomerId == code && f.FStatus < 3)
            .CountAsync();

        _logger.LogInformation(
            "客户 {CustomerId} 转移前关联数据：进行中工单={PendingOrders}, 拜访记录={Visits}, 推荐记录={Referrals}, 待处理反馈={Feedbacks}",
            code, pendingOrders, visitRecords, referralRecords, pendingFeedbacks);

        // 3. 使用事务确保转移操作原子性
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.Now;

            // 3a. 创建转移记录
            var transfer = new CrmCustomerTransfer
            {
                FCustomerId = code,
                FTransferType = request.TransferType,
                FOriginalOrgId = customer.FOrgId,
                FNewOrgId = request.NewOrgId ?? customer.FOrgId,
                FOriginalBdEmployeeId = customer.FBdEmployeeId,
                FNewBdEmployeeId = request.NewBdEmployeeId ?? customer.FBdEmployeeId,
                FReason = request.Reason,
                FCreatedTime = now
            };
            await _transferRepository.AddAsync(transfer);

            // 3b. 转移关联数据的负责人
            if (request.NewBdEmployeeId.HasValue)
            {
                // 转移拜访记录的拜访人
                var visits = await _visitRepository.Query()
                    .AsTracking()
                    .Where(v => v.FCustomerId == code && v.FVisitorId == (customer.FBdEmployeeId ?? 0))
                    .ToListAsync();
                foreach (var visit in visits)
                {
                    visit.FVisitorId = request.NewBdEmployeeId.Value;
                    visit.FUpdatedTime = now;
                    await _visitRepository.UpdateAsync(visit);
                }

                // 转移进行中工单的受理人
                var orders = await _orderRepository.Query()
                    .AsTracking()
                    .Where(o => o.FCustomerId == code && o.FStatus < 3 && o.FAssigneeId == customer.FBdEmployeeId)
                    .ToListAsync();
                foreach (var order in orders)
                {
                    order.FAssigneeId = request.NewBdEmployeeId.Value;
                    order.FUpdatedTime = now;
                    await _orderRepository.UpdateAsync(order);
                }

                // 转移推荐记录中内部员工为原BD的记录
                var referrals = await _referralRepository.Query()
                    .AsTracking()
                    .Where(r => r.FCustomerId == code && r.FReferrerType == 1 && r.FEmployeeId == customer.FBdEmployeeId)
                    .ToListAsync();
                foreach (var referral in referrals)
                {
                    referral.FEmployeeId = request.NewBdEmployeeId.Value;
                    referral.FUpdatedTime = now;
                    await _referralRepository.UpdateAsync(referral);
                }
            }

            // 3c. 更新客户归属
            if (request.NewOrgId.HasValue)
                customer.FOrgId = request.NewOrgId.Value;
            if (request.NewBdEmployeeId.HasValue)
                customer.FBdEmployeeId = request.NewBdEmployeeId.Value;

            customer.FUpdatedTime = now;
            await _customerRepository.UpdateAsync(customer);

            await transaction.CommitAsync();

            _logger.LogInformation(
                "客户 {CustomerId} 转移成功：BD {OldBd} -> {NewBd}, 组织 {OldOrg} -> {NewOrg}",
                code, transfer.FOriginalBdEmployeeId, transfer.FNewBdEmployeeId,
                transfer.FOriginalOrgId, transfer.FNewOrgId);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "客户 {CustomerId} 转移失败", code);
            throw;
        }
    }

    #endregion

    #region Duplicate Check

    public async Task<List<CustomerListItemDto>> CheckDuplicateAsync(CustomerDuplicateCheckRequest request)
    {
        var query = _customerRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.ShortName) && !string.IsNullOrWhiteSpace(request.Phone))
        {
            query = query.Where(c => c.FShortName.Contains(request.ShortName) || (c.FPhone != null && c.FPhone.Contains(request.Phone)));
        }
        else if (!string.IsNullOrWhiteSpace(request.ShortName))
        {
            query = query.Where(c => c.FShortName.Contains(request.ShortName));
        }
        else if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            query = query.Where(c => c.FPhone != null && c.FPhone.Contains(request.Phone));
        }
        else
        {
            return new List<CustomerListItemDto>();
        }

        var items = await query.Take(20).ToListAsync();
        return items.Select(MapToListItemDto).ToList();
    }

    #endregion

    #region Timeline

    public async Task<List<CustomerTimelineItemDto>> GetTimelineAsync(string code, int count = 20)
    {
        var timeline = new List<CustomerTimelineItemDto>();

        // 拜访记录
        var visits = await _visitRepository.Query()
            .Where(v => v.FCustomerId == code)
            .OrderByDescending(v => v.FCreatedTime)
            .Take(count)
            .ToListAsync();

        timeline.AddRange(visits.Select(v => new CustomerTimelineItemDto
        {
            Type = "visit",
            Id = v.FID,
            Title = $"拜访记录",
            Content = v.FContent,
            OccurredTime = v.FVisitDate,
            CreatorName = v.FCreatorName
        }));

        // 工单
        var orders = await _orderRepository.Query()
            .Where(o => o.FCustomerId == code)
            .OrderByDescending(o => o.FCreatedTime)
            .Take(count)
            .ToListAsync();

        timeline.AddRange(orders.Select(o => new CustomerTimelineItemDto
        {
            Type = "order",
            Id = o.FID,
            Title = o.FTitle,
            Content = o.FDescription,
            OccurredTime = DateOnly.FromDateTime(o.FCreatedTime),
            CreatorName = o.FCreatorName
        }));

        // 反馈
        var feedbacks = await _feedbackRepository.Query()
            .Where(f => f.FCustomerId == code)
            .OrderByDescending(f => f.FCreatedTime)
            .Take(count)
            .ToListAsync();

        timeline.AddRange(feedbacks.Select(f => new CustomerTimelineItemDto
        {
            Type = "feedback",
            Id = f.FID,
            Title = f.FTitle,
            Content = f.FDescription,
            OccurredTime = DateOnly.FromDateTime(f.FCreatedTime),
            CreatorName = f.FCreatorName
        }));

        // 合并排序，取最近N条
        return timeline
            .OrderByDescending(t => t.OccurredTime)
            .Take(count)
            .ToList();
    }

    #endregion

    #region Mapping

    private static CustomerDto MapToDto(CrmCustomer entity)
    {
        return new CustomerDto
        {
            Id = entity.FCode,
            Code = entity.FCode,
            ServiceObjectId = entity.FServiceObjectId,
            ShortName = entity.FShortName,
            FullName = entity.FFullName,
            Contact = entity.FContact,
            Phone = entity.FPhone,
            Industry = entity.FIndustry,
            Scale = entity.FScale,
            Status = entity.FStatus,
            OrgId = entity.FOrgId,
            BdEmployeeId = entity.FBdEmployeeId,
            MaintenanceEmployeeId = entity.FMaintenanceEmployeeId,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime,
            Contacts = entity.Contacts.Select(MapContactToDto).ToList(),

            // 客户扩展属性
            SenderAddress = entity.FSenderAddress,
            OfficeAddress = entity.FOfficeAddress,
            CargoType = entity.FCargoType,
            PrepayPerTicket = entity.FPrepayPerTicket,
            AttachmentPath = entity.FAttachmentPath,
            SourceClientType = entity.FSourceClientType,
            SettlementModeText = entity.FSettlementModeText,
            WarehouseCategory = entity.FWarehouseCategory,
            CutoffTime = entity.FCutoffTime,
            RequiredArea = entity.FRequiredArea,
            DailyOrderVolume = entity.FDailyOrderVolume,
            SkuStructure = entity.FSkuStructure,
            CombinedProducts = entity.FCombinedProducts,
            Platform = entity.FPlatform,
            ExpressPriority = entity.FExpressPriority,
            RemoteDelivery = entity.FRemoteDelivery,
            ReturnRestock = entity.FReturnRestock,
            CustomerSoftware = entity.FCustomerSoftware,
            TempClientId = entity.FTempClientId,
        };
    }

    private static CustomerListItemDto MapToListItemDto(CrmCustomer entity)
    {
        return new CustomerListItemDto
        {
            Id = entity.FCode,
            Code = entity.FCode,
            ShortName = entity.FShortName,
            FullName = entity.FFullName,
            Contact = entity.FContact,
            Phone = entity.FPhone,
            Industry = entity.FIndustry,
            Status = entity.FStatus,
            OrgId = entity.FOrgId,
            BdEmployeeId = entity.FBdEmployeeId,
            ServiceObjectId = entity.FServiceObjectId,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static CustomerContactDto MapContactToDto(CrmCustomerContact entity)
    {
        return new CustomerContactDto
        {
            Id = entity.FID,
            CustomerId = entity.FCustomerId,
            Name = entity.FName,
            Phone = entity.FPhone,
            Position = entity.FPosition,
            RoleTag = entity.FRoleTag,
            IsPrimary = entity.FIsPrimary,
            Remark = entity.FRemark,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
