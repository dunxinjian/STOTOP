# 阿米巴指标分区固定特别区 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 编辑器把全局唯一的指标分区做成"固定特别 Tab"（最左、独立样式、不可删改），移除"+指标分区"按钮，并去掉"指标分区钉在每个 Tab 顶部"的全局渲染；报表左右两栏已满足，仅验证。

**Architecture:** 模型不变（指标分区仍是根级 `group` + `isIndicatorSection=1`，全局唯一，不迁移、不改库、不改后端）。改动集中在前端 `AmoebaPLTemplate.vue`：标签栏额外渲染一个代表指标分区的固定 Tab（分区不存在时用哨兵 id，首次在该 Tab "新增项目"时懒创建根级分区）。报表 `AmoebaPL.vue` 已是"左栏指标 + 右栏各 Tab 数据"，本计划只做回归验证。

**Tech Stack:** Vue 3 `<script setup>` + ant-design-vue 4 + TypeScript；契约测试为 `scripts/tests/*.mjs`（Node `assert` 静态断言），运行器 `scripts/dev/test-contracts.ps1`；类型检查 `vue-tsc`。无前端组件测试框架，故 TDD 以"契约测试（红→绿）+ `vue-tsc` 类型检查 + 跑应用人工验证"落地。

---

## 测试方式说明（重要）

本项目前端无组件测试框架。验证手段三层：
1. **契约测试**（静态文本断言）：`node scripts/tests/amoeba-indicator-fixed-tab-contract.test.mjs`，或 `pwsh scripts/dev/test-contracts.ps1 amoeba-indicator`。
2. **类型检查**：`npm --prefix web run type-check`。
3. **人工验证**：用 `restart-dev` 起前后端，打开"阿米巴损益模板"页实际点验。

契约测试 Task 1 先写好（红），实现在 Task 2/3 完成后转绿（Task 4 统一验证）；中间步骤以 `vue-tsc` 类型检查作为快速护栏。

## 文件结构

- 改：`web/src/views/finance/AmoebaPLTemplate.vue`（编辑器主改动，唯一改动的业务文件）
- 验证（不改）：`web/src/views/finance/AmoebaPL.vue`（报表左右两栏，已满足）
- 新增：`scripts/tests/amoeba-indicator-fixed-tab-contract.test.mjs`（契约回归守卫）

---

## Task 1: 契约测试（红）

**Files:**
- Create: `scripts/tests/amoeba-indicator-fixed-tab-contract.test.mjs`

- [ ] **Step 1: 写契约测试**

```js
import fs from 'node:fs'
import assert from 'node:assert/strict'

const read = (p) => fs.readFileSync(new URL(`../../${p}`, import.meta.url), 'utf8')
const tpl = read('web/src/views/finance/AmoebaPLTemplate.vue')
const report = read('web/src/views/finance/AmoebaPL.vue')

// 1) "+指标分区"手动新增入口已移除
assert.doesNotMatch(tpl, /@click="handleAddIndicatorSection"/, '"+指标分区"按钮应已移除')
assert.doesNotMatch(tpl, /function handleAddIndicatorSection/, 'handleAddIndicatorSection 应被 ensureIndicatorSection 取代')

// 2) treeData 不再把指标分区全局置顶到每个 Tab（"出现在所有Tab"根因）
assert.doesNotMatch(tpl, /指标分区始终放在树最顶部/, 'treeData 不应再全局置顶指标分区')

// 3) 固定"指标分区"特别 Tab：哨兵常量 + 计算节点 + 特别样式类
assert.match(tpl, /const\s+INDICATOR_TAB_ID\s*=\s*-1/, '应定义固定Tab哨兵 id')
assert.match(tpl, /const\s+indicatorTabNode\s*=\s*computed/, '应有 indicatorTabNode 计算属性')
assert.match(tpl, /class="dir-tab-label indicator-tab"/, '固定指标Tab应有 indicator-tab 特别样式类')

// 4) 懒创建：首次在指标Tab新增指标项时创建根级指标分区
assert.match(tpl, /async\s+function\s+ensureIndicatorSection/, '应有 ensureIndicatorSection 懒创建函数')
assert.match(tpl, /isIndicatorSection:\s*true/, 'ensureIndicatorSection 应以 isIndicatorSection=true 建根级分区')

// 5) tabNodes 仍排除指标分区根节点，避免与固定Tab重复
assert.match(tpl, /!i\.isIndicatorSection/, 'tabNodes 应继续排除指标分区根节点')

// 6) 报表左栏不回归：仍读取全局 indicatorSections 并保留左栏面板
assert.match(report, /indicatorSections\.value\s*=\s*res\?\.indicatorSections/, '报表应继续读取全局 indicatorSections')
assert.match(report, /class="indicator-panel"/, '报表应保留左栏指标面板')

console.log('Amoeba indicator fixed-tab contracts are aligned.')
```

