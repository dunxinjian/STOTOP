using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Services;
using Xunit;

namespace STOTOP.Module.Task.Tests.Projects;

using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// ProjectService 单元测试。
/// 被测服务构造：new ProjectService(STOTOPDbContext db)（仅依赖 DbContext）。
/// 组织隔离：用 TestDbContextFactory.Create(name, orgId: 1) 建库，seed 不显式设 FOrgId，
/// 保存时由 DbContext.FillOrgIdForNewEntities 自动回填当前组织。
/// ApiResult 成功 Code = 200，失败默认 Code = 400（见 STOTOP.Core.Models.ApiResult）。
/// </summary>
public class ProjectServiceTests
{
    [Fact]
    public async Task 新建项目默认状态为零且负责人自动成为成员()
    {
        await using var db = TestDbContextFactory.Create(nameof(新建项目默认状态为零且负责人自动成为成员), orgId: 1);
        var svc = new ProjectService(db);

        var result = await svc.CreateAsync(
            new CreateProjectRequest { Name = "项目A", ManagerId = 7 },
            orgId: 1,
            creatorId: 99);

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        // 项目默认状态 = 0（TmProject.FStatus 默认值，CreateAsync 不写 Status）
        Assert.Equal(0, result.Data!.Status);
        Assert.Equal("项目A", result.Data.Name);
        Assert.Equal(7, result.Data.ManagerId);
        Assert.Equal(99, result.Data.CreatorId);

        // 负责人被自动加为成员，角色 0（负责人）
        var managerMember = Assert.Single(result.Data.Members);
        Assert.Equal(7, managerMember.UserId);
        Assert.Equal(0, managerMember.Role);
    }

