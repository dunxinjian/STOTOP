# CardFlow 设计器重设计落地 · 总体实施 Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **分批执行约定**：本 plan 是总纲+首两批（M0/M1）全细化。M2-M8 每批开工前，由执行会话按本文的任务卡+接口契约展开为该批的细化 TDD plan（与 2026-07-04 B1-B9 plan 同一工作方式），不允许跳过展开直接糊代码。

**Goal:** 把五篇设计稿（骨架 9 屏 + 面板 A/B 16 屏 + 微交互 C 11 屏 + 规格 D 10 屏 + 修订 E 8 屏，共 54 屏）落成 CardFlow 流程设计器的实际功能，以结构化竖向流程图替代自由画布为编辑入口。

**Architecture:** 不重写编辑页——`FlowDefinitionEditPage.vue`（4481 行）已有 5 步向导、撤销/重做、自动保存、诊断合流四大地基，全部保留复用。增量路径：新建竖向图组件接管 STEP_STAGES 的编辑入口（画布降级只读）→ 扩展 StageConfigPanel 3 Tab → 5 Tab → 新建矩阵/干跑/发布确认三个工作台 → 后端按需求清单补端点。**表结构与实体不动**（stages/routes/FCardSchemaJson 容器不变；容器内 JSON 允许受控演进——如 M3-1 条件组格式，须读旧写新兼容+消费方同批级联），竖向图是 routes 的**投影视图**而非新存储。

**Tech Stack:** Vue 3 + TS strict + Pinia + AntD（现状栈）；vitest 4.x（已搭）；后端 .NET 10 CardFlow 模块。

## Global Constraints

- **UI 保真（用户硬约束）**：前端页面**严格按 mock 图的布局、样式、字体、字号**实现——排版结构、间距、圆角、投影、字号阶梯（11/11.5/12/12.5/13/13.5/14/14.5px，字重仅 400/600/700）、组件尺寸（节点 340px/头部 46px/图标 26×26 r7/"+"26px/抽屉 400px/分支列 max 250 gap 20 等，全集见 mock-shared.css 与 C11）逐项对齐，不得"神似"。**唯一豁免是色调**：mock 中的具体色值按语义映射到项目主题令牌（mock 主蓝→`var(--color-primary)` 实际色相、成功/警告/错误→项目对应令牌；新增 flow-auto/flow-cc 的色相若与项目主题冲突，以主题派生为准并回写 TOKENS.md）。核对机制见 M0-5，每个 UI 任务的 preview 步骤含 `preview_inspect` 对照基准表。
- 禁裸 hex（stylelint 门禁）；新增令牌先落 `web/src/stores/theme.ts` + `web/docs/TOKENS.md` + `variables.scss`。
- 禁裸 any；`npm run type-check` 每任务收尾必绿。
- 后端编译走 `scripts/dev/build-filter.ps1 cardflow`（.slnf 闭包）；测试 `scripts/dev/test-dotnet.ps1 CardFlow`。
- 不新建模块；后端改动全部在 `STOTOP.Module.CardFlow`（版本化 seeder，无 EF migrations）。
- 术语表（设计 D7）为文案唯一真源：流程/卡片/节点/处理人/发起人/干跑预览/在途卡片/兜底分支——PR 内不得混用"审批流/表单/环节/审批人（泛指时）"。
- 中文消息、英文代码标识；`.cs` 缩进 4、其余 2。
- 每任务独立 commit；提交经 hook 编译门禁；不 push（用户点头才推）。
- 设计稿即 spec：`docs/superpowers/specs/2026-07-07-cardflow-designer-mocks/`（cardflow-designer-mock.html + mock-detail-part1..5.html + mock-shared.css，已固化）。

---

## 一、现状差距地图（as-built → 设计的处置）

| 设计能力 | 现状锚点 | 处置 |
|---|---|---|
| 四步向导可往返 | `FlowDefinitionEditPage.vue:189` 现有 5 步（basic/schema/stages/settings/preview） | **对齐 mock 四步**（UI 保真硬约束）：preview 从步骤条摘除，改为顶栏常驻「预览」按钮唤起干跑工作台（M5-0）；M5 前的过渡期 preview 步暂留 |
| 撤销/重做/自动保存 | 编辑页 `:1613` 撤销栈 + 防抖自动保存已有 | 保存胶囊四态（C5）→ **M0-4**；历史下拉+跳回任意步（C6）→ **M7-2** |
| 结构化竖向流程图 | 无。现状=StageDefinitionEditor 左列表 + FlowStateCanvas（vue-flow 画布） | **M1 新建** `FlowVerticalGraph.vue`；画布降级只读总览 |
| 节点抽屉五 Tab | `StageConfigPanel.vue` 现有 3 Tab（基础/处理人/节点视图） | **M2 扩展**为 基础/处理人/字段权限/动作/高级（节点视图并入字段权限） |
| 条件组且/或视觉化 | `ConditionBuilder.vue`（559 行）扁平条件行 | **M3 升级**（组容器+或徽标+类型算子） |
| 强制兜底分支 | route 有 default 语义（StageRouteResolver 消费） | M1 投影时兜底列固定；M3 删除保护 |
| 字段权限矩阵 | 无（权限散在 stage 的 viewProfile） | **M4 新建**矩阵视图（数据仍只存 stages 上） |
| 路由依赖锁定（字段/选项） | 无 | **M4 新建**（诊断已有 target 机制可挂） |
| 干跑预览 | `PathPreviewPanel.vue` + preview-presentation 端点 + ApproverResolver 干跑（B3/B4 已建） | **M5 升级**：历史卡片代入/失败态推演/命中率 |
| 发布确认（diff+在途策略） | `PublishAsync` + `CardComponentPublishValidator` 门禁已有；无 diff、无在途策略 | **M6 新建**弹窗+后端 3 端点 |
| 版本历史/回滚 | `IFlowDefinitionService.GetVersionsAsync` 已有；无回滚 | **M6** 回滚=快照转草稿（新端点） |
| 模板库/复制/引用重绑 | 列表页有复制？（执行时核实 `FlowDefinitionListPage`）；重绑无 | **M6** |
| 编辑锁/接管 | 无 | **M7 后端+前端**（E7 协议） |
| 导入触发型双模 | 导入流程走 seeder 配置（暂存导入框架），设计器不支持 | **M8**（二期，独立立项） |
| 超时升级链/去重例外/自定义动作 | 引擎无消费 | **M8**（引擎增强，二期）——M2 的动作/高级 Tab 只落**引擎已支持**的配置项，不做假配置 |
| 发起节点属性（B1 发起范围/代提交/撤回/重提） | 无发起节点实体（起点隐含） | **拆两半**：撤回规则等引擎已支持项→ M2-8（发起节点抽屉变体）；发起范围/代提交/重提走向（依赖 E1 引擎裁决）→ **M8**——不做假配置 |
| 抄送节点（B2） | 引擎 FType 疑无独立抄送类型（执行时核实） | 抄送=自动处理「通知」子类的语义封装（图标/文案独立，底层复用通知 action）；若通知 action 也缺→菜单项隐藏、立 M8 |
| 令牌 --color-flow-auto/--color-flow-cc/--shadow-lift | `theme.ts` 无 | **M0** |

