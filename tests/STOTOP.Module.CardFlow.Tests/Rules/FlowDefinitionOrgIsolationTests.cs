using Microsoft.EntityFrameworkCore;
using STOTOP.Module.CardFlow.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Rules;

public class FlowDefinitionOrgIsolationTests
{
    [Fact]
    public void SuppressOrgIdFill_KeepsOrgZero_WhileNormalAddGetsCurrentOrg()
    {
        using var db = TestDbContextFactory.Create(nameof(SuppressOrgIdFill_KeepsOrgZero_WhileNormalAddGetsCurrentOrg), 192);

        using (db.SuppressOrgIdFill())
        {
            db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FFlowName = "T", FFlowCode = "TPL_A", FOrgId = 0, FStatus = "published" });
            db.SaveChanges();
        }
        var tpl = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "TPL_A");
        Assert.Equal(0, tpl.FOrgId);

        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FFlowName = "N", FFlowCode = "NORMAL_A", FOrgId = 0, FStatus = "draft" });
        db.SaveChanges();
        var normal = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "NORMAL_A");
        Assert.Equal(192, normal.FOrgId);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task GetTemplatesAsync_ReturnsOnlyGlobalTemplates()
    {
        using var db = TestDbContextFactory.Create(nameof(GetTemplatesAsync_ReturnsOnlyGlobalTemplates), null);
        db.Set<CfFlowDefinition>().AddRange(
            new CfFlowDefinition { FFlowName = "全局模板", FFlowCode = "G1", FOrgId = 0, FIsTemplate = true, FStatus = "published" },
            new CfFlowDefinition { FFlowName = "组织伪模板", FFlowCode = "O1", FOrgId = 192, FIsTemplate = true, FStatus = "published" },
            new CfFlowDefinition { FFlowName = "全局非模板", FFlowCode = "G2", FOrgId = 0, FIsTemplate = false, FStatus = "published" });
        await db.SaveChangesAsync();

        var svc = new STOTOP.Module.CardFlow.Services.FlowDefinitionService(
            db, Microsoft.Extensions.Logging.Abstractions.NullLogger<STOTOP.Module.CardFlow.Services.FlowDefinitionService>.Instance);
        var templates = await svc.GetTemplatesAsync();

        Assert.Single(templates);
        Assert.Equal("G1", templates[0].FlowCode);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CloneFlowDefinitionAsync_IgnoresRequestOrgId()
    {
        using var db = TestDbContextFactory.Create(nameof(CloneFlowDefinitionAsync_IgnoresRequestOrgId), null); // accessor=null → FillOrgId no-op → FOrgId stays 0
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FFlowName = "源", FFlowCode = "SRC1", FOrgId = 0, FStatus = "published" });
        await db.SaveChangesAsync();
        var srcId = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "SRC1").FID;

        var svc = new STOTOP.Module.CardFlow.Services.FlowDefinitionService(
            db, Microsoft.Extensions.Logging.Abstractions.NullLogger<STOTOP.Module.CardFlow.Services.FlowDefinitionService>.Instance);
        await svc.CloneFlowDefinitionAsync(srcId,
            new STOTOP.Module.CardFlow.Dtos.CloneFlowDefinitionRequest { FlowName = "克隆", FlowCode = "CLONE1", OrgId = 999 }, operatorId: 1);

        var cloned = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "CLONE1");
        Assert.NotEqual(999, cloned.FOrgId);   // request.OrgId=999 ignored
        Assert.Equal(0, cloned.FOrgId);        // accessor=null → stays 0 (prod: FillOrgId sets current org; see Task 5)
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CloneFlowDefinitionAsync_OtherOrgPrivateSource_NotFound()
    {
        using var db = TestDbContextFactory.Create(nameof(CloneFlowDefinitionAsync_OtherOrgPrivateSource_NotFound), 192);
        using (db.SuppressOrgIdFill())
        {
            db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FFlowName = "他组织私有", FFlowCode = "OTHER1", FOrgId = 888, FStatus = "published" });
            db.SaveChanges();
        }
        var otherId = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "OTHER1").FID;

        var svc = new STOTOP.Module.CardFlow.Services.FlowDefinitionService(
            db, Microsoft.Extensions.Logging.Abstractions.NullLogger<STOTOP.Module.CardFlow.Services.FlowDefinitionService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CloneFlowDefinitionAsync(otherId,
            new STOTOP.Module.CardFlow.Dtos.CloneFlowDefinitionRequest { FlowName = "x", FlowCode = "x" }, 1));
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CloneFlowDefinitionAsync_GlobalTemplateSource_Works()
    {
        using var db = TestDbContextFactory.Create(nameof(CloneFlowDefinitionAsync_GlobalTemplateSource_Works), 192);
        using (db.SuppressOrgIdFill())
        {
            db.Set<CfFlowDefinition>().Add(new CfFlowDefinition { FFlowName = "全局模板", FFlowCode = "GT1", FOrgId = 0, FIsTemplate = true, FStatus = "published" });
            db.SaveChanges();
        }
        var tplId = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "GT1").FID;

        var svc = new STOTOP.Module.CardFlow.Services.FlowDefinitionService(
            db, Microsoft.Extensions.Logging.Abstractions.NullLogger<STOTOP.Module.CardFlow.Services.FlowDefinitionService>.Instance);
        var dto = await svc.CloneFlowDefinitionAsync(tplId,
            new STOTOP.Module.CardFlow.Dtos.CloneFlowDefinitionRequest { FlowName = "从模板", FlowCode = "FROMTPL1" }, 1);

        Assert.NotNull(dto);
        var cloned = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "FROMTPL1");
        Assert.Equal(192, cloned.FOrgId); // 克隆落当前组织
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SaveAsTemplate_PersistsGlobalOrgZero_DespiteOrgFill()
    {
        using var db = TestDbContextFactory.Create(nameof(SaveAsTemplate_PersistsGlobalOrgZero_DespiteOrgFill), 192);
        var src = new CfFlowDefinition { FFlowName = "报销", FFlowCode = "EXP_SRC", FStatus = "published" }; // FOrgId filled to 192 by FillOrgId
        db.Set<CfFlowDefinition>().Add(src);
        await db.SaveChangesAsync();
        db.Set<CfFlowVersion>().Add(new CfFlowVersion { FFlowDefinitionId = src.FID, FVersionNumber = 1, FStatus = "published", FIsCurrentVersion = true });
        await db.SaveChangesAsync();

        var svc = new STOTOP.Module.CardFlow.Services.FlowDefinitionService(
            db, Microsoft.Extensions.Logging.Abstractions.NullLogger<STOTOP.Module.CardFlow.Services.FlowDefinitionService>.Instance);
        await svc.SaveAsTemplateAsync(src.FID, operatorId: 1);

        var tpl = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "EXP_SRC" && x.FIsTemplate);
        Assert.Equal(0, tpl.FOrgId);                 // 模板真正落全局 0（抑制生效）
        Assert.True(tpl.FIsTemplate);
        Assert.Equal("published", tpl.FStatus);

        var tplVersion = db.Set<CfFlowVersion>().IgnoreQueryFilters().Single(v => v.FFlowDefinitionId == tpl.FID);
        Assert.True(tplVersion.FIsCurrentVersion);      // 模板版本须为当前版本，否则从模板克隆会得到空流程
        Assert.Equal("published", tplVersion.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Clone_NonTemplate_GetsCurrentOrg()
    {
        using var db = TestDbContextFactory.Create(nameof(Clone_NonTemplate_GetsCurrentOrg), 192);
        var src = new CfFlowDefinition { FFlowName = "源", FFlowCode = "SRC_ORG", FStatus = "published" };
        db.Set<CfFlowDefinition>().Add(src);
        await db.SaveChangesAsync();

        var svc = new STOTOP.Module.CardFlow.Services.FlowDefinitionService(
            db, Microsoft.Extensions.Logging.Abstractions.NullLogger<STOTOP.Module.CardFlow.Services.FlowDefinitionService>.Instance);
        await svc.CloneFlowDefinitionAsync(src.FID,
            new STOTOP.Module.CardFlow.Dtos.CloneFlowDefinitionRequest { FlowName = "克隆", FlowCode = "CLONE_ORG", OrgId = 999 }, 1);

        var cloned = db.Set<CfFlowDefinition>().IgnoreQueryFilters().Single(x => x.FFlowCode == "CLONE_ORG");
        Assert.Equal(192, cloned.FOrgId); // 普通克隆经 FillOrgId 落当前组织，请求体 999 无效
    }
}
