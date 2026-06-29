# CardFlow 流程编辑器视觉债收敛（橙化 + 去绿）— 设计

> Phase 2 痛点页 #4（主巡检 spec §5 排序 #1，因最重故后置至基建成熟）。前三页：①报价模块视觉债(纯 C) ②毛利分析重做(A+B+C) ③合同模块 C+B。本页 = **C 视觉债令牌化 + reuse 复用收敛**，范围**整个流程定义编辑器模块 10 文件**。
> 日期：2026-06-19。复用 [[stotop-theme-token-system]] 的 TOKENS.md/PATTERNS.md 契约与共享组件（`PageHeader(backTo)`/`BaseCard`/`StatusTag`/`EmptyState`/`.page-section__title`）。
> 上游契约（只消费不重做）：`web/docs/TOKENS.md`（令牌真源）、`web/docs/PATTERNS.md`（页面骨架 + 共享组件契约）。
> 视觉方向：**克制收敛**——把这页自带的「绿青第二品牌」收编进全局「中性墨 + 橙只在交互点缀」体系。

---

## 1. 背景与真痛点（已实证，推翻旧假设）

本页 = `web/src/views/cardflow/FlowDefinitionEditPage.vue`（4412 行 / 141KB，6 步向导式流程定义编辑器），配套列表页 `FlowDefinitionListPage.vue`（882 行）及消费的 8 个 `components/cardflow/designer/*` 组件（约 3445 行）。经 9 代理多维审计 + 3 论断对抗式验证，确认：

- **旧 spec「蓝灰 / 蓝色视觉语言」被证伪**。品牌橙 `--color-primary` 在本页字面出现 **0 次**；占据全部「选中/激活/hover/聚焦」交互强调位的是绿青 `#1f6f5f` 一族（5 次纯色 + ~19 处 `rgba(31,111,95,*)` 低透明梯度）——它是事实上的**伪 primary**。蓝仅 7 处且角色受限（工具栏返回链接 `#1d4ed8` + 版本徽标 + saving 态），不承载主调。
- **绿青不是 success**。本页 success 另有专用 `#16a34a`/`#1f9d55`（saved 态、就绪 OK 图标）。`#1f6f5f` 干的是交互强调，职责与 `--color-primary` 完全重合。
- **中性轴被绿青污染成两套**。正文/边/底各有「纯灰」与「绿染」两版并存（绿染文字 ~30 处、绿染边 ~24 处、绿染底 ~24 处），是绿青强调溢出。
- **真语义色不可中性化**。保存状态条 4 态（saved 绿 / saving 蓝 / dirty 金 / error 红）、暖金告警面板（预演未就绪 warning）、红校验态，都是状态语义编码。
- **主页与 8 designer 内件「一个视觉系统拆成两半各自硬编码」**（confirmed）。绿青 `#1f6f5f`、墨黑 `#1f3029`、绿染次文字 `#75827c/#74817a` 在主页与 designer 树**逐字共享**；designer 树共 149 裸 hex + 15 rgba，仅 4 处 `var()`（半截令牌化未收口）。只改主页不动内件 → 主色基准变动时主页变了、画布/编目仍停绿青——复现 ContractList「主页改了内件没改」割裂。
- **列表页方向相反**——它本就更合规（几乎无绿青、已用语义令牌）。真割裂在**状态文案**：编辑页把 `archived` 显示为「已停用」（`FlowDefinitionEditPage.vue:1808`），列表页拆「已停用/已归档」两态——同值两页文案不同，是真 bug。
- **字形误用**：`saveStateText` 把 `●`/`✓` 当文本前缀与同行 Ant 矢量图标重复；拖拽手柄用 `⋮⋮` 字面字符。
- **复用阻碍（partially）**：StatusTag/BaseCard/EmptyState 三靶稳妥；但外壳 `.fdef-edit`→`PageLayout variant=flow` **不可直接换**——本页是 a-spin 锁定高度 + 内层独立内滚 + vue-flow 画布 `min-height:100%`，与 `.page-flow` 整页滚动**模型相反**，硬换画布塌缩。