## 二、批次总览与依赖

```
M0 令牌+共享原语 ──→ M1 竖向流程图 ──→ M2 抽屉五Tab ──→ M4 矩阵+路由锁
                          │                                │
                          └──→ M3 条件编辑器+分支操作 ──────┤
                                                           ↓
                              M5 干跑工作台 ←──────────────┘
                              M6 发布与版本（可与 M5 并行）
                              M7 微交互/锁/无障碍（收尾抛光）
                              M8 二期：导入双模 + 引擎增强（独立拍板）
```

每批验收门槛（统一）：type-check 绿 + `build-filter cardflow` 绿 + 该批 vitest/xUnit 新测试绿 + preview 浏览器实测该批 golden path + **UI 保真核对（每个 UI 任务按 M0-5 固定收尾：与对应 mock 屏并排目检布局 + preview_inspect ≥5 项属性对 ui-baseline.md）** + 规约审（/rule-review）。M2-M7 展开细化 plan 时，每个 UI 任务必须注明其对应的 mock 屏编号（如 M2-1→A4-A8，M4-2→总体屏6+E3）。

---

## 三、M0：令牌与共享原语（预计 5 任务）

### Task M0-1: 流程色令牌三件套

**Files:**
- Modify: `web/src/stores/theme.ts`（令牌派生表）
- Modify: `web/docs/TOKENS.md`
- Modify: `web/src/styles/variables.scss`

**Interfaces:**
- Produces: CSS 变量 `--color-flow-auto`（浅 #722ed1/深 #9d66ff）、`--color-flow-cc`（浅 #fa8c16/深 #ffa940）、`--shadow-lift`（`0 10px 26px rgba(0,0,0,.16)`/深色配描边替代）——后续所有批次的节点着色唯一来源。

- [ ] Step 1: 读 `theme.ts` 现有令牌派生结构，按同款模式追加三条（浅/深两态）；hex 只出现在 theme.ts 派生源与 TOKENS.md 文档（豁免区），组件侧一律 var()。
- [ ] Step 2: TOKENS.md 增补三行语义说明（用途照抄设计 C11 表）。
- [ ] Step 3: `npm run type-check && npm run lint:style` 绿。
- [ ] Step 4: Commit `feat(theme): 新增流程设计器令牌 flow-auto/flow-cc/shadow-lift`。

### Task M0-2: EllipsisText / MemberSummary / ConditionSummary 三个边界态组件

**Files:**
- Create: `web/src/components/common/EllipsisText.vue`
- Create: `web/src/components/cardflow/designer/MemberSummary.vue`
- Create: `web/src/components/cardflow/designer/ConditionSummary.vue`
- Test: `web/src/components/__tests__/boundaryText.spec.ts`（vitest，node 环境纯逻辑部分）

**Interfaces:**
- Produces:
  - `EllipsisText` props `{ text: string; maxWidth?: string; lines?: 1|2 }`——尾部省略+自动 tooltip（仅溢出时挂）。
  - `MemberSummary` props `{ members: {id:string;name:string}[]; max?: number }`——"前2人 等N人"，点击 emit `expand`。
  - `ConditionSummary` props `{ conditions: RouteConditionDraft[]; maxLines?: number }`——"首条 + 等N条条件"，格式化函数 `formatCondition(c): string` **导出**供矩阵/diff 复用。
- Consumes: `RouteConditionDraft` 类型取自 `web/src/types/cardflow.ts` 现有 `StageRouteRuleRequest.conditions` 元素类型（执行时核对字段名 field/op/value）。

- [ ] Step 1: 写 `formatCondition` 的失败测试（金额 gte→`金额 ≥ 10000`、enum in→`费用类型 属于 [差旅]`、空条件→`—`）。
- [ ] Step 2: 跑测试确认红。
- [ ] Step 3: 实现三组件+纯函数；空值统一 em dash（设计 D3）。
- [ ] Step 4: vitest 绿 + type-check 绿。
- [ ] Step 5: Commit `feat(cardflow): 边界态三组件 EllipsisText/MemberSummary/ConditionSummary`。

### Task M0-3: 三态权限胶囊 PermissionTri

**Files:**
- Create: `web/src/components/cardflow/designer/PermissionTri.vue`
- Test: `web/src/components/__tests__/permissionTri.spec.ts`

**Interfaces:**
- Produces: props `{ value:'edit'|'read'|'hidden'; lockedStates?: ('edit'|'hidden')[]; lockReason?: string }`，emit `update:value`。锁定项渲染划线+tooltip 原因、点击无效（设计 C2"禁用永远给原因"）。M2 抽屉与 M4 矩阵共用此组件。

- [ ] Step 1-4: 同 TDD 节奏（测试锁定项点击不 emit → 红 → 实现 → 绿 → commit `feat(cardflow): 三态权限胶囊组件`）。

### Task M0-4: 保存状态胶囊 SaveStateChip

