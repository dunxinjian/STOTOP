# 指标项拖入"指标Tab"改归属 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 允许用户把当前 Tab 树里可见的「指标」类型叶子项拖到标签栏的"运营指标"指标 Tab 上，从而把它移成全局指标分区的直接子项（无分区则懒创建）。要拖其它 Tab 的指标项需先切到该 Tab（树一次只渲染当前 Tab）。

**Architecture:** 纯前端 + 复用现有更新接口。a-tree 的 `@dragstart` 记下被拖项 id；指标 Tab 标签加原生 `@dragover/@dragleave/@drop`；落下时校验是指标项、`ensureIndicatorSection()`（懒创建）、`updateAmoebaPLItem` 改 `parentId`、重载。后端不动。

**Tech Stack:** Vue 3 `<script setup>` + ant-design-vue 4（Tree/Tabs，原生 HTML5 拖拽）+ TypeScript；契约测试 `scripts/tests/*.mjs`（Node `assert` 静态断言）；类型检查 `vue-tsc`。

---

## 测试方式说明

前端无组件测试框架。验证三层：① 契约测试（静态文本断言）`node scripts/tests/amoeba-indicator-drag-to-tab-contract.test.mjs`；② 类型检查 `npm --prefix web run type-check`（仅看本文件是否零错误，项目存在约 556 条预存无关错误）；③ **运行态人工拖拽**（HTML5 拖拽无法用预览/eval 可靠模拟，需真人拖）。契约测试 Task 1 先红，实现后转绿。

## 文件结构

- 改：`web/src/views/finance/AmoebaPLTemplate.vue`（唯一业务文件：模板加事件、script 加 ref+处理函数、style 加高亮）
- 新增：`scripts/tests/amoeba-indicator-drag-to-tab-contract.test.mjs`

复用的现有函数（不改）：`parseItemKey`、`getNodeRole`、`ensureIndicatorSection`、`buildItemUpdatePayload`、`computeNextSortOrder`、`updateAmoebaPLItem`、`loadTemplateItems`。

---

## Task 1: 契约测试（红）

**Files:**
- Create: `scripts/tests/amoeba-indicator-drag-to-tab-contract.test.mjs`

- [ ] **Step 1: 写契约测试**

```js
import fs from 'node:fs'
import assert from 'node:assert/strict'

const read = (p) => fs.readFileSync(new URL(`../../${p}`, import.meta.url), 'utf8')
const tpl = read('web/src/views/finance/AmoebaPLTemplate.vue')

// 1) a-tree 记录被拖项
assert.match(tpl, /@dragstart="onTreeDragStart"/, 'a-tree 应监听 dragstart 记录被拖项')
assert.match(tpl, /@dragend="onTreeDragEnd"/, 'a-tree 应监听 dragend 清状态')
assert.match(tpl, /const\s+draggingItemId\s*=\s*ref/, '应有 draggingItemId ref')

// 2) 指标 Tab 标签作为放置目标
assert.match(tpl, /@dragover="onIndicatorTabDragOver"/, '指标Tab标签应监听 dragover')
assert.match(tpl, /@drop\.prevent="onIndicatorTabDrop"/, '指标Tab标签应监听 drop')
assert.match(tpl, /indicator-tab--drop-active/, '应有放置高亮类')

// 3) 落下处理：校验指标项 + 懒创建 + 改父
assert.match(tpl, /async\s+function\s+onIndicatorTabDrop/, '应有 onIndicatorTabDrop 处理函数')
assert.match(tpl, /只有指标项可移入指标分区/, '非指标项应被拒收并提示')
assert.match(tpl, /onIndicatorTabDrop[\s\S]*ensureIndicatorSection\(\)/, '落下处理应懒创建指标分区')
assert.match(tpl, /onIndicatorTabDrop[\s\S]*updateAmoebaPLItem\(/, '落下处理应调用 updateAmoebaPLItem 改父')

console.log('Amoeba indicator drag-to-tab contracts are aligned.')
```

- [ ] **Step 2: 运行，确认失败**

Run: `node scripts/tests/amoeba-indicator-drag-to-tab-contract.test.mjs`
Expected: FAIL（AssertionError，找不到 `onTreeDragStart` 等——功能未实现）

- [ ] **Step 3: 提交**

```bash
git add scripts/tests/amoeba-indicator-drag-to-tab-contract.test.mjs
git commit -m "test(amoeba): 指标项拖入指标Tab 契约测试(红)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 实现拖入指标Tab改归属

**Files:**
- Modify: `web/src/views/finance/AmoebaPLTemplate.vue`

- [ ] **Step 1: a-tree 加 @dragstart/@dragend**

定位 a-tree 元素，在 `@drop="handleTreeDrop"` 之后插入两行。

OLD:
```
              @select="handleTreeSelect"
              @drop="handleTreeDrop"
              @expand="handleTreeExpand"
```
NEW:
```
              @select="handleTreeSelect"
              @drop="handleTreeDrop"
              @dragstart="onTreeDragStart"
              @dragend="onTreeDragEnd"
              @expand="handleTreeExpand"
