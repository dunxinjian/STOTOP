using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Entities;
using STOTOP.Module.Contract.Services.Interfaces;

namespace STOTOP.Module.Contract.Services;

public class ESignService : IESignService
{
    private readonly IRepository<ConESignRecord> _repository;
    private readonly IRepository<ConContract> _contractRepository;
    private readonly IESignProvider _signProvider;

    public ESignService(
        IRepository<ConESignRecord> repository,
        IRepository<ConContract> contractRepository,
        IESignProvider signProvider)
    {
        _repository = repository;
        _contractRepository = contractRepository;
        _signProvider = signProvider;
    }

    public async Task<PagedResult<ESignRecordDto>> GetRecordsAsync(ESignRecordQueryRequest request)
    {
        var query = _repository.Query().Include(r => r.Contract).AsQueryable();

        if (request.ContractId.HasValue)
        {
            query = query.Where(r => r.FContractId == request.ContractId.Value);
        }

        if (request.SignStatus.HasValue)
        {
            query = query.Where(r => r.FSignStatus == request.SignStatus.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<ESignRecordDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<ESignRecordDto?> GetRecordByIdAsync(long id)
    {
        var entity = await _repository.Query()
            .Include(r => r.Contract)
            .FirstOrDefaultAsync(r => r.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ESignRecordDto> CreateRecordAsync(CreateESignRecordRequest request)
    {
        return await _signProvider.InitiateSignAsync(request.ContractId, request);
    }

    public async Task<ESignRecordDto?> CompleteSignAsync(long id, ManualSignRequest request)
    {
        return await _signProvider.CompleteSignAsync(id, request);
    }

    public async Task<bool> RejectSignAsync(long id)
    {
        return await _signProvider.RejectSignAsync(id);
    }

    public async Task<bool> DeleteRecordAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return false;

        await _repository.DeleteAsync(id);
        return true;
    }

    private static ESignRecordDto MapToDto(ConESignRecord entity)
    {
        return new ESignRecordDto
        {
            Id = entity.FID,
            ContractId = entity.FContractId,
            ContractNo = entity.Contract?.FContractNo,
            Signer = entity.FSigner,
            SignerRole = entity.FSignerRole,
            SignMethod = entity.FSignMethod,
            SignStatus = entity.FSignStatus,
            SignedTime = entity.FSignedTime,
            ThirdPartyNo = entity.FThirdPartyNo,
            SignedFilePath = entity.FSignedFilePath,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }
}
