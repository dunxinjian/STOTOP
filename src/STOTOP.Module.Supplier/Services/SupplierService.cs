using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Supplier.Dtos;
using STOTOP.Module.Supplier.Entities;
using STOTOP.Module.Supplier.Services.Interfaces;
using STOTOP.Module.System.Services.Interfaces;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Supplier.Services;

public class SupplierService : ISupplierService
{
    private readonly IRepository<SupSupplier> _supplierRepository;
    private readonly IRepository<SupBankAccount> _bankAccountRepository;
    private readonly ICodeRuleService _codeRuleService;
    private readonly IEventDispatcher _eventDispatcher;

    public SupplierService(
        IRepository<SupSupplier> supplierRepository,
        IRepository<SupBankAccount> bankAccountRepository,
        ICodeRuleService codeRuleService,
        IEventDispatcher eventDispatcher)
    {
        _supplierRepository = supplierRepository;
        _bankAccountRepository = bankAccountRepository;
        _codeRuleService = codeRuleService;
        _eventDispatcher = eventDispatcher;
    }

    #region Supplier CRUD

    public async Task<PagedResult<SupplierListItemDto>> GetSuppliersAsync(SupplierQueryRequest request)
    {
        var query = _supplierRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(s => s.FCode.Contains(keyword) || s.FFullName.Contains(keyword) || (s.FShortName != null && s.FShortName.Contains(keyword)));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<SupplierListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<SupplierListItemDto>> GetAllEnabledSuppliersAsync()
    {
        var suppliers = await _supplierRepository.Query()
            .Where(s => s.FStatus == 1)
            .OrderBy(s => s.FCode)
            .ToListAsync();

        return suppliers.Select(MapToListItemDto).ToList();
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(long id)
    {
        var supplier = await _supplierRepository.Query()
            .Include(s => s.BankAccounts)
            .FirstOrDefaultAsync(s => s.FID == id);

        return supplier == null ? null : MapToDto(supplier);
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request)
    {
        // 自动生成编码
        var code = await _codeRuleService.GenerateNextCodeAsync("SUP");

        var supplier = new SupSupplier
        {
            FUID = Guid.NewGuid().ToString("N"),
            FCode = code,
            FFullName = request.FullName,
            FShortName = request.ShortName,
            FCreditCode = request.CreditCode,
            FTaxNumber = request.TaxNumber,
            FContact = request.Contact,
            FPhone = request.Phone,
            FEmail = request.Email,
            FAddress = request.Address,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _supplierRepository.AddAsync(supplier);

        // 创建收款账户
        if (request.BankAccounts != null && request.BankAccounts.Count > 0)
        {
            foreach (var bankReq in request.BankAccounts)
            {
                var bankAccount = new SupBankAccount
                {
                    FSupplierId = supplier.FID,
                    FAccountName = bankReq.AccountName,
                    FBankName = bankReq.BankName,
                    FBankAccountNumber = bankReq.BankAccountNumber,
                    FBranchName = bankReq.BranchName,
                    FIsDefault = bankReq.IsDefault,
                    FStatus = 1,
                    FRemark = bankReq.Remark,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                await _bankAccountRepository.AddAsync(bankAccount);
            }
        }

        // 重新查询以获取完整数据（含收款账户）
        return (await GetSupplierByIdAsync(supplier.FID))!;
    }

    public async Task<SupplierDto?> UpdateSupplierAsync(long id, UpdateSupplierRequest request)
    {
        var supplier = await _supplierRepository.Query()
            .AsTracking()
            .Include(s => s.BankAccounts)
            .FirstOrDefaultAsync(s => s.FID == id);

        if (supplier == null) return null;

        // 验证编码唯一性（排除自身）
        var codeExists = await _supplierRepository.Query()
            .AnyAsync(s => s.FCode == request.Code && s.FID != id);
        if (codeExists)
        {
            throw new InvalidOperationException($"供应商编码 {request.Code} 已存在");
        }

        var oldName = supplier.FFullName;
        var oldCode = supplier.FCode;

        supplier.FCode = request.Code;
        supplier.FFullName = request.FullName;
        supplier.FShortName = request.ShortName;
        supplier.FCreditCode = request.CreditCode;
        supplier.FTaxNumber = request.TaxNumber;
        supplier.FContact = request.Contact;
        supplier.FPhone = request.Phone;
        supplier.FEmail = request.Email;
        supplier.FAddress = request.Address;
        supplier.FRemark = request.Remark;
        supplier.FUpdatedTime = DateTime.Now;

        await _supplierRepository.UpdateAsync(supplier);

        // 名称或编码变更时发布辅助核算同步事件
        if (oldName != request.FullName || oldCode != request.Code || supplier.FShortName != request.ShortName)
        {
            await _eventDispatcher.PublishAsync(new AuxiliarySourceChangedEvent
            {
                SourceType = "SUP供应商",
                SourceId = supplier.FID,
                NewName = request.ShortName ?? request.FullName,
                NewCode = oldCode != request.Code ? request.Code : null
            });
        }

        // 处理收款账户：删除旧的，创建新的
        if (request.BankAccounts != null)
        {
            // 删除现有收款账户
            foreach (var existing in supplier.BankAccounts)
            {
                await _bankAccountRepository.DeleteAsync(existing.FID);
            }

            // 创建新的收款账户
            foreach (var bankReq in request.BankAccounts)
            {
                var bankAccount = new SupBankAccount
                {
                    FSupplierId = supplier.FID,
                    FAccountName = bankReq.AccountName,
                    FBankName = bankReq.BankName,
                    FBankAccountNumber = bankReq.BankAccountNumber,
                    FBranchName = bankReq.BranchName,
                    FIsDefault = bankReq.IsDefault,
                    FStatus = 1,
                    FRemark = bankReq.Remark,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                await _bankAccountRepository.AddAsync(bankAccount);
            }
        }

        return await GetSupplierByIdAsync(id);
    }

    public async Task<bool> DeleteSupplierAsync(long id)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null) return false;

        await _supplierRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, int status)
    {
        var supplier = await _supplierRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (supplier == null) return false;

        supplier.FStatus = status;
        supplier.FUpdatedTime = DateTime.Now;
        await _supplierRepository.UpdateAsync(supplier);
        return true;
    }

    public async Task<bool> CheckCodeExistsAsync(string code, long excludeId)
    {
        return await _supplierRepository.Query()
            .AnyAsync(s => s.FCode == code && s.FID != excludeId);
    }

    #endregion

    #region Bank Account CRUD

    public async Task<List<BankAccountDto>> GetBankAccountsAsync(long supplierId)
    {
        var accounts = await _bankAccountRepository.Query()
            .Where(a => a.FSupplierId == supplierId)
            .OrderByDescending(a => a.FIsDefault)
            .ThenBy(a => a.FCreatedTime)
            .ToListAsync();

        return accounts.Select(MapBankAccountToDto).ToList();
    }

    public async Task<BankAccountDto> CreateBankAccountAsync(long supplierId, CreateBankAccountRequest request)
    {
        // 验证供应商是否存在
        var supplier = await _supplierRepository.GetByIdAsync(supplierId);
        if (supplier == null)
        {
            throw new InvalidOperationException("供应商不存在");
        }

        // 如果设为默认，先取消其他默认
        if (request.IsDefault)
        {
            await UnsetDefaultAccountsAsync(supplierId);
        }

        var bankAccount = new SupBankAccount
        {
            FSupplierId = supplierId,
            FAccountName = request.AccountName,
            FBankName = request.BankName,
            FBankAccountNumber = request.BankAccountNumber,
            FBranchName = request.BranchName,
            FIsDefault = request.IsDefault,
            FStatus = 1,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _bankAccountRepository.AddAsync(bankAccount);
        return MapBankAccountToDto(bankAccount);
    }

    public async Task<BankAccountDto?> UpdateBankAccountAsync(long id, UpdateBankAccountRequest request)
    {
        var account = await _bankAccountRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (account == null) return null;

        // 如果设为默认，先取消其他默认
        if (request.IsDefault && !account.FIsDefault)
        {
            await UnsetDefaultAccountsAsync(account.FSupplierId);
        }

        account.FAccountName = request.AccountName;
        account.FBankName = request.BankName;
        account.FBankAccountNumber = request.BankAccountNumber;
        account.FBranchName = request.BranchName;
        account.FIsDefault = request.IsDefault;
        account.FRemark = request.Remark;
        account.FUpdatedTime = DateTime.Now;

        await _bankAccountRepository.UpdateAsync(account);
        return MapBankAccountToDto(account);
    }

    public async Task<bool> DeleteBankAccountAsync(long id)
    {
        var account = await _bankAccountRepository.GetByIdAsync(id);
        if (account == null) return false;

        await _bankAccountRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> SetDefaultBankAccountAsync(long id)
    {
        var account = await _bankAccountRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (account == null) return false;

        // 取消同供应商下其他默认账户
        await UnsetDefaultAccountsAsync(account.FSupplierId);

        account.FIsDefault = true;
        account.FUpdatedTime = DateTime.Now;
        await _bankAccountRepository.UpdateAsync(account);
        return true;
    }

    private async Task UnsetDefaultAccountsAsync(long supplierId)
    {
        var defaults = await _bankAccountRepository.Query()
            .AsTracking()
            .Where(a => a.FSupplierId == supplierId && a.FIsDefault)
            .ToListAsync();

        foreach (var d in defaults)
        {
            d.FIsDefault = false;
            d.FUpdatedTime = DateTime.Now;
            await _bankAccountRepository.UpdateAsync(d);
        }
    }

    #endregion

    #region Mapping

    private static SupplierDto MapToDto(SupSupplier entity)
    {
        return new SupplierDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            Code = entity.FCode,
            FullName = entity.FFullName,
            ShortName = entity.FShortName,
            CreditCode = entity.FCreditCode,
            TaxNumber = entity.FTaxNumber,
            Contact = entity.FContact,
            Phone = entity.FPhone,
            Email = entity.FEmail,
            Address = entity.FAddress,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            BankAccounts = entity.BankAccounts.Select(MapBankAccountToDto).ToList()
        };
    }

    private static SupplierListItemDto MapToListItemDto(SupSupplier entity)
    {
        return new SupplierListItemDto
        {
            Id = entity.FID,
            Code = entity.FCode,
            FullName = entity.FFullName,
            ShortName = entity.FShortName,
            Contact = entity.FContact,
            Phone = entity.FPhone,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static BankAccountDto MapBankAccountToDto(SupBankAccount entity)
    {
        return new BankAccountDto
        {
            Id = entity.FID,
            SupplierId = entity.FSupplierId,
            AccountName = entity.FAccountName,
            BankName = entity.FBankName,
            BankAccountNumber = entity.FBankAccountNumber,
            BranchName = entity.FBranchName,
            IsDefault = entity.FIsDefault,
            Status = entity.FStatus,
            Remark = entity.FRemark,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion
}
