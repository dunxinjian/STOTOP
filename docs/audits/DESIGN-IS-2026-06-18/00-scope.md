# 00 · 审计范围

- **审计对象**：美申办公「工作台首页」(`/workhub`)
- **入口组件**：`web/src/views/workhub/index.vue`（三栏 shell）
  - 左栏：`TriggerActionPanel.vue`（我要发起）
  - 中栏：`WorkHubCenter.vue`（质量预警条 + 全部/待办/通知 Feed）
  - 右栏：`QualitySummaryCard.vue`（运单与仓储质量）+ `WorkHubRecentVisits.vue`（快速导航）
  - 全局壳：`layouts/TopBar.vue`（顶栏）+ 左侧全局导航（截图最左列）
- **主要用户**：网点管理员 / 业务办公人员（截图右上「管理员」）
- **首要任务**：① 处理待办（审批/异常/任务/运单/合同），② 发起业务流程，③ 快速跳转常用功能
- **输入材料**：用户提供的工作台首页截图（2026-06-18 状态）+ 上述源码
- **约束**：Vue3 + Ant Design Vue 技术栈；令牌色系单一真源（stylelint 禁裸 hex）；中文界面
- **未运行实例**：按静态源码 + 截图取证，性能项（#9）标记为「估算」
