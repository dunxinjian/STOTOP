using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.FormulaEngine;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class FormulaService : IFormulaService
{
    private readonly IRepository<FinReportFormula> _formulaRepository;
    private readonly IFormulaEngine _formulaEngine;

    public FormulaService(
        IRepository<FinReportFormula> formulaRepository,
        IFormulaEngine formulaEngine)
    {
        _formulaRepository = formulaRepository;
        _formulaEngine = formulaEngine;
    }

    public async Task<List<FormulaDto>> GetByReportTypeAsync(string reportType, long accountSetId)
    {
        var formulas = await _formulaRepository.Query()
            .Where(f => f.FReportType == reportType && f.FAccountSetId == accountSetId)
            .OrderBy(f => f.FSortOrder)
            .ThenBy(f => f.FRowIndex)
            .ToListAsync();

        return formulas.Select(MapToDto).ToList();
    }

    public async Task<FormulaDto> CreateAsync(CreateFormulaRequest request)
    {
        var entity = new FinReportFormula
        {
            FReportType = request.ReportType,
            FItemName = request.ItemName,
            FRowIndex = request.RowIndex,
            FFormula = request.Formula,
            FFormulaType = request.FormulaType,
            FAccountCodes = request.AccountCodes,
            FDisplayConfig = request.DisplayConfig,
            FIsEnabled = request.IsEnabled,
            FAccountSetId = request.AccountSetId,
            FSortOrder = request.SortOrder,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _formulaRepository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<FormulaDto?> UpdateAsync(long id, UpdateFormulaRequest request)
    {
        var entity = await _formulaRepository.GetByIdAsync(id);
        if (entity == null) return null;

        if (request.ItemName != null) entity.FItemName = request.ItemName;
        if (request.RowIndex.HasValue) entity.FRowIndex = request.RowIndex.Value;
        if (request.Formula != null) entity.FFormula = request.Formula;
        if (request.FormulaType != null) entity.FFormulaType = request.FormulaType;
        if (request.AccountCodes != null) entity.FAccountCodes = request.AccountCodes;
        if (request.DisplayConfig != null) entity.FDisplayConfig = request.DisplayConfig;
        if (request.IsEnabled.HasValue) entity.FIsEnabled = request.IsEnabled.Value;
        if (request.SortOrder.HasValue) entity.FSortOrder = request.SortOrder.Value;
        entity.FUpdatedTime = DateTime.Now;

        await _formulaRepository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _formulaRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await _formulaRepository.DeleteAsync(id);
        return true;
    }

    public async Task<FormulaTestResponse> TestFormulaAsync(FormulaTestRequest request)
    {
        try
        {
            if (!_formulaEngine.Validate(request.Formula, out var error))
            {
                return new FormulaTestResponse { Success = false, Error = error };
            }

            var context = new FormulaContext
            {
                AccountAmounts = request.AccountAmounts,
                RowResults = request.RowResults
            };

            var result = _formulaEngine.Evaluate(request.Formula, context);
            return new FormulaTestResponse { Success = true, Result = result };
        }
        catch (Exception ex)
        {
            return new FormulaTestResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<int> InitDefaultFormulasAsync(string reportType, long accountSetId)
    {
        // 检查是否已有公式
        var existing = await _formulaRepository.Query()
            .Where(f => f.FReportType == reportType && f.FAccountSetId == accountSetId)
            .AnyAsync();

        if (existing)
            return 0; // 已有公式，不重复初始化

        var formulas = reportType switch
        {
            "小企业利润表" => GetSmallEnterpriseProfitFormulas(accountSetId),
            "利润表" => GetSimpleProfitFormulas(accountSetId),
            "资产负债表" => GetBalanceSheetFormulas(accountSetId),
            "现金流量表" => GetCashFlowFormulas(accountSetId),
            _ => new List<FinReportFormula>()
        };

        foreach (var formula in formulas)
        {
            await _formulaRepository.AddAsync(formula);
        }

        return formulas.Count;
    }

    #region 默认公式模板

    /// <summary>
    /// 小企业利润表34行公式
    /// </summary>
    private List<FinReportFormula> GetSmallEnterpriseProfitFormulas(long accountSetId)
    {
        var now = DateTime.Now;
        return new List<FinReportFormula>
        {
            F("小企业利润表", 1, "一、营业收入", "account_sum", "SUM(5001,5051)", accountSetId, now, displayConfig: "{\"isMainTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 2, "减：营业成本", "account_sum", "SUM(5401,5402)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 3, "营业税金及附加", "account_sum", "ACCOUNT(5403)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 4, "其中：消费税", "account_sum", "ACCOUNT(540301)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 5, "营业税", "account_sum", "ACCOUNT(540302)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 6, "城市维护建设税", "account_sum", "ACCOUNT(540302)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 7, "资源税", "account_sum", "ACCOUNT(540310)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 8, "土地增值税", "account_sum", "ACCOUNT(540305)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 9, "城镇土地使用税、房产税、车船税、印花税", "account_sum", "SUM(540306,540307,540308,540309)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 10, "教育费附加、矿产资源补偿费、排污费", "account_sum", "SUM(540303,540304,540311,540312)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 11, "销售费用", "account_sum", "ACCOUNT(5601)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 12, "其中：商品维修费", "account_sum", "ACCOUNT(560107)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 13, "广告费和业务宣传费", "account_sum", "ACCOUNT(560115)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 14, "管理费用", "account_sum", "ACCOUNT(5602)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 15, "其中：开办费", "account_sum", "ACCOUNT(560221)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 16, "业务招待费", "account_sum", "ACCOUNT(560214)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 17, "研究费用", "account_sum", "ACCOUNT(560222)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 18, "财务费用", "account_sum", "ACCOUNT(5603)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 19, "其中：利息费用", "account_sum", "ACCOUNT(560301)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 20, "其他损失", "literal", "0", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 21, "加：投资收益", "account_sum", "ACCOUNT(5111)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 22, "其他收益", "literal", "0", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":0}"),
            F("小企业利润表", 23, "二、营业利润", "row_calc", "ROW(1)-ROW(2)-ROW(3)-ROW(11)-ROW(14)-ROW(18)-ROW(20)+ROW(21)+ROW(22)", accountSetId, now, displayConfig: "{\"isMainTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 24, "加：营业外收入", "account_sum", "ACCOUNT(5301)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 25, "其中：政府补助", "account_sum", "ACCOUNT(530107)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 26, "减：营业外支出", "account_sum", "ACCOUNT(5711)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 27, "其中：坏账损失", "account_sum", "ACCOUNT(571103)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 28, "无法收回的长期债券投资损失", "account_sum", "ACCOUNT(571104)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 29, "无法收回的长期股权投资损失", "account_sum", "ACCOUNT(571105)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 30, "自然灾害等不可抗力因素造成的损失", "account_sum", "ACCOUNT(571106)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 31, "税收滞纳金", "account_sum", "ACCOUNT(571107)", accountSetId, now, displayConfig: "{\"isIndent\":true,\"indentLevel\":1}"),
            F("小企业利润表", 32, "三、利润总额", "row_calc", "ROW(23)+ROW(24)-ROW(26)", accountSetId, now, displayConfig: "{\"isMainTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 33, "减：所得税费用", "account_sum", "ACCOUNT(5801)", accountSetId, now, displayConfig: "{\"isSubTitle\":true,\"indentLevel\":0}"),
            F("小企业利润表", 34, "四、净利润", "row_calc", "ROW(32)-ROW(33)", accountSetId, now, displayConfig: "{\"isMainTitle\":true,\"indentLevel\":0}"),
        };
    }

    /// <summary>
    /// 简版利润表12行公式
    /// </summary>
    private List<FinReportFormula> GetSimpleProfitFormulas(long accountSetId)
    {
        var now = DateTime.Now;
        return new List<FinReportFormula>
        {
            F("利润表", 1, "一、营业收入", "account_sum", "SUM(5001,5051)", accountSetId, now),
            F("利润表", 2, "减：营业成本", "account_sum", "SUM(5401,5402)", accountSetId, now),
            F("利润表", 3, "营业税金及附加", "account_sum", "ACCOUNT(5403)", accountSetId, now),
            F("利润表", 4, "销售费用", "account_sum", "ACCOUNT(5601)", accountSetId, now),
            F("利润表", 5, "管理费用", "account_sum", "ACCOUNT(5602)", accountSetId, now),
            F("利润表", 6, "财务费用", "account_sum", "ACCOUNT(5603)", accountSetId, now),
            F("利润表", 7, "二、营业利润", "row_calc", "ROW(1)-ROW(2)-ROW(3)-ROW(4)-ROW(5)-ROW(6)", accountSetId, now),
            F("利润表", 8, "加：营业外收入", "account_sum", "ACCOUNT(5301)", accountSetId, now),
            F("利润表", 9, "减：营业外支出", "account_sum", "ACCOUNT(5711)", accountSetId, now),
            F("利润表", 10, "三、利润总额", "row_calc", "ROW(7)+ROW(8)-ROW(9)", accountSetId, now),
            F("利润表", 11, "减：所得税费用", "account_sum", "ACCOUNT(5801)", accountSetId, now),
            F("利润表", 12, "四、净利润", "row_calc", "ROW(10)-ROW(11)", accountSetId, now),
        };
    }

    /// <summary>
    /// 资产负债表公式
    /// </summary>
    private List<FinReportFormula> GetBalanceSheetFormulas(long accountSetId)
    {
        var now = DateTime.Now;
        return new List<FinReportFormula>
        {
            F("资产负债表", 1, "流动资产：", "header", null, accountSetId, now, displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 2, "货币资金", "balance_sum", "SUM(1001,1002,1012)", accountSetId, now, lineNo: "1", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 3, "应收票据", "balance_sum", "ACCOUNT(1121)", accountSetId, now, lineNo: "2", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 4, "应收账款", "balance_sum", "ACCOUNT(1122)", accountSetId, now, lineNo: "3", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 5, "预付账款", "balance_sum", "ACCOUNT(1123)", accountSetId, now, lineNo: "4", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 6, "其他应收款", "balance_sum", "ACCOUNT(1221)", accountSetId, now, lineNo: "5", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 7, "存货", "balance_sum", "SUM(1401,1403,1405)", accountSetId, now, lineNo: "6", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 8, "流动资产合计", "row_calc", "ROW(2)+ROW(3)+ROW(4)+ROW(5)+ROW(6)+ROW(7)", accountSetId, now, lineNo: "7", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 9, "非流动资产：", "header", null, accountSetId, now, displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 10, "固定资产原价", "balance_sum", "ACCOUNT(1601)", accountSetId, now, lineNo: "8", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 11, "减：累计折旧", "balance_sum", "ACCOUNT(1602)", accountSetId, now, lineNo: "9", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 12, "固定资产账面价值", "row_calc", "ACCOUNT(1601)-ACCOUNT(1602)", accountSetId, now, lineNo: "10", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 13, "无形资产", "balance_sum", "ACCOUNT(1701)", accountSetId, now, lineNo: "11", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 14, "非流动资产合计", "row_calc", "ROW(12)+ROW(13)", accountSetId, now, lineNo: "12", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 15, "资产总计", "row_calc", "ROW(8)+ROW(14)", accountSetId, now, lineNo: "13", displayConfig: "{\"category\":\"资产\"}"),
            F("资产负债表", 16, "流动负债：", "header", null, accountSetId, now, displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 17, "短期借款", "balance_sum", "ACCOUNT(2001)", accountSetId, now, lineNo: "14", displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 18, "应付票据", "balance_sum", "ACCOUNT(2201)", accountSetId, now, lineNo: "15", displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 19, "应付账款", "balance_sum", "ACCOUNT(2202)", accountSetId, now, lineNo: "16", displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 20, "应付职工薪酬", "balance_sum", "ACCOUNT(2211)", accountSetId, now, lineNo: "17", displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 21, "应交税费", "balance_sum", "ACCOUNT(2221)", accountSetId, now, lineNo: "18", displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 22, "其他应付款", "balance_sum", "ACCOUNT(2241)", accountSetId, now, lineNo: "19", displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 23, "流动负债合计", "row_calc", "ROW(17)+ROW(18)+ROW(19)+ROW(20)+ROW(21)+ROW(22)", accountSetId, now, lineNo: "20", displayConfig: "{\"category\":\"负债\"}"),
            F("资产负债表", 24, "所有者权益：", "header", null, accountSetId, now, displayConfig: "{\"category\":\"权益\"}"),
            F("资产负债表", 25, "实收资本", "balance_sum", "ACCOUNT(3001)", accountSetId, now, lineNo: "21", displayConfig: "{\"category\":\"权益\"}"),
            F("资产负债表", 26, "盈余公积", "balance_sum", "ACCOUNT(3101)", accountSetId, now, lineNo: "22", displayConfig: "{\"category\":\"权益\"}"),
            F("资产负债表", 27, "未分配利润", "balance_sum", "SUM(3103,3104)", accountSetId, now, lineNo: "23", displayConfig: "{\"category\":\"权益\"}"),
            F("资产负债表", 28, "所有者权益合计", "row_calc", "ROW(25)+ROW(26)+ROW(27)", accountSetId, now, lineNo: "24", displayConfig: "{\"category\":\"权益\"}"),
            F("资产负债表", 29, "负债和所有者权益总计", "row_calc", "ROW(23)+ROW(28)", accountSetId, now, lineNo: "25", displayConfig: "{\"category\":\"权益\"}"),
        };
    }

    /// <summary>
    /// 现金流量表框架公式
    /// </summary>
    private List<FinReportFormula> GetCashFlowFormulas(long accountSetId)
    {
        var now = DateTime.Now;
        return new List<FinReportFormula>
        {
            F("现金流量表", 1, "一、经营活动产生的现金流量：", "header", null, accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 2, "销售商品、提供劳务收到的现金", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 3, "收到的其他与经营活动有关的现金", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 4, "现金流入小计", "row_calc", "ROW(2)+ROW(3)", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 5, "购买商品、接受劳务支付的现金", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 6, "支付给职工以及为职工支付的现金", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 7, "支付的各项税费", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 8, "支付的其他与经营活动有关的现金", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 9, "现金流出小计", "row_calc", "ROW(5)+ROW(6)+ROW(7)+ROW(8)", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 10, "经营活动产生的现金流量净额", "row_calc", "ROW(4)-ROW(9)", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
            F("现金流量表", 11, "二、投资活动产生的现金流量：", "header", null, accountSetId, now, displayConfig: "{\"category\":\"投资\"}"),
            F("现金流量表", 12, "投资活动产生的现金流量净额", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"投资\"}"),
            F("现金流量表", 13, "三、筹资活动产生的现金流量：", "header", null, accountSetId, now, displayConfig: "{\"category\":\"筹资\"}"),
            F("现金流量表", 14, "筹资活动产生的现金流量净额", "literal", "0", accountSetId, now, displayConfig: "{\"category\":\"筹资\"}"),
            F("现金流量表", 15, "四、现金及现金等价物净增加额", "row_calc", "ROW(10)+ROW(12)+ROW(14)", accountSetId, now, displayConfig: "{\"category\":\"经营\"}"),
        };
    }

    /// <summary>
    /// 工厂方法：创建 FinReportFormula 实体
    /// </summary>
    private static FinReportFormula F(string reportType, int rowIndex, string itemName, string formulaType, string? formula, long accountSetId, DateTime now, string? lineNo = null, string? displayConfig = null)
    {
        return new FinReportFormula
        {
            FReportType = reportType,
            FRowIndex = rowIndex,
            FItemName = itemName,
            FFormulaType = formulaType,
            FFormula = formula,
            FAccountCodes = lineNo,
            FDisplayConfig = displayConfig,
            FIsEnabled = true,
            FAccountSetId = accountSetId,
            FSortOrder = rowIndex,
            FCreatedTime = now,
            FUpdatedTime = now
        };
    }

    private static FormulaDto MapToDto(FinReportFormula entity)
    {
        return new FormulaDto
        {
            Id = entity.FID,
            ReportType = entity.FReportType,
            ItemName = entity.FItemName,
            RowIndex = entity.FRowIndex,
            Formula = entity.FFormula,
            FormulaType = entity.FFormulaType,
            AccountCodes = entity.FAccountCodes,
            DisplayConfig = entity.FDisplayConfig,
            IsEnabled = entity.FIsEnabled,
            AccountSetId = entity.FAccountSetId,
            SortOrder = entity.FSortOrder
        };
    }

    #endregion
}
