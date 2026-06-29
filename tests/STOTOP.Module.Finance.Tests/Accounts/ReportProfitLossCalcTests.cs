using System.Reflection;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class ReportProfitLossCalcTests
{
    // CalculateAccountAmounts 不碰 Repository，全 null 构造 + 反射调用即可。
    private static Dictionary<string, decimal> Invoke(List<FinVoucherEntry> entries, List<FinAccount> accounts)
    {
        var svc = new ReportService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var method = typeof(ReportService).GetMethod(
            "CalculateAccountAmounts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Dictionary<string, decimal>)method.Invoke(svc, new object[] { entries, accounts })!;
    }

    [Fact]
    public void Revenue_account_with_subcategory_name_is_no_longer_zeroed()
    {
        // 损益科目 F类别 存子类名「营业收入」——修复前 == "损益" 不命中 → 被置 0
        var revenue = new FinAccount { FCode = "5001", FName = "主营业务收入", FCategory = "营业收入", FBalanceDirection = "贷", FIsLeaf = 1 };
        var asset = new FinAccount { FCode = "1001", FName = "库存现金", FCategory = "流动资产", FBalanceDirection = "借", FIsLeaf = 1 };
        var entries = new List<FinVoucherEntry>
        {
            new() { FAccountCode = "5001", FCreditAmount = 1000m, FDebitAmount = 0m },
            new() { FAccountCode = "1001", FDebitAmount = 100m, FCreditAmount = 0m },
        };

        var result = Invoke(entries, new List<FinAccount> { revenue, asset });

        Assert.Equal(1000m, result["5001"]); // 收入类：贷 - 借
        Assert.Equal(0m, result["1001"]);    // 资产类不计入损益取数，保持 0
    }

    [Fact]
    public void Expense_account_with_subcategory_name_nets_debit_minus_credit()
    {
        var expense = new FinAccount { FCode = "5602", FName = "管理费用", FCategory = "期间费用", FBalanceDirection = "借", FIsLeaf = 1 };
        var entries = new List<FinVoucherEntry>
        {
            new() { FAccountCode = "5602", FDebitAmount = 300m, FCreditAmount = 50m },
        };

        var result = Invoke(entries, new List<FinAccount> { expense });

        Assert.Equal(250m, result["5602"]); // 费用类：借 - 贷
    }
}
