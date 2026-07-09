# CardFlow 设计器保真度修复 · 实施 Plan

> 承接 `2026-07-07-cardflow-设计器重设计落地-总体plan.md` 与 2026-07-08 保真度审计。
> **工作方式（已确认）**：主树 master 直接做，小步 commit，过 hook 编译门禁，**不 push**（等用户点头）。
> **用户决策（已确认）**：① 发布设置=开关式+仅真配置（无引擎消费项标二期灰置）；② 干跑=重排贴 mock 屏7 三栏；③ 发起/抄送=接线可落地+诚实占位；④ 主树 master。

## 背景与红线

审计确认功能逻辑完成度高（~85%），**差距集中在前端 UI 布局保真度**。修复须守住原 plan 两条红线：
- **UI 保真**：严格按 mock 布局/尺寸/字号，色调豁免（映射项目令牌）。
- **不做假配置**：引擎不消费的配置项 UI 不出真开关，改二期灰置占位。

### 引擎能力核实结论（本轮已查证，决定"真配置 vs 占位"边界）

| 能力 | 引擎消费点 | 结论 |
|---|---|---|
| 节点级超时 `FTimeoutHours` | `CardFlowTimeoutJob.cs:65-111`（含 2x/3x 升级级别） | ✅ 真消费，可配 |
| 节点级失败策略 `FFailurePolicyJson`（stuckWithNotify/maxRetry） | `FlowEngineService.cs:2329-2345` | ✅ 真消费，可配 |
| 抄送 cc（AlertNotifyPlugin「告警通知」） | `FlowEngineService.cs:1448`（cc action）+ `AlertNotifyPlugin` 自动插件存在 | ✅ 以 auto+notify 插件封装可落地 |
| 节点自动裁决 `autoDecision` | `FlowEngineService.cs:2614` | ✅ 真消费（已实现） |
| **全局默认**超时/失败策略 | 无定义级字段 | ❌ 无消费 → 二期占位 |
| **审批人去重** | 引擎无（仅 AutoPlugin import dedup） | ❌ 无消费 → 二期占位 |
| **允许发起人撤回**（定义级开关） | 仅批次级 `RevokeBatchAsync`，无定义级 | ❌ 无消费 → 二期占位 |
| **停用节点开关** | 引擎 FType 二元分派，无 disabled-skip | ❌ 无消费 → 二期占位 |
| **cc 作为独立 FType** | 引擎 `FType=="auto"?auto:human`（`FlowEngineService.cs:1651`），cc 会被当人工节点卡住 | ❌ 不新增 FType；cc=auto+notify 插件封装 |

**关键裁决**：抄送节点**不新增 `cc` FType**（会被引擎当人工待办卡死）。抄送 = **auto 节点 + AlertNotifyPlugin 预设**，视觉用 flow-cc 橙色令牌+"抄"字标识区分，底层是引擎已支持的自动节点。

---

## 批次总览

```
F1 竖向图: 插入菜单补抄送人 + 节点五类视觉 + 自动二级子菜单
F2 视图切换: a-tabs → a-segmented, 去"节点链", 加"只读总览图"
F3 干跑工作台: 重排贴 mock 屏7 三栏
F4 发布设置(第四步): 改名+开关式布局, 真配置落地/无消费项二期灰置
F5 抽屉 Tab 补齐: 红点badge/敏感锁/路由锁/节点说明/动作逐行/高级
F6 条件编辑器: 类型emoji图标 + "去设为必填"跳转链接
F7 矩阵: 复制左列 + 批量toast计数
F8 发布diff富文本 + 版本抽屉宽度
F9 整体回归 + UI保真核对
```

每批收尾：`npm run type-check` 绿 + 相关 vitest 绿 + （涉后端）`build-filter cardflow` 绿 + 独立 commit。

---

## F1：竖向图插入菜单 + 节点视觉五类 + 自动二级子菜单

**Files:**
- Modify `web/src/components/cardflow/designer/FlowVerticalGraph.vue`（MENU_ITEMS 补抄送人 + 自动二级菜单）
- Modify `web/src/components/cardflow/designer/FlowGraphNode.vue`（节点类型 5 类视觉：起/审/自/抄/终）
- Modify `web/src/components/cardflow/StageDefinitionEditor.vue`（StageDefinition 支持 cc 语义标记——非新 FType，而是 auto+notify 预设的判定）
- 可能 Modify `web/src/utils/flowGraphProjection.ts`（插入抄送=插入 auto+notify 预设 stage）

