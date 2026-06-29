# STOTOP IIS 发布与版本管理设计

日期：2026-06-09
状态：已确认，进入实施计划阶段

## 目标

为 STOTOP 建立一套简单、可重复、可回滚的发布流程：

- 先发布到 IIS 测试服务器；
- 测试人员验证该版本；
- 测试通过后，将同一个发布包晋级到正式 IIS 服务器；
- 按版本归档发布包；
- 在每台服务器上支持一键发布和一键回滚。

首版实现面向 Windows Server + IIS。发布包上传到服务器后，在服务器本机执行 PowerShell 发布和回滚脚本。

## 当前项目上下文

STOTOP 由 .NET WebAPI 后端和 Vite 前端组成：

- 后端项目：`src/STOTOP.WebAPI/STOTOP.WebAPI.csproj`；
- 前端项目：`web`；
- 前端生产 API 基础路径：`/api`；
- 后端健康检查端点：`/health`；
- 后端版本端点：`/api/version`；
- 后端已有 IIS `web.config`；
- 生产配置和数据库连接文件已被 git 忽略，必须由各环境服务器自行维护。

当前前端构建命令 `npm run build` 只执行 Vite 构建。既有 TypeScript 债务通过 `npm run type-check` 单独暴露；除非本次改动引入新的类型错误，否则首版部署自动化不把既有类型债务作为发布阻塞项。

## 已选方案

采用“版本目录切换”方案：每次发布解压到独立版本目录，然后切换 IIS 站点物理路径。

示例服务器目录：

```text
D:\STOTOP\
  packages\
    STOTOP-v1.2.3+abc1234.zip
  releases\
    v1.2.2\
    v1.2.3\
  shared\
    appsettings.Production.json
    db-connections.json
    backup-config.json
    uploads\
    logs\
  deploy-history.jsonl
  current-version.txt
```

每次发布将发布包解压到新的不可变版本目录。IIS 站点指向当前生效的版本目录。回滚时只需把 IIS 站点物理路径切回上一个成功版本目录。

这样可以避免覆盖式发布导致的新旧文件混杂，也能保证测试环境验证过的包就是正式环境发布的同一个包。

## 发布包

本地打包脚本每个版本生成一个 zip。

包名格式：

```text
STOTOP-vX.Y.Z+<short-git-sha>.zip
```

包内结构：

```text
release-manifest.json
site\
  STOTOP.WebAPI.dll
  web.config
  appsettings.json
  appsettings.Production.template.json
  wwwroot\
    index.html
    mobile.html
    redirect.html
    assets\
```

发布包不能包含服务器专属敏感信息：

- 不包含真实 `db-connections.json`；
- 不包含真实 `backup-config.json`；
- 不包含真实生产环境密钥覆盖配置。

发布脚本从 `D:\STOTOP\shared` 将环境自有配置复制或链接到当前发布目录。

## 版本规则

发布版本来自 Git tag。

预期 tag 格式：

```text
v1.2.3
```

打包脚本将以下元数据写入 `release-manifest.json`：

- 版本号；
- Git commit SHA；
- Git 分支；
- 构建时间；
- 发布包名称；
- 后端目标框架；
- 前端 package 版本；
- 构建机器；
- 发布包校验值。

打包阶段还需要将 `AppVersion` 写入后端发布结果，使 `/api/version` 返回实际部署版本和运行环境。如果首版中构建时注入不够顺手，也可以由发布脚本写入环境自有版本文件或 appsettings 覆盖项，但部署后 `/api/version` 必须能显示发布版本。

## IIS 形态

测试环境和正式环境使用同样的 IIS 形态：

- 每个环境一个 IIS 站点；
- 每个站点一个应用程序池；
- 前端和后端由同一个站点提供；
- 浏览器访问站点根路径；
- API 请求走 `/api`；
- SignalR 走 `/hubs`；
- Hangfire 走 `/hangfire`。

发布结果必须支持前端深层路由刷新。IIS 重写规则需要把非文件、非 API、非 hubs、非 Hangfire 的路径回退到 `index.html`。

现有 `web.config` 可以继续作为后端托管入口，但最终发布站点也必须从 `wwwroot` 提供 Vite 构建产物。

## 发布脚本

脚本路径：

```text
scripts/deploy/deploy-iis.ps1
```

服务器执行示例：

```powershell
.\deploy-iis.ps1 -Environment test -Package D:\STOTOP\packages\STOTOP-v1.2.3+abc1234.zip
.\deploy-iis.ps1 -Environment prod -Package D:\STOTOP\packages\STOTOP-v1.2.3+abc1234.zip
```

脚本职责：

