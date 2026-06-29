using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// CF流程管道 API
/// 提供流程引擎元数据查询，如 AutoPlugin 类型列表、暂存表元数据。
/// </summary>
[ApiController]
[Route("api/cardflow/pipeline")]
public class CfPipelineController : ControllerBase
{
    private readonly AutoPluginFactory _autoPluginFactory;
    private readonly STOTOPDbContext _context;

    public CfPipelineController(AutoPluginFactory autoPluginFactory, STOTOPDbContext context)
    {
        _autoPluginFactory = autoPluginFactory;
        _context = context;
    }

    /// <summary>获取管道（流程定义）列表</summary>
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    [HttpGet]
    public async Task<ApiResult<List<PipelineListDto>>> GetPipelines()
    {
        try
        {
            var pipelines = await _context.Set<CfFlowDefinition>()
                .AsNoTracking()
                .OrderByDescending(p => p.FCreatedTime)
                .Select(p => new PipelineListDto
                {
                    Id = p.FID,
                    Name = p.FFlowName,
                    Description = p.FDescription,
                    BizTag = p.FFlowCode,
                    TagColor = null,
                    EnableSubBatchParallel = false,
                    Status = p.FStatus == "published" ? 1 : 0,
                    OrgId = (int)p.FOrgId,
                    AutoPluginCount = 0,
                    CreateTime = p.FCreatedTime,
                    UpdateTime = p.FUpdatedTime,
                })
                .ToListAsync();

            return ApiResult<List<PipelineListDto>>.Success(pipelines);
        }
        catch (Exception)
        {
            // 表不存在或查询异常时返回空列表，避免未处理异常导致前端“网络异常”
            return ApiResult<List<PipelineListDto>>.Success(new List<PipelineListDto>());
        }
    }