**做法（守红线）：**
- 插入菜单 4 项，顺序对齐 mock 屏4 L523-526：**审批人 / 抄送人 / 条件分支 / 自动处理**。
- **抄送人** insert → 生成 `type:'auto'` + 预设 `pluginRegistryId`=AlertNotify(通知) + `ccConfigJson` 初值；视觉标记走 flow-cc 令牌。若注册表无 AlertNotify 行 → F1 附一条 seeder 补注册（版本化 V 编号，`SeederHelper.ExecuteRawSql`）。
- **自动处理** → 二级子菜单（凭证/质检/通知/写入），各生成对应 `pluginRegistryId` 预设；引擎不支持的子类不出菜单项（先查注册表实际有哪些粒度=card 的插件）。
- `FlowGraphNode` 节点视觉：按 `nodeKind()` 派生 5 类 `cfd-node--{start|appr|auto|cc|end}`，图标字 起/审/自/抄/终；cc 用 flow-cc 橙、auto 用 flow-auto 紫虚线（SCSS 已就位）。cc 判定=auto 且 notify 插件。

**验收**：插入菜单四项+自动二级；抄送节点橙色"抄"字渲染；type-check + flowGraphProjection.spec 绿。commit `fix(cardflow): 竖向图插入菜单补抄送人/自动二级子菜单 + 节点五类视觉`。

---

## F2：视图切换 segmented + 去列表 + 只读总览图

**Files:**
- Modify `web/src/views/cardflow/FlowDefinitionEditPage.vue`（STEP_STAGES 区 `a-tabs` → `a-segmented`）

**做法：**
- 三视图改 `a-segmented`：**流程视图 / 只读总览图 / 字段权限矩阵**（对齐 mock 屏3/6 + 原 plan M1-2 最终态）。
- **移除「节点链」tab**（原 plan M1-6 收口要求）；`StageDefinitionEditor` 组件文件保留（抽屉子块仍复用），仅撤视图入口。
- 「只读总览图」= 复用既有 `FlowStateCanvas.vue`（已降级只读）。
- segmented 样式覆盖对齐 mock `.seg`（padding 5px 13px/font 13px/选中主色）。

**验收**：三项 segmented 切换正常，节点链入口消失，只读总览图可看；type-check 绿。commit `fix(cardflow): 流程设计视图切换改 segmented 三项 + 移除节点链列表`。

---

## F3：干跑工作台重排贴 mock 屏7 三栏

**Files:**
- Modify `web/src/views/cardflow/FlowDefinitionEditPage.vue`（`.fdef-preview-workbench` 三栏内容重排 + grid CSS）
- Modify `web/src/components/cardflow/designer/PathPreviewPanel.vue`（拆分：样例输入区独立为左栏内容）

**做法（对齐 mock 屏7 三栏 = 样例输入 / 竖向图命中高亮 / 手机卡片呈现）：**
- **左栏 ①样例表单值**：样例字段输入 + 视角(处理人/观察者/发起人) + 设备(PC/移动) seg + "重新干跑"按钮。复用 PathPreviewPanel 的 sample form + 现有 `previewViewerMode`/`previewDevice`。
- **中栏 ②路径推演**：复用 `FlowVerticalGraph`（只读态）+ `hitStageKeys` 绿色命中高亮（`is-hot` 已就位）+ 命中分支绿标注 + 节点运行角标(派给谁 runbadge)。**核心：路径文字升级为竖向图**。
- **右栏 ③卡片呈现**：复用现有 SchemaRenderer 手机预览（`fdef-preview-card--mobile`）+ 脱敏提示文案。
- grid 改 `minmax(280px,.9fr) minmax(0,1.4fr) 340px` 贴 mock 三栏比例。
- 现「发布校验」栏内容不丢：并入左栏底部或右栏（信息不删，仅重排位置）。