**Files:**
- Create: `web/src/components/common/SaveStateChip.vue`
- Modify: `web/src/views/cardflow/FlowDefinitionEditPage.vue`（顶栏接入，替换现状保存文案位）

**Interfaces:**
- Produces: props `{ state:'editing'|'saving'|'saved'|'failed'; savedAt?: string }` 四态视觉（设计 C5）；emit `retry`。编辑页现有自动保存逻辑映射到四态（防抖窗口内=editing）。

- [ ] Step 1: 组件实现+编辑页接线（现有 autoSave 状态机字段执行时定位，约 `:236` 历史栈附近）。
- [ ] Step 2: preview 实测：编辑→2s 后转"已保存 HH:mm"。
- [ ] Step 3: type-check 绿，commit `feat(cardflow): 顶栏保存状态四态胶囊`。

### Task M0-5: UI 保真基准——designer-tokens.scss + 基准表 + 核对流程

**Files:**
- Create: `web/src/styles/cardflow-designer.scss`（设计器专属 partial，从 spec 的 `mock-shared.css` 移植）
- Create: `docs/superpowers/specs/2026-07-07-cardflow-designer-mocks/ui-baseline.md`（保真基准表）
- Modify: `web/src/styles/index.scss`（引入 partial）

**Interfaces:**
- Produces: 全体后续 UI 任务的两件基准物：
  1. **`cardflow-designer.scss`**：把 mock-shared.css 中的结构类逐条移植为项目 SCSS——`.fnode/.nh/.ni/.nb`（节点卡片）、`.connector/.plus`（连接件）、`.bhead/.bcol/.prio`（分支）、`.drawer/.dtabs/.dbd`（抽屉）、`.tri`（权限胶囊）、`.fplist/.fprow`（配置列表）、`.optlist/.opt`（选项卡列表）、`.cgroup/.orsep`（条件组）等。**移植规则**：尺寸/间距/圆角/字号/字重/投影逐值照抄；颜色值全部替换为语义令牌 var()（mock #1677ff→`var(--color-primary)`，#722ed1→`var(--color-flow-auto)`，红黄绿灰→项目对应令牌，边线→`var(--color-border)` 系）；类名加 `cfd-` 前缀避免污染（`.fnode`→`.cfd-node`），映射表写入 ui-baseline.md。
  2. **`ui-baseline.md`**：三张表——①组件尺寸表（组件×属性×期望值，值取自 mock-shared.css+C11 规格屏）；②字号阶梯表（用途→font-size/weight/line-height）；③mock色→令牌映射表。每个 UI 任务的验收步骤引用此表做 `preview_inspect` 断言。
- **每个 UI 任务的固定收尾步骤（M1 起全批适用，展开细化 plan 时必须带上）**：
  a. mock 对应屏与实现页并排（mock 可直接本地开 spec 目录 html）；
  b. `preview_inspect` 抽查该任务核心元素 ≥5 项属性（width/height/padding/border-radius/font-size/box-shadow）与 ui-baseline.md 比对，偏差>1px 即修；
  c. 布局结构（栏位/分区/元素顺序）与 mock 屏逐块目检一致。

- [ ] Step 1: 编写 ui-baseline.md 三张表（从 mock-shared.css 与 C11/D3 提取，尺寸值逐条誊录不省略）。
- [ ] Step 2: 移植 cardflow-designer.scss（cfd- 前缀+令牌替换），index.scss 引入。
- [ ] Step 3: `npm run lint:style` 绿（零裸 hex）+ type-check 绿。
- [ ] Step 4: 写一个临时验证页（scratchpad 级，不入库）挂 .cfd-node/.cfd-drawer 各一枚，preview_inspect 对照基准表核 8 项值全中。
- [ ] Step 5: Commit `feat(cardflow): 设计器 UI 保真基准（样式 partial + 基准表）`。

---

## 四、M1：结构化竖向流程图（核心批，预计 6 任务）

> 风险最高批。核心是**投影规则**：现有扁平 `stages[] + routes[]` ↔ 竖向树。先锁投影纯函数并测透，再做渲染。

### Task M1-1: 投影纯函数 buildFlowTree（本批地基）

**Files:**
- Create: `web/src/utils/flowGraphProjection.ts`
- Test: `web/src/utils/__tests__/flowGraphProjection.spec.ts`

**Interfaces:**
- Consumes: `StageDefinition[]`、`StageRouteRuleRequest[]`（编辑页 state 现有类型）。
- Produces:
  ```ts
  export interface FlowTreeNode {
    kind: 'stage' | 'branchGroup' | 'terminal'
    stageId?: string                 // kind=stage
    branches?: FlowTreeBranch[]      // kind=branchGroup
  }
  export interface FlowTreeBranch {
    routeEdgeKey: string             // 对应 route 的 edgeKey（诊断 target 同键）
    isDefault: boolean               // 兜底列
    priority: number
    children: FlowTreeNode[]
  }
  export function buildFlowTree(stages: StageDefinition[], routes: StageRouteRuleRequest[]): { tree: FlowTreeNode[]; orphans: string[] }
  export function insertStageAfter(tree, anchor: InsertAnchor, newStage: StageDefinition): { stages; routes }  // 反向写回
  export function insertBranchGroup(tree, anchor, branchCount: number): { stages; routes }  // 自动含兜底 route
  ```
- **投影规则（锁死）**：① 同源节点多条带条件 route = 一个 branchGroup，无条件/default route = 兜底列恒最右；② 分支在下游汇合点（共同后继）收拢回主干；③ 投影失败（真 DAG 交叉/环）→ 该区段降级为"复杂区段"占位节点 + 提示到只读画布查看，**不阻塞其余区段编辑**；orphans 列表报孤儿节点进诊断。

- [ ] Step 1: 写测试组——线性链、单分支组两列+兜底、嵌套分支、汇合、孤儿节点、交叉边降级，≥8 用例（用现有 2331/2350 流程的真实 JSON 形状做夹具，执行时从 dev 库导一份脱敏夹具存 `__tests__/fixtures/`）。
- [ ] Step 2: 跑 vitest 红。
- [ ] Step 3: 实现 buildFlowTree（只读投影先行，insert* 反向写回随 M1-3）。
- [ ] Step 4: vitest 绿。
- [ ] Step 5: Commit `feat(cardflow): 竖向流程图投影纯函数 buildFlowTree`。