    [Fact]
    public async Task 获取不存在的项目返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(获取不存在的项目返回失败), orgId: 1);
        var svc = new ProjectService(db);

        var result = await svc.GetByIdAsync(999999);

        Assert.Equal(400, result.Code);
        Assert.Null(result.Data);
        Assert.Equal("项目不存在", result.Message);
    }

    [Fact]
    public async Task 分页查询按关键字过滤项目名称()
    {
        await using var db = TestDbContextFactory.Create(nameof(分页查询按关键字过滤项目名称), orgId: 1);
        await SeedProjectAsync(db, name: "营销活动项目", managerId: 1);
        await SeedProjectAsync(db, name: "研发迭代项目", managerId: 1);
        await SeedProjectAsync(db, name: "营销复盘", managerId: 1);

        var svc = new ProjectService(db);
        // 关键字带前后空格，验证 Trim 行为
        var result = await svc.GetPagedListAsync(
            new ProjectPagedRequest { Keyword = "  营销  ", PageIndex = 1, PageSize = 20 },
            orgId: 1);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Total);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.All(result.Data.Items, i => Assert.Contains("营销", i.Name));
    }

    [Fact]
    public async Task 分页查询按状态过滤()
    {
        await using var db = TestDbContextFactory.Create(nameof(分页查询按状态过滤), orgId: 1);
        await SeedProjectAsync(db, name: "进行中1", managerId: 1, status: 1);
        await SeedProjectAsync(db, name: "已完成1", managerId: 1, status: 2);
        await SeedProjectAsync(db, name: "进行中2", managerId: 1, status: 1);

        var svc = new ProjectService(db);
        var result = await svc.GetPagedListAsync(
            new ProjectPagedRequest { Status = 1, PageIndex = 1, PageSize = 20 },
            orgId: 1);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Total);
        Assert.All(result.Data.Items, i => Assert.Equal(1, i.Status));
    }

    [Fact]
    public async Task 分页查询按负责人过滤()
    {
        await using var db = TestDbContextFactory.Create(nameof(分页查询按负责人过滤), orgId: 1);
        await SeedProjectAsync(db, name: "甲负责", managerId: 10);
        await SeedProjectAsync(db, name: "乙负责", managerId: 20);
        await SeedProjectAsync(db, name: "甲再负责", managerId: 10);

        var svc = new ProjectService(db);
        var result = await svc.GetPagedListAsync(
            new ProjectPagedRequest { ManagerId = 10, PageIndex = 1, PageSize = 20 },
            orgId: 1);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Total);
        Assert.All(result.Data.Items, i => Assert.Equal(10, i.ManagerId));
    }

    [Fact]
    public async Task 分页查询按创建时间倒序并分页()
    {
        await using var db = TestDbContextFactory.Create(nameof(分页查询按创建时间倒序并分页), orgId: 1);
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0);
        await SeedProjectAsync(db, name: "最早", managerId: 1, createTime: baseTime);
        await SeedProjectAsync(db, name: "居中", managerId: 1, createTime: baseTime.AddDays(1));
        await SeedProjectAsync(db, name: "最新", managerId: 1, createTime: baseTime.AddDays(2));

        var svc = new ProjectService(db);

        // 第一页 2 条：按 FCreateTime 倒序应为 [最新, 居中]
        var page1 = await svc.GetPagedListAsync(
            new ProjectPagedRequest { PageIndex = 1, PageSize = 2 }, orgId: 1);
        Assert.Equal(200, page1.Code);
        Assert.Equal(3, page1.Data!.Total);
        Assert.Equal(2, page1.Data.Items.Count);
        Assert.Equal("最新", page1.Data.Items[0].Name);
        Assert.Equal("居中", page1.Data.Items[1].Name);

        // 第二页 1 条：剩 [最早]
        var page2 = await svc.GetPagedListAsync(
            new ProjectPagedRequest { PageIndex = 2, PageSize = 2 }, orgId: 1);
        Assert.Equal(3, page2.Data!.Total);
        var only = Assert.Single(page2.Data.Items);
        Assert.Equal("最早", only.Name);
    }

    [Fact]
    public async Task 分页查询按组织隔离仅返回指定组织项目()
    {
        // 在组织 1 上下文 seed，查询 orgId=2 时显式 Where(FOrgId==2) 应为空。
        await using var db = TestDbContextFactory.Create(nameof(分页查询按组织隔离仅返回指定组织项目), orgId: 1);
        await SeedProjectAsync(db, name: "组织1项目", managerId: 1);

        var svc = new ProjectService(db);

        var inOrg1 = await svc.GetPagedListAsync(
            new ProjectPagedRequest { PageIndex = 1, PageSize = 20 }, orgId: 1);
        Assert.Equal(1, inOrg1.Data!.Total);

        var inOrg2 = await svc.GetPagedListAsync(
            new ProjectPagedRequest { PageIndex = 1, PageSize = 20 }, orgId: 2);
        Assert.Equal(0, inOrg2.Data!.Total);
        Assert.Empty(inOrg2.Data.Items);
    }

    [Fact]
    public async Task 重复添加同一成员返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(重复添加同一成员返回失败), orgId: 1);
        var projectId = await SeedProjectAsync(db, name: "P", managerId: 1);
        var svc = new ProjectService(db);

        var first = await svc.AddMemberAsync(projectId, new AddProjectMemberRequest { UserId = 5, Role = 1 });
        Assert.Equal(200, first.Code);

        var second = await svc.AddMemberAsync(projectId, new AddProjectMemberRequest { UserId = 5, Role = 1 });
        Assert.Equal(400, second.Code);
        Assert.Null(second.Data);
        Assert.Equal("该用户已是项目成员", second.Message);
    }

    [Fact]
    public async Task 添加并移除成员移除不存在成员返回失败()
    {
        await using var db = TestDbContextFactory.Create(nameof(添加并移除成员移除不存在成员返回失败), orgId: 1);
        var projectId = await SeedProjectAsync(db, name: "P", managerId: 1);
        var svc = new ProjectService(db);

        var added = await svc.AddMemberAsync(projectId, new AddProjectMemberRequest { UserId = 8, Role = 2 });
        Assert.Equal(200, added.Code);
        Assert.Equal(8, added.Data!.UserId);
        Assert.Equal(2, added.Data.Role);

        var removed = await svc.RemoveMemberAsync(projectId, userId: 8);
        Assert.Equal(200, removed.Code);
        Assert.True(removed.Data);

        // 再次移除已不存在的成员
        var removeAgain = await svc.RemoveMemberAsync(projectId, userId: 8);
        Assert.Equal(400, removeAgain.Code);
        Assert.Equal("成员不存在", removeAgain.Message);
    }

    [Fact]
    public async Task 获取成员列表按角色再按加入时间排序()
    {
        await using var db = TestDbContextFactory.Create(nameof(获取成员列表按角色再按加入时间排序), orgId: 1);
        var projectId = await SeedProjectAsync(db, name: "P", managerId: 1);

        var baseTime = new DateTime(2026, 2, 1, 0, 0, 0);
        // 故意乱序插入：role/join 组合，期望输出按 FRole 升序、同角色按 FJoinTime 升序
        await SeedMemberAsync(db, projectId, userId: 100, role: 1, joinTime: baseTime.AddHours(2));
        await SeedMemberAsync(db, projectId, userId: 200, role: 0, joinTime: baseTime.AddHours(5));
        await SeedMemberAsync(db, projectId, userId: 300, role: 1, joinTime: baseTime.AddHours(1));

        var svc = new ProjectService(db);
        var result = await svc.GetMembersAsync(projectId);

        Assert.Equal(200, result.Code);
        var ids = result.Data!.Select(m => m.UserId).ToList();
        // role 0 在前(200)，role 1 内部按 join 升序(300 早于 100)
        Assert.Equal(new List<long> { 200, 300, 100 }, ids);
    }

    #region Seed Helpers

    private static async Task<long> SeedProjectAsync(
        STOTOPDbContext db,
        string name,
        long managerId,
        int status = 0,
        DateTime? createTime = null)
    {
        var project = new TmProject
        {
            FName = name,
            FManagerId = managerId,
            FStatus = status,
            FCreatorId = 1,
            FCreateTime = createTime ?? DateTime.Now,
            FUpdateTime = createTime ?? DateTime.Now
            // FOrgId 不显式设置：SaveChanges 自动回填当前组织
        };
        db.Set<TmProject>().Add(project);
        await db.SaveChangesAsync();
        return project.FID;
    }

    private static async Task SeedMemberAsync(
        STOTOPDbContext db,
        long projectId,
        long userId,
        int role,
        DateTime joinTime)
    {
        db.Set<TmProjectMember>().Add(new TmProjectMember
        {
            FProjectId = projectId,
            FUserId = userId,
            FRole = role,
            FJoinTime = joinTime
        });
        await db.SaveChangesAsync();
    }

    #endregion
}