- [ ] **Step 2: 运行，确认失败**

Run: `node scripts/tests/amoeba-indicator-fixed-tab-contract.test.mjs`
Expected: FAIL（断言 3/4 找不到 `INDICATOR_TAB_ID`/`indicatorTabNode`/`ensureIndicatorSection`，AssertionError）

- [ ] **Step 3: 提交**

```bash
git add scripts/tests/amoeba-indicator-fixed-tab-contract.test.mjs
git commit -m "test: 阿米巴指标分区固定特别Tab 契约测试(红)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 指标分区改为固定特别 Tab（编辑器主改动）

**Files:**
- Modify: `web/src/views/finance/AmoebaPLTemplate.vue`

- [ ] **Step 1: 导入图标 BarChartOutlined**

定位图标导入块（`@ant-design/icons-vue`），把 `RightOutlined,` 后追加一行。

old:
```ts
  DownOutlined,
  RightOutlined,
} from '@ant-design/icons-vue'
```
new:
```ts
  DownOutlined,
  RightOutlined,
  BarChartOutlined,
} from '@ant-design/icons-vue'
```

- [ ] **Step 2: 移除"+指标分区"按钮**

定位左树工具栏，删除整段按钮（紧跟在"新增项目"按钮之后）。

old:
```html
          <a-button size="small" @click="handleAddItem(null)">
            <template #icon><PlusOutlined /></template>
            新增项目
          </a-button>
          <a-button
            v-if="!hasIndicatorSection"
            size="small"
            type="dashed"
            @click="handleAddIndicatorSection"
          >
            <template #icon><PlusOutlined /></template>
            指标分区
          </a-button>
```
new:
```html
          <a-button size="small" @click="handleAddItem(null)">
            <template #icon><PlusOutlined /></template>
            新增项目
          </a-button>
```

- [ ] **Step 3: 标签栏渲染固定指标 Tab（最左）**

定位 `<a-tab-pane v-for="tab in tabNodes" :key="tab.id">`，在它之前插入固定指标 Tab。

old:
```html
          <a-tab-pane
            v-for="tab in tabNodes"
            :key="tab.id"
          >
            <template #tab>
              <span class="dir-tab-label">
```
new:
```html
          <!-- 固定"指标分区"特别 Tab：始终最左，无改名/删除入口 -->
          <a-tab-pane :key="indicatorTabNode.id">
            <template #tab>
              <span class="dir-tab-label indicator-tab">
                <BarChartOutlined />
                <span>{{ indicatorTabNode.name }}</span>
                <span class="dir-tab-count">({{ indicatorTabNode.childCount }})</span>
              </span>
            </template>
          </a-tab-pane>
          <a-tab-pane
            v-for="tab in tabNodes"
            :key="tab.id"
          >
            <template #tab>
              <span class="dir-tab-label">