### Task M1-2: FlowVerticalGraph 渲染组件（只读）

**Files:**
- Create: `web/src/components/cardflow/designer/FlowVerticalGraph.vue`
- Create: `web/src/components/cardflow/designer/FlowGraphNode.vue`（节点卡片：类型图标/徽标/摘要行/错误角标/停用置灰，状态见设计 C2）

**Interfaces:**
- Consumes: M1-1 `buildFlowTree`；`cardflowDiagnostics.ts` 的 `HealthItem.target`（错误角标计数按 target.key 聚合）；**M0-5 `cardflow-designer.scss` 的 `.cfd-node/.cfd-connector/.cfd-plus/.cfd-branch*` 结构类（组件内不重写这些样式，只写状态修饰）**。
- Produces: props `{ stages; routes; diagnostics; selectedKey }`，emit `select(key)`、`insert(anchor)`（M1-3 接）。节点着色用 M0-1 令牌；尺寸规格以 ui-baseline.md 为准（节点 340px/分支列 max 250 gap 20）。
- **AntD 边界规则（全批适用）**：mock 中的结构性自绘元素（节点卡片/连接线/"+"/分支列/权限胶囊/条件组容器）用 cfd- 类实现；标准控件（下拉/开关/tabs/segmented/按钮/输入框）仍用 AntD 组件（项目规范），以局部样式覆盖对齐 mock 的尺寸与字号——不为像素级一致而手搓标准控件。

- [ ] Step 1: 组件实现（先只读：渲染树+选中态+错误角标，"+"渲染但 disabled）。
- [ ] Step 2: 接入编辑页 STEP_STAGES：**点节点 → 打开现有右侧抽屉（designerSelection 机制承载 StageConfigPanel）；点分支头 → 打开现有 edge 抽屉（RouteRuleCardEditor）作为条件编辑的过渡入口（M3 再升级为条件组弹层）**——竖向图上线首日即可编辑节点与条件，无断档。与现有 StageDefinitionEditor 左列表**并存一个开关期**。**视图 segmented 演进路线（锁死）**：过渡期=「流程视图/列表视图/只读总览图」；M1-6 收口去掉列表视图；M4-2 追加「字段权限矩阵」为第三项——最终态即 mock 总体屏 3 的三项。
- [ ] Step 3: preview 实测：加载 2331 流程 → 竖向图正确呈现、点节点开抽屉、点分支头开条件编辑。
- [ ] Step 4: **UI 保真核对（M0-5 固定收尾）**：与 mock 总体方案屏 3 并排比对布局结构；preview_inspect 核 .cfd-node 的 width/border-radius/头部高度/图标尺寸/字号 ≥5 项对 ui-baseline.md。
- [ ] Step 5: type-check 绿，commit `feat(cardflow): 竖向流程图只读渲染+编辑页接入`。

### Task M1-3: "+"插入菜单与反向写回

**Files:**
- Modify: `web/src/components/cardflow/designer/FlowVerticalGraph.vue`
- Modify: `web/src/utils/flowGraphProjection.ts`（insertStageAfter/insertBranchGroup 实现）
- Test: 追加投影 spec 用例（插入后 stages/routes 断言）

**Interfaces:**
- Produces: 四类插入（审批人/抄送人/条件分支/自动处理二级菜单）；插入走 `insert*` 纯函数产出新 stages/routes → 经编辑页 state 深监听自动进撤销栈+自动保存（**复用既有管道，不新建保存路径**——B9 教训：子组件持镜像+回抛会放大成整表替换，本组件无内部镜像、只 emit 纯数据）。
- 插入分支组时自动生成兜底 route（isDefault），新人工节点默认 FType='human'（既有坑：人工节点 FType 必须 'human'）。

- [ ] Step 1: 先写 insert* 反向写回测试（插入线性节点/插入分支组含兜底/分支内插入），跑红。
- [ ] Step 2: 实现纯函数至绿。
- [ ] Step 3: 菜单 UI（四类+自动处理二级：凭证/质检/通知/写入——仅生成对应 FType 与空配置，子类面板 M2 接）。
- [ ] Step 4: preview 实测：插入审批节点 → 撤销 → 重做 → 自动保存均正常；新节点挂"未配置处理人"警告（诊断既有规则覆盖则通过，缺则在 `cardflowDiagnostics.ts` 补一条 check）。
- [ ] Step 5: Commit `feat(cardflow): 竖向图插入菜单与反向写回`。

### Task M1-4: 兜底分支保护与分支操作（删除确认三要素/复制/调序）

**Files:**
- Modify: `web/src/components/cardflow/designer/FlowVerticalGraph.vue`
- Modify: `web/src/utils/flowGraphProjection.ts`（追加 `deleteBranch(tree, routeEdgeKey)` / `copyBranch(tree, routeEdgeKey)` / `reorderBranch(tree, routeEdgeKey, dir)` 三个反向写回纯函数，返回 `{stages; routes}`——与 insert* 同一模式）
- Create: `web/src/components/cardflow/designer/BranchDeleteConfirm.vue`

**Interfaces:**
- Produces: 兜底列无删除入口；非兜底分支删除弹 Modal 列三要素（支内节点数/流量去向=兜底/在途卡片数——在途数本批先显示"发布后生效期核算"占位文案，M6 接真数）；分支头 ⧉ 复制（深拷贝 route 条件+支内 stages 重生成 id）；优先级左右调序按钮（拖拽调序放 M7 抛光）。
- 删除走 toast 5s 撤销（复用撤销栈：删除本身即一步可撤销操作，toast 的"撤销"=触发 undo）。**竞态防护**：toast 的 undo 按「操作序号」定点撤销——若删除后用户已做了 N 步新编辑，点 toast 撤销时提示「将同时撤销其后 N 步操作」二次确认，或 N>0 时 toast 撤销按钮直接失效转提示「请用 Ctrl+Z 逐步撤销」（实现取后者，简单且无歧义）。

