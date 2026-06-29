using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Import;

public class AuditTrailService
{
    private readonly STOTOPDbContext _context;

    public AuditTrailService(STOTOPDbContext context)
    {
        _context = context;
    }

    /// <summary>从凭证反向追溯到原始 Excel 行（旧映射表已移除，暂返回 null）</summary>
    public async Task<VoucherTraceDto?> TraceVoucherSourceAsync(long voucherId)
    {
        // 旧的 ImpImportVoucherMapping / ImpImportStaging 已删除
        // TODO: 后续通过 STG 暂存表实现新的追溯逻辑
        return null;
    }

    /// <summary>获取批次的审计信息</summary>
    public async Task<BatchAuditDto?> GetBatchAuditAsync(long batchId)
    {
        var batch = await _context.Set<CfBatch>()
            .FirstOrDefaultAsync(b => b.FID == batchId);

        if (batch == null)
            return null;

        return new BatchAuditDto
        {
            BatchId = batch.FID,
            BatchNo = batch.FBatchNo ?? string.Empty,
            FileName = batch.FFileName,
            FileHash = batch.FFileHash,
            FileSize = batch.FFileSize ?? 0,
            Operator = batch.FUploadMethod, // CfBatch 无 FOperator，使用上传方式代替
            UploadMethod = batch.FUploadMethod,
            CreateTime = batch.FCreatedTime,
            ImportStartTime = batch.FImportStartTime,
            ImportEndTime = batch.FImportEndTime,
            TotalRows = batch.FTotalRows,
            SuccessRows = batch.FSuccessRows,
            FailRows = batch.FFailedRows
        };
    }
}