```

- [ ] **Step 2: 指标 Tab 标签加放置监听 + 高亮绑定**

定位指标 Tab 标签 span，改为带 `:class` 与三个原生监听。

OLD:
```
              <span class="dir-tab-label indicator-tab">
                <BarChartOutlined />
                <span>{{ indicatorTabNode.name }}</span>
                <span class="dir-tab-count">({{ indicatorTabNode.childCount }})</span>
              </span>
```
NEW:
```
              <span
                class="dir-tab-label indicator-tab"
                :class="{ 'indicator-tab--drop-active': indicatorTabDropActive }"
                @dragover="onIndicatorTabDragOver"
                @dragleave="onIndicatorTabDragLeave"
                @drop.prevent="onIndicatorTabDrop"
              >
                <BarChartOutlined />
                <span>{{ indicatorTabNode.name }}</span>
                <span class="dir-tab-count">({{ indicatorTabNode.childCount }})</span>
              </span>
```

- [ ] **Step 3: script 加 ref 与拖拽处理函数**

定位 `function handleTreeDrop` 之前的 `function parseItemKey`（解析 key 的工具），在它之后、`async function handleTreeDrop` 之前插入下面整段。

OLD:
```
// 解析树节点 key（形如 "item-123"）→ 数据库 id
function parseItemKey(key: any): number {
  const k = String(key ?? '')
  const id = parseInt(k.replace('item-', ''))
  return Number.isNaN(id) ? 0 : id
}
```
NEW:
```
// 解析树节点 key（形如 "item-123"）→ 数据库 id
function parseItemKey(key: any): number {
  const k = String(key ?? '')
  const id = parseInt(k.replace('item-', ''))
  return Number.isNaN(id) ? 0 : id
}

// ==================== 拖指标项到「指标Tab」改归属（单向） ====================
const draggingItemId = ref<number | null>(null)
const indicatorTabDropActive = ref(false)

function isIndicatorLeaf(item: any): boolean {
  return !!item && ((item.itemCategory || '') === 'indicator' || getNodeRole(item) === 'indicator')
}

function onTreeDragStart(info: any) {
  draggingItemId.value = parseItemKey(info?.node?.key)
}
function onTreeDragEnd() {
  draggingItemId.value = null
  indicatorTabDropActive.value = false
}

function onIndicatorTabDragOver(e: DragEvent) {
  const item = flatItems.value.find(i => i.id === draggingItemId.value)
  if (item && isIndicatorLeaf(item)) {
    e.preventDefault() // 允许放置（仅指标项）
    indicatorTabDropActive.value = true
  }
}
function onIndicatorTabDragLeave() {
  indicatorTabDropActive.value = false
}

async function onIndicatorTabDrop() {
  indicatorTabDropActive.value = false
  const itemId = draggingItemId.value
  draggingItemId.value = null
  if (!itemId || !selectedTemplateId.value) return
  let item = flatItems.value.find(i => i.id === itemId)
  if (!item) return
  if (!isIndicatorLeaf(item)) { message.warning('只有指标项可移入指标分区'); return }
  try {
    const secId = await ensureIndicatorSection()
    if (!secId) return // ensureIndicatorSection 失败时已给提示
    item = flatItems.value.find(i => i.id === itemId) || item // ensure 可能已重载
    if ((item.parentId ?? 0) === secId) { message.info('该指标已在指标分区中'); return }
    await updateAmoebaPLItem(selectedTemplateId.value, itemId, buildItemUpdatePayload(item, {
      parentId: secId,
      sort: computeNextSortOrder(secId),
    }))
    const savedKeys = [...expandedKeys.value]
    await loadTemplateItems()
    expandedKeys.value = [...new Set([...savedKeys, `item-${secId}`])]
    message.success('已移入指标分区')
  } catch (e: any) {
    message.error(e?.message || '移入指标分区失败')
  }
}
```

- [ ] **Step 4: style 加放置高亮**

定位 `.indicator-tab {` 规则块，在其 `}` 之后追加：

```
.indicator-tab--drop-active {
  outline: 2px dashed #52c41a;
  outline-offset: 2px;
  border-radius: 4px;
  background: rgba(82, 196, 26, 0.08);
}
```

- [ ] **Step 5: 类型检查**

Run: `npm --prefix web run type-check`
Expected: PASS（`AmoebaPLTemplate.vue` 无新增类型错误；忽略约 556 条预存无关错误。可 `npm --prefix web run type-check 2>&1 | Select-String AmoebaPLTemplate` 过滤确认无本文件报错）

- [ ] **Step 6: 契约测试转绿**

Run: `node scripts/tests/amoeba-indicator-drag-to-tab-contract.test.mjs`
Expected: PASS，输出 `Amoeba indicator drag-to-tab contracts are aligned.`

- [ ] **Step 7: 提交**

```bash
git add web/src/views/finance/AmoebaPLTemplate.vue
git commit -m "feat(amoeba): 指标项拖到指标Tab即归入全局指标分区

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 验证（自动化 + 运行态人工拖拽）

**Files:** 无改动（仅验证；若 Step 3 运行态发现原生 drop 不触发，按 Step 4 回退）