## 2. 用户决策（拍板记录）

1. **绿青走向 = 橙化 + 去绿**（A）。绿青伪主色收敛为 `--color-primary`，绿染中性一并去绿。看过橙化前后对比图后确认（先选保留绿、复看后改定橙化）。
2. **工单范围 = 整模块**（主页 + 8 designer + 列表页）。
3. **列表页状态快筛 StatFilterTabs（B）本轮不做**，依赖后端 statistics 端点，单独排期。
4. **A 类结构性重做（含外壳迁移）全部另排期**。外壳 `.fdef-edit` 本轮保留不动（逃生路径）。
5. **绿染中性 = 去绿→中性令牌**（单一中性轴），不随业务色保留。

## 3. 令牌架构（三分映射，橙化版）

**零新增令牌**——`--color-primary` 体系（含 `-light`/`-border`）已够；低透明各档用既有 `color-mix` 惯例表达（项目内 QualityAlertBar/WorkHubCenter/TriggerActionPanel 已用），好处是强调底**自动跟随运行时品牌色**（主色来自后端 DefaultConfigJson，color-mix 联动）。

### ① 橙化（绿青伪主色 → `--color-primary` 体系）

| 用途 | 现状（绿青） | → 橙化目标 |
|---|---|---|
| 实色强调（选中边/激活态文字/图标 hover/聚焦态） | `#1f6f5f` | `var(--color-primary)` |
| 强调底（选中节点底/激活底） | `rgba(31,111,95,.08~.1)` | `var(--color-primary-light)` 或 `color-mix(in srgb, var(--color-primary) N%, transparent)` |
| 焦点环/描边/强调阴影 | `rgba(31,111,95,.18~.55)` | `var(--color-primary-border)` 或 `color-mix` 对应档 |
| 画布网格线 | `rgba(31,111,95,.05)`（绿调） | **中性**网格线 `color-mix(in srgb, var(--text-3) 10%, transparent)`（去装饰、不染橙） |

### ② 去绿 + 中性归一（绿染 ∪ 纯灰 → 单一中性轴）

见 §5 映射表。原则：绿染中性是强调溢出，全部回中性令牌；与同位纯灰统一到同一令牌。

### ③ 真语义色（保留语义，只换源）

见 §5 映射表。状态条/告警/校验/链接各自映射到 success/info/warning/danger 令牌；蓝返回键改 `PageHeader backTo`（消除全站唯一蓝来源）。

### 画布 SVG 例外

`FlowStateCanvas` 的 `#2878a8`（条件分支边）/`#475569`（默认边）是图论语义色，vue-flow 不解析 `var()`——收成组件内 `const` 常量去重（现 JS `stroke` 对象与 CSS 各写一遍），按 SVG 豁免不套令牌。但 `#1f6f5f` 的 JS stroke **同步改主色真值**，保持画布选中态与 CSS 一致。

## 4. 范围分层（参照 ContractList「整模块避割裂」）

| 层 | 文件 | 本轮做什么 |
|---|---|---|
| **Tier 1 主页** | `FlowDefinitionEditPage.vue` | §5 三分映射全套 + 字形清理(C11-C13) + 复用收敛(R1/R3/R4/R5/R6) + B2 版本 pill 显示逻辑 |
| **Tier 2 必带** | 8 designer 组件¹ | 同口径三分映射（149 裸 hex）；`FlowStateCanvas` JS/SVG stroke 收 const；`CardComponentConfigDrawer` a-tag 字面色→StatusTag。**铁证同源、不可拆** |
| **列表页** | `FlowDefinitionListPage.vue` | C14 中性 hex + `a-tag color="blue"`→StatusTag(info)；R8 状态渲染→StatusTag；R9 空态→EmptyState；R10 抽 `flowStatusMeta` 共享常量根治两页文案分叉（顺带修编辑页 archived 误显示 bug）；**保留 a-table**（不迁 DataTable） |
| **明确不做（A 类另排期）** | — | A1 单文件拆分 / A2 外壳 `.fdef-edit`→PageLayout 迁移（需 PageLayout 加 `flow-locked` variant）/ A3 预览三入口收敛 / A4 就绪面板按钮双用 / A5 步骤内双层 tab / 列表页 StatFilterTabs（依赖后端 statistics 端点） |

