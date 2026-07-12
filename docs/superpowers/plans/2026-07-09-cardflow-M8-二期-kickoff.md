# CardFlow 设计器二期(M8)· Kickoff

> 新会话接手二期的**唯一入口文档**。承接：
> - 总纲 `2026-07-07-cardflow-设计器重设计落地-总体plan.md`（**第五节 M8**）
> - 需求真源 `2026-07-08-cardflow-设计器保真度修复-plan.md`（**末尾「假配置边界备忘」**——每项"引擎要做什么才能落地"）
> - 已完成基线 `2026-07-08-cardflow-设计器实施进度.md`
> 一期(M0–M7)+保真度修复(F1–F9)已 push 到 `origin/master`（HEAD 起点 `48e79fe`）。
>
> **进度(2026-07-11)：M8-A/B/C/D 均已 push origin/master**（A 发起三件·B 抄送时机渠道·C 引擎增强四件·D 定义级开关）；**剩 E/F/G**。⚠️ **seeder 号别信下文旧数**：现已到 **V78**（M8-C 占到 V75；V76/V78=极兔、V77=韵达，导入线在持续加 V）——动 schema 前**必查 `CardFlowSeeder.cs` 实际末版本 + SYS迁移历史再定号**（下一个约 V79，以代码为准）。

## 一、二期的性质（先读这段再动手）

M8 各项之所以一期没做，是因为**引擎不消费**——所以二期是**"引擎先、UI 后"**：先核实/改后端引擎消费点，再解锁 UI 占位。**不是纯前端活**。

**三条铁律（一期已验证，二期继续守）**：
1. **不新增 `cc` FType**——引擎节点分派是 `FType=="auto" ? auto : human` 二元（`FlowEngineService.cs:1651`），cc 会被当人工待办卡死。抄送=auto+AlertNotify 插件封装。
2. **不做假配置**——引擎不消费的配置项，UI 不出真开关（灰置二期占位）。每项动 UI 前先用子代理核实引擎消费点。
3. 后端全在 `STOTOP.Module.CardFlow`，版本化 seeder(无 EF migrations)，`build-filter cardflow` / `test-dotnet cardflow`，提交经 hook 门禁，**不 push 等人点头**。

## 二、拆批（每批独立立项，引擎先→UI 后）

| 批 | 引擎改动（先核实现状再动） | 落地后 UI | 关键核实点 |
|---|---|---|---|
| **M8-A 发起节点三件** | 发起范围校验 / 代提交 / 重提强制重路由(E1) | B1 发起抽屉变体解占位（现为说明弹层） | `OrchestrationEngineService`/`FlowEngineService` 有无 initiatorScope/onBehalf/resubmit 重路由消费点（预期无，多属绿地新增） |
| **M8-B 抄送时机/渠道** | 核实 `AlertNotifyPlugin`/`FCcConfigJson` 是否消费 timing(onEnter/onApprove/onReject)与 channel(应用内/钉钉/企微/bot) | B2 抄送面板对象+时机+渠道 | `FlowEngineService.cs:1448` cc action + `AlertNotifyHandler`；`FCcConfigJson` 现仅存不解析则需补消费 |
| **M8-C 引擎增强四件** | ①超时三级升级链 ②去重节点例外 ③自定义动作挂自动处理 ④会签比例 | 高级Tab超时升级/去重例外、动作Tab自定义动作、处理人Tab会签比例——各自解占位 | ①`CardFlowTimeoutJob.cs` 现有 2x/3x level 但无"升级到上级/自动通过"动作 ②引擎无审批人去重 ③`actionPolicy` 无自定义动作 ④approvalMode 仅 any/all 无比例 |
| **M8-D 定义级开关** ✅已push | ①审批人去重(定义级) ②允许发起人撤回(定义级) | 发布设置**两**灰行转真开关(去重/撤回) | 实际：①②走 `FFlowSettingsJson` **零 schema**(新建 `FlowSettingsReader`)；①与 M8-C 节点级 `FSkipDuplicateApprover` OR 叠加；②撤回引擎是卡片级 `WithdrawAsync`(**非**批次 `RevokeBatchAsync`)已落地只缺 gate、缺失即允许；**③停用节点 skip 不做**(规则模式路由歧义+前端无占位——原写"三灰行"有误，第三灰行实为"允许加签/转交") |
| **M8-E 处理人策略补全** | ApproverResolver 补 发起人自选 / 连续多级主管 / 上一节点处理人指定 | 处理人Tab策略下拉 5→8 | `ASSIGNEE_STRATEGIES`(StageConfigPanel:51) 现 5 项；`IApproverResolver` 实现清单为准 |
| **M8-F 模板库+导入双模** | 跨组织模板中心(E5 占位符剥离) + 导入触发型双模(E4 全套) | 列表页模板入口、导入流程设计器支持 | 独立大项，建议最后做；导入现走 seeder 配置(暂存导入框架) |
| **M8-G 骨架屏(原 M7-5)** | 无（纯前端） | D5 断点四档 + D6 骨架三形状分级加载 | 优先级最低，现 a-spin 已覆盖基本体验 |

