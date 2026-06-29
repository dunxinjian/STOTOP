using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Points.Services;

public class RedeemService : IRedeemService
{
    private readonly STOTOPDbContext _db;

    public RedeemService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<RedeemItemListDto>>> GetItemsPagedAsync(long orgId, RedeemItemPagedRequest request)
    {
        var query = _db.Set<PmRedeemItem>()
            .Where(i => i.FOrgId == orgId)
            .AsQueryable();

        if (request.Category.HasValue)
            query = query.Where(i => i.FCategory == request.Category.Value);
        if (request.Status.HasValue)
            query = query.Where(i => i.FStatus == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(i => i.FName.Contains(kw));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(i => i.FSortOrder)
            .ThenByDescending(i => i.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = items.Select(MapToListDto).ToList();

        return ApiResult<PagedResult<RedeemItemListDto>>.Success(new PagedResult<RedeemItemListDto>
        {
            Items = dtos,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<RedeemItemDetailDto>> CreateItemAsync(long orgId, CreateRedeemItemRequest request)
    {
        var entity = new PmRedeemItem
        {
            FUID = Guid.NewGuid().ToString("N"),
            FOrgId = orgId,
            FName = request.Name,
            FCategory = request.Category,
            FDescription = request.Description,
            FImage = request.Image,
            FRequiredPoints = request.RequiredPoints,
            FStock = request.Stock,
            FRedeemedCount = 0,
            FStatus = 1, // 1=上架
            FSortOrder = request.SortOrder,
            FCreateTime = DateTime.Now,
            FUpdateTime = DateTime.Now
        };

        _db.Set<PmRedeemItem>().Add(entity);
        await _db.SaveChangesAsync();

        return ApiResult<RedeemItemDetailDto>.Success(MapToDetailDto(entity));
    }

    public async Task<ApiResult<RedeemItemDetailDto>> UpdateItemAsync(long id, UpdateRedeemItemRequest request)
    {
        var entity = await _db.Set<PmRedeemItem>()
            .AsTracking()
            .FirstOrDefaultAsync(i => i.FID == id);

        if (entity == null)
            return ApiResult<RedeemItemDetailDto>.Fail("兑换商品不存在");

        entity.FName = request.Name;
        entity.FCategory = request.Category;
        entity.FDescription = request.Description;
        entity.FImage = request.Image;
        entity.FRequiredPoints = request.RequiredPoints;
        entity.FStock = request.Stock;
        entity.FSortOrder = request.SortOrder;
        entity.FStatus = request.Status;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return ApiResult<RedeemItemDetailDto>.Success(MapToDetailDto(entity));
    }

    public async Task<ApiResult<bool>> ToggleItemAsync(long id)
    {
        var entity = await _db.Set<PmRedeemItem>()
            .AsTracking()
            .FirstOrDefaultAsync(i => i.FID == id);

        if (entity == null)
            return ApiResult<bool>.Fail("兑换商品不存在");

        entity.FStatus = entity.FStatus == 1 ? 0 : 1; // 上架/下架切换
        entity.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, entity.FStatus == 1 ? "已上架" : "已下架");
    }

    public async Task<ApiResult<RedeemRecordListDto>> ExchangeAsync(long orgId, long userId, ExchangeRequest request)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. 获取商品（只读校验，不依赖 tracking 做更新）
            var item = await _db.Set<PmRedeemItem>().AsNoTracking()
                .FirstOrDefaultAsync(i => i.FID == request.ItemId && i.FOrgId == orgId);

            if (item == null)
                return ApiResult<RedeemRecordListDto>.Fail("兑换商品不存在");

            if (item.FStatus != 1)
                return ApiResult<RedeemRecordListDto>.Fail("商品已下架");

            var requiredPoints = item.FRequiredPoints;

            // 2. 原子扣库存（限库存时由数据库 WHERE FStock>=1 保证，杜绝并发超兑）
            if (item.FStock >= 0)
            {
                var stockAffected = await _db.Set<PmRedeemItem>()
                    .Where(i => i.FID == item.FID && i.FStock >= 1)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.FStock, i => i.FStock - 1)
                        .SetProperty(i => i.FRedeemedCount, i => i.FRedeemedCount + 1)
                        .SetProperty(i => i.FUpdateTime, DateTime.Now));
                if (stockAffected == 0)
                    return ApiResult<RedeemRecordListDto>.Fail("商品库存不足");
            }
            else
            {
                await _db.Set<PmRedeemItem>()
                    .Where(i => i.FID == item.FID)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.FRedeemedCount, i => i.FRedeemedCount + 1)
                        .SetProperty(i => i.FUpdateTime, DateTime.Now));
            }

