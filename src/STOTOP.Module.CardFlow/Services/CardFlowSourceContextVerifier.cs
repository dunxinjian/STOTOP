using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.OA.Entities;

namespace STOTOP.Module.CardFlow.Services;

public sealed class CardFlowSourceContextVerifier : ICardFlowSourceContextVerifier
{
    private readonly STOTOPDbContext _dbContext;

    public CardFlowSourceContextVerifier(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SourceContextVerificationResult> VerifyAsync(
        CreateCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new SourceContextVerificationResult
        {
            HasSourceContext = HasSourceContext(request),
            StoredInitialDataJson = NormalizeJson(request.InitialDataJson)
        };
        if (!result.HasSourceContext)
            return result;

        var sourceType = NormalizeSourceType(request.SourceType);
        if (!request.SourceId.HasValue)
        {
            result.Warnings.Add("来源上下文缺少 SourceId，初始数据不会提升为可信事实");
            return result;
        }

        var trusted = sourceType switch
        {
            "expense_request" => await BuildExpenseRequestFactsAsync(request.SourceId.Value, request.OrgId, cancellationToken),
            "loan_apply" => await BuildLoanApplyFactsAsync(request.SourceId.Value, request.OrgId, cancellationToken),
            "expense_reimburse" => await BuildExpenseReimburseFactsAsync(request.SourceId.Value, request.OrgId, cancellationToken),
            _ => null
        };

        if (trusted == null)
        {
            if (IsKnownSourceType(sourceType))
            {
                result.ErrorMessage = $"来源单据不存在或不属于当前组织：{request.SourceType}/{request.SourceId}";
            }
            else
            {
                result.Warnings.Add("没有可用的来源事实提供器");
            }
            return result;
        }

        trusted["sourceModule"] = NormalizeText(request.SourceModule);
        trusted["sourceType"] = sourceType;
        trusted["sourceId"] = request.SourceId.Value;
        trusted["sourceVerified"] = true;

        result.SourceVerified = true;
        result.TrustedDataJson = trusted.ToJsonString();
        return result;
    }

    private async Task<JsonObject?> BuildExpenseRequestFactsAsync(
        long sourceId,
        long orgId,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.Set<OaExpenseRequest>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.FID == sourceId && item.FOrgId == orgId, cancellationToken);
        if (request == null) return null;

        return new JsonObject
        {
            ["requestRefId"] = request.FID,
            ["sourceDocNumber"] = request.FDocNumber,
            ["amount"] = request.FAmount,
            ["expenseType"] = request.FExpenseType,
            ["reason"] = request.FReason,
            ["applicantId"] = request.FApplicantId,
            ["departmentId"] = request.FDeptId,
            ["sourceOrgId"] = request.FOrgId,
            ["sourceStatus"] = request.FDocStatus,
            ["referencedAmount"] = request.FReferencedAmount
        };
    }

    private async Task<JsonObject?> BuildLoanApplyFactsAsync(
        long sourceId,
        long orgId,
        CancellationToken cancellationToken)
    {
        var loan = await _dbContext.Set<OaLoanApplication>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.FID == sourceId && item.FOrgId == orgId, cancellationToken);
        if (loan == null) return null;

        return new JsonObject
        {
            ["loanRefId"] = loan.FID,
            ["sourceDocNumber"] = loan.FDocNumber,
            ["loanAmount"] = loan.FLoanAmount,
            ["amount"] = loan.FLoanAmount,
            ["outstandingBalance"] = loan.FOutstandingBalance,
            ["reimburseOffsetAmount"] = loan.FReimburseOffsetAmount,
            ["repaidAmount"] = loan.FRepaidAmount,
            ["reason"] = loan.FLoanReason,
            ["applicantId"] = loan.FApplicantId,
            ["departmentId"] = loan.FDeptId,
            ["sourceOrgId"] = loan.FOrgId,
            ["sourceStatus"] = loan.FDocStatus
        };
    }

    private async Task<JsonObject?> BuildExpenseReimburseFactsAsync(
        long sourceId,
        long orgId,
        CancellationToken cancellationToken)
    {
        var reimburse = await _dbContext.Set<OaExpenseReimbursement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.FID == sourceId && item.FOrgId == orgId, cancellationToken);
        if (reimburse == null) return null;

        return new JsonObject
        {
            ["reimburseRefId"] = reimburse.FID,
            ["sourceDocNumber"] = reimburse.FDocNumber,
            ["amount"] = reimburse.FTotalAmount,
            ["totalAmount"] = reimburse.FTotalAmount,
            ["requestRefId"] = reimburse.FRequestRefId,
            ["loanRefId"] = reimburse.FLoanRefId,
            ["reason"] = reimburse.FReason,
            ["applicantId"] = reimburse.FApplicantId,
            ["departmentId"] = reimburse.FDeptId,
            ["sourceOrgId"] = reimburse.FOrgId,
            ["sourceStatus"] = reimburse.FDocStatus
        };
    }

    private static bool HasSourceContext(CreateCardRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.SourceModule)
            || !string.IsNullOrWhiteSpace(request.SourceType)
            || request.SourceId.HasValue;
    }

    private static bool IsKnownSourceType(string sourceType)
    {
        return sourceType is "expense_request" or "loan_apply" or "expense_reimburse";
    }

    private static string NormalizeSourceType(string? sourceType)
    {
        var value = NormalizeText(sourceType);
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return value switch
        {
            "expenserequest" or "expenseapply" or "request" => "expense_request",
            "loan" or "loanapply" => "loan_apply",
            "expensereimburse" or "reimburse" => "expense_reimburse",
            _ => value
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            var node = JsonNode.Parse(value);
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return null;
        }
    }
}