**验收**：三栏结构对齐 mock 屏7；干跑后中栏竖向图绿色点亮命中路径；type-check 绿；preview_inspect（若环境可用）核三栏宽度/命中色。commit `fix(cardflow): 干跑工作台重排为 mock 屏7 三栏(样例/竖向图命中/卡片)`。

---

## F4：第四步「发布设置」开关式 + 真配置/二期占位

**Files:**
- Modify `web/src/views/cardflow/FlowDefinitionEditPage.vue`（STEPS[3] 改名 + STEP_SETTINGS 区改 setrow 开关式布局）
- Modify `web/src/styles/cardflow-designer.scss`（补 `.cfd-setrow` 结构类，移植 mock `.setrow`）

**做法（守"不做假配置"）：**
- 第四步标题「流程配置」→ **「发布设置」**；STEPS[3].title 同步。
- 布局改 mock 屏8 `.setrow` 开关式（每行：toggle + 标题 + 描述 + 右侧控件）。移植 `.cfd-setrow`（padding 13px 4px/边线/标题 13.5px/描述 12px mute）。
- **真配置行**（引擎消费，落真开关）：
  - 节点超时提醒默认（承载 `FTimeoutHours` 全局默认——**注意**：引擎无全局默认字段，若要真生效须每节点回填；本轮**降级**为"新建节点默认超时值"前端预填，标注清楚，不谎称全局生效）
  - 自动节点失败默认策略（同上，前端预填新建 auto 节点的 failurePolicy，不谎称全局）
  - 保留现有真配置：退回策略/重提策略/审批管理员/前置依赖/冲销/余额（移入"审批规则"分区，仍 setrow 或分组卡）
- **二期占位行**（引擎无消费，灰置不可点 + "二期"标签 + tooltip 说明）：审批人去重 / 允许加签转交(实例级已有但定义级开关无消费) / 允许发起人撤回。
- 诚实文案：占位项明说"引擎暂未消费，规划中"，不做假开关。

**验收**：第四步开关式布局贴 mock；真配置项可存可读；占位项灰置有说明；type-check 绿。commit `fix(cardflow): 第四步改名发布设置+开关式布局(真配置落地/无消费项二期占位)`。

---

## F5：节点抽屉 Tab 补齐

**Files:**
- Modify `web/src/components/cardflow/StageConfigPanel.vue`
- 可能 Modify `web/src/components/cardflow/designer/PermissionTri.vue`（lockedStates 已有接口）

**做法（逐项，守红线）：**
- **F5a Tab 错误红点 badge**（M2-1 遗留）：Tab 标题按诊断 target=本 stage + tab 归属映射渲染红点（mock `.dtabs .dotbadge`）。
- **F5b 字段权限敏感🔒/路由🔗锁定**（M2-5 遗留）：StageConfigPanel 向 PermissionTri 传 `lockedStates`——敏感字段锁"可编辑"、路由字段(读 routeFieldIndex)锁只读。敏感行红底。
- **F5c 基础 Tab 节点说明**：加 textarea 存 stage 备注字段（核实实体有无 remark 列；无则存 FConfigJson.note，前端展示为主，不谎称引擎消费）。
- **F5d 动作 Tab 逐行布局**：多选下拉 → 逐动作行(toggle 启用 + 意见必填 seg)，对齐 mock A7 `.fplist`。数据仍写 actionPolicy（结构不变，仅 UI 重排）。
- **F5e 高级 Tab dtabs 样式**：修 `.sde-tabs` padding 7px 0→10px 12px、font 12px→13px 对齐 ui-baseline。
- **停用开关**：引擎无消费 → **不做**（或灰置二期），不做假配置。
- **会签比例/自定义动作/超时升级链**：原 plan 已定 M8，维持不做。

**验收**：红点/敏感锁/路由锁/节点说明/动作逐行/dtabs 样式；type-check 绿；permissionTriShared.spec 绿。分 2-3 个 commit（badge+锁 / 动作重排 / 样式）。

---

## F6：条件编辑器类型图标 + 去设为必填链接

**Files:**
- Modify `web/src/components/cardflow/ConditionBuilder.vue`
- 可能 Modify `web/src/components/cardflow/conditionBuilderShared.ts`（类型→emoji 映射）

