using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Task.Services;

public class KeyResultService : IKeyResultService
{
    private readonly STOTOPDbContext _db;
    private readonly IServiceProvider _serviceProvider;

    public KeyResultService(STOTOPDbContext db, IServiceProvider serviceProvider)
    {
        _db = db;
        _serviceProvider = serviceProvider;
    }

    public async Task<ApiResult<List<KeyResultListDto>>> GetByGoalIdAsync(long goalId)
    {
        var krs = await _db.Set<TmKeyResult>()
            .Where(kr => kr.FGoalId == goalId)
            .OrderBy(kr => kr.FSort)
            .ThenBy(kr => kr.FCreateTime)
            .ToListAsync();

        var userIds = krs.Where(kr => kr.FResponsibleId.HasValue).Select(kr => kr.FResponsibleId!.Value).Distinct().ToList();
        var userDict = await GetUserNameDict(userIds);

        var dtos = krs.Select(kr => MapToListDto(kr, userDict)).ToList();
        return ApiResult<List<KeyResultListDto>>.Success(dtos);
    }

    public async Task<ApiResult<KeyResultListDto>> CreateAsync(long goalId, CreateKeyResultRequest request)
    {
        var goalExists = await _db.Set<TmGoal>().AnyAsync(g => g.FID == goalId);
        if (!goalExists)
            return ApiResult<KeyResultListDto>.Fail("目标不存在");

        var kr = new TmKeyResult
        {
            FGoalId = goalId,
            FTitle = request.Title,
            FMeasureType = request.MeasureType,
            FTargetValue = request.TargetValue,
            FStartValue = request.StartValue,
            FUnit = request.Unit,
            FWeight = request.Weight,
            FResponsibleId = request.ResponsibleId,
            FSort = request.Sort,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<TmKeyResult>().Add(kr);
        await _db.SaveChangesAsync();

        // 重算目标进度
        var goalService = _serviceProvider.GetRequiredService<IGoalService>();
        await goalService.RecalculateProgressAsync(goalId);

        var userDict = kr.FResponsibleId.HasValue
            ? await GetUserNameDict(new List<long> { kr.FResponsibleId.Value })
            : new Dictionary<long, string>();

        return ApiResult<KeyResultListDto>.Success(MapToListDto(kr, userDict));
    }

    public async Task<ApiResult<KeyResultListDto>> UpdateAsync(long id, UpdateKeyResultRequest request)
    {
        var kr = await _db.Set<TmKeyResult>().AsTracking().FirstOrDefaultAsync(k => k.FID == id);
        if (kr == null)
            return ApiResult<KeyResultListDto>.Fail("关键成果不存在");

        kr.FTitle = request.Title;
        kr.FMeasureType = request.MeasureType;
        kr.FTargetValue = request.TargetValue;
        kr.FStartValue = request.StartValue;
        kr.FUnit = request.Unit;
        kr.FWeight = request.Weight;
        kr.FResponsibleId = request.ResponsibleId;
        kr.FSort = request.Sort;
        kr.FStatus = request.Status;
        kr.FUpdateTime = DateTime.Now;

        // 重算KR进度
        kr.FProgress = CalculateProgress(kr);

        await _db.SaveChangesAsync();

        // 重算目标进度
        var goalService = _serviceProvider.GetRequiredService<IGoalService>();
        await goalService.RecalculateProgressAsync(kr.FGoalId);

        var userDict = kr.FResponsibleId.HasValue
            ? await GetUserNameDict(new List<long> { kr.FResponsibleId.Value })
            : new Dictionary<long, string>();

        return ApiResult<KeyResultListDto>.Success(MapToListDto(kr, userDict));
    }

    public async Task<ApiResult<KeyResultListDto>> UpdateProgressAsync(long id, UpdateKeyResultProgressRequest request)
    {
        var kr = await _db.Set<TmKeyResult>().AsTracking().FirstOrDefaultAsync(k => k.FID == id);
        if (kr == null)
            return ApiResult<KeyResultListDto>.Fail("关键成果不存在");

        kr.FCurrentValue = request.CurrentValue;
        kr.FProgress = CalculateProgress(kr);
        kr.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();

        // 触发目标进度重算
        var goalService = _serviceProvider.GetRequiredService<IGoalService>();
        await goalService.RecalculateProgressAsync(kr.FGoalId);

        var userDict = kr.FResponsibleId.HasValue
            ? await GetUserNameDict(new List<long> { kr.FResponsibleId.Value })
            : new Dictionary<long, string>();

        return ApiResult<KeyResultListDto>.Success(MapToListDto(kr, userDict));
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var kr = await _db.Set<TmKeyResult>().FirstOrDefaultAsync(k => k.FID == id);
        if (kr == null)
            return ApiResult<bool>.Fail("关键成果不存在");

        var goalId = kr.FGoalId;
        _db.Set<TmKeyResult>().Remove(kr);
        await _db.SaveChangesAsync();

        // 重算目标进度
        var goalService = _serviceProvider.GetRequiredService<IGoalService>();
        await goalService.RecalculateProgressAsync(goalId);

        return ApiResult<bool>.Success(true, "删除成功");
    }

    #region Private Helpers

    /// <summary>
    /// 计算KR进度:
    /// 数值型(0): (当前值-起始值)/(目标值-起始值)*100
    /// 百分比型(1): 当前值
    /// 里程碑型(2): 0或100
    /// </summary>
    private static int CalculateProgress(TmKeyResult kr)
    {
        switch (kr.FMeasureType)
        {
            case 0: // 数值型
                var range = kr.FTargetValue - kr.FStartValue;
                if (range == 0) return kr.FCurrentValue >= kr.FTargetValue ? 100 : 0;
                var progress = (double)(kr.FCurrentValue - kr.FStartValue) / (double)range * 100;
                return (int)Math.Clamp(Math.Round(progress), 0, 100);

            case 1: // 百分比型
                return (int)Math.Clamp(Math.Round((double)kr.FCurrentValue), 0, 100);

            case 2: // 里程碑型
                return kr.FCurrentValue >= kr.FTargetValue ? 100 : 0;

            default:
                return 0;
        }
    }

    private async Task<Dictionary<long, string>> GetUserNameDict(List<long> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<long, string>();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        return users.ToDictionary(u => u.FID, u => u.FName);
    }

    private static KeyResultListDto MapToListDto(TmKeyResult kr, Dictionary<long, string> userDict) => new()
    {
        Id = kr.FID,
        UID = kr.FUID,
        GoalId = kr.FGoalId,
        Title = kr.FTitle,
        MeasureType = kr.FMeasureType,
        TargetValue = kr.FTargetValue,
        CurrentValue = kr.FCurrentValue,
        StartValue = kr.FStartValue,
        Unit = kr.FUnit,
        Weight = kr.FWeight,
        Progress = kr.FProgress,
        Status = kr.FStatus,
        ResponsibleId = kr.FResponsibleId,
        ResponsibleName = kr.FResponsibleId.HasValue && userDict.ContainsKey(kr.FResponsibleId.Value) ? userDict[kr.FResponsibleId.Value] : null,
        Sort = kr.FSort,
        CreateTime = kr.FCreateTime,
        UpdateTime = kr.FUpdateTime
    };

    #endregion
}
