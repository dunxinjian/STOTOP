# 04 · /make-plan 交接提示

````
/make-plan 重构「美申办公·工作台首页」(/workhub) 的信息架构。当前设计 Dieter Rams 审计 18/30，承重维度 #4 理解性、#10 克制 各 1 分。

裁决（引自 03-verdict.md）：
> 合计 18/30 且 #4 理解性、#10 克制两个承重维度均为 1 分；最高杠杆的修复（合并双筛选、统一两个质量部件、收敛三处导航面）改的是信息架构而非样式——故判 REDESIGN，范围严格限定 /workhub 首页布局，底层组件、Feed 引擎、令牌体系保留。

为何重构而非微调：核心缺陷需重排「哪些信息放哪栏」与删除重复控件，属信息架构层面，单靠样式调整无法解决。

必须保留（Preserve）：
- 令牌色系与 SCSS 变量（`@/styles/variables.scss`、`--biz-* / --space-* / --color-*`）。
- Feed 引擎与卡片：`useWorkHub` 组合式（SignalR 实时推送、撤销/分级确认、延后/归档）、`WorkItemCard.vue`、`WorkItemSkeleton.vue`。
- 三栏 shell 骨架 `index.vue:99-123` 与顶栏 `TopBar.vue`、`OrgSwitcher`。
- 状态覆盖（骨架/空/载入/成功/错误/撤销/确认）`WorkHubCenter.vue:430-806`。

必须舍弃（Discard）：
- 同维度双筛选模式（统计 chip + 来源复选框组）。证据 `WorkHubCenter.vue:509-522` 与 `:551-560`。致 #4、#10 失分。
- 两个独立数据源的并存质量部件。证据 `QualityAlertBar.vue` + `QualitySummaryCard.vue`。致 #4、#6 失分。
- 空态/低量态下常驻展开的高级筛选条。证据 `WorkHubCenter.vue:551-597`。致 #5、#10 失分。

审计 Top 动作（逐字）：
1. #10/#4 合并同维度双筛选：保留可点统计 chip 为唯一来源筛选，删除复选框行。证据 `WorkHubCenter.vue:55-62` vs `:64-71`。
2. #4/#6 统一两个质量部件：二选一收口或明确分工（中栏=今日可处理告警 / 右栏=趋势），口径一致。证据 `QualityAlertBar.vue:60-77`、`QualitySummaryCard.vue:53-90`。
3. #10/#5 降低空态密度：0 待办时以质量待处理 + 我要发起为主，高级筛选折叠进「筛选」开关。证据 `WorkHubCenter.vue:551-597`、`:671-680`。
4. #2/#10 收敛三处导航面：右栏「最近访问」与「常用功能」合并为单一「快速导航」。证据 `WorkHubRecentVisits.vue:11-43`。
5. #8/可达性：stat/action/frequent/mixed-item 改真按钮（role/tabindex/keydown），接上选中项 Enter 主操作。证据 `index.vue:81-86`。

重构原则（按优先级）：
1. 理解性 (#4) — 每个维度只有一个控件、每个数字只有一个来源、信号互不矛盾。
2. 克制 (#10) — 0 待办时屏幕安静；删一个元素任务即受损才保留。
3. 有用 (#2) — 处理待办与发起流程在最少步内完成。

交付物：
- 新信息架构（四列→职责重排：导航 / Feed / 上下文，不沿用旧分布）。
- 主流程低保真线框（含 0 待办、有待办两态），与现状并排对比。
- 状态清单（空/载入/错误/成功/focus/disabled）。
- Token/规格集中变更点 + 裸 hex 清零清单。
- 老用户迁移说明与切换标准（何时下线旧首页）。

需防的反模式：
- 旧结构换皮（把双筛选/双质量卡换样式后保留）。
- 双版本长期挂 flag。
- 为追潮流而非按上述原则重构。
- 把 Preserve 清单当可选项。
````
