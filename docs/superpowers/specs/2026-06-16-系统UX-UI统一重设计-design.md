# 系统 UX/UI 统一重设计 — 设计方案（Spec）

- 日期：2026-06-16
- 范围：登录、主页工作台、全局搜索、主框架外壳、移动端，贯穿全系统的**配色 / 主题令牌**，以及**全站所有功能页的风格统一**
- 目标基调：企业内部办公，**严谨而非活泼、干净整洁、舒适雅致、全系统风格统一**

---

## 0. 背景（审计结论）

多智能体审计（6 agent，覆盖登录 / 主页 / 搜索 / 主题令牌 / 外壳）确认：**"风格不统一"的根因不在某个页面，而在没有配色单一真源**。

- **主色三套打架**：真正生效的是申通橙 `#FF6700`（`stores/theme.ts` 注入 `<a-config-provider>`）；但 ant-design 默认蓝 `#1677ff` 被**硬编码 221 处、横跨 86 个文件**；后台又用紫 `#722ED1` 整套换肤；死文件 `config/theme.ts` 还写着第三种蓝 `#409eff`（全仓零 import，纯维护陷阱）。改一次品牌色，80% 交互元素纹丝不动。
- **同义多值发散**：金黄 4 值、红 2 值、蓝 3 值、内容区灰底 3 值、侧栏底色 2 值；圆角 / 阴影 / 字号 / 间距各写各的。
- **活泼包袱集中在登录页**：四季粒子动画、物流路线 SVG、几何装饰环、光斑、渐变 Logo/按钮。
- **令牌层物理割裂**：`variables.scss`（编译期）↔ `stores/theme.ts`（运行时）↔ 221 处写死 hex，三者不联动；运行时"颜色配置"只注入了侧栏 4 个 CSS 变量。

## 1. 已确认的决策

| # | 决策点 | 选择 |
|---|---|---|
| D1 | 主色方向 | **方向A · 暖橙收敛**：保留申通橙做克制强调色，整体中性化；靠"克制"而非换色达成严谨雅致 |
| D2 | 登录页装饰 | **去掉全部动画与装饰**（四季粒子 / 物流 SVG / 几何环 / 光斑 / 渐变） |
| D3 | 登录左品牌区基调 | **精致深墨**（`#1F2430` 静态深墨左栏 + 右白表单）；过渡屏承担"由深转浅"桥接 |
| D4 | 前台橙 / 后台紫双主题 | **统一单主色，中性区分**：拆掉后台紫主题，前后台同一套令牌；环境差异改用"顶栏深一档 + 文案标识" |
| D5 | 落地节奏 | **一次性全量改造**（去蓝 + 拆紫 + 重做登录，一轮内协调完成） |
| D6 | 深色模式 | **只做 dark-ready**：令牌做成支持深色的结构，本轮**不交付**整套深色皮肤 |
| D7 | 移动端（oa-mobile / vant） | **本轮一并对齐**（令牌打通 + 主色 / 状态 / 表面对齐；逐组件精修可留后续） |
| D8 | 全站功能页风格统一深度 | **逐模块巡检对齐**：先建设计系统层（共享组件 + 全局覆盖，全站自动继承），再对每个业务模块逐页巡检收敛偏离项 |

## 2. 令牌架构（地基）

所有视觉都挂在令牌上。这是本方案的核心，先于一切视觉改动。

### 2.1 单一真源
- **`stores/theme.ts`** 为运行时权威真源；**`variables.scss`** 同值仅作编译期回退（二者用同一组字面量）。
- **删除** `web/src/config/theme.ts`（死代码、值错误、零 import）。
- 产出 **`web/docs/TOKENS.md`**：列举全部令牌及映射关系，作为团队约定。

### 2.2 CSS 变量桥（让运行时主题真正全局生效）
store 启动时把**完整令牌集**注入 `:root`（而非现状只注入侧栏 4 个变量）：