**做法：**
- **F6a 类型 emoji 前缀**：字段下拉每行左侧加类型图标（💰金额/#数字/◉单选/👤人员/🏢组织/📅日期），对齐 mock C8。加 `CONDITION_TYPE_ICONS` 映射。
- **F6b "去卡片设计设为必填"跳转链接**：非必填被禁用字段附蓝链，点击 emit navigate → 编辑页跳 STEP_SCHEMA 高亮该字段（复用 focusDiagnosticTarget 模式）。需区分"类型不可用"(纯禁用)与"非必填"(可去修复)两类禁用原因。

**验收**：字段下拉有 emoji + 非必填字段有跳转链接；type-check + conditionBuilderShared.spec 绿。commit `fix(cardflow): 条件字段下拉类型图标 + 去设为必填跳转链接`。

---

## F7：矩阵复制左列 + 批量 toast 计数

**Files:**
- Modify `web/src/components/cardflow/designer/FieldPermissionMatrix.vue`

**做法：**
- **F7a 列头下拉补"复制左侧列的设置"**（E3 四选三→四）：读左邻列 access 覆盖本列。
- **F7b 批量 toast 计数**：`batchColumn` 已返回 `skipped`，接线 message.info "N 个锁定字段未变更"。
- **F7c th/td padding** 7px 10px→7px 11px 对齐 mock（1px 差）。

**验收**：复制左列生效 + 批量后 toast；type-check 绿。commit `fix(cardflow): 矩阵列头复制左列 + 批量锁定计数提示`。

---

## F8：发布 diff 富文本 + 版本抽屉宽度

**Files:**
- Modify `web/src/utils/flowVersionDiff.ts`（detail 结构化：old/new 分离）
- Modify `web/src/components/cardflow/designer/PublishConfirmModal.vue`（渲染旧删除线→新加粗）
- Modify `web/src/components/cardflow/designer/VersionHistoryDrawer.vue`（width 420→400）

**做法：**
- ChangeItem.detail 从 `"旧 → 新"` 字符串改为 `{ before, after }[]` 结构；弹窗渲染 `<del>旧</del> <strong>新</strong>`（E1/P-2）。
- flowVersionDiff.spec 补富文本结构断言。
- 版本抽屉 width 420→400 对齐 baseline。

**验收**：diff 旧删除线/新加粗；type-check + flowVersionDiff.spec 绿。commit `fix(cardflow): 发布diff条件值级富文本 + 版本抽屉宽度对齐`。

---

## F9：整体回归 + UI 保真核对 + 终审

- [ ] `npm run type-check` 全绿
- [ ] `npx vitest run`（全部 designer spec）全绿
- [ ] `npm run lint:style` 设计器文件零裸 hex（新增令牌走 theme.ts）
- [ ] `scripts/dev/build-filter.ps1 cardflow` 绿（若动后端/seeder）
- [ ] 若加 seeder：dev 库跑通验证（拷 db-connections.json）
- [ ] UI 保真核对（M0-5）：起前端 preview（若环境可用），preview_inspect 抽查 F1/F3/F4 核心元素 ≥5 属性对 ui-baseline.md
- [ ] 子代理整体终审（对抗性只读，抓集成缝）→ 修 → 复验
- [ ] 更新 `2026-07-08-cardflow-设计器实施进度.md` 记录本轮修复
- [ ] 汇总变更给用户，**等点头再 push**

---

## 假配置边界备忘（诚实交付，不谎称生效）

以下项**明确不做真开关**（引擎无消费），UI 灰置"二期"+ tooltip 说明：
- 审批人去重 / 允许发起人撤回(定义级) / 停用节点开关 / 全局默认超时(改为新建节点预填) / 全局默认失败策略(改为新建节点预填) / 会签比例 / 自定义动作 / 超时三级升级链 / 发起范围·代提交·重提走向 / 抄送时机·渠道(若 AlertNotify 不消费则占位)。

以下项**做真配置**（引擎消费已核实）：
- 抄送节点(=auto+AlertNotify) / 节点级超时 / 节点级失败策略 / 节点自动裁决 / 意见必填 / 字段权限(含敏感·路由锁) / 退回·重提·审批管理员·前置依赖·冲销·余额。
