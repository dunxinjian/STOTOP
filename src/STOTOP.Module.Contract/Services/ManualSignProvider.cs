using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Entities;
using STOTOP.Module.Contract.Events;
using STOTOP.Module.Contract.Services.Interfaces;

namespace STOTOP.Module.Contract.Services;

/// <summary>
/// 手动签署提供者（线下签署 + 上传签署文件）
/// </summary>
public class ManualSignProvider : IESignProvider
{
    private readonly IRepository<ConESignRecord> _repository;
    private readonly IRepository<ConContract> _contractRepository;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ManualSignProvider> _logger;

    public ManualSignProvider(
        IRepository<ConESignRecord> repository,
        IRepository<ConContract> contractRepository,
        IEventDispatcher eventDispatcher,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ManualSignProvider> logger)
    {
        _repository = repository;
        _contractRepository = contractRepository;
        _eventDispatcher = eventDispatcher;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private long GetCurrentUserId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && long.TryParse(claim.Value, out var userId))
            return userId;
        return 0;
    }

    public async Task<ESignRecordDto> InitiateSignAsync(long contractId, CreateESignRecordRequest request)
    {
        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract == null)
        {
            throw new InvalidOperationException("合同不存在");
        }

        var entity = new ConESignRecord
        {
            FContractId = contractId,
            FSigner = request.Signer,
            FSignerRole = request.SignerRole,
            FSignMethod = request.SignMethod ?? "手动",
            FSignStatus = 0, // 待签
            FCreatedTime = DateTime.Now
        };

        await _repository.AddAsync(entity);

        return new ESignRecordDto
        {
            Id = entity.FID,
            ContractId = entity.FContractId,
            ContractNo = contract.FContractNo,
            Signer = entity.FSigner,
            SignerRole = entity.FSignerRole,
            SignMethod = entity.FSignMethod,
            SignStatus = entity.FSignStatus,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    public async Task<int> GetSignStatusAsync(long recordId)
    {
        var entity = await _repository.GetByIdAsync(recordId);
        return entity?.FSignStatus ?? -1;
    }

    public async Task<ESignRecordDto?> CompleteSignAsync(long recordId, ManualSignRequest request)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .Include(r => r.Contract)
            .FirstOrDefaultAsync(r => r.FID == recordId);

        if (entity == null) return null;

        entity.FSignStatus = 1; // 已签
        entity.FSignedTime = DateTime.Now;
        entity.FSignedFilePath = request.SignedFilePath;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);

        // 检查该合同所有签署记录状态，判断签署整体结果
        var records = await _repository.Query()
            .Where(r => r.FContractId == entity.FContractId)
            .Select(r => r.FSignStatus)
            .ToListAsync();

        // 只有所有签署方状态均为"已签署(1)"时才算全部完成
        var allSigned = records.Count > 0 && records.All(s => s == 1);

        if (allSigned)
        {
            var contract = await _contractRepository.Query()
                .AsTracking()
                .FirstOrDefaultAsync(c => c.FID == entity.FContractId);

            if (contract != null && contract.FStatus == 2) // 待签署
            {
                contract.FStatus = 3; // 已生效
                contract.FUpdatedTime = DateTime.Now;
                await _contractRepository.UpdateAsync(contract);

                // 发布合同签署事件
                try
                {
                    var currentUserId = GetCurrentUserId();
                    await _eventDispatcher.PublishAsync(new ContractSignedEvent
                    {
                        ContractId = contract.FID,
                        ContractNo = contract.FContractNo ?? "",
                        SignedByUserId = currentUserId,
                        TriggeredByUserId = currentUserId,
                        ModuleCode = "contract"
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "发布ContractSignedEvent失败");
                }
            }
        }

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
            SignedFilePath = entity.FSignedFilePath,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    public async Task<bool> RejectSignAsync(long recordId)
    {
        var entity = await _repository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == recordId);

        if (entity == null) return false;

        entity.FSignStatus = 2; // 已拒签
        entity.FUpdatedTime = DateTime.Now;
        await _repository.UpdateAsync(entity);

        // 任何签署方拒签时，将合同状态回退为草稿（签署失败）
        var contract = await _contractRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == entity.FContractId);

        if (contract != null && contract.FStatus == 2) // 待签署
        {
            contract.FStatus = 0; // 回退为草稿
            contract.FUpdatedTime = DateTime.Now;
            await _contractRepository.UpdateAsync(contract);
        }

        return true;
    }

    public async Task<string?> GetSignedFilePathAsync(long recordId)
    {
        var entity = await _repository.GetByIdAsync(recordId);
        return entity?.FSignedFilePath;
    }
}