```
--color-primary / -hover / -active / -light / -border
--color-success / -warning / -danger / -info（含各自 -light / -text）
--text-1 / -2 / -3 / -disabled
--bg-page / -card / -muted
--border / -border-strong
--topbar-ink / -topbar-ink-admin / -topbar-border
--sidebar-bg / -item-hover / -item-active-bg / -item-active-text / -active-indicator
--biz-waybill / -contract / -quality / -approval / -points / -finance
--radius-sm / -md / -lg / -modal / -pill
--shadow-sm / -md / -lg
--font-xs / -sm / -sm2 / -base / -lg / -xl / -2xl
--space-2xs / -xs / -sm / -md / -lg / -xl
```

- 组件 SCSS 一律从 `$字面量 / 写死 hex` 改用 `var(--token)`。
- antd 通过 `ConfigProvider` 注入对应 token；vant 通过 `--van-*` 变量桥映射（见 §3.5）。
- 同步 antd `ConfigProvider` 的 `cssVar: true`，使 antd 组件也吃同一套 CSS 变量。

### 2.3 去蓝（221 处）
按语义二分，**不是无脑替换**：
- **交互态**（按钮 / 链接 / 选中 / 激活）→ `var(--color-primary)`
- **纯信息态**（信息标签 / 中性强调 / 审批来源等）→ `var(--color-info)` 或对应 `--biz-*`
- 手段：codemod 批量定位 + **逐处人工判定语义** + `stylelint` 规则禁止组件内裸 hex 兜底防回潮。
- 覆盖 `#1677ff` / `#1890ff` / 死文件 `#409eff`。

### 2.4 同义多值收敛
- 金黄 4→1（`--color-warning`）、红 2→1（`--color-danger`）、内容区灰 3→1（`--bg-page`）、侧栏底色 2→1（`--sidebar-bg`）。

### 2.5 业务语义色板 `--biz-*`（与品牌主色解耦）
运单 / 合同 / 质量 / 审批 / 积分 / 财务 各一色，降饱和、相互可辨；**紫 `#722ED1` 退役**——不再兼任后台主题，仅作"运单"业务色且降饱和。

## 3. 配色规范（方向A · 核心令牌）

> 下表为提案值，最终细调在实现期的 TOKENS.md 中收口；约束是必须满足 §4 的 WCAG AA。

### 3.1 主色与中性
| 令牌 | 值 | 用途 |
|---|---|---|
| `--color-primary` | `#E85E00` | 链接 / 激活竖条 / 选中 / 图标 / 主按钮填充（比 #FF6700 深一档，文本级达 AA） |
| `--color-primary-hover` | `#FF6700` | 悬停、品牌标识点 |
| `--color-primary-active` | `#C94E00` | 按下态 |
| `--color-primary-light` | `#FFF3EA` | 选中行 / hover 浅底 |
| `--bg-page` | `#F5F6F8` | 页底（统一三种灰底） |
| `--bg-card` | `#FFFFFF` | 卡片 |
| `--bg-muted` | `#EEF0F3` | 表头 / 次级填充 |
| `--border` / `--border-strong` | `#E6E8EB` / `#D6D9DD` | 分隔 / 强分隔 |
| `--text-1 / -2 / -3` | `#1F2329` / `#5A6068` / `#8A9099` | 主 / 次 / 辅文字 |

### 3.2 外壳
| 令牌 | 值 | 用途 |
|---|---|---|
| `--topbar-ink` | `#1F2430` | 前台顶栏深墨（去纯黑、去光斑） |
| `--topbar-ink-admin` | `#171A22` | 后台顶栏深一档（中性区分，替代紫） |
| `--topbar-border` | `rgba(255,255,255,0.10)` | 顶栏底线（比现 0.06 略可见） |
| `--sidebar-bg` | `#EDEEF1` | 侧栏单一底色（收口 `#E2E2E2` / `#e4e7ef`） |
| `--sidebar-item-hover` | `rgba(0,0,0,0.05)` | 可感知的 hover 反馈 |
| `--sidebar-item-active-bg / -text / -indicator` | `--color-primary-light / --color-primary / --color-primary` | 激活态 |

