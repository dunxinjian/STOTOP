using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Entities;
using STOTOP.Module.Contract.Services.Interfaces;

namespace STOTOP.Module.Contract.Services;

public class ContractReminderService : IContractReminderService
{
    private readonly IRepository<ConContractReminder> _repository;
    private readonly IRepository<ConContract> _contractRepository;

    public ContractReminderService(
        IRepository<ConContractReminder> repository,
        IRepository<ConContract> contractRepository)
    {
        _repository = repository;
        _contractRepository = contractRepository;
    }

    public async Task<PagedResult<ContractReminderDto>> GetRemindersAsync(ContractReminderQueryRequest request)
    {
        var query = _repository.Query().Include(r => r.Contract).AsQueryable();

        if (request.ContractId.HasValue)
        {
            query = query.Where(r => r.FContractId == request.ContractId.Value);
        }

        if (request.RecipientId.HasValue)
        {
            query = query.Where(r => r.FRecipientId == request.RecipientId.Value);
        }

        if (request.IsHandled.HasValue)
        {
            query = query.Where(r => r.FIsHandled == request.IsHandled.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(r => r.FReminderDate)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ContractReminderDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ContractReminderDto?> GetReminderByIdAsync(long id)
    {
        var entity = await _repository.Query()
            .Include(r => r.Contract)
            .FirstOrDefaultAsync(r => r.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ContractReminderDto> CreateReminderAsync(CreateContractReminderRequest request)
    {
        var contract = await _contractRepository.GetByIdAsync(request.ContractId);
        if (contract == null)
        {
            throw new InvalidOperationException("合同不存在");
        }

        var entity = new ConContractReminder
        {
            FContractId = request.ContractId,
            FReminderType = request.ReminderType,
            FReminderDate = request.ReminderDate,
            FRecipientId = request.RecipientId,
            FIsHandled = false,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now
        };

        await _repository.AddAsync(entity);
        return (await GetReminderByIdAsync(entity.FID))!;
    }

    public async Task<ContractReminderDto?> UpdateReminderAsync(long id, UpdateContractReminderRequest request)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (entity == null) return null;

        entity.FReminderType = request.ReminderType;
        entity.FReminderDate = request.ReminderDate;
        entity.FRecipientId = request.RecipientId;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);
        return await GetReminderByIdAsync(id);
    }

    public async Task<bool> DeleteReminderAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return false;

        await _repository.DeleteAsync(id);
        return true;
    }

    public async Task<List<ContractReminderDto>> GetPendingRemindersAsync(long recipientId)
    {
        var items = await _repository.Query()
            .Include(r => r.Contract)
            .Where(r => r.FRecipientId == recipientId && !r.FIsHandled)
            .OrderBy(r => r.FReminderDate)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<bool> MarkAsHandledAsync(long id)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (entity == null) return false;

        entity.FIsHandled = true;
        entity.FUpdatedTime = DateTime.Now;
        await _repository.UpdateAsync(entity);
        return true;
    }

    private static ContractReminderDto MapToDto(ConContractReminder entity)
    {
        return new ContractReminderDto
        {
            Id = entity.FID,
            ContractId = entity.FContractId,
            ContractNo = entity.Contract?.FContractNo,
            ContractTitle = entity.Contract?.FTitle,
            ReminderType = entity.FReminderType,
            ReminderDate = entity.FReminderDate,
            RecipientId = entity.FRecipientId,
            IsHandled = entity.FIsHandled,
            Remark = entity.FRemark,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }
}
