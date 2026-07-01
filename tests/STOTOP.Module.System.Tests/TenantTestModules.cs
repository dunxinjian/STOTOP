using System.Runtime.CompilerServices;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 隔离自检的全模块注册。跨模块漏标扫描须让 DbContext 模型包含【全部活跃业务模块】的实体，
/// 否则未注册模块的实体不在模型里 → 漏标扫描假阴性（全绿但没扫到）。
/// <para>
/// 关键：EF Core 按 context 类型缓存模型，故必须在【进程内第一个 STOTOPDbContext 构建前】就注册齐全，
/// 否则首个上下文会把"仅部分模块"的模型缓存住、后续注册不再生效。用 <see cref="ModuleInitializer"/>
/// 在测试程序集加载时（早于任何测试/上下文）注册，杜绝该缓存竞态。
/// </para>
/// 退役模块 OA 有意不纳入（不引用其项目、不注册其程序集）。
/// </summary>
internal static class TenantTestModules
{
    /// <summary>每个活跃业务模块取一个代表实体类型——注册其程序集 + 作为"该模块已注册"的护栏断言锚点。</summary>
    private static readonly Type[] ModuleMarkers =
    {
        typeof(global::STOTOP.Module.System.Entities.SysFeedbackCard),
        typeof(global::STOTOP.Module.Finance.Entities.FinVoucher),
        typeof(global::STOTOP.Module.Express.Entities.ExpWaybill),
        typeof(global::STOTOP.Module.CRM.Entities.CrmCustomer),
        typeof(global::STOTOP.Module.CardFlow.Entities.CfCard),
        typeof(global::STOTOP.Module.Points.Entities.PmPointAccount),
        typeof(global::STOTOP.Module.Dormitory.Entities.DorBuilding),
        typeof(global::STOTOP.Module.Quality.Entities.QlReview),
        typeof(global::STOTOP.Module.Insurance.Entities.InsPolicy),
        typeof(global::STOTOP.Module.Salary.Entities.SalaryPayroll),
        typeof(global::STOTOP.Module.Contract.Entities.ConContract),
        typeof(global::STOTOP.Module.Workflow.Entities.WfWorkItem),
        typeof(global::STOTOP.Module.KSF.Entities.KsfPlan),
        typeof(global::STOTOP.Module.Vehicle.Entities.VehVehicle),
        typeof(global::STOTOP.Module.PPV.Entities.PpvRecord),
        typeof(global::STOTOP.Module.Supplier.Entities.SupSupplier),
        typeof(global::STOTOP.Module.Task.Entities.TmTask),
        typeof(global::STOTOP.Module.Conference.Entities.ConfEvent),
        typeof(global::STOTOP.Module.HR.Entities.HrEmployee),
    };

    /// <summary>供漏标扫描护栏断言"每个模块的代表实体都在模型里"。</summary>
    public static IReadOnlyList<Type> Markers => ModuleMarkers;

    private static bool _registered;
    private static readonly object _lock = new();

    [ModuleInitializer]
    internal static void RegisterAll()
    {
        lock (_lock)
        {
            if (_registered) return;
            foreach (var t in ModuleMarkers)
                STOTOPDbContext.RegisterModuleAssembly(t.Assembly);
            _registered = true;
        }
    }
}