    /// <summary>获取指定批次的 AutoPlugin 执行轨迹</summary>
    [RequirePermission(CardFlowPermissions.UploadCenter)]
    [HttpGet("{batchId}/auto-plugin-trail")]
    public async Task<ApiResult<AutoPluginTrailDto>> GetAutoPluginTrail(long batchId)
    {
        try
        {
            // 查询批次基本信息
            var batch = await _context.Set<CfBatch>()
                .AsNoTracking()
                .Where(b => b.FID == batchId)
                .FirstOrDefaultAsync();

            if (batch == null)
                return ApiResult<AutoPluginTrailDto>.Fail("批次不存在");

            // 查询流程定义名称
            var flowName = await _context.Set<CfFlowDefinition>()
                .AsNoTracking()
                .Where(fd => fd.FID == batch.FFlowDefinitionId)
                .Select(fd => fd.FFlowName)
                .FirstOrDefaultAsync() ?? "未知流程";

            // 查询插件执行记录
            var executions = await _context.Set<CfPluginExecution>()
                .AsNoTracking()
                .Where(e => e.FBatchId == batchId)
                .OrderBy(e => e.FAutoPluginIndex)
                .ToListAsync();

            // 查询快照记录（用于判断 hasSnapshot 和 snapshotTime）
            var snapshots = await _context.Set<CfBatchSnapshot>()
                .AsNoTracking()
                .Where(s => s.FBatchId == batchId)
                .ToListAsync();

            // 获取插件工厂元数据（用于 pluginType / pluginImplType / supportsRollback）
            var pluginMetadataDict = _autoPluginFactory.GetAllMetadata()
                .ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);

            // 计算当前插件索引：优先取进行中的，其次取第一个待处理的
            var currentIndex = executions
                .FirstOrDefault(e => e.FStatus == 11)?.FAutoPluginIndex
                ?? executions.FirstOrDefault(e => e.FStatus == 10)?.FAutoPluginIndex
                ?? -1;

            // 批次状态映射
            var batchStatus = batch.FStatus switch
            {
                0 => "解析中",
                1 => "已暂存",
                2 => "质检中",
                3 => "已创建卡片",
                4 => "处理中",
                5 => "已完成",
                _ => $"未知({batch.FStatus})"
            };

            // 组装轨迹项
            var trailItems = executions.Select(e =>
            {
                pluginMetadataDict.TryGetValue(e.FAutoPluginName, out var metadata);
                var snapshot = snapshots.FirstOrDefault(s => s.FAutoPluginIndex == e.FAutoPluginIndex);

                return new AutoPluginTrailItemDto
                {
                    PluginName = e.FAutoPluginName,
                    PluginType = metadata?.AutoPluginType ?? "Processing",
                    PluginImplType = metadata?.Code ?? e.FAutoPluginName,
                    SortIndex = e.FAutoPluginIndex,
                    SupportsRollback = true, // 默认支持回撤，后续可从 CfPluginDef 查询
                    Status = e.FStatus switch
                    {
                        10 => "pending",
                        11 => "running",
                        12 => "completed",
                        13 => "failed",
                        14 => "skipped",
                        _ => $"unknown({e.FStatus})"
                    },
                    HasSnapshot = snapshot != null,
                    SnapshotTime = snapshot?.FCreatedTime,
                };
            }).ToList();

            return ApiResult<AutoPluginTrailDto>.Success(new AutoPluginTrailDto
            {
                BatchId = batchId,
                FlowName = flowName,
                CurrentPluginIndex = currentIndex,
                BatchStatus = batchStatus,
                AutoPlugins = trailItems,
            });
        }
        catch (Exception)
        {
            // 查询异常时返回空轨迹，避免未处理异常
            return ApiResult<AutoPluginTrailDto>.Success(new AutoPluginTrailDto
            {
                BatchId = batchId,
                FlowName = "",
                CurrentPluginIndex = -1,
                BatchStatus = "",
                AutoPlugins = new List<AutoPluginTrailItemDto>(),
            });
        }
    }

    /// <summary>获取所有已注册的自动插件类型元数据列表</summary>
    [AllowAnonymous]
    [HttpGet("auto-plugin-types")]
    public ApiResult<object> GetAutoPluginTypes()
    {
        var list = _autoPluginFactory.GetAllMetadata();
        return ApiResult<object>.Success(list);
    }

    /// <summary>获取暂存表列表（默认STG前缀）</summary>
    [AllowAnonymous]
    [HttpGet("staging-tables")]
    public async Task<ApiResult<List<StagingTableInfo>>> GetStagingTables([FromQuery] string prefix = "STG")
    {
        var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "STG" : prefix.Trim();

        var tables = new List<StagingTableInfo>();
        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                  AND TABLE_NAME LIKE @prefix + '%'
                ORDER BY TABLE_NAME";
            cmd.Parameters.Add(new SqlParameter("@prefix", safePrefix));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new StagingTableInfo
                {
                    TableName = reader.GetString(0),
                    Columns = new List<StagingColumnInfo>()
                });
            }
        }
        finally
        {
            await conn.CloseAsync();
        }

        return ApiResult<List<StagingTableInfo>>.Success(tables);
    }

    /// <summary>获取指定暂存表的列元数据</summary>
    [AllowAnonymous]
    [HttpGet("staging-tables/{tableName}/columns")]
    public async Task<ApiResult<List<StagingColumnInfo>>> GetStagingTableColumns(string tableName)
    {
        // 安全校验：tableName只允许中文、字母、数字、下划线，且必须以STG开头
        if (string.IsNullOrWhiteSpace(tableName) || !Regex.IsMatch(tableName, @"^[A-Za-z一-鿿_][A-Za-z0-9一-鿿_]*$"))
            return ApiResult<List<StagingColumnInfo>>.Fail("无效的表名格式");

        if (!tableName.StartsWith("STG", StringComparison.OrdinalIgnoreCase))
            return ApiResult<List<StagingColumnInfo>>.Fail("仅允许查询STG前缀的暂存表");

        var columns = new List<StagingColumnInfo>();
        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName
                ORDER BY ORDINAL_POSITION";
            cmd.Parameters.Add(new SqlParameter("@tableName", tableName));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new StagingColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    MaxLength = reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3)
                });
            }
        }
        finally
        {
            await conn.CloseAsync();
        }

        return ApiResult<List<StagingColumnInfo>>.Success(columns);
    }
}
