# STOTOP 开发环境配置与工具工程化设计

日期：2026-06-12
状态：已确认，等待实施计划

## 目标

为 STOTOP 建立一套可重复、可诊断、可交接的本地开发工具链，让新机器可以按固定路径完成环境检查、依赖准备、启动、构建、测试和发布前检查；让日常开发可以用统一命令判断当前问题属于环境缺失、服务未启动、数据库配置缺失、构建失败、历史类型债，还是本次改动引入的阻塞。

首版目标是把现有可用经验产品化到仓库内，不引入 Docker、CI 平台或业务重构。

## 当前项目上下文

STOTOP 当前由后端 WebAPI、模块化 .NET 项目、Vite 前端和本地 SQL Server 开发库组成：

- 后端入口：`src/STOTOP.WebAPI/STOTOP.WebAPI.csproj`。
- 后端目标框架：`net10.0`。
- 后端本地默认端口：`http://localhost:5001`。
- 前端目录：`web`。
- 前端本地默认端口：`http://localhost:5173`。
- 前端开发代理：`/api`、`/hangfire`、`/hubs` 代理到 `http://127.0.0.1:5001`。
- 数据库连接文件：`src/STOTOP.WebAPI/db-connections.json`，该文件包含敏感信息，必须继续被 git 忽略。
- 数据库连接样例：`src/STOTOP.WebAPI/db-connections.example.json`。
- 当前可用脚本集中在 `scripts/dev`。
- 当前可用开发文档在 `docs/dev/mac-development.md`，根 README 和 `web/README.md` 仍未形成可靠入口。

当前前端构建信号需要明确拆分：`npm run build` 表示 Vite 生产构建，`npm run type-check` 暴露既有 TypeScript 类型债。除非本次工作明确处理类型债，否则 preflight 不能把历史类型债伪装成新引入的发布阻塞。

## 已选方案

采用“仓库内轻量工程化工具链”方案：

1. 保留 `scripts/dev` 作为开发脚本根目录。
2. 增加少量统一入口脚本，串联现有检查、构建和测试命令。
3. 补齐根 README、Web README、开发指南和编辑器配置。
4. 使用 `.editorconfig`、`global.json`、Node 版本提示和 VS Code 配置降低新环境差异。
5. 用分层检查命令表达风险，而不是用一个大命令吞掉所有历史债务。

这套方案能服务当前个人和小团队开发节奏，也不会把项目锁进额外平台。

## 非目标

本次不做以下事项：

- 不引入 Docker 或容器化数据库。
- 不新增 GitHub Actions、Gitee 流水线或其他远端 CI 配置。
- 不修复既有 `vue-tsc` 类型债。
- 不自动创建、重置或清空真实数据库。
- 不改业务模块、接口、菜单、权限或页面行为。
- 不改变生产发布方案；发布自动化继续按已有 IIS 发布设计独立推进。
- 不把敏感配置、真实数据库连接或服务器密钥写入仓库。

## 文件与工具边界

### 根目录配置

新增或调整：

- `.editorconfig`：统一缩进、换行、编码、末尾空行等基础格式。
- `global.json`：固定 .NET SDK 主版本为 `10.0`，允许安装补丁版本滚动。
- `.node-version` 或 `.nvmrc`：声明当前前端开发推荐 Node 主版本为 `24`。
- `.vscode/extensions.json`：推荐 C#、Vue、ESLint/TypeScript、EditorConfig 等开发扩展。
- `.vscode/tasks.json`：提供常用任务入口，调用仓库脚本而不是复制命令。
- `.vscode/settings.json`：保留已有 Windows 终端和索引排除设置，补充 macOS/Linux 友好的终端、文件监听和格式化默认项。

这些文件只表达开发约定，不承载敏感配置。

### 文档入口

更新：

- `README.md`：作为中文主入口，说明项目结构、必备工具、快速启动、常用命令、数据库配置、已知限制。
- `README.en.md`：作为简短英文入口，指向中文主文档和开发指南。
- `web/README.md`：替换 Vite 模板内容，说明前端启动、构建、类型检查、代理和常见问题。
- `docs/dev/mac-development.md`：保留 Mac 落地指南，补充统一脚本入口和 preflight 语义。