### 3.3 状态色（全系统统一）
| 语义 | 主 | 浅底 | 文字 |
|---|---|---|---|
| success | `#2BA471` | `#E7F5EF` | `#0F6E56` |
| warning | `#E6A700` | `#FBF1D8` | `#8A6200` |
| danger | `#E5484D` | `#FCEBEC` | `#A3282C` |
| info | `#3A6FB0` | `#E9F0F8` | `#1C4366` |

### 3.4 业务语义色 `--biz-*`（降饱和、互辨）
| 令牌 | 值 | 业务 |
|---|---|---|
| `--biz-waybill` | `#6B4FB0` | 运单 / CardFlow（退役的紫，降饱和） |
| `--biz-contract` | `#8A6D3B` | 合同 |
| `--biz-quality` | `#D9603A` | 质量异常（与品牌橙拉开） |
| `--biz-approval` | `#3A6FB0` | 审批（= info） |
| `--biz-points` | `#C99A2E` | 积分 |
| `--biz-finance` | `#B8860B` | 财务 |

## 4. 横切规范

- **WCAG AA**：正文文本对比 ≥ 4.5:1，大字 / 图形 ≥ 3:1。主按钮用 `--color-primary` 填充 + 白色**中黑体**（大字达 AA）；正文链接 / 小图标用 `#E85E00`。
- **焦点**：`focus-visible` 统一橙色描边环（取代当前蓝色焦点光晕 `rgba(22,119,255,0.1)`）。
- **圆角**：`--radius-sm 4 / -md 6 / -lg 8`，弹窗 `--radius-modal 12`；收口现状（搜索 12 / 卡片 8 / 导航 6 / 后台无圆角）。
- **阴影**：三级 `--shadow-sm/md/lg`，弹窗 / 浮层统一 `--shadow-lg`；去掉无文档的多级自定义阴影与多余 glassmorphism。
- **字号**：沿用现有刻度（11/12/13/14/16/18/24）令牌化，组件一律引用 `--font-*`，禁止硬编码 13.5/10 等。
- **间距**：双轨择一——以 4 基数刻度为准，antd `margin*` / `padding*` 组件 token 与 SCSS `$spacing-*` 对齐同值（避免改一处不影响另一处）。

## 5. 各屏重设计

### 5.1 登录（重做 · 精致深墨）
文件：`web/src/views/login/index.vue`、`web/src/utils/seasonTheme.ts`、`web/src/components/OrgSelectModal.vue`
- **删除**：四季粒子动画及全部 keyframe、物流路线 SVG、几何装饰环 `.brand-decoration`、`.login-brand` 与过渡屏的光斑 `::before/::after`、渐变 Logo 文字、渐变按钮辉光、表单区顶部渐变线。
- **删除 / 退役** `seasonTheme.ts`（独立季节色系，不再使用）。
- **保留**：左栏深墨 `--topbar-ink #1F2430`（静态）+ Logo（纯白文字或 logo 图，不做渐变裁切）+ tagline + 3 条业务亮点（简化为朴素文本行）+ 页脚；右白表单。
- 焦点环改橙；主按钮 `--color-primary` 实色填充（无渐变 / 无 translateY 辉光）。
- `OrgSelectModal` 去 `#1677ff` → 令牌。
- **过渡屏**：保留深墨 `#1F2430`（桥接深登录 → 浅系统）、去光斑、放慢步骤切换（450ms → ~700ms）、保留极简步骤列表；结束时淡入浅色系统。

