using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class PerformanceService : IPerformanceService
{
    private readonly STOTOPDbContext _db;

    public PerformanceService(STOTOPDbContext db)
    {
        _db = db;
    }

    // ===== 考核周期管理 =====

    public async Task<ApiResult<PagedResult<PerformancePeriodListDto>>> GetPeriodsPagedAsync(
        PerformancePeriodPagedRequest request, long orgId)
    {
        var query = _db.Set<TmPerformancePeriod>()
            .Where(p => p.FOrgId == orgId)
            .AsQueryable();

        if (request.Type.HasValue)
            query = query.Where(p => p.FType == request.Type.Value);
        if (request.Status.HasValue)
            query = query.Where(p => p.FStatus == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(p => p.FName.Contains(kw));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PerformancePeriodListDto
            {
                Id = p.FID,
                UID = p.FUID,
                Name = p.FName,
                OrgId = p.FOrgId,
                Type = p.FType,
                StartDate = p.FStartDate,
                EndDate = p.FEndDate,
                Status = p.FStatus,
                RecordCount = p.Records.Count,
                CreateTime = p.FCreateTime,
                UpdateTime = p.FUpdateTime
            })
            .ToListAsync();

        return ApiResult<PagedResult<PerformancePeriodListDto>>.Success(new PagedResult<PerformancePeriodListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<PerformancePeriodListDto>> CreatePeriodAsync(
        CreatePerformancePeriodRequest request, long orgId, long operatorId)
    {
        var period = new TmPerformancePeriod
        {
            FName = request.Name,
            FOrgId = orgId,
            FType = request.Type,
            FStartDate = request.StartDate,
            FEndDate = request.EndDate,
            FStatus = 0, // 草稿
            FCreatorId = operatorId,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmPerformancePeriod>().Add(period);
        await _db.SaveChangesAsync();

        return ApiResult<PerformancePeriodListDto>.Success(new PerformancePeriodListDto
        {
            Id = period.FID,
            UID = period.FUID,
            Name = period.FName,
            OrgId = period.FOrgId,
            Type = period.FType,
            StartDate = period.FStartDate,
            EndDate = period.FEndDate,
            Status = period.FStatus,
            RecordCount = 0,
            CreateTime = period.FCreateTime,
            UpdateTime = period.FUpdateTime
        });
    }

    public async Task<ApiResult<PerformancePeriodListDto>> UpdatePeriodAsync(
        long id, UpdatePerformancePeriodRequest request)
    {
        var period = await _db.Set<TmPerformancePeriod>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);

        if (period == null)
            return ApiResult<PerformancePeriodListDto>.Fail("考核周期不存在");

        period.FName = request.Name;
        period.FType = request.Type;
        period.FStartDate = request.StartDate;
        period.FEndDate = request.EndDate;
        period.FStatus = request.Status;
        period.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        var recordCount = await _db.Set<TmPerformanceRecord>().CountAsync(r => r.FPeriodId == id);

        return ApiResult<PerformancePeriodListDto>.Success(new PerformancePeriodListDto
        {
            Id = period.FID,
            UID = period.FUID,
            Name = period.FName,
            OrgId = period.FOrgId,
            Type = period.FType,
            StartDate = period.FStartDate,
            EndDate = period.FEndDate,
            Status = period.FStatus,
            RecordCount = recordCount,
            CreateTime = period.FCreateTime,
            UpdateTime = period.FUpdateTime
        });
    }

    // ===== 考核记录 =====

    public async Task<ApiResult<List<PerformanceRecordListDto>>> GetRecordsByPeriodAsync(long periodId)
    {
        var records = await _db.Set<TmPerformanceRecord>()
            .Where(r => r.FPeriodId == periodId)
            .ToListAsync();

        // 批量获取员工姓名
        var employeeIds = records.Select(r => r.FEmployeeId).Distinct().ToList();
        var userNames = await _db.Set<SysUser>()
            .Where(u => employeeIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var nameDict = userNames.ToDictionary(u => u.FID, u => u.FName);

        var period = await _db.Set<TmPerformancePeriod>()
            .Where(p => p.FID == periodId)
            .Select(p => p.FName)
            .FirstOrDefaultAsync();

        var items = records.Select(r => new PerformanceRecordListDto
        {
            Id = r.FID,
            PeriodId = r.FPeriodId,
            PeriodName = period,
            EmployeeId = r.FEmployeeId,
            EmployeeName = nameDict.GetValueOrDefault(r.FEmployeeId),
            OrgId = r.FOrgId,
            TaskTotal = r.FTaskTotal,
            CompletedCount = r.FCompletedCount,
            OnTimeCount = r.FOnTimeCount,
            OverdueCount = r.FOverdueCount,
            CompletionRate = r.FCompletionRate,
            OnTimeRate = r.FOnTimeRate,
            GoalAchievementRate = r.FGoalAchievementRate,
            OverallScore = r.FOverallScore,
            Grade = r.FGrade,
            Status = r.FStatus,
            UpdateTime = r.FUpdateTime
        }).ToList();

        return ApiResult<List<PerformanceRecordListDto>>.Success(items);
    }

    public async Task<ApiResult<PerformanceRecordDetailDto>> GetRecordDetailAsync(long id)
    {
        var record = await _db.Set<TmPerformanceRecord>()
            .Include(r => r.Scores)
                .ThenInclude(s => s.Dimension)
            .Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.FID == id);

        if (record == null)
            return ApiResult<PerformanceRecordDetailDto>.Fail("考核记录不存在");

        var employeeName = await _db.Set<SysUser>()
            .Where(u => u.FID == record.FEmployeeId)
            .Select(u => u.FName)
            .FirstOrDefaultAsync();

        var dto = new PerformanceRecordDetailDto
        {
            Id = record.FID,
            PeriodId = record.FPeriodId,
            PeriodName = record.Period?.FName,
            EmployeeId = record.FEmployeeId,
            EmployeeName = employeeName,
            OrgId = record.FOrgId,
            TaskTotal = record.FTaskTotal,
            CompletedCount = record.FCompletedCount,
            OnTimeCount = record.FOnTimeCount,
            OverdueCount = record.FOverdueCount,
            CompletionRate = record.FCompletionRate,
            OnTimeRate = record.FOnTimeRate,
            GoalAchievementRate = record.FGoalAchievementRate,
            QualityScore = record.FQualityScore,
            SelfScore = record.FSelfScore,
            OverallScore = record.FOverallScore,
            Grade = record.FGrade,
            Comment = record.FComment,
            SelfComment = record.FSelfComment,
            Status = record.FStatus,
            CreateTime = record.FCreateTime,
            UpdateTime = record.FUpdateTime,
            DimensionScores = record.Scores.Select(s => new PerformanceScoreDto
            {
                Id = s.FID,
                RecordId = s.FRecordId,
                DimensionId = s.FDimensionId,
                DimensionName = s.Dimension?.FDimensionName,
                DimensionCode = s.Dimension?.FDimensionCode,
                DataSource = s.Dimension?.FDataSource ?? 0,
                Weight = s.Dimension?.FWeight ?? 0,
                MaxScore = s.Dimension?.FMaxScore ?? 100,
                Score = s.FScore,
                Evaluator = s.FEvaluator,
                Remark = s.FRemark
            }).OrderBy(s => s.DimensionId).ToList()
        };

        return ApiResult<PerformanceRecordDetailDto>.Success(dto);
    }

    public async Task<ApiResult<bool>> SelfEvaluateAsync(long id, SelfEvaluateRequest request, long operatorId)
    {
        var record = await _db.Set<TmPerformanceRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (record == null)
            return ApiResult<bool>.Fail("考核记录不存在");

        if (record.FEmployeeId != operatorId)
            return ApiResult<bool>.Fail("只能对自己的考核进行自评");

        record.FSelfComment = request.SelfComment;
        record.FUpdateTime = DateTime.Now;

        // 更新自评维度评分
        foreach (var scoreInput in request.DimensionScores)
        {
            var existing = await _db.Set<TmPerformanceScore>()
                .AsTracking()
                .FirstOrDefaultAsync(s =>
                    s.FRecordId == id &&
                    s.FDimensionId == scoreInput.DimensionId &&
                    s.FEvaluator == "self");

            if (existing != null)
            {
                existing.FScore = scoreInput.Score;
                existing.FRemark = scoreInput.Remark;
            }
            else
            {
                _db.Set<TmPerformanceScore>().Add(new TmPerformanceScore
                {
                    FRecordId = id,
                    FDimensionId = scoreInput.DimensionId,
                    FScore = scoreInput.Score,
                    FEvaluator = "self",
                    FRemark = scoreInput.Remark
                });
            }
        }

        // 先保存自评分数，确保下面的综合分查询能读到刚写入/更新的行（EF 查询看不到未保存的变更）——
        // 与 ReviewAsync 一致；缺此保存会导致「首次自评、库中无 self 行」时综合分算成 null。
        await _db.SaveChangesAsync();

        // 计算自评综合得分
        var selfScores = await _db.Set<TmPerformanceScore>()
            .Where(s => s.FRecordId == id && s.FEvaluator == "self" && s.FScore.HasValue)
            .Join(_db.Set<TmPerformanceDimension>(),
                s => s.FDimensionId, d => d.FID,
                (s, d) => new { s.FScore, d.FWeight })
            .ToListAsync();

        if (selfScores.Any())
        {
            var totalWeight = selfScores.Sum(x => x.FWeight);
            record.FSelfScore = totalWeight > 0
                ? selfScores.Sum(x => x.FScore!.Value * x.FWeight) / totalWeight
                : 0;
        }

        // 更新状态：待自评 -> 待上级评分
        if (record.FStatus == 0)
            record.FStatus = 1;

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true, "自评提交成功");
    }

    public async Task<ApiResult<bool>> ReviewAsync(long id, SuperiorReviewRequest request, long operatorId)
    {
        var record = await _db.Set<TmPerformanceRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (record == null)
            return ApiResult<bool>.Fail("考核记录不存在");

        record.FComment = request.Comment;
        record.FGrade = request.Grade;
        record.FUpdateTime = DateTime.Now;

        // 更新上级评分维度
        foreach (var scoreInput in request.DimensionScores)
        {
            var existing = await _db.Set<TmPerformanceScore>()
                .AsTracking()
                .FirstOrDefaultAsync(s =>
                    s.FRecordId == id &&
                    s.FDimensionId == scoreInput.DimensionId &&
                    s.FEvaluator == "superior");

            if (existing != null)
            {
                existing.FScore = scoreInput.Score;
                existing.FRemark = scoreInput.Remark;
            }
            else
            {
                _db.Set<TmPerformanceScore>().Add(new TmPerformanceScore
                {
                    FRecordId = id,
                    FDimensionId = scoreInput.DimensionId,
                    FScore = scoreInput.Score,
                    FEvaluator = "superior",
                    FRemark = scoreInput.Remark
                });
            }
        }

        // 重算综合得分：综合得分 = SUM(维度得分 * 维度权重) / SUM(维度权重)
        // 优先取上级评分，无则取自评/自动计算的评分
        await _db.SaveChangesAsync(); // 先保存以便后续查询最新数据

        record.FOverallScore = await CalcOverallScoreAsync(id, record.FOrgId);

        // 更新状态：已评分
        record.FStatus = 2;
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "上级评分提交成功");
    }

    public async Task<ApiResult<List<PerformanceRecordListDto>>> GetMyPerformanceAsync(long userId, long orgId)
    {
        var records = await _db.Set<TmPerformanceRecord>()
            .Include(r => r.Period)
            .Where(r => r.FEmployeeId == userId && r.FOrgId == orgId)
            .OrderByDescending(r => r.FCreateTime)
            .ToListAsync();

        var items = records.Select(r => new PerformanceRecordListDto
        {
            Id = r.FID,
            PeriodId = r.FPeriodId,
            PeriodName = r.Period?.FName,
            EmployeeId = r.FEmployeeId,
            OrgId = r.FOrgId,
            TaskTotal = r.FTaskTotal,
            CompletedCount = r.FCompletedCount,
            OnTimeCount = r.FOnTimeCount,
            OverdueCount = r.FOverdueCount,
            CompletionRate = r.FCompletionRate,
            OnTimeRate = r.FOnTimeRate,
            GoalAchievementRate = r.FGoalAchievementRate,
            OverallScore = r.FOverallScore,
            Grade = r.FGrade,
            Status = r.FStatus,
            UpdateTime = r.FUpdateTime
        }).ToList();

        return ApiResult<List<PerformanceRecordListDto>>.Success(items);
    }

    public async Task<ApiResult<PerformanceDashboardDto>> GetDashboardAsync(long orgId, long? periodId)
    {
        // 获取最新周期或指定周期
        TmPerformancePeriod? period;
        if (periodId.HasValue)
        {
            period = await _db.Set<TmPerformancePeriod>()
                .FirstOrDefaultAsync(p => p.FID == periodId.Value);
        }
        else
        {
            period = await _db.Set<TmPerformancePeriod>()
                .Where(p => p.FOrgId == orgId)
                .OrderByDescending(p => p.FCreateTime)
                .FirstOrDefaultAsync();
        }

        if (period == null)
            return ApiResult<PerformanceDashboardDto>.Fail("无考核周期数据");

        var records = await _db.Set<TmPerformanceRecord>()
            .Where(r => r.FPeriodId == period.FID)
            .ToListAsync();

        var totalEmployees = records.Count;
        var evaluatedCount = records.Count(r => r.FStatus >= 2);
        var pendingSelfCount = records.Count(r => r.FStatus == 0);
        var pendingReviewCount = records.Count(r => r.FStatus == 1);

        var avgCompletionRate = totalEmployees > 0 ? records.Average(r => r.FCompletionRate) : 0;
        var avgOnTimeRate = totalEmployees > 0 ? records.Average(r => r.FOnTimeRate) : 0;
        var scoredRecords = records.Where(r => r.FOverallScore.HasValue).ToList();
        var avgOverallScore = scoredRecords.Any() ? scoredRecords.Average(r => r.FOverallScore!.Value) : 0;

        // 等级分布
        var gradeGroups = records.Where(r => r.FGrade != null)
            .GroupBy(r => r.FGrade!)
            .Select(g => new GradeDistributionDto
            {
                Grade = g.Key,
                Count = g.Count(),
                Percentage = totalEmployees > 0 ? Math.Round((decimal)g.Count() / totalEmployees * 100, 1) : 0
            }).ToList();

        // 确保 S/A/B/C/D 都有
        foreach (var grade in new[] { "S", "A", "B", "C", "D" })
        {
            if (!gradeGroups.Any(g => g.Grade == grade))
                gradeGroups.Add(new GradeDistributionDto { Grade = grade, Count = 0, Percentage = 0 });
        }
        gradeGroups = gradeGroups.OrderBy(g => g.Grade).ToList();

        return ApiResult<PerformanceDashboardDto>.Success(new PerformanceDashboardDto
        {
            PeriodId = period.FID,
            PeriodName = period.FName,
            TotalEmployees = totalEmployees,
            EvaluatedCount = evaluatedCount,
            PendingSelfCount = pendingSelfCount,
            PendingReviewCount = pendingReviewCount,
            AvgCompletionRate = Math.Round(avgCompletionRate, 2),
            AvgOnTimeRate = Math.Round(avgOnTimeRate, 2),
            AvgOverallScore = Math.Round(avgOverallScore, 2),
            GradeDistribution = gradeGroups
        });
    }

    // ===== 维度配置 =====

    public async Task<ApiResult<List<PerformanceDimensionListDto>>> GetDimensionsAsync(long orgId)
    {
        var items = await _db.Set<TmPerformanceDimension>()
            .Where(d => d.FOrgId == orgId)
            .OrderBy(d => d.FSort)
            .Select(d => new PerformanceDimensionListDto
            {
                Id = d.FID,
                OrgId = d.FOrgId,
                DimensionName = d.FDimensionName,
                DimensionCode = d.FDimensionCode,
                DataSource = d.FDataSource,
                Weight = d.FWeight,
                MaxScore = d.FMaxScore,
                Sort = d.FSort,
                IsEnabled = d.FIsEnabled
            })
            .ToListAsync();

        return ApiResult<List<PerformanceDimensionListDto>>.Success(items);
    }

    public async Task<ApiResult<PerformanceDimensionListDto>> CreateDimensionAsync(
        CreatePerformanceDimensionRequest request, long orgId)
    {
        // 检查编码唯一性
        var exists = await _db.Set<TmPerformanceDimension>()
            .AnyAsync(d => d.FOrgId == orgId && d.FDimensionCode == request.DimensionCode);
        if (exists)
            return ApiResult<PerformanceDimensionListDto>.Fail("维度编码已存在");

        var dim = new TmPerformanceDimension
        {
            FOrgId = orgId,
            FDimensionName = request.DimensionName,
            FDimensionCode = request.DimensionCode,
            FDataSource = request.DataSource,
            FWeight = request.Weight,
            FMaxScore = request.MaxScore,
            FSort = request.Sort,
            FIsEnabled = true
        };

        _db.Set<TmPerformanceDimension>().Add(dim);
        await _db.SaveChangesAsync();

        return ApiResult<PerformanceDimensionListDto>.Success(new PerformanceDimensionListDto
        {
            Id = dim.FID,
            OrgId = dim.FOrgId,
            DimensionName = dim.FDimensionName,
            DimensionCode = dim.FDimensionCode,
            DataSource = dim.FDataSource,
            Weight = dim.FWeight,
            MaxScore = dim.FMaxScore,
            Sort = dim.FSort,
            IsEnabled = dim.FIsEnabled
        });
    }

    public async Task<ApiResult<PerformanceDimensionListDto>> UpdateDimensionAsync(
        long id, UpdatePerformanceDimensionRequest request)
    {
        var dim = await _db.Set<TmPerformanceDimension>()
            .AsTracking()
            .FirstOrDefaultAsync(d => d.FID == id);

        if (dim == null)
            return ApiResult<PerformanceDimensionListDto>.Fail("评价维度不存在");

        dim.FDimensionName = request.DimensionName;
        dim.FDimensionCode = request.DimensionCode;
        dim.FDataSource = request.DataSource;
        dim.FWeight = request.Weight;
        dim.FMaxScore = request.MaxScore;
        dim.FSort = request.Sort;
        dim.FIsEnabled = request.IsEnabled;

        await _db.SaveChangesAsync();

        return ApiResult<PerformanceDimensionListDto>.Success(new PerformanceDimensionListDto
        {
            Id = dim.FID,
            OrgId = dim.FOrgId,
            DimensionName = dim.FDimensionName,
            DimensionCode = dim.FDimensionCode,
            DataSource = dim.FDataSource,
            Weight = dim.FWeight,
            MaxScore = dim.FMaxScore,
            Sort = dim.FSort,
            IsEnabled = dim.FIsEnabled
        });
    }

    public async Task<ApiResult<bool>> DeleteDimensionAsync(long id)
    {
        var dim = await _db.Set<TmPerformanceDimension>()
            .Include(d => d.Scores)
            .FirstOrDefaultAsync(d => d.FID == id);

        if (dim == null)
            return ApiResult<bool>.Fail("评价维度不存在");

        if (dim.Scores.Any())
            return ApiResult<bool>.Fail("该维度已有评分记录，无法删除");

        _db.Set<TmPerformanceDimension>().Remove(dim);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, "删除成功");
    }

    // ===== 绩效汇算 =====

    public async Task<ApiResult<bool>> CalculateAsync(long periodId)
    {
        var period = await _db.Set<TmPerformancePeriod>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == periodId);

        if (period == null)
            return ApiResult<bool>.Fail("考核周期不存在");

        var orgId = period.FOrgId;

        // 1. 查询周期内所有组织成员
        var members = await _db.Set<SysUserOrganization>()
            .Where(uo => uo.FOrgId == orgId && uo.FStatus == 1)
            .Select(uo => uo.FUserId)
            .Distinct()
            .ToListAsync();

        if (!members.Any())
            return ApiResult<bool>.Fail("该组织下无活跃成员");

        // 2. 获取启用的自动计算维度
        var autoDimensions = await _db.Set<TmPerformanceDimension>()
            .Where(d => d.FOrgId == orgId && d.FIsEnabled && d.FDataSource == 0)
            .ToListAsync();

        // 3. 获取已有的考核记录
        var existingRecords = await _db.Set<TmPerformanceRecord>()
            .AsTracking()
            .Where(r => r.FPeriodId == periodId)
            .ToListAsync();
        var existingDict = existingRecords.ToDictionary(r => r.FEmployeeId);

        // 4. 逐人统计任务数据
        foreach (var userId in members)
        {
            // 统计周期内的任务数据
            var tasks = await _db.Set<TmTask>()
                .Where(t => t.FAssigneeId == userId
                    && t.FOrgId == orgId
                    && t.FParentTaskId == 0 // 仅统计主任务
                    && t.FCreateTime >= period.FStartDate
                    && t.FCreateTime <= period.FEndDate)
                .ToListAsync();

            var taskTotal = tasks.Count;
            var completedCount = tasks.Count(t => t.FStatus == 2); // 已完成
            var onTimeCount = tasks.Count(t => t.FStatus == 2 && t.FActualEnd.HasValue && t.FPlanEnd.HasValue && t.FActualEnd <= t.FPlanEnd);
            var overdueCount = tasks.Count(t =>
                (t.FStatus != 2 && t.FPlanEnd.HasValue && t.FPlanEnd < DateTime.Now) ||
                (t.FStatus == 2 && t.FActualEnd.HasValue && t.FPlanEnd.HasValue && t.FActualEnd > t.FPlanEnd));

            var completionRate = taskTotal > 0 ? Math.Round((decimal)completedCount / taskTotal * 100, 2) : 0;
            var onTimeRate = completedCount > 0 ? Math.Round((decimal)onTimeCount / completedCount * 100, 2) : 0;

            // 5. 计算目标达成率（关联目标的KR进度加权平均）
            var goalAchievementRate = await CalcGoalAchievementRateAsync(userId, orgId, period.FStartDate, period.FEndDate);

            // 6. 生成/更新考核记录
            TmPerformanceRecord record;
            if (existingDict.TryGetValue(userId, out var existing))
            {
                record = existing;
            }
            else
            {
                record = new TmPerformanceRecord
                {
                    FPeriodId = periodId,
                    FEmployeeId = userId,
                    FOrgId = orgId,
                    FCreateTime = DateTime.Now
                };
                _db.Set<TmPerformanceRecord>().Add(record);
            }

            record.FTaskTotal = taskTotal;
            record.FCompletedCount = completedCount;
            record.FOnTimeCount = onTimeCount;
            record.FOverdueCount = overdueCount;
            record.FCompletionRate = completionRate;
            record.FOnTimeRate = onTimeRate;
            record.FGoalAchievementRate = goalAchievementRate;
            record.FUpdateTime = DateTime.Now;

            await _db.SaveChangesAsync(); // 保存以获取 record.FID

            // 7. 自动计算维度得分（FDataSource=0）
            foreach (var dim in autoDimensions)
            {
                var autoScore = CalcAutoDimensionScore(dim.FDimensionCode, record, dim.FMaxScore);

                var scoreEntity = await _db.Set<TmPerformanceScore>()
                    .AsTracking()
                    .FirstOrDefaultAsync(s =>
                        s.FRecordId == record.FID &&
                        s.FDimensionId == dim.FID &&
                        s.FEvaluator == "auto");

                if (scoreEntity != null)
                {
                    scoreEntity.FScore = autoScore;
                }
                else
                {
                    _db.Set<TmPerformanceScore>().Add(new TmPerformanceScore
                    {
                        FRecordId = record.FID,
                        FDimensionId = dim.FID,
                        FScore = autoScore,
                        FEvaluator = "auto",
                        FRemark = "系统自动计算"
                    });
                }
            }

            await _db.SaveChangesAsync();

            // 8. 重算综合得分
            record.FOverallScore = await CalcOverallScoreAsync(record.FID, orgId);
            await _db.SaveChangesAsync();
        }

        // 更新周期状态为已汇算（如果原来是进行中）
        if (period.FStatus == 1)
        {
            period.FStatus = 2; // 已汇算
            period.FUpdateTime = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        return ApiResult<bool>.Success(true, "绩效汇算完成");
    }

    // ===== 私有辅助方法 =====

    /// <summary>
    /// 计算目标达成率：用户在周期内关联目标的KR进度加权平均
    /// </summary>
    private async Task<decimal> CalcGoalAchievementRateAsync(long userId, long orgId, DateTime startDate, DateTime endDate)
    {
        // 查找用户负责的目标
        var goals = await _db.Set<TmGoal>()
            .Where(g => g.FResponsibleId == userId
                && g.FOrgId == orgId
                && g.FStartDate <= endDate
                && g.FEndDate >= startDate)
            .Select(g => g.FID)
            .ToListAsync();

        if (!goals.Any())
            return 0;

        // 获取关联的 KR
        var krs = await _db.Set<TmKeyResult>()
            .Where(kr => goals.Contains(kr.FGoalId))
            .ToListAsync();

        if (!krs.Any())
            return 0;

        // 加权平均进度
        var totalWeight = krs.Sum(kr => kr.FWeight);
        if (totalWeight == 0) return 0;

        var weightedProgress = krs.Sum(kr => kr.FProgress * kr.FWeight);
        return Math.Round((decimal)weightedProgress / totalWeight, 2);
    }

    /// <summary>
    /// 根据维度编码自动计算得分
    /// </summary>
    private static decimal CalcAutoDimensionScore(string dimensionCode, TmPerformanceRecord record, decimal maxScore)
    {
        return dimensionCode switch
        {
            "completion_rate" => Math.Round(record.FCompletionRate / 100 * maxScore, 2),
            "ontime_rate" => Math.Round(record.FOnTimeRate / 100 * maxScore, 2),
            "goal_achievement" => Math.Round(record.FGoalAchievementRate / 100 * maxScore, 2),
            _ => 0
        };
    }

    /// <summary>
    /// 计算综合得分：SUM(维度得分 * 维度权重) / SUM(维度权重)
    /// 优先取上级评分，无则取自评，最后取自动计算
    /// </summary>
    private async Task<decimal?> CalcOverallScoreAsync(long recordId, long orgId)
    {
        var allScores = await _db.Set<TmPerformanceScore>()
            .Where(s => s.FRecordId == recordId && s.FScore.HasValue)
            .Join(_db.Set<TmPerformanceDimension>().Where(d => d.FOrgId == orgId && d.FIsEnabled),
                s => s.FDimensionId, d => d.FID,
                (s, d) => new { s.FDimensionId, s.FScore, s.FEvaluator, d.FWeight })
            .ToListAsync();

        if (!allScores.Any())
            return null;

        // 按维度分组，优先级：superior > self > auto
        var dimensionScores = allScores
            .GroupBy(s => s.FDimensionId)
            .Select(g =>
            {
                var best = g.OrderByDescending(s => s.FEvaluator switch
                {
                    "superior" => 3,
                    "self" => 2,
                    "auto" => 1,
                    _ => 0
                }).First();
                return new { best.FScore, best.FWeight };
            })
            .ToList();

        var totalWeight = dimensionScores.Sum(x => x.FWeight);
        if (totalWeight == 0) return null;

        return Math.Round(dimensionScores.Sum(x => x.FScore!.Value * x.FWeight) / totalWeight, 2);
    }
}