```

- [ ] **Step 4: 新增固定指标 Tab 的状态（哨兵/计算节点/激活判断）**

定位 `tabNodes` 计算属性结尾（`globalFormulaNodes` 之前），插入下面整段。

old:
```ts
// 全局formula节点 = depth=0 formula节点
const globalFormulaNodes = computed(() => {
```
new:
```ts
// ==================== 固定"指标分区"特别 Tab ====================
// 指标分区按设计为全局唯一、根级；编辑器把它固定成标签栏最左的特别 Tab，
// 不再钉在每个普通 Tab 顶部。分区尚未创建时用哨兵 id，首次在该 Tab 新增指标项时懒创建。
const INDICATOR_TAB_ID = -1

const indicatorSectionItem = computed(() =>
  flatItems.value.find(i => i.isIndicatorSection && (!i.parentId || i.parentId === 0)) || null
)

const indicatorTabNode = computed(() => {
  const sec = indicatorSectionItem.value
  return {
    id: sec ? sec.id : INDICATOR_TAB_ID,
    name: sec ? getItemName(sec) : '运营指标',
    childCount: sec ? flatItems.value.filter(c => c.parentId === sec.id).length : 0,
  }
})

const isIndicatorTabActive = computed(() => activeTabId.value === indicatorTabNode.value.id)

// 全局formula节点 = depth=0 formula节点
const globalFormulaNodes = computed(() => {
```

- [ ] **Step 5: watch(tabNodes) 放行固定指标 Tab**

定位 Tab 合法性 watch，加一行放行。

old:
```ts
watch(tabNodes, (tabs) => {
  if (tabs.length === 0) {
    activeTabId.value = null
    return
  }
  if (!tabs.find(t => t.id === activeTabId.value)) {
    activeTabId.value = tabs[0].id
  }
})
```
new:
```ts
watch(tabNodes, (tabs) => {
  // 指标分区固定Tab始终合法，不参与回退（其 id 不在 tabNodes 中）
  if (isIndicatorTabActive.value) return
  if (tabs.length === 0) {
    activeTabId.value = null
    return
  }
  if (!tabs.find(t => t.id === activeTabId.value)) {
    activeTabId.value = tabs[0].id
  }
})
```

- [ ] **Step 6: 删除 treeData 全局置顶（第1步）**

old:
```ts
const treeData = computed(() => {
  const nodes: any[] = []

  // 1. 指标分区始终放在树最顶部（全局，不依赖Tab）。
  // 仅认根级标记：历史脏数据中 Tab 内分组可能被误标（FinanceSeeder.MigrateV4 前），按普通分组留在 Tab 树中
  const indicatorSection = flatItems.value.find(i => i.isIndicatorSection && (!i.parentId || i.parentId === 0))
  if (indicatorSection) {
    nodes.push(buildItemNode(indicatorSection))
  }

  // 2. 当前Tab下的项目
  if (activeTabId.value) {
```
new:
```ts
const treeData = computed(() => {
  const nodes: any[] = []

  // 当前Tab（含固定指标分区Tab）下的项目；指标分区不再全局置顶到每个 Tab
  if (activeTabId.value) {
```

- [ ] **Step 7: hasIndicatorSection 复用 indicatorSectionItem（DRY）**

old:
```ts
/** 是否已有指标分区（仅认根级标记，与报表服务和树渲染同口径） */
const hasIndicatorSection = computed(() => {
  return flatItems.value.some(item => item.isIndicatorSection && (!item.parentId || item.parentId === 0))
})
```
new:
```ts
/** 是否已有指标分区（仅认根级标记，与报表服务和树渲染同口径） */
const hasIndicatorSection = computed(() => !!indicatorSectionItem.value)
```

- [ ] **Step 8: 加 .indicator-tab 特别样式**

定位 `<style scoped lang="scss">` 内的 `.dir-tab-label`，在其规则之后追加：

```scss
.indicator-tab {
  font-weight: 600;
  color: #d46b08; // 琥珀色，与报表"运营指标"呼应
  .anticon {
    margin-right: 2px;
  }
}
```

- [ ] **Step 9: 类型检查**

Run: `npm --prefix web run type-check`
Expected: PASS（无类型错误；此时 `ensureIndicatorSection`/`handleAddIndicatorSection` 尚未处理，但本步未引用已删函数——`handleAddIndicatorSection` 仍存在于 Task 3 处理前，不报错）

注：本任务尚未删除 `handleAddIndicatorSection` 定义（Task 3 处理）。它已无调用方（按钮已删），但保留定义不影响类型检查与编译。

- [ ] **Step 10: 运行契约测试（部分转绿）**

Run: `node scripts/tests/amoeba-indicator-fixed-tab-contract.test.mjs`
Expected: 仍 FAIL，但失败点收敛到第 (1)/(4) 组关于 `handleAddIndicatorSection`/`ensureIndicatorSection` 的断言（Task 3 处理）；第 (2)(3)(5)(6) 组应已通过。

- [ ] **Step 11: 提交**

```bash
git add web/src/views/finance/AmoebaPLTemplate.vue
git commit -m "feat(amoeba): 指标分区改为编辑器固定特别Tab，移除全局置顶与+指标分区按钮

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 懒创建指标分区

**Files:**
- Modify: `web/src/views/finance/AmoebaPLTemplate.vue`

- [ ] **Step 1: 用 ensureIndicatorSection 取代 handleAddIndicatorSection**

old:
```ts
function handleAddIndicatorSection() {
  if (!selectedTemplateId.value) { message.warning('请先选择模板'); return }
  if (hasIndicatorSection.value) { message.warning('每个模板只能有一个指标分区'); return }
  addItemForm.itemName = '运营指标'
  addItemForm.parentId = 0
  addItemForm.sort = computeNextSortOrder(0)
  addItemForm.itemCategory = 'section'
  addItemForm.valueSource = ''
  addItemForm.systemDataSource = null
  addItemForm.isIndicatorSection = true
  addItemForm.indicatorDirectionScope = null
  syncAddItemLegacyFields()
  addItemModalVisible.value = true
}
```
new:
```ts
// 懒创建全局唯一指标分区（根级 group + isIndicatorSection），返回其 id；已存在则直接返回。
async function ensureIndicatorSection(): Promise<number | null> {
  if (indicatorSectionItem.value) return indicatorSectionItem.value.id
  if (!selectedTemplateId.value) return null
  const created: any = await addAmoebaPLItem(selectedTemplateId.value, {
    itemName: '运营指标',
    nodeRole: 'group',
    parentId: 0,
    sort: computeNextSortOrder(0),
    itemCategory: 'section',
    isIndicatorSection: true,
  })
  const newId = created?.id ?? created?.data?.id ?? null
  await loadTemplateItems()
  return newId
}
```

- [ ] **Step 2: handleAddItem 改 async，在固定指标 Tab 下懒创建**

old:
```ts
function handleAddItem(parentItem: any) {
  if (!selectedTemplateId.value) { message.warning('请先选择模板'); return }
  const defaultParentId = parentItem?.id || activeTabId.value || 0
```
new:
```ts
async function handleAddItem(parentItem: any) {
  if (!selectedTemplateId.value) { message.warning('请先选择模板'); return }
  // 在固定指标分区Tab下新增：若分区尚未创建，先懒创建并切到真实分区
  if (!parentItem && isIndicatorTabActive.value) {
    const secId = await ensureIndicatorSection()
    if (!secId) { message.error('指标分区创建失败'); return }
    activeTabId.value = secId
  }
  const defaultParentId = parentItem?.id || activeTabId.value || 0
```

说明：懒创建后 `defaultParentId` 即新分区 id，其直接父就是指标分区，`checkAncestorIsIndicatorSection(defaultParentId)` 返回 true，故 `addItemForm.itemCategory` 自动锁为 `indicator`（沿用现有逻辑，无需额外改）。

- [ ] **Step 3: 类型检查**

Run: `npm --prefix web run type-check`
Expected: PASS

- [ ] **Step 4: 运行契约测试（全绿）**

Run: `node scripts/tests/amoeba-indicator-fixed-tab-contract.test.mjs`
Expected: PASS，输出 `Amoeba indicator fixed-tab contracts are aligned.`

- [ ] **Step 5: 提交**

```bash
git add web/src/views/finance/AmoebaPLTemplate.vue
git commit -m "feat(amoeba): 固定指标Tab下首次新增指标项时懒创建指标分区

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: 整体验证（契约/类型/构建/人工）

**Files:** 无改动（仅验证；如人工发现样式问题再回到 Task 2 Step 8 微调）

- [ ] **Step 1: 全量契约测试**

Run: `pwsh scripts/dev/test-contracts.ps1`
Expected: 全部通过，含 `amoeba-indicator-fixed-tab-contract.test.mjs`；尤其确认 `finance-report-contract.test.mjs` 未被破坏。

- [ ] **Step 2: 类型检查 + 构建**

Run: `npm --prefix web run type-check`
Expected: PASS

Run: `npm --prefix web run build`
Expected: 构建成功，无报错。

- [ ] **Step 3: 人工验证（起应用点验）**

用 `restart-dev` 起前后端，打开"阿米巴损益模板"页，逐项核对：

1. 选一个已有指标分区的模板（如默认模板含"出港指标/进港指标"对应的根级"运营指标"若存在）：标签栏最左出现带图标的"运营指标"特别 Tab；普通 Tab（出港/进港）的左树里**不再**出现指标分区。
2. 点"运营指标"特别 Tab：左树显示其指标子项；该 Tab 无改名/删除图标。
3. 左树工具栏**没有**"+指标分区"按钮。
4. 选一个**没有**指标分区的模板：仍显示"运营指标"固定 Tab（空树 + "暂无项目"引导）；点"新增项目"→ 自动建出根级指标分区并切入，新增项类别锁定为"指标"，提交后出现在该 Tab 下；切到普通 Tab 确认指标分区不串台。
5. 报表页（阿米巴报表）：左栏"运营指标"固定展示，右栏切换各 Tab 数据时左栏保持不变。

- [ ] **Step 4: 提交（如 Step 3 有微调）**

```bash
git add -A
git commit -m "chore(amoeba): 指标分区固定特别Tab 验证收尾

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

若 Step 1-3 全过且无改动，本步可跳过。

---

## 自检（计划 vs 设计）

- **spec 覆盖**：固定特别Tab(Task2)、移除"+指标分区"按钮(Task2 S2)、删 treeData 全局置顶(Task2 S6)、懒创建(Task3)、报表已满足仅验证(Task4 S3 + 契约第6组)、后端无改动(未触碰)、测试三层(全计划)、边界(无分区懒创建/多个根级取 `.find` 第一个/固定Tab无删改入口) —— 均有对应。
- **占位符**：无 TBD/TODO，所有代码步骤含完整代码。
- **命名一致**：`INDICATOR_TAB_ID`、`indicatorSectionItem`、`indicatorTabNode`、`isIndicatorTabActive`、`ensureIndicatorSection`、`hasIndicatorSection` 在各任务中前后一致；`handleAddIndicatorSection` 在 Task3 被 `ensureIndicatorSection` 取代后全文无残留调用。