- [ ] **Step 1: 全量契约 + 类型检查 + 构建**

Run: `pwsh scripts/dev/test-contracts.ps1`
Expected: 全部通过，含 `amoeba-indicator-drag-to-tab-contract.test.mjs` 与既有 `amoeba-indicator-fixed-tab-contract.test.mjs`（不回归）。

Run: `npm --prefix web run type-check`（确认本文件零错误）

Run: `npm --prefix web run build`
Expected: 构建成功。

- [ ] **Step 2: 起应用**

用 `restart-dev` 起前后端（后端 9000、前端 9001）。打开"阿米巴损益模板"，选一个含「普通 Tab 内分组里的指标项」的模板（或新建模板：建 Tab"出港"→在出港下建分组"出港指标"→其下建指标项"发件票量"；并保证存在或可懒创建指标分区）。

- [ ] **Step 3: 运行态人工拖拽点验**

用真实鼠标拖拽（HTML5 拖拽无法用自动化可靠模拟）：
1. 把出港 Tab 里"出港指标"组下的指标项"发件票量"**拖到标签栏"运营指标"Tab** 上：拖到 Tab 上方时该 Tab 出现绿色虚线高亮 → 松手后该项从原处消失、"运营指标"计数+1、提示"已移入指标分区"；点指标 Tab 可见它已成为直接子项。
2. 无指标分区的模板：拖入触发懒创建后再移入。
3. 把**非指标项**（如收入项/分组）拖到指标 Tab：无高亮、松手不接收（若触发则提示"只有指标项可移入指标分区"）。
4. 把已在指标分区里的指标项再拖到指标 Tab：提示"该指标已在指标分区中"、无变化。

**若第 1 步松手后无任何反应**（说明 a-tree 吞掉了外部 drop，drop 未在 Tab 触发）→ 执行 Step 4 回退。否则跳过 Step 4。

- [ ] **Step 4: 回退方案（仅当 Step 3 第1步 drop 不触发时）**

改为在 a-tree 的 `@dragend` 用落点坐标命中指标 Tab 标签来判定。两处改动：

(a) 模板：撤掉 Step 2 给 span 加的 `@dragover/@dragleave/@drop`（保留 `:class` 高亮绑定），并给该 span 一个 ref 锚点 `ref="indicatorTabLabelRef"`：
```
              <span
                ref="indicatorTabLabelRef"
                class="dir-tab-label indicator-tab"
                :class="{ 'indicator-tab--drop-active': indicatorTabDropActive }"
              >
```
(b) script：声明 `const indicatorTabLabelRef = ref<HTMLElement | null>(null)`，并把 `onTreeDragEnd` 改为用 `event.clientX/clientY` 命中标签包围盒后调用落下逻辑：
```
function onTreeDragEnd(info: any) {
  const e: DragEvent = info?.event || info
  const el = indicatorTabLabelRef.value
  // 命中指标Tab标签包围盒则执行落下（onIndicatorTabDrop 内部读取并清空 draggingItemId）
  if (el && e && typeof e.clientX === 'number') {
    const r = el.getBoundingClientRect()
    const hit = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom
    if (hit) { onIndicatorTabDrop(); return }
  }
  draggingItemId.value = null
  indicatorTabDropActive.value = false
}
```
（`onIndicatorTabDrop` 不变，仍读取 `draggingItemId` 并在末尾已清空。）再重跑 Step 1 的契约/类型检查/构建与 Step 3 人工拖拽。

- [ ] **Step 5: 提交（若 Step 4 有改动）**

```bash
git add web/src/views/finance/AmoebaPLTemplate.vue
git commit -m "fix(amoeba): 指标项拖入指标Tab 改用 dragend 落点命中（回退方案）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
若 Step 3 主方案通过、无 Step 4 改动，本步跳过。

---

## 自检（计划 vs 设计）

- **spec 覆盖**：单向拖入(Task2)、记录被拖项(`draggingItemId`/`onTreeDragStart`)、指标Tab放置目标(`onIndicatorTabDragOver/Drop`)、仅指标项可放+拒收提示(`isIndicatorLeaf`+`只有指标项可移入指标分区`)、懒创建(`ensureIndicatorSection`)、改父落库(`updateAmoebaPLItem`+`buildItemUpdatePayload`)、末尾落点(`computeNextSortOrder`)、no-op(`该指标已在指标分区中`)、高亮(`indicator-tab--drop-active`)、后端无改动、实现风险与回退(Task3 Step4)——均覆盖。
- **占位符**：无 TBD/TODO，代码步骤含完整代码。
- **命名一致**：`draggingItemId`、`indicatorTabDropActive`、`isIndicatorLeaf`、`onTreeDragStart`、`onTreeDragEnd`、`onIndicatorTabDragOver`、`onIndicatorTabDragLeave`、`onIndicatorTabDrop`、`indicator-tab--drop-active` 全程一致；复用函数 `ensureIndicatorSection`/`buildItemUpdatePayload`/`computeNextSortOrder`/`updateAmoebaPLItem`/`parseItemKey`/`getNodeRole` 均为本文件既有。