- [ ] Step 1: 投影 spec 补删除/复制用例（红→绿）。
- [ ] Step 2: UI 实现+preview 实测三要素弹窗与复制。
- [ ] Step 3: Commit `feat(cardflow): 分支删除保护/复制/调序`。

### Task M1-5: FlowStateCanvas 降级只读 + 诊断定位接线

**Files:**
- Modify: `web/src/views/cardflow/FlowDefinitionEditPage.vue`（`focusDiagnosticTarget` 增竖向图定位分支：target.kind=node→滚动+选中竖向图节点；kind=edge→高亮分支头）
- Modify: `web/src/components/cardflow/designer/FlowStateCanvas.vue`（编辑态入口移除，保留查看）

**Interfaces:**
- Consumes: 既有 `focusDiagnosticTarget`（B9 diag-nav 已建，`HealthItem.target{kind,key}`）。
- Produces: 诊断面板/发布校验点击 → 竖向图滚动定位+外环脉冲 2 次（设计 D9：360ms scroll + 2×600ms 脉冲；`prefers-reduced-motion` 降级一次性高亮）。

- [ ] Step 1: 接线+动效；Step 2: preview 实测诊断跳转；Step 3: commit `feat(cardflow): 诊断定位接入竖向图·画布降级只读`。

### Task M1-6: M1 批收口——开关期结束评估 + 整体回归

- [ ] Step 1: 用 dev 库 47 个流程定义跑投影冒烟（node 脚本登录遍历，参照 B7 dry-run 脚本模式，脚本存 scratchpad）。**通过判据（三条全满足）**：① `orphans` 为空；② 无"复杂区段"降级占位；③ 树中 stage 节点数 = stages 总数、分支列数 = 该源节点 route 数（守恒断言）。
- [ ] Step 2: 47 全过 → 移除「列表视图」回退开关与 StageDefinitionEditor 左列表入口（保留组件文件——抽屉仍复用其子块）；任一失败 → 保留开关、失败样本**导出为投影 spec 夹具**记 issue 进 M2 前置（失败样本即免费测试用例）。
- [ ] Step 3: 整批回归：type-check + vitest 全量 + build-filter cardflow + preview 实测（新建流程→插分支→发布门禁全链）。
- [ ] Step 4: Commit `feat(cardflow): M1 竖向流程图收口`。

---

## 五、M2-M7 任务卡（每批开工前展开细化 plan）

> 下列每张任务卡给出：文件锚点、接口契约、验收断言。展开细化时不得改契约，只许加步骤。

### M2 节点抽屉五 Tab + 节点类型变体（9 任务）

> **抽屉承载契约（M2-1 落定，全批遵守）**：StageConfigPanel 按 stage 类型渲染变体——人工节点=五 Tab；自动节点=「基础/子类配置/高级」三 Tab；发起节点=「发起范围/撤回与重提/字段权限」（B1，仅引擎已支持项）。变体分发在 StageConfigPanel 顶层 v-if，子面板各自独立文件。

| # | 任务 | 文件锚点 | 契约/验收 |
|---|---|---|---|
| M2-1 | Tab 重构：基础/处理人/字段权限/动作/高级 | `StageConfigPanel.vue:571-766`（现 3 Tab） | 现"节点视图"内容并入"字段权限"；Tab 带错误红点（按诊断 target=本 stage + tab 归属映射）；非人工节点禁用处理人/动作 Tab（现状已有 disabled 逻辑保留） |
| M2-2 | 基础 Tab：审批类型三态+条件行+节点说明+停用 | StageConfigPanel + `stageDefinitionShared.ts` | 自动通过/拒绝条件存 stage `FConfigJson.autoDecision{mode,conditions}`；**引擎消费**：`OrchestrationEngineService` 人工节点进入时评估 autoDecision（后端任务，xUnit：条件命中→自动通过留系统事件）；停用=stage `FStatus` 停用位，引擎跳过+图置灰 |
| M2-3 | 处理人 Tab：类型参数区+空缺兜底必填 | 同上；`ApproverResolver` 现有策略枚举为准 | 只暴露 resolver 已支持的类型（执行时枚举 `IApproverResolver` 实现清单）；空缺兜底 `FConfigJson.assigneeFallback{mode:'autoPass'|'transfer'|'admin', userId?}` + resolver 空结果时消费（后端任务+测试）；兜底未配=错误级诊断 |
| M2-4 | 处理人 Tab：会签比例 | 引擎 approvalMode 现状核实 | 若引擎仅 any/all：UI 仅出"依次/或签/会签"三态，比例通过**不做**（引擎增强进 M8）——不做假配置 |
| M2-5 | 字段权限 Tab：PermissionTri 接线+批量+明细列级 | StageConfigPanel + `useStageWorkView.ts` 口径 | 数据仍写 stage viewProfile（现有结构）；明细列级权限新增 `detailColumnAccess{tableKey:{col:access}}`，`CardRedactionService`/`SchemaRenderer` 消费（前后端各一任务）；🔒 敏感字段（B1 敏感列 baseline）锁"可编辑"、🔗 路由字段锁只读（读 M4-1 的引用索引，M4 前先按 routes 现算） |
| M2-6 | 动作 Tab：逐动作开关+意见必填+退回目标说明 | stage `FConfigJson.actions` 现状核实（DEFAULT_ACTIONS 在 stageDefinitionShared） | 意见必填=提交动作时校验（`CardService` 动作链，后端任务+测试）；自定义动作/挂自动处理**不做**（M8）；退回目标=只读说明文字指向发起节点配置 |
| M2-7 | 高级 Tab：超时提醒一级 + 恢复默认 | Hangfire 超时 Job 现状核实 | 若无超时 Job：本批只落**一级超时提醒**（新 Job `StageTimeoutReminderJob`，per-tenant 迭代用 `ITenantIterationService`——CLAUDE.md 硬约束）；三级升级链进 M8；节点级设置>全局默认的继承显示（灰斜体）。**全局默认的存储与 UI**：流程级默认值存 definition `FConfigJson.defaults{timeoutHours,...}`，配置区落 STEP_SETTINGS（mock 屏 8 的开关式布局）——M2-7 一并落此步，继承链才成立 |
| M2-8 | 自动处理子类面板（凭证/质检/通知/写入） | Create `AutoStagePanel*.vue` ×核实后子集；引擎现有自动 stage 的 FConfigJson 结构为准 | **先核实引擎四子类现状**（凭证节点已有=2331 链；质检=质量模块接线核实；通知/写入核实 action 类型）：已支持的落配置面板（B3 mock 布局）+失败策略必配区；不支持的子类菜单项不出现、立 M8。凭证面板含 M5-3 试算按钮位（M5 前禁用+tooltip"随干跑工作台上线"） |
| M2-9 | 发起节点抽屉变体 | StageConfigPanel 变体分发 | 撤回规则（进行中允许撤回开关——引擎 revoke 链已有则接线，无则本项也进 M8）；字段权限 Tab 复用 M2-5；发起范围/代提交/重提走向显示为「二期」占位说明（灰卡，不可配——诚实呈现而非假配置） |

