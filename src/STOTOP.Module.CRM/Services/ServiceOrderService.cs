using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class ServiceOrderService : IServiceOrderService
{
    private readonly IRepository<CrmServiceOrder> _orderRepository;
    private readonly IRepository<CrmServiceOrderLog> _logRepository;
    private readonly IRepository<CrmCustomer> _customerRepository;

    public ServiceOrderService(
        IRepository<CrmServiceOrder> orderRepository,
        IRepository<CrmServiceOrderLog> logRepository,
        IRepository<CrmCustomer> customerRepository)
    {
        _orderRepository = orderRepository;
        _logRepository = logRepository;
        _customerRepository = customerRepository;
    }

    public async Task<PagedResult<ServiceOrderListItemDto>> GetServiceOrdersAsync(ServiceOrderQueryRequest request)
    {
        IQueryable<CrmServiceOrder> query = _orderRepository.Query().Include(o => o.Customer);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(o => o.FOrderNo.Contains(keyword) || o.FTitle.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            query = query.Where(o => o.FCustomerId == request.CustomerId);

        if (request.AssigneeId.HasValue)
            query = query.Where(o => o.FAssigneeId == request.AssigneeId.Value);

        if (request.Category.HasValue)
            query = query.Where(o => o.FCategory == request.Category.Value);

        if (request.Priority.HasValue)
            query = query.Where(o => o.FPriority == request.Priority.Value);

        if (request.Status.HasValue)
            query = query.Where(o => o.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ServiceOrderListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ServiceOrderDto?> GetServiceOrderByIdAsync(long id)
    {
        var entity = await _orderRepository.Query()
            .Include(o => o.Customer)
            .Include(o => o.Logs)
            .FirstOrDefaultAsync(o => o.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ServiceOrderDto> CreateServiceOrderAsync(CreateServiceOrderRequest request)
    {
        var customer = await _customerRepository.Query()
            .FirstOrDefaultAsync(c => c.FCode == request.CustomerId);
        if (customer == null)
            throw new InvalidOperationException("客户不存在");

        // 生成工单号
        var orderNo = $"SO{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";

        var entity = new CrmServiceOrder
        {
            FOrderNo = orderNo,
            FCustomerId = request.CustomerId,
            FAssigneeId = request.AssigneeId,
            FCategory = request.Category,
            FPriority = request.Priority,
            FTitle = request.Title,
            FDescription = request.Description,
            FStatus = 0, // 待接单
            FCreatedTime = DateTime.Now
        };

        await _orderRepository.AddAsync(entity);

        // 记录创建日志
        var log = new CrmServiceOrderLog
        {
            FOrderId = entity.FID,
            FOperatorId = 0,
            FOperationType = 0, // 创建
            FContent = "工单已创建",
            FCreatedTime = DateTime.Now
        };
        await _logRepository.AddAsync(log);

        return (await GetServiceOrderByIdAsync(entity.FID))!;
    }

    public async Task<ServiceOrderDto?> UpdateServiceOrderAsync(long id, UpdateServiceOrderRequest request)
    {
        var entity = await _orderRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(o => o.FID == id);

        if (entity == null) return null;

        if (entity.FStatus >= 3) // 已完成或已关闭不允许修改
            throw new InvalidOperationException("已完成或已关闭的工单不允许修改");

        entity.FAssigneeId = request.AssigneeId;
        entity.FCategory = request.Category;
        entity.FPriority = request.Priority;
        entity.FTitle = request.Title;
        entity.FDescription = request.Description;
        entity.FUpdatedTime = DateTime.Now;

        await _orderRepository.UpdateAsync(entity);
        return await GetServiceOrderByIdAsync(id);
    }

    public async Task<bool> DeleteServiceOrderAsync(long id)
    {
        var entity = await _orderRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await _orderRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> ExecuteActionAsync(long id, ServiceOrderActionRequest request)
    {
        var entity = await _orderRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(o => o.FID == id);

        if (entity == null) return false;

        switch (request.OperationType)
        {
            case 1: // 接单
                if (entity.FStatus != 0)
                    throw new InvalidOperationException("只有待接单状态的工单才能接单");
                entity.FStatus = 1; // 处理中
                entity.FAssigneeId = request.OperatorId;
                break;

            case 2: // 处理完成
                if (entity.FStatus != 1)
                    throw new InvalidOperationException("只有处理中的工单才能完成处理");
                entity.FStatus = 2; // 待确认
                entity.FResolvedTime = DateTime.Now;
                break;

            case 3: // 转派
                if (entity.FStatus > 2)
                    throw new InvalidOperationException("已完成或已关闭的工单不能转派");
                if (!request.TransferToId.HasValue)
                    throw new InvalidOperationException("转派必须指定目标人员");
                entity.FAssigneeId = request.TransferToId.Value;
                entity.FStatus = 1; // 转派后回到处理中
                break;

            case 4: // 关闭
                entity.FStatus = 4; // 已关闭
                break;

            default:
                throw new InvalidOperationException($"不支持的操作类型: {request.OperationType}");
        }

        entity.FUpdatedTime = DateTime.Now;
        await _orderRepository.UpdateAsync(entity);

        // 记录操作日志
        var log = new CrmServiceOrderLog
        {
            FOrderId = id,
            FOperatorId = request.OperatorId,
            FOperationType = request.OperationType,
            FContent = request.Content,
            FAttachments = request.Attachments,
            FCreatedTime = DateTime.Now
        };
        await _logRepository.AddAsync(log);

        return true;
    }

    public async Task<ServiceOrderStatisticsDto> GetStatisticsAsync(long? assigneeId = null)
    {
        var query = _orderRepository.Query();

        if (assigneeId.HasValue)
            query = query.Where(o => o.FAssigneeId == assigneeId.Value);

        var total = await query.CountAsync();
        var pending = await query.CountAsync(o => o.FStatus == 0);
        var processing = await query.CountAsync(o => o.FStatus == 1);
        var waitingConfirm = await query.CountAsync(o => o.FStatus == 2);
        var completed = await query.CountAsync(o => o.FStatus == 3);
        var closed = await query.CountAsync(o => o.FStatus == 4);

        return new ServiceOrderStatisticsDto
        {
            Total = total,
            Pending = pending,
            Processing = processing,
            WaitingConfirm = waitingConfirm,
            Completed = completed,
            Closed = closed
        };
    }

    #region Mapping

    private static ServiceOrderDto MapToDto(CrmServiceOrder entity)
    {
        return new ServiceOrderDto
        {
            Id = entity.FID,
            OrderNo = entity.FOrderNo,
            CustomerId = entity.FCustomerId,
            CustomerName = entity.Customer?.FShortName,
            AssigneeId = entity.FAssigneeId,
            Category = entity.FCategory,
            Priority = entity.FPriority,
            Title = entity.FTitle,
            Description = entity.FDescription,
            Status = entity.FStatus,
            ResolvedTime = entity.FResolvedTime,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime,
            UpdaterName = entity.FUpdaterName,
            UpdatedTime = entity.FUpdatedTime,
            Logs = entity.Logs.Select(MapLogToDto).OrderByDescending(l => l.CreatedTime).ToList()
        };
    }

    private static ServiceOrderListItemDto MapToListItemDto(CrmServiceOrder entity)
    {
        return new ServiceOrderListItemDto
        {
            Id = entity.FID,
            OrderNo = entity.FOrderNo,
            CustomerId = entity.FCustomerId,
            CustomerName = entity.Customer?.FShortName,
            AssigneeId = entity.FAssigneeId,
            Category = entity.FCategory,
            Priority = entity.FPriority,
            Title = entity.FTitle,
            Status = entity.FStatus,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static ServiceOrderLogDto MapLogToDto(CrmServiceOrderLog entity)
    {
        return new ServiceOrderLogDto
        {
            Id = entity.FID,
            OrderId = entity.FOrderId,
            OperatorId = entity.FOperatorId,
            OperationType = entity.FOperationType,
            Content = entity.FContent,
            Attachments = entity.FAttachments,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