¹ `FlowStateCanvas / RouteRuleCardEditor / DynamicApprovalPolicyEditor / PathPreviewPanel / RuleHealthPanel / CardComponentCatalog / CardComponentConfigDrawer / StageComponentViewEditor`

## 5. 完整 hex→令牌映射表

### ① 橙化

| 现状 | → 令牌 | 用途 |
|---|---|---|
| `#1f6f5f`（实色） | `var(--color-primary)` | 选中边/激活文字/图标 hover/强调标题 |
| `rgba(31,111,95,.05)` | `color-mix(in srgb, var(--text-3) 10%, transparent)` | 画布网格线（去染） |
| `rgba(31,111,95,.08/.1)` | `var(--color-primary-light)` 或 `color-mix(…N%…)` | 选中节点底/激活底 |
| `rgba(31,111,95,.18/.2/.32/.4/.55)` | `var(--color-primary-border)` 或 `color-mix` 对应档 | 焦点环/描边/强调阴影 |

### ② 去绿 + 中性归一

| 现状（绿染 / 纯灰） | → 令牌 |
|---|---|
| `#1f3029`·`#22332c`·`#18241f` / `#111827`·`#1f2937` | `--text-1` |
| `#718078`·`#66756e`·`#74817a` / `#4b5563`·`#667085` | `--text-2` |
| `#5f6f67`·`#a6b0ab` / `#9ca3af`·`#8b95a1` | `--text-3` |
| `#d7e3df`·`#e6ebe8`·`#bfd2c9` / `#e5e7eb`·`#eef0f3` | `--border`（强边 `#c4cad3`/`#d4d8e0`→`--border-strong`） |
| `#f7faf9`·`#f8fbfa`·`#edf1ef` / `#fafbfc`·`#f8fafc` | `--bg-page` / `--bg-muted` |
| `#fff`·`#ffffff` | `--bg-card` |
| `rgba(23,37,31,.08)`·`rgba(25,39,32,.12)` / `rgba(0,0,0,.06)`·`rgba(15,23,42,.04)` | `--shadow-sm` / `--shadow-md` |

### ③ 语义色

| 现状 | → 令牌 | 用途 |
|---|---|---|
| `#16a34a`/`#1f9d55`/`#edf7f2` | `--color-success`/`-text`/`-light` | saved 态、就绪 OK 图标 |
| `#2563eb`/`#1d4ed8`/`#eff6ff`/`#bfdbfe`/`#1e40af` | `--color-info`/`-light`/`-text` | saving 态、链接、版本徽标 |
| `#fffaf0`/`#f0c98e`/`#8a5e14`·`#b45309`/`#d97706` | `--color-warning-light`/`-warning`/`-text` | 暖金告警面板、dirty 态 |
| `#ef4444`/`#fecaca`·`#fff8f8`/`rgba(239,68,68,.12)` | `--color-danger`/`-light`/`-border` | 必填星（内联 `:1775`）、error 态、校验错误环 |
| 返回键 `#1d4ed8` + `rgba(29,78,216,.06)` | `PageHeader backTo`（中性 + 橙 hover） | 消除全站唯一蓝来源 |

## 6. 非颜色工作项

### 6.1 复用收敛（reuse）