### M3 条件编辑器与分支操作（4 任务）

| # | 任务 | 文件锚点 | 契约/验收 |
|---|---|---|---|
| M3-1 | 条件组容器视觉（组内且/组间或） | `ConditionBuilder.vue`（559 行） | 数据结构核实：现有 conditions 是否已分组；若扁平→UI 分组层落在 route 条件 JSON `{groups:[{conditions:[]}]}`，`StageRouteResolver` 消费兼容旧扁平（后端任务：旧格式读入视为单组，写出新格式；xUnit 双格式解析）。**级联修正**：新格式落地同批更新 ①`formatCondition`/`ConditionSummary`（M0-2）签名改收 `RouteConditionGroup[]`（组间「或」分隔渲染）②M4-1 `buildRouteFieldIndex` 遍历双格式 ③M6-1 diff 的条件比较按组粒度——三处消费方在 M3-1 的细化 plan 中列为同批任务，不得跨批悬置 |
| M3-2 | 算子按类型 + 人员/组织算子 | ConditionBuilder + FieldOption 类型 | 金额/数字：= ≠ > ≥ < ≤ 介于；单选：属于/不属于；人员：是/属于部门/属于角色；组织：等于/在子树内（子树判定用 SYS组织闭包表，后端 resolver 任务）；算子-类型映射表 vitest 锁定 |
| M3-3 | 条件字段下拉三组+禁用说明+去修复链接 | ConditionBuilder | 表单/派生/系统三组；非必填字段灰显+「去卡片设计设为必填→」跳 STEP_SCHEMA 并高亮字段（复用 focusDiagnosticTarget 模式） |
| M3-4 | 命中率试算 | 新端点（后端需求清单 #5）+ ConditionBuilder 展示 | 三态降级（E6）：全量/部分覆盖标注/零历史隐藏；0%/100% 标红；惰性计算+缓存至条件变化 |

### M4 字段权限矩阵 + 路由依赖锁（4 任务）

| # | 任务 | 文件锚点 | 契约/验收 |
|---|---|---|---|
| M4-1 | 路由引用索引 buildRouteFieldIndex | Create `web/src/utils/routeFieldIndex.ts` | `(routes)=>{ fields: Map<fieldKey, edgeKey[]>; options: Map<fieldKey+'.'+optValue, edgeKey[]> }`；vitest 锁定；schema 编辑器消费：删字段/改类型/取消必填/删被引用选项→拦截并列引用方（E0-M2 强度：拦截非确认）。**存量兼容**：现存流程可能已有"被引用字段非必填"，此形态记**警告级**诊断（提示去补必填）而非错误——只有"引用了 schema 中不存在的字段"才是错误级；新配置侧（M3-3 下拉）从源头只允许必填字段 |
| M4-2 | 矩阵视图组件 | Create `FieldPermissionMatrix.vue`；编辑页顶栏 segmented 第三项接入 | 列=深度优先序（M1-1 树遍历即得）+分支色带组头+兜底恒组尾；行=schema 字段+分组折叠+敏感行高亮；单元格=PermissionTri；数据直读写 stages（无副本）；漏配隐藏告警（敏感字段在非发起节点非隐藏→警告级诊断，落 `cardflowDiagnostics.ts` 新 check+快照测试） |
| M4-3 | 列头批量下拉 | FieldPermissionMatrix | 整列只读/隐藏/复制左列/打开抽屉 Tab③；锁定单元格豁免+toast 计数（E3） |
| M4-4 | 摘要字段设置 | STEP_SETTINGS 或 schema 步内新区块；后端 FSummaryFields 存储核实 | 最多 3 个+拖拽排序+实时预览待办卡；敏感字段/明细表禁选（A3）；待办列表消费端接线 |

### M5 干跑工作台（5 任务）

| # | 任务 | 文件锚点 | 契约/验收 |
|---|---|---|---|
| M5-0 | 干跑工作台化+步骤条改四步 | `FlowDefinitionEditPage.vue`（STEPS 数组 `:189`）+ PathPreviewPanel 升格 | preview 从 STEPS 摘除（五步→四步，对齐 mock 屏 1 步骤条——UI 保真硬约束的一部分）；顶栏「预览」按钮唤起干跑工作台（全屏抽屉或独立视图，布局按 mock 屏 7 三栏：样例输入/路径推演/卡片呈现）；现 preview 步内容整体迁入；深链/书签兼容：activeStep 越界回落 STEP_STAGES |
| M5-1 | 样例值三来源 | `PathPreviewPanel.vue` + 新端点（需求 #6 历史卡片取样） | 手填/历史卡片代入（跨版本缺字段标黄）/随机生成（按类型合法随机）；必填豁免=只强制 🔗 路由字段（M4-1 索引） |
| M5-2 | 失败态推演 | `CardFlowPathPreviewService`（后端） | 三类失败不终止：解析失败→标注+按兜底推演；无命中→兜底列；自动节点试算失败→按失败策略推演；StepDto 增 `failure{kind,message,fallbackApplied}`；xUnit 三场景 |
| M5-3 | 凭证节点试算 | 新端点（需求 #7）+ 抽屉凭证子面板展示 | 复用凭证引擎 rulesBased 干跑（纯内存不落库），返回借贷分录预览；无匹配规则→失败态口径同 M5-2；同批解禁 M2-8 预留的试算按钮 |
| M5-4 | 路径点亮动效+步骤联动 | PathPreviewPanel + FlowVerticalGraph | 命中路径绿色 stagger 点亮（D9：每段 160ms/stagger 80ms）；点步骤↔图节点双向联动（B4 已有 step-select 基础） |