### 5.2 主页工作台
文件：`web/src/views/workhub/WorkHubCenter.vue`、`WorkItemCard.vue`、`WorkHubRecentVisits.vue`、`QualitySummaryCard.vue`、`QualityAlertBar.vue`、`index.vue`
- **降卡片密度**：`WorkItemCard` 建立清晰层级（标题 14/500、摘要 `--text-2`、元信息 `--text-3`），加大纵向呼吸（8px → `--space` 令牌），次要操作 hover 显现，限制可见标签数。
- 页底统一 `--bg-page`（收口 `#f4f6f8`）。
- Tab 徽章**优先级分层**：急 = `danger` / 待办 = `primary` / 通知 = 中性或 `info`。
- "仓配工作带"装饰条纹 → **可点击入口**（运单 / 入库 / 异常 / 结算导航）；若不导航则移除。
- "最近访问"(pill) 与"常用功能"(row) → 统一为一致列表样式。
- 来源色挂 `--biz-*` 并修对比度（提高浅底可视度）。
- 统计栏与空状态顺序、"新消息横幅 vs 统计栏"概念重复 → 收敛（设计层提示）。

### 5.3 全局搜索 / 命令面板
文件：`web/src/components/GlobalSearch.vue`、`web/src/composables/useCommandPalette.ts`、`web/src/layouts/TopBar.vue`
- 删除组件内本地 `$brand / $brand-bg` → 全面迁令牌；字号 / 圆角(12→`--radius-modal`) / 阴影(→`--shadow-lg`) 令牌化。
- 修 active 项 `3px 边框 + padding 补偿` 的位移 hack → 改整行底色 `--color-primary-light` + 不引发布局位移的指示（inset 或预留透明边框）。
- 区分"推荐 vs hover"（现差异仅 ~0.03）。
- `slice(0,20)` 硬截断 → "显示更多" / 虚拟列表，并提示被截断。
- 修焦点视觉反馈、修搜索 / 浏览两套索引切换时的聚焦同步。
- 记录并核查 `allFlatMenus` 权限缓存依赖链（组织切换时结果正确性）。

### 5.4 外壳（顶栏 + 侧栏）
文件：`web/src/layouts/MainLayout.vue`、`AdminLayout.vue`、`TopBar.vue`、`SmartSidebar.vue`、`web/src/styles/layout.scss`
- **后台紫主题退役**：AdminLayout 改用与前台同一套令牌驱动；后台区分 = `--topbar-ink-admin`（深一档）+ "管理后台"文案标识，**移除** `linear-gradient(135deg,#722ED1,…)` 与紫色激活态。
- **前后台统一**：圆角(6) / 分组标题字号(令牌) / 菜单项高度密度(36) / hover 反馈可感知度 / 激活指示，全部对齐；理想情况下收敛 `SmartSidebar` 与 `AdminLayout` 的分叉。
- 侧栏单一底色 `--sidebar-bg`；TopBar 工作台按钮 active 色对齐 `--color-primary`（修 `#FF8533` vs `#E85E00`）。

### 5.5 移动端（oa-mobile / vant）
文件：`web/src/views/oa-mobile/**`，以及 vant 主题入口
- 通过 vant 的 `--van-primary-color` 等变量桥，映射到统一令牌（`--color-primary` / 状态 / 表面）。
- 替换移动端组件中硬编码的蓝 / 绿等 → 令牌。
- 视觉对齐主色、状态、表面与圆角；逐组件像素级精修可留后续。

### 5.6 设计系统层（共享组件 + 全局覆盖，全站自动继承）
标准化共享 UI 原子 / 分子，让所有功能页自动获得统一风格（高杠杆，先于逐模块巡检）：
- **页头** `PageHeader.vue`：统一标题 / 面包屑 / 操作区 / 返回，去硬编码蓝。
- **页容器**：统一 `.page-container` 内外边距、`--bg-page` 底、卡片间距（收口 pagePadding 双轨）。
- **卡片**：统一 `--radius-lg` / `--border` / `--shadow-sm` / 内边距。
- **工具栏**：统一 `$toolbar-height/padding`、按钮组、筛选区节奏。
- **表格**：经 `ant-override.scss` + ConfigProvider Table token 统一表头 / 行高 / hover / 斑马 / 对齐（全站表格自动一致）。
- **表单**：统一 label 宽度 / 项间距 / 必填星 / 校验态。
- **空状态** `EmptyState.vue`：统一插画 / 文案 / 操作位（替换各页自造空态）。
- **标签 / 徽章**：统一状态色与 `--biz-*`，去各页自定义色。
- 产出 `PATTERNS.md`（页面骨架范式：页头 + 工具栏 + 内容卡 + 表格/表单 + 空态）。