1. 校验 PowerShell 是否以管理员身份运行。
2. 校验 zip 是否存在，且包含 `release-manifest.json`。
3. 校验目标 IIS 站点和应用程序池是否存在。
4. 停止应用程序池。
5. 将发布包解压到 `D:\STOTOP\releases\<version>`。
6. 从 `D:\STOTOP\shared` 复制必要的环境自有配置。
7. 确保 `uploads` 和 `logs` 使用共享运行时存储。
8. 将 IIS 站点物理路径切换到新版本目录。
9. 启动应用程序池。
10. 检查 `/health`。
11. 检查 `/api/version` 是否匹配 manifest 版本。
12. 将成功或失败事件追加写入 `deploy-history.jsonl`。
13. 只有健康检查和版本检查通过后，才更新 `current-version.txt`。

如果发布在站点路径切换前失败，当前线上版本保持不变。如果发布在路径切换后失败，脚本应尝试切回上一个活动版本，并记录失败事件。

## 回滚脚本

脚本路径：

```text
scripts/deploy/rollback-iis.ps1
```

服务器执行示例：

```powershell
.\rollback-iis.ps1 -Environment prod
.\rollback-iis.ps1 -Environment prod -Version v1.2.2
```

脚本职责：

1. 如果未提供 `-Version`，从 `deploy-history.jsonl` 找到上一个成功发布版本。
2. 校验目标回滚版本目录是否存在。
3. 停止应用程序池。
4. 将 IIS 站点物理路径切换到回滚版本目录。
5. 启动应用程序池。
6. 检查 `/health`。
7. 检查 `/api/version`。
8. 将回滚事件追加写入 `deploy-history.jsonl`。
9. 验证通过后更新 `current-version.txt`。

回滚不修改数据库。因此，首版发布自动化阶段要求数据库变更保持向后兼容。

## 环境配置

每台服务器在 `D:\STOTOP\shared` 下维护自己的环境配置。

必需共享文件：

- `appsettings.Production.json`；
- `db-connections.json`。

可选共享文件：

- `backup-config.json`；
- 当前服务器使用的证书、钉钉等额外配置文件；
- `uploads`；
- `logs`。

发布脚本可以在缺失时创建 `uploads` 和 `logs` 目录，但不能伪造缺失的数据库连接或密钥配置。

## 晋级流程

1. 开发者创建或选择 Git tag，例如 `v1.2.3`。
2. 开发者在本地运行打包脚本。
3. 将发布包上传到测试服务器。
4. 测试服务器执行 `deploy-iis.ps1 -Environment test -Package ...`。
5. 测试人员在测试站点验证业务流程。
6. 将同一个 zip 上传到正式服务器。
7. 正式服务器执行 `deploy-iis.ps1 -Environment prod -Package ...`。
8. 正式环境自动完成健康检查和版本检查。

测试通过到正式发布之间不允许重新构建。

## 验证要求

打包验证：

- 后端 `dotnet publish` 成功；
- 前端 `npm run build` 成功；
- `release-manifest.json` 存在，且包含版本和 commit；
- 发布包校验值已记录。

发布验证：

- IIS 应用程序池启动成功；
- `/health` 返回健康；
- `/api/version` 返回发布版本；
- 站点根路径返回 HTML；
- 发布历史记录本次操作。

发布后的人工冒烟测试：

- 登录页可加载；
- 用户可以登录；
- 一个代表性的 PC 页面可加载；
- 如果本次发布涉及移动端或 CardFlow，则验证一个代表性的移动端/CardFlow 页面；
- 如果本次发布涉及文件功能，则验证上传/下载仍写入共享存储。

## 版本保留

每台服务器至少保留最近 5 个成功发布版本。

首版脚本默认不自动删除旧版本，除非显式提供保留清理参数。等发布历史机制稳定后，再增加默认自动清理也不迟。

## 安全与权限

脚本假设：

- 以管理员身份运行；
- IIS PowerShell 模块可用；
- 应用程序池身份可以读取当前发布目录；
- 应用程序池身份可以读取共享配置文件；
- 应用程序池身份可以写入共享 `uploads` 和 `logs`；
- 首版中发布包上传由人工或其他外部方式完成。

密钥留在服务器，不提交到 git，也不写入发布包。

## 首版不包含

- GitHub Actions 或其他完整 CI/CD；
- 从 Mac 开发机直接远程发布；
- Docker；
- 数据库回滚；
- 蓝绿流量切换；
- 服务器端自动浏览器冒烟测试；
- 默认自动清理旧发布版本。

这些能力可以在基础的“测试到正式晋级”流程稳定后继续追加。

## 实施计划需确定的细节

实施计划阶段需要明确：

- Windows 基础目录默认值；
- 测试和正式 IIS 站点名称、应用程序池名称；
- 配置文件复制到每个版本目录，还是从 `shared` 链接；
- `/api/version` 的 `AppVersion` 注入方式；
- IIS 重写规则写入 `web.config`，还是要求安装 URL Rewrite 模块。

推荐默认值：

- 基础目录：`D:\STOTOP`；
- 小型配置文件在发布时复制到当前版本目录；
- `uploads` 和 `logs` 使用共享运行时目录；
- 通过打包或发布阶段生成 appsettings 覆盖项注入版本号；
- 如服务器安装 URL Rewrite，则支持前端深层路由重写，同时排除 API、hubs、Hangfire 和静态文件路径。
