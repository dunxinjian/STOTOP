# CardFlow 设计器防误/反馈速赢（设计稿）

> 日期：2026-06-20　状态：设计已确认（纯前端行为修复，4 项小改）
> 上游：CardFlow 三轮审计（[统一报告](../../audits/audit-report-0-unified.md)）§五速赢清单。CardFlow ③ 节点路由 backlog 已收官，转清设计器 UX 速赢（行为类）。
> 本轮选「防误操作 + 真实反馈」一批（#1/#3/#4/#6）；#2 enum/人员/组织值选择器、#5 动态加签折叠各自后续。
> 本文为方案文档，**不修改代码**；结论带真实 file:line（2026-06-20 复核，代码自审计后有演进，已重核现状）。

---

## 0. 背景与缺陷（已核实现状）

设计器（流程定义编辑页及其子组件）有一批小的「误导/无反馈/无防护」缺陷，逐项核实当前代码：

| # | 缺陷（现状核实） | 控制点 |
|---|---|---|
| 1 | **自动保存 pill 编辑后短时假显「已保存」**：`dirty` 已正确置位/重置，但 `auto.markDirty()` 从未调用，故 `auto.saveState` 编辑后不立即变 `dirty`，pill 在下一次 30s flush 前一直显示「已保存」——对用户撒谎、诱导切走丢草稿。 | [FlowDefinitionEditPage.vue:224](../../../web/src/views/cardflow/FlowDefinitionEditPage.vue:224)、[useAutoSave.ts:28](../../../web/src/composables/useAutoSave.ts:28) |
| 3 | **字段编辑保存失败静默 return**：`commitEditor` 空 key（:137）、重复 key（:139）两处直接 `return`，无任何 `message`，用户点确认后「什么都没发生」。该组件未 import `message`。 | [SchemaFieldEditor.vue:137](../../../web/src/components/cardflow/SchemaFieldEditor.vue:137) |
| 4 | **删节点无二次确认**：`removeStage` 直接 `splice`，点删即删（其它删除如条件项有 popconfirm，此处无防护）。 | [StageDefinitionEditor.vue:197](../../../web/src/components/cardflow/StageDefinitionEditor.vue:197)、按钮 [:823](../../../web/src/components/cardflow/StageDefinitionEditor.vue:823) |
| 6 | **离开页用原生 `window.confirm`**：`goBack` 脏态弹原生 confirm（样式与系统不一致）。 | [FlowDefinitionEditPage.vue:1676](../../../web/src/views/cardflow/FlowDefinitionEditPage.vue:1676) |

### 已核实的关键事实
- **保存状态机**：`useAutoSave` 暴露 `markDirty()`（置 `saveState='dirty'`，saving 时不置）与 `flush()`（30s 周期，isDirty 则 saving→save→saved/error）。页面 `dirty` ref 由 `watch(() => state, … , {deep:true})` 置 true；`silentSave` 置 false（[:1356](../../../web/src/views/cardflow/FlowDefinitionEditPage.vue:1356)）；`handleSaveDraft`（手动保存）`silentSave` 后显式 `auto.saveState.value='saved'`（[:1377](../../../web/src/views/cardflow/FlowDefinitionEditPage.vue:1377)）；`loadData` `await nextTick()` 后重置 `dirty=false`+`saveState='saved'`（[:1158-1161](../../../web/src/views/cardflow/FlowDefinitionEditPage.vue:1158)）。**故所有重置点都同时复位 `saveState`**——在 watcher 里补 `markDirty()` 安全（load 的 nextTick+重置在 watcher 之后覆盖）。
- `FlowDefinitionEditPage.vue` 已 `import { message } from 'ant-design-vue'`（:19）、`watch` 自 vue（:17）；需补 `Modal`。
- `SchemaFieldEditor.vue` / `StageDefinitionEditor.vue` 均未 import `message`/`Modal`。
- **前端无测试运行器**（无 `.spec/.test`、无 vitest/jest 配置）；验证走 `vite build` + 用户 9001 live 会话。改动全在 `<script>`，不碰 `<style>`、不加裸 hex（stylelint/裸 hex 不受影响）。
- 页面有 `useUndoRedo`/`useAutoCommit`，删节点经 emitUpdate 入 undo 栈，**可撤销**——故删确认文案不写「不可撤销」。

---

## 1. 目标 / 非目标

### 目标
1. 自动保存 pill 编辑后**立即**显示「未保存的更改」（补 `auto.markDirty()`）。
2. 字段编辑保存失败（空 key / 重复 key）给 `message.warning` 反馈。
3. 删节点弹 `Modal.confirm` 二次确认。
4. 离开页脏态用 `Modal.confirm` 替代原生 `window.confirm`。

### 非目标（明确不做）
- ❌ `onBeforeRouteLeave` 路由守卫（拦浏览器后退/直接改地址栏）——本轮只换 `goBack` 的 confirm；router-guard + 异步 Modal 是更大增强，另排。
- ❌ #2 enum/人员/组织值选择器（任务级阻断，最重，单独一轮）、#5 动态加签折叠——各自后续。
- ❌ 不动求值/保存逻辑本身、不改 `useAutoSave`、不碰 `<style>`/视觉、不加路由/状态字段。

---

## 2. 设计

### 2.1 自动保存 pill 真实化（#1，`FlowDefinitionEditPage.vue`）
把 `<script>` 里 `watch(() => state, () => { dirty.value = true }, { deep: true })`（[:224](../../../web/src/views/cardflow/FlowDefinitionEditPage.vue:224)）**移到 `auto = useAutoSave(...)` 定义之后**，并补 `auto.markDirty()`：

