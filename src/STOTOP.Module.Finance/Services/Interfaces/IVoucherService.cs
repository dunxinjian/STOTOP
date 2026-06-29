using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IVoucherService
{
    Task<VoucherPagedResult> GetPagedListAsync(VoucherQueryRequest request, long accountSetId = 0);
    Task<VoucherDto?> GetByIdAsync(long id);
    Task<VoucherDto> CreateAsync(CreateVoucherRequest request, string creator, long accountSetId = 0, bool enforceAuxContract = false);
    Task<VoucherDto?> UpdateAsync(long id, CreateVoucherRequest request, string modifier, bool enforceAuxContract = false);
    Task<bool> DeleteAsync(long id);
    Task<bool> AuditAsync(long id, string auditor);
    Task<bool> UnAuditAsync(long id);
    Task<VoucherDto> SaveDraftAsync(CreateVoucherRequest request, string creator, long accountSetId = 0);
    Task<List<VoucherListDto>> GetDraftsAsync(long accountSetId = 0);
    Task<bool> ReorderNumbersAsync(long periodId, long accountSetId = 0);
    Task<int> GetNextNumberAsync(string voucherWord, long periodId, long accountSetId = 0);
    Task<int> GetPendingAuditCountAsync(long accountSetId = 0);
    Task<ApiResult<object>> CopyAsync(long voucherId);
    Task<ApiResult<object>> ReverseAsync(long voucherId);
    Task<ApiResult<object>> BatchAuditAsync(List<long> voucherIds, long auditorId, string auditorName);
    Task<ApiResult<object>> CheckGapAsync(long accountSetId, int year, int periodNo);
    Task<ApiResult> CompleteRecordAsync(long id);
}