### M6 发布与版本（6 任务）

| # | 任务 | 文件锚点 | 契约/验收 |
|---|---|---|---|
| M6-1 | 结构 diff 纯函数 | Create `web/src/utils/flowVersionDiff.ts` | `(oldVer, newVer)=>ChangeItem[{kind:'add'|'modify'|'remove', scope:'stage'|'route'|'field', label, detail}]`；条件值级 diff 内联（旧删除线→新加粗，E1/P-2）；vitest 锁定 |
| M6-2 | 发布确认弹窗 | Create `PublishConfirmModal.vue`；`FlowDefinitionEditPage` 发布链改造 | 门禁结果内嵌（绿可发/红禁用+链诊断）；警告知情确认勾选（C4）；变更清单=M6-1；在途策略必选二选一（**在途数为 0 时策略区隐藏、直接发布**——首版流程无旧版可保） |
| M6-3 | 在途卡片计数+迁移 | 后端需求 #3/#4 | 计数按 definitionId+version 聚合进行中卡片；迁移=被删节点上的卡片移入后继+迁移日志（xUnit：迁移幂等/失败逐张列出不整体回滚）；M1-4 删除确认三要素的在途数占位同批接真 |
| M6-4 | 版本历史页+回滚 | 版本列表 UI（编辑页侧栏或列表页入口）；后端需求 #2 | 每版本在途数常驻；回滚=`CreateDraftFromVersionAsync`（快照转草稿，仍走门禁+发布确认——不绕发布唯一入口）；**已有未发布草稿时回滚被拦截**：提示「先发布或放弃当前草稿」（单草稿不变量——definition 至多一份草稿，与现有 draft-version 模型一致，执行时核实） |
| M6-5 | 复制流程+引用重绑 | 列表页复制链改造；后端需求 #8 引用扫描 | 复制含**目标组织**参数（同组织=直接复制跳过弹窗；跨组织=E5 重绑弹窗）；悬空（成员/规则组/账套/暂存表/bot）→重绑弹窗；未处理悬空=错误级诊断阻发布不阻编辑 |
| M6-6 | 列表页双态+新建入口 | `FlowDefinitionListPage`（执行时核实文件名） | 「已发布/有未发布草稿」双态行（A1 mock 黄行）+「继续编辑/放弃草稿」入口（放弃草稿接口已有——flowdef 大修批新增，接线即可）；新建弹窗=空白/复制二选（**模板库项不出**——模板中心属 M8，不出假入口）；在途卡片数列接需求 #3 |

### M7 微交互/锁/无障碍（5 任务）

| # | 任务 | 契约/验收 |
|---|---|---|
| M7-1 | 编辑锁+接管协议 | 后端需求 #9（锁表+心跳+接管三端点）；前端只读横幅+接管弹窗；E7 不变量：弹窗时强制 flush、全局唯一请求、移交原子序列；xUnit：并发接管仅一胜。**「弹窗时强制 flush」的实现依赖持锁端在线推送**——接管请求经 SignalR（既有 ProgressHub 模式起新 Hub 或复用）通知持锁端触发 flush+弹窗；持锁端离线（无 SignalR 连接）→ 直接走心跳超时路径（其最后一次自动保存即最终态，丢失窗口≤防抖 2s，可接受并写入锁接口文档） |
| M7-2 | 撤销历史下拉+跳回 | 现有撤销栈（编辑页 :1613）加历史下拉（操作语义标签在入栈时记录）；跳回=连续 undo/redo 到目标位 |
| M7-3 | 插入时序动效 | D1 六帧：让位→生长→抽屉重叠滑入；FLIP 限定局部容器；reduced-motion 降级 80ms fade |
| M7-4 | 键盘导航+ARIA | 画布 roving tabindex（↑↓节点/←→分支/Enter 抽屉/Del 删除）；aria-label 按 D8 模板；快捷键面板 `?` |
| M7-5 | 断点四档+骨架屏 | D5 容器查询按画布区宽；D6 骨架三形状+P0-P3 分级加载（干跑预热=进 STEP_STAGES 时预热一次 path-preview） |

### M8 二期（本 plan 只立项不展开，独立拍板）

导入触发型双模（E4 全套）／引擎增强：超时三级升级链、去重节点例外、自定义动作挂自动处理、会签比例、M2 核实中裁掉的自动子类与撤回链／发起节点三件：发起范围、代提交、重提走向+E1 强制重路由（`OrchestrationEngineService` returned 重提链）／模板库（跨组织模板中心，含 A1 模板创建入口与 E5 模板占位符剥离）。

---

## 六、后端需求清单（新端点/字段签名）

