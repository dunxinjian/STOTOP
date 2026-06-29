# STOTOP

STOTOP 是面向快递、财务、CRM、任务、质量、合同、行政后勤等场景的一体化企业管理系统。当前审批与卡片流转统一以 CardFlow 为运行时，历史 OA 控制器不再作为新业务入口暴露。

## 技术栈

- 后端：.NET 10、ASP.NET Core、EF Core、SQL Server、Hangfire、SignalR
- 前端：Vue 3、Vite、TypeScript、Pinia、Ant Design Vue、Vant
- 数据库连接：`db-connections.json` 是系统连接的运行时来源

## 本地开发

后端默认监听 `9000`，前端默认监听 `9001`。

**macOS / Linux：**

```bash
scripts/dev/backend.sh    # 后端
scripts/dev/frontend.sh   # 前端
curl http://localhost:9000/health && curl -I http://localhost:9001/
```

**Windows (PowerShell)：**

```powershell
scripts\dev\backend.ps1       # 后端
scripts\dev\frontend.ps1      # 前端
scripts\dev\check-health.ps1  # 健康检查
```

首次准备见 [Windows 本地开发](docs/dev/windows-development.md) / [Mac 本地开发](docs/dev/mac-development.md)。

## 当前边界

- CardFlow 承接动态表单、审批节点、待办、预算/财务流程钩子和导入校验工作台。
- Workflow 仍作为事件、派发、质量处理等底层协作能力保留。
- OA 项目与 Seeder 只保留历史数据迁移、引用兼容和退役清理用途，不再注册 OA 控制器。
- 旧 DataCenter 设计已并入 CardFlow 的导入、暂存、校验、自动插件能力，不应新增独立 `STOTOP.Module.DataCenter`。

## 文档入口

- [系统总览](design/00-overview.md)
- [WebAPI 启动层](design/19-webapi.md)
- [Windows 本地开发](docs/dev/windows-development.md)
- [Mac 本地开发](docs/dev/mac-development.md)
- [钉钉 H5 微应用部署](docs/dingtalk-h5-setup.md)