            // 3. 原子扣积分（WHERE 可用积分>=所需，杜绝并发超花/丢失更新）
            var ptAffected = await _db.Set<PmPointAccount>()
                .Where(a => a.FOrgId == orgId && a.FUserId == userId && a.FAvailablePoints >= requiredPoints)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.FAvailablePoints, a => a.FAvailablePoints - requiredPoints)
                    .SetProperty(a => a.FUsedPoints, a => a.FUsedPoints + requiredPoints)
                    .SetProperty(a => a.FUpdateTime, DateTime.Now));
            if (ptAffected == 0)
                return ApiResult<RedeemRecordListDto>.Fail("积分余额不足"); // 事务未提交，前面扣的库存随回滚撤销

            // 4. 重读扣减后余额，用于记录快照
            var newBalance = await _db.Set<PmPointAccount>().AsNoTracking()
                .Where(a => a.FOrgId == orgId && a.FUserId == userId)
                .Select(a => a.FAvailablePoints)
                .FirstOrDefaultAsync();

            // 5. 创建兑换记录
            var record = new PmRedeemRecord
            {
                FOrgId = orgId,
                FUserId = userId,
                FItemId = item.FID,
                FDeductedPoints = item.FRequiredPoints,
                FStatus = 0, // 0=待发放
                FRemark = request.Remark,
                FCreateTime = DateTime.Now
            };
            _db.Set<PmRedeemRecord>().Add(record);

            // 6. 创建积分扣减记录
            var pointRecord = new PmPointRecord
            {
                FOrgId = orgId,
                FUserId = userId,
                FSourceId = 0, // 兑换扣减
                FType = 3, // Type=3 兑换扣减
                FPointValue = -requiredPoints,
                FBalance = newBalance,
                FRelatedModule = "Points",
                FRelatedEntityType = "RedeemItem",
                FRelatedEntityId = item.FID,
                FOperatorId = userId,
                FRemark = $"兑换商品：{item.FName}",
                FCreateTime = DateTime.Now
            };
            _db.Set<PmPointRecord>().Add(pointRecord);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = new RedeemRecordListDto
            {
                Id = record.FID,
                OrgId = record.FOrgId,
                UserId = record.FUserId,
                ItemId = record.FItemId,
                ItemName = item.FName,
                DeductedPoints = record.FDeductedPoints,
                Status = record.FStatus,
                Remark = record.FRemark,
                CreateTime = record.FCreateTime
            };

            // 获取用户名
            var user = await _db.Set<SysUser>()
                .Where(u => u.FID == userId)
                .Select(u => new { u.FName })
                .FirstOrDefaultAsync();
            dto.UserName = user?.FName;

            return ApiResult<RedeemRecordListDto>.Success(dto, "兑换成功");
        }
        catch
        {
            await transaction.RollbackAsync();
            return ApiResult<RedeemRecordListDto>.Fail("兑换失败，请重试");
        }
    }

    public async Task<ApiResult<PagedResult<RedeemRecordListDto>>> GetRecordsPagedAsync(long orgId, RedeemRecordPagedRequest request)
    {
        var query = _db.Set<PmRedeemRecord>()
            .Where(r => r.FOrgId == orgId)
            .AsQueryable();

        if (request.UserId.HasValue)
            query = query.Where(r => r.FUserId == request.UserId.Value);
        if (request.ItemId.HasValue)
            query = query.Where(r => r.FItemId == request.ItemId.Value);
        if (request.Status.HasValue)
            query = query.Where(r => r.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(r => r.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = await MapRecordsToDtos(records);

        return ApiResult<PagedResult<RedeemRecordListDto>>.Success(new PagedResult<RedeemRecordListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<PagedResult<RedeemRecordListDto>>> GetMyRecordsAsync(long orgId, long userId, RedeemRecordPagedRequest request)
    {
        var query = _db.Set<PmRedeemRecord>()
            .Where(r => r.FOrgId == orgId && r.FUserId == userId)
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(r => r.FStatus == request.Status.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(r => r.FCreateTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = await MapRecordsToDtos(records);

        return ApiResult<PagedResult<RedeemRecordListDto>>.Success(new PagedResult<RedeemRecordListDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
    }

    public async Task<ApiResult<bool>> DeliverAsync(long id, long issuerId, DeliverRequest request)
    {
        var entity = await _db.Set<PmRedeemRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (entity == null)
            return ApiResult<bool>.Fail("兑换记录不存在");

        if (entity.FStatus != 0)
            return ApiResult<bool>.Fail("该记录状态不允许发放");

        entity.FStatus = 1; // 1=已发放
        entity.FIssuerId = issuerId;
        entity.FIssueTime = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(request.Remark))
            entity.FRemark = request.Remark;

        await _db.SaveChangesAsync();
        return ApiResult<bool>.Success(true, "发放成功");
    }

    public async Task<ApiResult<bool>> CancelAsync(long id, long operatorId)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var record = await _db.Set<PmRedeemRecord>()
                .AsTracking()
                .FirstOrDefaultAsync(r => r.FID == id);

            if (record == null)
                return ApiResult<bool>.Fail("兑换记录不存在");

            if (record.FStatus == 2)
                return ApiResult<bool>.Fail("该记录已取消");

            if (record.FStatus == 1)
                return ApiResult<bool>.Fail("已发放的记录不可取消");

            // 1. 退还积分
            var account = await _db.Set<PmPointAccount>()
                .AsTracking()
                .FirstOrDefaultAsync(a => a.FOrgId == record.FOrgId && a.FUserId == record.FUserId);

            if (account != null)
            {
                account.FAvailablePoints += record.FDeductedPoints;
                account.FUsedPoints -= record.FDeductedPoints;
                account.FUpdateTime = DateTime.Now;
            }

            // 2. 恢复库存
            var item = await _db.Set<PmRedeemItem>()
                .AsTracking()
                .FirstOrDefaultAsync(i => i.FID == record.FItemId);

            if (item != null)
            {
                if (item.FStock >= 0)
                    item.FStock += 1;
                item.FRedeemedCount = Math.Max(0, item.FRedeemedCount - 1);
                item.FUpdateTime = DateTime.Now;
            }

            // 3. 更新记录状态
            record.FStatus = 2; // 2=已取消

            // 4. 创建积分退还记录
            var pointRecord = new PmPointRecord
            {
                FOrgId = record.FOrgId,
                FUserId = record.FUserId,
                FSourceId = 0,
                FType = 4, // Type=4 兑换退还
                FPointValue = record.FDeductedPoints,
                FBalance = account?.FAvailablePoints ?? 0,
                FRelatedModule = "Points",
                FRelatedEntityType = "RedeemRecord",
                FRelatedEntityId = record.FID,
                FOperatorId = operatorId,
                FRemark = $"取消兑换退还积分",
                FCreateTime = DateTime.Now
            };
            _db.Set<PmPointRecord>().Add(pointRecord);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResult<bool>.Success(true, "取消成功，积分已退还");
        }
        catch
        {
            await transaction.RollbackAsync();
            return ApiResult<bool>.Fail("取消失败，请重试");
        }
    }

    private async Task<List<RedeemRecordListDto>> MapRecordsToDtos(List<PmRedeemRecord> records)
    {
        if (records.Count == 0) return new List<RedeemRecordListDto>();

        // 批量获取用户名
        var userIds = records.Select(r => r.FUserId)
            .Union(records.Where(r => r.FIssuerId.HasValue).Select(r => r.FIssuerId!.Value))
            .Distinct().ToList();
        var users = await _db.Set<SysUser>()
            .Where(u => userIds.Contains(u.FID))
            .Select(u => new { u.FID, u.FName })
            .ToListAsync();
        var userDict = users.ToDictionary(u => u.FID, u => u.FName);

        // 批量获取商品名
        var itemIds = records.Select(r => r.FItemId).Distinct().ToList();
        var items = await _db.Set<PmRedeemItem>()
            .Where(i => itemIds.Contains(i.FID))
            .Select(i => new { i.FID, i.FName })
            .ToListAsync();
        var itemDict = items.ToDictionary(i => i.FID, i => i.FName);

        return records.Select(r => new RedeemRecordListDto
        {
            Id = r.FID,
            OrgId = r.FOrgId,
            UserId = r.FUserId,
            UserName = userDict.GetValueOrDefault(r.FUserId),
            ItemId = r.FItemId,
            ItemName = itemDict.GetValueOrDefault(r.FItemId),
            DeductedPoints = r.FDeductedPoints,
            Status = r.FStatus,
            IssuerId = r.FIssuerId,
            IssuerName = r.FIssuerId.HasValue ? userDict.GetValueOrDefault(r.FIssuerId.Value) : null,
            IssueTime = r.FIssueTime,
            Remark = r.FRemark,
            CreateTime = r.FCreateTime
        }).ToList();
    }

    private static RedeemItemListDto MapToListDto(PmRedeemItem e) => new()
    {
        Id = e.FID,
        FUID = e.FUID,
        OrgId = e.FOrgId,
        Name = e.FName,
        Category = e.FCategory,
        Image = e.FImage,
        RequiredPoints = e.FRequiredPoints,
        Stock = e.FStock,
        RedeemedCount = e.FRedeemedCount,
        Status = e.FStatus,
        SortOrder = e.FSortOrder
    };

    private static RedeemItemDetailDto MapToDetailDto(PmRedeemItem e) => new()
    {
        Id = e.FID,
        FUID = e.FUID,
        OrgId = e.FOrgId,
        Name = e.FName,
        Category = e.FCategory,
        Description = e.FDescription,
        Image = e.FImage,
        RequiredPoints = e.FRequiredPoints,
        Stock = e.FStock,
        RedeemedCount = e.FRedeemedCount,
        Status = e.FStatus,
        SortOrder = e.FSortOrder,
        CreateTime = e.FCreateTime,
        UpdateTime = e.FUpdateTime
    };
}