| # | 现状 | → 靶 | 风险 |
|---|---|---|---|
| R1 | 工具栏手搓 `<button class=tb-back>`（蓝，违反 backTo 契约） | `PageHeader backTo`（参 QuotationEdit） | 低 |
| R3 | `a-tag` 字面色 + `runtimeAccessColor()` 颜色工厂 | `StatusTag`（access→type；**`'blue'`→info 需确认**，非机械替换） | 低 |
| R4 | 三类手搓卡片面板（bg+border+radius+head 三件套，`:3848`/`:4146`/`:3047`） | `BaseCard`（title/#extra）；轻量容器用 `.page-card` | 中 |
| R5 | 区块小标题 `__head` 多处手搓 | `.page-section__title` / BaseCard #title | 低 |
| R6 | 四处手搓空态（`:1963`/`:2185`/`:2261`·`:2562`/`:2493`·`:2507`） | `EmptyState`（size=small；富空态外层占位+图标+主操作） | 中 |
| R8 | 列表页状态 `status-dot+status-text` 手搓 | `StatusTag`（dot 属性 + type） | 低 |
| R9 | 列表页空态 `.empty-guide`（冷蓝白渐变） | `EmptyState` | 低 |
| R10 | 两页 status 文案分叉（archived 误显示「已停用」） | 抽 `flowStatusMeta` 共享常量两页共用，**顺带修 bug** | 低 |

> R2（撤销/重做 chrome 重造）功能已对、仅外观，**列为可选低优**，不强做以免无谓回归。

### 6.2 字形清理 + 显示逻辑

- **C11**：`saveStateText`（`:228-229`）剥掉 `●`/`✓` 字形（同行已有 Ant 矢量图标，现双重符号）。
- **C12**：拖拽手柄 `⋮⋮` 字面字符（`:1924`）→ `HolderOutlined`。
- **C13**：字段引导条箭头字符（`:1826`/`:1831`）→ `RightOutlined`。
- **B2**：版本 pill（`:1688-1696` 双条件 v-if）改为**总显示当前编辑态**（至少「草稿」徽标），消除新流程/纯草稿版本意识真空。

## 7. 执行方式

1. **零新增令牌**——跳过令牌定义阶段。
2. **主页**（Tier 1）：三分映射 + 字形 + backTo + StatusTag + B2 + BaseCard/EmptyState 容器收敛——体量最大，单独成阶段；建议「色彩+字形+backTo+StatusTag」先行（低风险高密度），「BaseCard/EmptyState 容器 swap」作子阶段。
3. **8 designer 组件**（Tier 2）：同口径三分映射 + FlowStateCanvas const 收口——**后台工作流并行分文件**（互不耦合）。
4. **列表页**：C14 + StatusTag + EmptyState + `flowStatusMeta` 共享常量 + archived bug 修。
5. **中央复验** → **用户 9001 live HMR 逐页**（不自起第二 vite 实例，避免 .vite 缓存双 Vue 报错）。

## 8. 验证口径

- **前端** `vite build` 绿（`vue-tsc` 基线红可过滤，见 [[frontend-typecheck-baseline-red]]）。
- **改动文件 `stylelint color-no-hex` 0 报错**。
- **`rg` 对 10 文件复扫颜色清零**——含 inline `style` / JS `stroke` / `a-tag` 字面色 / ECharts 等 **stylelint 盲区**（真元凶，报价页教训：stylelint 抓不到 template 内联/JS）。
- **纯前端无后端改动**：`flowStatusMeta` 是前端常量，无 dotnet/单测需求。
- **用户 live 逐页**：编辑器 6 步（橙化观感/选中态/状态条/告警/字形）+ designer 内件（画布/编目/抽屉）+ 列表页（状态 tag/空态/两页文案一致）。

## 9. 明确不做（重申）

A1 单文件拆分 / A2 外壳 `.fdef-edit`→PageLayout 迁移（需 PageLayout 加 `flow-locked` variant）/ A3 预览三入口收敛 / A4 就绪面板按钮双用 / A5 步骤内双层 tab / 列表页 StatFilterTabs（依赖后端 statistics 端点）/ R2 撤销重做 chrome 重造 / cardflow 模块标识色不一致（主页借用 `--biz-waybill` 紫 vs 编辑器绿，属另一观察项）。外壳 `.fdef-edit` 本轮**保留不动**。

---

> 下一步：本 spec 经用户复审后，进入 `writing-plans` 产出分阶段实现计划（主页 → designer 并行 → 列表页 → 复验）。
