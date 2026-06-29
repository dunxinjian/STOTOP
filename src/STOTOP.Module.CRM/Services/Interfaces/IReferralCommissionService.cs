using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface IReferralCommissionService
{
    // External Contacts
    Task<PagedResult<ExternalContactDto>> GetExternalContactsAsync(ExternalContactQueryRequest request);
    Task<ExternalContactDto?> GetExternalContactByIdAsync(long id);
    Task<ExternalContactDto> CreateExternalContactAsync(CreateExternalContactRequest request);
    Task<ExternalContactDto?> UpdateExternalContactAsync(long id, UpdateExternalContactRequest request);
    Task<bool> DeleteExternalContactAsync(long id);

    // Referrals
    Task<PagedResult<ReferralDto>> GetReferralsAsync(ReferralQueryRequest request);
    Task<ReferralDto?> GetReferralByIdAsync(long id);
    Task<ReferralDto> CreateReferralAsync(CreateReferralRequest request);
    Task<bool> DeleteReferralAsync(long id);

    // Commissions
    Task<PagedResult<CommissionDto>> GetCommissionsAsync(CommissionQueryRequest request);
    Task<CommissionDto?> GetCommissionByIdAsync(long id);
    Task<CommissionDto> CreateCommissionAsync(CreateCommissionRequest request);
    Task<bool> UpdateCommissionStatusAsync(long id, int status);
    Task<CalcCommissionResultDto> CalcCommissionAsync(CalcCommissionRequest request);
    Task<CommissionDto> SubmitForApprovalAsync(SubmitCommissionRequest request, long userId);
    Task HandleApprovalCallbackAsync(ApprovalCallbackRequest request);

    // Statistics
    Task<List<ReferralStatisticsDto>> GetStatisticsByReferrerAsync(long? orgId, DateOnly? startDate, DateOnly? endDate);
}
