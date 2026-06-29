namespace STOTOP.Module.Dormitory.Constants;

/// <summary>
/// 宿舍模块各实体状态值常量——集中维护语义，消除散落的魔法数字。
/// （前端对应映射见各 *Manage.vue 的 statusOptions/get*StatusText）
/// </summary>
public static class DorStatus
{
    /// <summary>床位状态（入住/退宿时由 ResidenceService 联动维护）</summary>
    public static class Bed
    {
        public const int Free = 1;        // 空闲
        public const int Occupied = 2;    // 已入住
        public const int Maintenance = 3; // 维修中
    }

    /// <summary>入住记录状态</summary>
    public static class Residence
    {
        public const int CheckedIn = 1;   // 入住中
        public const int CheckedOut = 2;  // 已退宿
    }

    /// <summary>访客状态</summary>
    public static class Visitor
    {
        public const int Visiting = 1;    // 来访中
        public const int Left = 2;        // 已离开
    }

    /// <summary>费用缴纳状态</summary>
    public static class Expense
    {
        public const int Unpaid = 0;      // 待缴
        public const int Paid = 1;        // 已缴
        public const int Waived = 2;      // 减免
    }

    /// <summary>报修工单状态</summary>
    public static class Repair
    {
        public const int Pending = 1;     // 待处理
        public const int Processing = 2;  // 处理中
        public const int Done = 3;        // 已完成
        public const int Closed = 4;      // 已关闭
    }

    /// <summary>楼栋/房间 启用状态</summary>
    public static class Enable
    {
        public const int Disabled = 0;    // 停用
        public const int Enabled = 1;     // 启用
    }
}