**建议顺序**：~~A → B → C → D~~ ✅已完成并 push → **E（下一批）** → F/G 收尾。各自独立可发布，也可按业务优先级重排。

## 三、每批统一工作流

1. **核实先行**：子代理(general-purpose/module-explorer)只读核实该批引擎消费现状，产出"可真落地 / 必须占位"清单。
2. **锁模块出 TDD 细化 plan**（与一期 F 批同一方式，与本文任务卡对齐，不改契约只加步骤）。
3. **引擎改动**：xUnit 先红后绿；seeder 建表/改列走 V 编号，原生 SQL 用 `SeederHelper.ExecuteRawSql`；改 baseline JSON 后重建到 bin。
4. **UI 解占位**：`type-check` + `vitest` + `lint:style`(零裸 hex) 每任务收尾必绿；UI 保真按 `ui-baseline.md` 核对(preview_inspect ≥5 项)。
5. **每项独立 commit**，经 hook 编译门禁，不 push 等点头；批收口做整体终审(子代理对抗性只读)+回归。

## 四、复用资产（勿重造）

- 节点视觉/令牌：`web/src/styles/cardflow-designer.scss`（`.cfd-*` 族）+ `stageDefinitionShared.ts`（`stageVisualKind`/`NOTIFY_PLUGIN_REGISTRY_ID`）
- 投影：`web/src/utils/flowGraphProjection.ts`（`buildFlowTree`/insert*/delete*）
- 权限胶囊：`PermissionTri.vue`（`lockedStates` 接口已就位）
- 路由引用索引：`web/src/utils/routeFieldIndex.ts`
- 干跑：`CardFlowPathPreviewService`(后端) + 干跑工作台三栏(FlowDefinitionEditPage)
- 插件注册表：`CardFlowSeeder.cs` + baseline JSON（card 粒度插件：SecurityCheck/Classification/WorkTask/AlertNotify(FID8)；batch：AutoVoucher/QualityAnalysis/InfoRecord…）

## 五、新会话开场白（粘贴即用）

> 继续 CardFlow 设计器二期(M8)。先读 `docs/superpowers/plans/2026-07-09-cardflow-M8-二期-kickoff.md` 建立上下文（**M8-A/B/C/D 已 push origin/master，剩 E/F/G**），再按拆批表做 **M8-E 处理人策略补全**：先用子代理核实 `IApproverResolver` 现有实现清单 与 `ASSIGNEE_STRATEGIES`(StageConfigPanel 约:51，现 5 项) 缺哪三项（发起人自选/连续多级主管/上一节点处理人指定），判定可真落地 vs 必须占位(守"不做假配置")，锁模块出细化 TDD plan 后子代理驱动执行。后端 `build-filter cardflow`/`test-dotnet CardFlow`，不新增 cc FType；**若需 seeder 先查 `CardFlowSeeder.cs` 末版本再定 V 号（现已到 V78，勿硬编号）**，每件独立 commit 经 hook 门禁，不 push 等我点头。

（把 `M8-E` 换成 F/G 即可切批。）