文档必须明确后端默认端口是 `5001`，前端默认端口是 `5173`。

### 脚本入口

保留现有脚本：

- `scripts/dev/check-env.sh`
- `scripts/dev/backend.sh`
- `scripts/dev/frontend.sh`
- `scripts/dev/build-backend.sh`
- `scripts/dev/build-frontend.sh`
- `scripts/dev/check-health.sh`
- `scripts/dev/check-sln.sh`
- `scripts/dev/init-database.sh`
- `scripts/dev/validate-database.sh`
- `scripts/dev/export-baseline.sh`

新增脚本：

- `scripts/dev/setup.sh`：检查必备工具、安装前端依赖、提示数据库配置状态，不自动写入真实连接。
- `scripts/dev/doctor.sh`：输出环境、端口、依赖、数据库配置、构建产物和常见风险的诊断摘要。
- `scripts/dev/build-all.sh`：顺序执行后端构建和前端 Vite 构建。
- `scripts/dev/test-contracts.sh`：执行 `scripts/tests/*.mjs` 下的 Node contract tests，支持按文件名过滤。
- `scripts/dev/test-dotnet.sh`：按模块执行 .NET 单测项目，先覆盖 Finance、CardFlow、Express 已有测试。
- `scripts/dev/preflight.sh`：执行提交或发布前本地检查，输出通过项、阻塞项和已知债务项。

所有脚本必须使用 `set -euo pipefail`，从脚本位置计算仓库根目录，不依赖调用者当前目录。

### Git 可执行位

`scripts/dev/*.sh` 必须在 Git 中记录为可执行文件，避免新机器克隆后不能直接运行 `./scripts/dev/*.sh`。

## 命令语义

### `setup.sh`

职责：

1. 调用或复用 `check-env.sh`。
2. 检查 `web/package-lock.json` 是否存在。
3. 如果 `web/node_modules` 缺失，运行 `npm install`。
4. 检查 `src/STOTOP.WebAPI/db-connections.json` 是否存在。
5. 如果数据库连接文件缺失，提示复制 `db-connections.example.json` 后自行填写真实连接。

`setup.sh` 不执行数据库初始化，不创建真实数据库，不覆盖已有配置。

### `doctor.sh`

职责：

1. 输出 .NET、Node、npm、Git、Xcode CLI 状态。
2. 检查后端端口 `5001` 是否已有监听。
3. 检查前端端口 `5173` 是否已有监听。
4. 检查数据库配置文件是否存在。
5. 检查前端依赖是否已安装。
6. 检查 `src/STOTOP.sln` 是否覆盖源码项目。
7. 给出下一步建议，例如运行 `setup.sh`、`backend.sh`、`frontend.sh`、`build-all.sh` 或 `preflight.sh`。

`doctor.sh` 是诊断命令，不应启动服务或修改文件。

### `build-all.sh`

职责：

1. 调用 `build-backend.sh`。
2. 调用 `build-frontend.sh`。

该脚本不运行 `npm run type-check`，因为当前仓库存在已知历史类型债。类型检查必须由显式命令暴露。

### `test-contracts.sh`

职责：

1. 默认执行 `scripts/tests/*.mjs` 下所有 Node contract tests。
2. 支持参数过滤，例如 `./scripts/dev/test-contracts.sh cardflow` 只执行文件名包含 `cardflow` 的测试。
3. 每个测试文件独立输出开始、成功、失败状态。
4. 任何测试失败时，最终退出码为非 0。

该脚本只跑静态或轻量 contract tests，不承担真实浏览器验收。

### `test-dotnet.sh`

职责：

1. 默认运行已有 .NET 测试项目：
   - `tests/STOTOP.Module.Finance.Tests/STOTOP.Module.Finance.Tests.csproj`
   - `tests/STOTOP.Module.CardFlow.Tests/STOTOP.Module.CardFlow.Tests.csproj`
   - `tests/STOTOP.Module.Express.Tests/STOTOP.Module.Express.Tests.csproj`
