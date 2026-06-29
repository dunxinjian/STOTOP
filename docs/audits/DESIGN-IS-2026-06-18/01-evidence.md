# 01 · 证据汇总

## A. 结构

- 三栏 flex 布局：`index.vue:99-123`；宽度 左 238px / 中 flex / 右 clamp(380,29vw,460) `index.vue:145-172`。**叠加最左侧全局导航后，屏幕实为「四列」**。
- 左栏「我要发起」三组（数据上传 / 高频流程 / 业务申请）`TriggerActionPanel.vue:110-114`，每项为 72px 高卡片 `:352-365`。
- **同维度两套筛选控件**（关键）：
  - 顶部统计 chip 6 个，标签 = 审批/异常/任务/运单/合同/积分 `WorkHubCenter.vue:55-62`，渲染于 `:509-522`，点击即按 source 筛选 `:258-266`。
  - 「来源」复选框组，标签 = OA/质量/任务/运单/合同/积分 `:64-71`，渲染于 `:554-560`。
  - 二者 key 完全相同（oa/quality/task/datacenter/contract/points），但**标签文案两套**（审批↔OA、异常↔质量）。
- **三处并存的导航/启动面**：左栏「我要发起」、右栏「快速导航」(最近访问+常用功能 `WorkHubRecentVisits.vue:11-43`)、最左全局菜单。截图中「上传中心」在右栏出现两次（最近访问 2 分钟前 + 常用功能 14 次）。
- 点击目标多为 `div @click`，无 `role/tabindex/keydown`：stat-item `:510-517`、action-item `TriggerActionPanel.vue:186-192`、frequent-item `WorkHubRecentVisits.vue:14-18`、mixed-item `:442-451` → 键盘不可达。
- 键盘 J/K 导航已接，但 Enter 主操作仍 TODO 未接 `index.vue:81-86`。

## B. 视觉

- 令牌体系基本贯彻（`--space-* / --radius-* / --color-* / --biz-*`）。
- **裸 hex 泄漏**（与项目 stylelint 禁裸 hex 约定冲突）：`TriggerActionPanel.vue:275 #f7f8fa, 378 #fff1e8, 381/386 rgb(114,46,209), 402-404 #f9f0ff/#efdbff`；`WorkHubCenter.vue:1228 #f5f5f5, 1394 #323232, 1448 #ffe7ba, 1483 #8c8c8c, 1504 #f0f0f0, 1525 #595959`。
- emoji 当图标（与 Ant 图标混用）`WorkHubCenter.vue:172-178`（📋✅🔔）。
- 状态覆盖充分：骨架 `:640`、加载 `:430-433`、空态多版 `:436-438/671-680`、撤销 toast `:773-783`、二次确认 `:786-806`、延后/归档折叠区 `:682-716`；缺失项为卡片 focus 态（div 按钮无焦点环）。
- 顶栏 tab 用 `backdrop-filter: blur(10px)` `:834`（轻度潮流标记）。

## C. 文案与诚实

- **两个质量部件可显示矛盾状态**：中栏 `QualityAlertBar`「待处理 {{pendingCount}}」`QualityAlertBar.vue:60-77`（源 getQualityDashboardStats）vs 右栏 `QualitySummaryCard`「暂无待处理质量问题」`QualitySummaryCard.vue:53-90`（源 getWorkHubQualitySummary）。代码注释自承「二者并存」`QualityAlertBar.vue:8-11`。**截图实测：中栏「待处理 1」，右栏「暂无待处理质量问题」——对用户构成自相矛盾**。
- 同时中栏 Feed 显示「当前没有待处理的工作」，与「质量预警 待处理 1」并存，三条信号无法相互印证。
- 顶栏「今天是…祥和的一天」`TopBar.vue` 公告位为友好彩蛋，无害。

## D. 重量与摩擦（估算）

- 右栏两部件各 5 分钟轮询：`QualityAlertBar.vue:49`、`QualitySummaryCard.vue:42`；外加 SignalR 长连 `WorkHubCenter.vue:389-394`。
- 首屏动效均为 hover/进出过渡，无空闲自动播放；Ant Design Vue 体量偏大但属标准依赖。JS 字节数未测。
