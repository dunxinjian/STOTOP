using STOTOP.WebAPI.Data.Seeders;
using Xunit;

namespace STOTOP.WebAPI.Tests.Data;

/// <summary>
/// 覆盖 BaselineReferenceDataSeeder 唯一键冲突（2601/2627）异常包装的消息构造逻辑。
/// SqlException 无公开构造函数、InMemory 不执行原生 SQL，故直接单测消息构造方法。
/// </summary>
public class BaselineUniqueKeyConflictMessageTests
{
    [Fact]
    public void 英文2601消息_对象名在前_提取第二个引号项为索引名()
    {
        var sqlError = "Cannot insert duplicate key row in object 'dbo.FIN科目模板_明细' " +
            "with unique index 'IX_FIN科目模板明细_模板ID_编码'. The duplicate key value is (3, 1001).";

        var message = BaselineReferenceDataSeeder.BuildUniqueKeyConflictMessage(
            "FIN科目模板_明细", "FID", 12345L, sqlError);

        Assert.Contains("[FIN科目模板_明细]", message);
        Assert.Contains("[FID] = 12345", message);
        Assert.Contains("[IX_FIN科目模板明细_模板ID_编码]", message);
    }

    [Fact]
    public void 中文2601消息_索引名在前_仍提取索引名而非对象名()
    {
        var sqlError = "不能在具有唯一索引 'IX_FIN科目模板明细_模板ID_编码' 的对象 'dbo.FIN科目模板_明细' " +
            "中插入重复键的行。重复键值为 (3, 1001)。";

        var message = BaselineReferenceDataSeeder.BuildUniqueKeyConflictMessage(
            "FIN科目模板_明细", "FID", 12345L, sqlError);

        Assert.Contains("[IX_FIN科目模板明细_模板ID_编码]", message);
        Assert.DoesNotContain("[dbo.FIN科目模板_明细]", message);
    }

    [Fact]
    public void 索引名含表名子串_不被误判为表引用()
    {
        var sqlError = "Cannot insert duplicate key row in object 'dbo.CRM客户' " +
            "with unique index 'IX_CRM客户_编码'. The duplicate key value is (X001).";

        var message = BaselineReferenceDataSeeder.BuildUniqueKeyConflictMessage(
            "CRM客户", "FID", 88L, sqlError);

        Assert.Contains("[IX_CRM客户_编码]", message);
    }

    [Fact]
    public void 英文2627消息_约束名在前_提取约束名()
    {
        var sqlError = "Violation of UNIQUE KEY constraint 'UQ_CRM客户_编码'. " +
            "Cannot insert duplicate key in object 'dbo.CRM客户'. The duplicate key value is (X001).";

        var message = BaselineReferenceDataSeeder.BuildUniqueKeyConflictMessage(
            "CRM客户", "FID", 88L, sqlError);

        Assert.Contains("[UQ_CRM客户_编码]", message);
    }

    [Fact]
    public void 消息无引号项_使用解析失败占位不抛异常()
    {
        var message = BaselineReferenceDataSeeder.BuildUniqueKeyConflictMessage(
            "CF操作日志", "FID", 1L, "some unparseable error");

        Assert.Contains("未能从 SQL 异常消息中解析索引名", message);
    }

    [Fact]
    public void 消息包含诊断指引与原始错误()
    {
        var sqlError = "Cannot insert duplicate key row in object 'dbo.FIN科目模板_明细' " +
            "with unique index 'IX_FIN科目模板明细_模板ID_编码'. The duplicate key value is (3, 1001).";

        var message = BaselineReferenceDataSeeder.BuildUniqueKeyConflictMessage(
            "FIN科目模板_明细", "FID", 12345L, sqlError);

        Assert.Contains("重生成 baseline 快照", message);
        Assert.Contains("而不是改 upsert 匹配键", message);
        Assert.Contains("单事务", message);
        Assert.Contains("The duplicate key value is (3, 1001)", message);
    }

    [Fact]
    public void Key值为null_显示NULL()
    {
        var message = BaselineReferenceDataSeeder.BuildUniqueKeyConflictMessage(
            "CF操作日志", "F编码", null, "Violation of UNIQUE KEY constraint 'UQ_X'.");

        Assert.Contains("[F编码] = NULL", message);
    }
}