### 5.7 逐模块风格巡检对齐（覆盖所有功能页）
设计系统层就位后，对**每个业务模块**逐页巡检，按统一"页面骨架范式 + 令牌"收敛偏离项。模块清单（以 `web/src/views/` 为准）：finance（财务 / 科目 / 凭证 / 账套 / 银行…）、express（报价 / 成本 / 返利…）、cardflow（卡片流 / 导入验证…）、crm、oa、task（任务 / OKR / 绩效 / 知识…）、quality、points、dormitory、vehicle、contract、conference、system（含 theme 配置页，去 emoji 标签）、workhub、其余零散页。
- 每模块一组任务：盘点该模块页面 → 套用页面骨架范式与令牌 → 修自造卡片 / 工具栏 / 表格 / 空态 / 硬编码色 → 关键页 preview 截图对比。
- 一致性基线：同类页面（列表页 / 详情页 / 表单页 / 仪表盘）在不同模块间观感一致。

## 6. 范围与非目标

**本轮范围内**：令牌单一真源 + CSS 变量桥；去蓝(221) + 拆紫(后台)；同义多值收敛；登录重做(深墨)；主页 / 搜索 / 外壳重设计；移动端令牌对齐；**设计系统层（共享组件 + 全局覆盖）**；**逐模块功能页风格巡检对齐（全站）**；WCAG AA；`TOKENS.md` + `PATTERNS.md`；dark-ready 令牌结构。

**非目标（本轮不做）**：
- 完整深色模式皮肤交付（仅做 dark-ready 结构）。
- 业务逻辑 / 功能行为变更（纯视觉与令牌）。
- 新功能。
- ECharts / 图表深度重做（仅做调色板映射对齐，不重排图表）。

## 7. 验收标准

- `grep` 全 `web/src` 无裸 `#1677ff` / `#1890ff` / `#409eff`（令牌定义文件除外）。
- `config/theme.ts` 已删除且无 import。
- 在后台"颜色配置"改 `--color-primary`，**前台 + 后台 + 移动端**的按钮 / 链接 / 激活 / 侧栏 / 搜索高亮**同步变色**。
- 登录页无粒子 / SVG / 光斑 / 渐变；焦点环为橙。
- 后台无紫色主题，靠"顶栏深一档 + 文案"区分。
- 所有状态色单值；金黄 / 红 / 灰底 / 蓝均已收敛。
- 关键页面 WCAG AA 抽检通过（主文本、主按钮）。
- **全站一致性**：各业务模块同类页面（列表 / 详情 / 表单 / 仪表盘）观感一致；页头 / 空状态 / 卡片 / 工具栏 / 表格以共享组件为唯一来源，无各页自造样式；抽检每个模块至少一页 preview 截图符合范式。
- 前端 `vite build` 通过（type-check 基线红可过滤，见项目约定）。

## 8. 风险与缓解

- **去蓝语义误判**：部分蓝本应是 `info` 而非 `primary` → 逐处人工判定 + 评审。
- **间距刻度调整**：改 `$spacing-*` 可能位移布局 → 先对齐值、逐屏视觉回归。
- **vant 主题桥**：vant 变量体系独立 → 单独验证移动端。
- **大 diff 回归**：一次性全量 → 用 preview 工具逐屏可视化验证（登录 / 主页 / 搜索 / 外壳 / 移动端）。
- **运行时主题与编译期回退不一致**：以 store 为权威，CI 校验二者同值。

---

> 下一步：本 spec 经确认后，进入 writing-plans 产出分阶段实现计划（令牌地基 → 去蓝/拆紫 → 各屏 → 移动端 → 验收）。