2. 支持参数过滤，例如 `./scripts/dev/test-dotnet.sh Finance`。
3. 使用 `dotnet test`，并关闭共享编译以降低本地编译进程干扰。

### `preflight.sh`

职责：

1. 执行 `doctor.sh`。
2. 执行 `check-sln.sh`。
3. 执行 `build-backend.sh`。
4. 执行 `build-frontend.sh`。
5. 执行 `test-contracts.sh`。
6. 执行 `test-dotnet.sh`。
7. 可选执行 `npm run type-check`，但结果归类为“类型债检查”，不与生产构建混淆。
8. 输出汇总表，清晰区分：
   - 通过项；
   - 阻塞项；
   - 已知债务项；
   - 跳过项。

如果后端构建、前端构建、contract tests 或 .NET 单测失败，`preflight.sh` 退出码必须为非 0。若只有类型债检查失败，首版可将退出码保持为 0，但必须在汇总里醒目标明。

## 服务启动约定

`backend.sh` 继续默认设置：

```text
ASPNETCORE_URLS=http://localhost:5001
DOTNET_hostBuilder__reloadConfigOnChange=false
ASPNETCORE_hostBuilder__reloadConfigOnChange=false
```

`frontend.sh` 继续默认启动 Vite `5173`，并在缺失依赖时安装前端依赖。

`check-health.sh` 继续检查 `http://localhost:5001/health`，并支持通过 `BACKEND_URL` 覆盖。

## 错误处理与输出风格

脚本输出必须满足：

- 缺少必要命令时，输出明确安装建议并退出 `127`。
- 缺少数据库配置时，说明样例路径和目标路径。
- 端口被占用时，提示具体端口和检查命令。
- 构建或测试失败时，保留原始命令输出，不吞掉错误。
- 汇总脚本只在最后输出结论，不提前宣称成功。

## 测试策略

本次工具链自身以脚本级验证为主：

1. 对新增脚本运行 `bash -n`。
2. 运行 `./scripts/dev/check-env.sh`。
3. 运行 `./scripts/dev/doctor.sh`。
4. 运行 `./scripts/dev/build-backend.sh`。
5. 运行 `./scripts/dev/build-frontend.sh`。
6. 运行 `./scripts/dev/test-contracts.sh cardflow`，确认过滤参数可用。
7. 运行 `./scripts/dev/test-dotnet.sh Finance`，确认 .NET 测试入口可用。
8. 运行 `./scripts/dev/preflight.sh`，记录通过项、阻塞项和已知债务项。
9. 运行 `git diff --check`，确认文档和脚本没有空白错误。

如果某项因为本地数据库、端口占用或外部依赖无法完整运行，最终交付必须说明实际输出和未完成原因。

## 交付标准

本次完成后，开发者应能从根目录执行以下路径：

```bash
./scripts/dev/setup.sh
./scripts/dev/doctor.sh
./scripts/dev/backend.sh
./scripts/dev/frontend.sh
./scripts/dev/build-all.sh
./scripts/dev/test-contracts.sh
./scripts/dev/test-dotnet.sh
./scripts/dev/preflight.sh
```

根 README 应能回答“我刚克隆项目，下一步怎么跑起来”。`docs/dev/mac-development.md` 应能回答“Mac 上遇到环境问题怎么查”。`web/README.md` 应能回答“前端为什么代理到 5001，以及 build 和 type-check 有什么区别”。

## 风险与缓解

- 风险：`preflight.sh` 首次运行耗时较长。
  缓解：提供分层脚本，允许开发者只运行当前模块相关检查。

- 风险：本地 SQL Server 状态差异导致数据库相关脚本失败。
  缓解：首版 preflight 不自动初始化数据库，数据库校验保持显式命令。

- 风险：历史 `vue-tsc` 类型债误导发布判断。
  缓解：文档和脚本明确拆分 Vite 构建与类型检查。

- 风险：工作树已有大量文档改动，容易把无关改动混入提交。
  缓解：实施时每次只暂存本次任务文件，并在最终说明中列出实际触达路径。