```js
const dirty = ref(false)

const auto = useAutoSave({
  intervalMs: 30_000,
  isDirty: () => dirty.value,
  save: () => silentSave(),
})

watch(() => state, () => { dirty.value = true; auto.markDirty() }, { deep: true })
```

`saveStateText`/pill 模板不变。编辑→`markDirty`→`saveState='dirty'`→pill「未保存的更改」；flush/手动保存/loadData 各自复位回「已保存」。

### 2.2 字段保存失败提示（#3，`SchemaFieldEditor.vue`）
顶部加 `import { message } from 'ant-design-vue'`；`commitEditor`（[:133](../../../web/src/components/cardflow/SchemaFieldEditor.vue:133)）：

```js
function commitEditor() {
  if (editingIndex.value < 0 || !draft.value) return   // 内部不变量，保持静默
  const key = (draft.value.key || '').trim()
  if (!key) { message.warning('请填写字段标识（key）'); return }
  const dup = fields.value.some((f, i) => i !== editingIndex.value && f.key === key)
  if (dup) { message.warning(`字段标识「${key}」已存在`); return }
  fields.value[editingIndex.value] = clone(draft.value)
  emitUpdate()
  cancelEditor()
}
```

### 2.3 删节点二次确认（#4，`StageDefinitionEditor.vue`）
顶部加 `import { Modal } from 'ant-design-vue'`；`removeStage`（[:197](../../../web/src/components/cardflow/StageDefinitionEditor.vue:197)）拆为「确认 + 执行」：

```js
function removeStage(idx: number) {
  Modal.confirm({
    title: '删除节点',
    content: `确定删除节点「${stages.value[idx]?.name || '未命名节点'}」？该节点的路由/配置将一并移除。`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk: () => doRemoveStage(idx),
  })
}

function doRemoveStage(idx: number) {
  stages.value.splice(idx, 1)
  if (selectedIndex.value === idx) {
    selectedIndex.value = -1
  } else if (selectedIndex.value > idx) {
    selectedIndex.value--
  }
  emitUpdate()
}
```

模板删除按钮（`@click.stop="removeStage(index)"`）不变。

### 2.4 离开页 Modal.confirm（#6，`FlowDefinitionEditPage.vue`）
import 改 `import { message, Modal } from 'ant-design-vue'`；`goBack`（[:1674](../../../web/src/views/cardflow/FlowDefinitionEditPage.vue:1674)）：

```js
function goBack() {
  if (!dirty.value) {
    router.push('/cardflow/definitions')
    return
  }
  Modal.confirm({
    title: '有未保存的更改',
    content: '离开将丢失未保存的更改，确定离开吗？',
    okText: '确定离开',
    okType: 'danger',
    cancelText: '取消',
    onOk: () => router.push('/cardflow/definitions'),
  })
}
```

---

## 3. 兼容性与风险

- **纯行为、纯 `<script>`**：不碰 `<style>`/视觉/裸 hex；不改 `useAutoSave`、保存/校验逻辑本身；不加路由/状态字段。
- **#1 watcher 移位无副作用**：`dirty` ref 定义在 `auto` 之前不变（`isDirty` 闭包需要它）；watcher 移到 `auto` 之后，仍 deep 监听 `state`；load 的 `nextTick`+重置在 watcher 之后复位 `saveState`，不会卡脏态。手动保存/flush 已各自复位。
- **#3/#4 新增 import**：两组件新增 `message`/`Modal` from ant-design-vue（全局已注册），无新依赖。
- **#4 删确认异步化**：`Modal.confirm` 回调式，`onOk` 执行 `doRemoveStage`；删行为本身逻辑逐字保留，仅加确认门。可撤销文案据实。
- **#6 confirm 异步化**：原生 confirm 同步 → Modal 异步回调；`goBack` 重构为「无脏直接走 / 脏弹 Modal onOk push」，等价。
- 风险低：4 处局部行为改动，无跨组件契约变化。

---

## 4. 验证（前端无测试运行器）

- **`vite build` 通过**（`cd web && npm run build` 或等价）——确认 4 处改动无引入语法/类型/编译错（vue-tsc 基线本就红、非门禁，以 vite build 为准）。
- **改动文件 stylelint 0**（实际未碰 `<style>`，确认无回潮）。
- **用户 9001 live 会话逐项验**（我不自起 vite，避免双 Vue 冲突）：
  1. 编辑任意字段/节点 → pill 立刻变「未保存的更改」；30s 后或手动保存 → 回「已保存」。
  2. 字段抽屉填空 key / 重复 key 点确认 → 弹 warning，抽屉不静默关。
  3. 节点删除按钮 → 弹「删除节点」确认框，取消则不删、确认才删。
  4. 有未保存更改点「返回」→ 弹 ant Modal（非原生 confirm），取消留页、确认离开。

---

## 5. 任务分解预览（供 writing-plans）
1. #1 pill markDirty（watcher 移位 + markDirty）。
2. #3 SchemaFieldEditor commitEditor 两处 message.warning（+ import message）。
3. #4 StageDefinitionEditor removeStage→Modal.confirm + doRemoveStage（+ import Modal）。
4. #6 FlowDefinitionEditPage goBack→Modal.confirm（+ import Modal）。
5. 收尾：`vite build` 绿；交用户 live 验四项。

> 改动小、无 TDD（前端无测试运行器）；执行建议走 **inline（executing-plans）+ build 检查点 + 用户 live**，非 subagent-driven。转 writing-plans 细化。