| # | 端点/能力 | 签名 | 归属批 |
|---|---|---|---|
| 1 | 明细列级权限消费 | viewProfile JSON 增 `detailColumnAccess`；`CardRedactionService.NormalizeAccess` 扩展（fail-closed 基准不变） | M2-5 |
| 2 | 回滚 | `POST api/cardflow/definitions/{id}/versions/{versionId}/create-draft` → 新草稿 | M6-4 |
| 3 | 在途计数 | `GET api/cardflow/definitions/{id}/inflight-summary` → `{byVersion:[{version,count,stuckStageIds}]}` | M6-3 |
| 4 | 在途迁移 | 发布请求体增 `inflightPolicy:'keepOld'|'migrate'`；迁移日志表 `CF版本迁移日志`（版本化 seeder 建表） | M6-3 |
| 5 | 命中率试算 | `POST api/cardflow/definitions/{id}/route-hit-estimate` body=conditions → `{total,withValue,hit}`（近30天卡片采样上限 500 张——采样即可，勿全量） | M3-4 |
| 6 | 历史卡片取样 | `GET api/cardflow/definitions/{id}/sample-cards?keyword=` → 卡片摘要+cardData（**经当前用户可视域过滤**，敏感字段按发起视角脱敏）。**注意**：干跑输入若代入脱敏后的值，路由字段为敏感字段时推演会失真——取样响应对 🔗 路由字段保留原值、其余敏感字段脱敏（路由字段本就要求可判定类型，实际重叠极小，但须显式处理） | M5-1 |
| 7 | 凭证试算 | `POST api/cardflow/definitions/{id}/draft-version/preview-voucher` body=stageKey+cardData → 分录预览（纯内存，复用凭证引擎；模式对齐 preview-presentation 端点） | M5-3 |
| 8 | 引用扫描 | `POST api/cardflow/definitions/{id}/copy` 响应增 `unresolvedRefs[{kind,label,path}]` | M6-5 |
| 9 | 编辑锁 | `CF定义编辑锁`表（definitionId 唯一键/holder/heartbeatAt）；`POST .../lock/acquire|heartbeat|takeover-request|takeover-respond`；心跳 30s、超时 2min 释放 | M7-1 |

全部端点：`[RequirePermission("cardflow:definition:update")]`（读类 view）；`ApiResult<T>` 泛型；控制器路由全小写；租户隔离走既有 `ITenantScoped` 墙（新表实现 ITenantScoped+F租户ID）。**新建两表（#4 迁移日志/#9 编辑锁）同时实现 `IOrgScoped`**（组织归属跟随 definition），建表走版本化 seeder V 编号、原生 SQL 用 `SeederHelper.ExecuteRawSql`。

## 六.五、批间桥接契约（跨批依赖的显式清单）

| 依赖 | 提供方→消费方 | 过渡期行为 |
|---|---|---|
| 在途卡片数 | M6-3 → M1-4 删除确认 / M6-6 列表列 | M6 前显示占位文案「发布后核算」 |
| 凭证试算按钮 | M5-3 → M2-8 凭证面板 | M5 前按钮禁用+tooltip |
| 路由字段锁只读 | M4-1 索引 → M2-5 字段权限 | M4 前 M2-5 内联按 routes 现算（M4-1 落地后替换为索引调用，签名兼容） |
| 条件组格式 | M3-1 → M0-2 formatCondition / M4-1 / M6-1 | M3-1 细化 plan 内列三消费方同批改（不跨批悬置） |
| 干跑失败态 | M5-2 → M2-3 兜底配置的干跑预警（A5 mock） | M5 前兜底区不显示干跑预警行 |
| 视图 segmented | M1-2 三项过渡 → M1-6 去列表 → M4-2 加矩阵 | 演进路线锁死于 M1-2 Step 2 |

## 七、风险与回滚

- **最大风险=M1 投影**：真实 47 流程若有投影盲区，靠 M1-6 冒烟兜底+「列表视图」开关回退；开关移除前旧入口不删。
- **假配置风险**：M2 明确"引擎不支持则 UI 不出"（M2-4/M2-6/M2-7 的裁剪决策），杜绝配了不生效。
- **并发会话共用主树**：本 plan 执行须 worktree 隔离（`.claude/worktrees/`），node_modules junction 复用（B9 经验：worktree npm install 会杀 junction，勿在 worktree 装依赖）。
- **CardFlow.Tests flaky**：判回归多跑 2-3 次，勿信单次 tail 退出码。
- 每批独立可发布：任何批中止，已并批次自洽可用。

## 八、Self-Review 结论（2026-07-07 二次审查后更新）

- 覆盖检查：54 屏 → M0-M8 映射完毕；显式不做项（假配置类）均落 M8 立项而非静默丢失；发起/抄送节点补入差距地图（首版审查遗漏）。
- 类型一致：`FlowTreeNode/FlowTreeBranch`（M1-1）被 M1-2/M4-2/M5-4 消费；`PermissionTri`（M0-3）被 M2-5/M4-2 消费；`formatCondition`（M0-2）被 M3/M6-1 消费且 M3-1 改组格式时三消费方同批级联（六.五桥接表）——签名以本文为准。
- 占位符扫描：M2-M7 为任务卡（批前展开细化是本 plan 的声明式工作方式，非遗漏）；M0/M1 无 TBD。
- **二次审查修订记录**（矛盾 3 + 缺陷 7，均已就地修订）：
  - 矛盾①五步 vs mock 四步与 UI 保真硬约束冲突 → 改为 M5-0 摘除 preview 步对齐四步；
  - 矛盾②保存胶囊 M0-4 落地但差距地图写 M7 → 地图更正 M0-4/M7-2 分列；
  - 矛盾③"数据模型不动"与 M3-1 条件组新格式 → 定性为 JSON 内格式演进+读旧写新兼容，并补三消费方级联（六.五）；
  - 缺陷①M1 期抽屉/条件编辑入口断档 → M1-2 Step 2 明确点节点开抽屉、点分支头开 RouteRuleCardEditor 过渡；
  - 缺陷②发起/抄送节点无归属 → 差距地图两行+M2-8/M2-9 任务卡+M8 兜底；
  - 缺陷③toast 撤销竞态 → N>0 失效转 Ctrl+Z 提示；
  - 缺陷④M1-4 缺 delete/copy/reorder 纯函数契约 → 补入 Files；
  - 缺陷⑤M6-4 回滚撞现存草稿 → 单草稿不变量拦截；M6-2 在途 0 隐藏策略区；M6-6 列表页双态补任务；
  - 缺陷⑥需求 #6 脱敏与干跑失真冲突 → 路由字段保留原值；
  - 缺陷⑦E7 flush 依赖在线通道未声明 → SignalR 通道+离线走心跳路径；新表补 IOrgScoped+seeder 规范；M1-6 通过判据从"或"改为三条全满足+失败样本转夹具。
