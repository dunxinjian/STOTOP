# PageLayout 基元（阶段 0c）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: 用 `superpowers:subagent-driven-development` 逐任务实现。步骤用 checkbox（`- [ ]`）跟踪。

**Goal:** 新建页面容器薄基元 `PageLayout`（`variant=table` 复用全局 `.page-container` 表格滚动链 / `variant=flow` 多卡片流式独立类），并以 `cardflow/OrchestrationDetailPage` 为验证页收编它手写的「解除全局锁定」覆写 hack。

**Architecture:** flow variant 用**独立类 `.page-flow`**（不叫 `.page-container`），因此全局 `index.scss` 里 `.page-container > .ant-card` / `.page-container .ant-spin-*` 的表格 flex 滚动链**根本不命中**——多卡片自然流式、整页滚动，无需任何 `:deep` 解除。table variant 仍渲染 `.page-container`，行为与现状完全一致。与 `PageHeader`（工具栏 Teleport）正交。承接 spec `2026-06-17-功能页逐模块巡检与收敛-design.md`。

**Tech Stack:** Vue 3.5 `<script setup>` + TS + Ant Design Vue 4.2 + SCSS 令牌。

---

## ⚠️ 验证方式（无前端测试运行器，同 0a/0b）

`npm run build`（vite build）绿 + `npx stylelint "<file>"` 0 problems + 必要时 preview。不写单测、不跳 hook。

## 文件结构

| 文件 | 责任 | 动作 |
|---|---|---|
| `web/src/components/PageLayout.vue` | 页面容器基元（table/flow variant） | 新建 |
| `web/src/views/cardflow/OrchestrationDetailPage.vue` | 验证页：root → PageLayout flow，删覆写 hack | 改 |
| `web/docs/PATTERNS.md` | §二 增 PageLayout 契约 + §一 流式页注记 | 改 |

---

## Task 1：新建 `PageLayout.vue`

**Files:** Create: `web/src/components/PageLayout.vue`

- [ ] **Step 1：创建文件（逐字）**

```vue
<!--
  PageLayout —— 页面容器基元
  统一页面外层容器：
  - variant="table"（默认）：渲染全局 .page-container，复用其表格 flex 滚动链（适合单表列表页，表体独立滚动）。
  - variant="flow"：渲染独立类 .page-flow（自带 token 内边距 + 卡片间距 + 整页滚动），不施加表格滚动链，
    收编各页手写的「解除全局 .page-container 锁定」覆写 hack（多卡片纵向流式详情页）。
  与 PageHeader（工具栏 Teleport）正交：标题→面包屑、操作→PageHeader、内容→PageLayout。
-->
<template>
  <div :class="variant === 'flow' ? 'page-flow' : 'page-container'">
    <slot />
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  /** table=单表列表页（保留全局表格滚动链）；flow=多卡片纵向流式 */
  variant?: 'table' | 'flow'
}>(), {
  variant: 'table',
})
</script>

<style scoped lang="scss">
.page-flow {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  gap: var(--space-md12);
  padding: var(--page-pad-y) var(--page-pad-x);
  background: var(--bg-page);
  overflow-x: hidden;
  overflow-y: auto;
}
</style>
```

> 说明：variant="table" 时 div 类名为 `page-container`（全局类，由 `index.scss` 提供内边距/背景/表格滚动链，PageLayout 不重复定义）；variant="flow" 时类名为 `page-flow`（仅 PageLayout 定义），故全局表格链不命中。`--space-md12`/`--page-pad-y`/`--page-pad-x`/`--bg-page` 均为既有令牌，无裸 hex。

- [ ] **Step 2：构建** `cd E:/STOTOP_Fable/web && npm run build` → 成功。
- [ ] **Step 3：stylelint** `npx stylelint "src/components/PageLayout.vue"` → 0 problems。
- [ ] **Step 4：提交**
```bash
git add web/src/components/PageLayout.vue
git commit -m "feat(ui): 新增 PageLayout 容器基元（table/flow variant，flow 独立类避开表格滚动链）"
```
（尾行 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`）

---

## Task 2：验证页 `OrchestrationDetailPage` 迁移到 PageLayout flow

**Files:** Modify: `web/src/views/cardflow/OrchestrationDetailPage.vue`。先 Read 确认锚点。

- [ ] **Step 1：root 容器 `<div class="page-container">` → `<PageLayout variant="flow">`**

模板第 604 行 `<div class="page-container">` 改为 `<PageLayout variant="flow">`；找到其**匹配的闭合 `</div>`**（在 `</template>`（约 1081 行）之前的那个根级闭合标签）改为 `</PageLayout>`。注意只改根容器这一对开合标签，内部结构不动。

- [ ] **Step 2：import PageLayout**

在 `<script setup>` 的组件 import 区（与 PageHeader 等同处）新增：
```ts
import PageLayout from '@/components/PageLayout.vue'
```

- [ ] **Step 3：删除 scoped `.page-container` 覆写块**

删除整段（约 1084–1094 行）：
```scss
.page-container {
  padding: 0 0 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  /* 确保页面本身拥有垂直滚动能力（覆盖全局多余使用） */
  overflow-y: auto;
  overflow-x: hidden;
  min-height: 0;
  flex: 1;
}
```

- [ ] **Step 4：删除「解除全局锁定」spin hack**

删除整段注释 + 规则（约 1096–1104 行）：
```scss
/* 该页为多卡片纵向流式布局，需解除全局 .page-container 对 a-spin 包裹层加的 flex/overflow:hidden 锁定，
   避免多张卡片被裁切、父容器无法滚动 */
:deep(.ant-spin-nested-loading),
:deep(.ant-spin-container) {
  flex: none;
  display: block;
  overflow: visible;
  min-height: 0;
}
```
（flow variant 用 `.page-flow` 独立类，全局表格链不命中，此 hack 不再需要。）

- [ ] **Step 5：去掉卡片的冗余水平 margin（改由 flow 内边距统一）**

把：
```scss
.info-card,
.section-card {
  margin: 0 12px;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  flex: none;
}
```
改为（仅删 `margin: 0 12px;` 行，其余保留——卡片 bg/圆角/阴影的裸 hex 留待 Phase 1 hex 收敛，不在 0c 范围）：
```scss
.info-card,
.section-card {
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  flex: none;
}
```

并删除 `.info-card { margin-top: 12px; }`（约 1115–1117 行，整段）——flow 的 `gap` + `padding-top` 已提供顶部间距，保留会叠加成双倍。

> 不动该页其它样式/逻辑/卡片内容。`#fff`/rgba 等裸 hex 属 Phase 1 收敛，本任务不碰。

- [ ] **Step 6：构建 + stylelint**

`cd E:/STOTOP_Fable/web && npm run build` → 成功；`npx stylelint "src/views/cardflow/OrchestrationDetailPage.vue"` → 注意：该文件本就有既存裸 hex 违规（Phase 1 处理），**本任务只需确认违规数不增加**（不新引入裸 hex；我们只删了规则、没加 hex）。grep 确认无残留：`grep -nE "\.page-container|ant-spin-nested-loading|margin: 0 12px" web/src/views/cardflow/OrchestrationDetailPage.vue` 应无命中。

- [ ] **Step 7：提交**
```bash
git add web/src/views/cardflow/OrchestrationDetailPage.vue
git commit -m "refactor(ui): OrchestrationDetailPage 迁移 PageLayout flow，收编解除全局锁定 hack"
```
（尾行 Co-Authored-By 同上）

---

## Task 3：PATTERNS.md 增 PageLayout 契约

**Files:** Modify: `web/docs/PATTERNS.md`

- [ ] **Step 1：§二 增 PageLayout 段**（在 §二 StatFilterTabs 段之后插入）

```
### PageLayout

页面容器基元（样板：列表页 `dormitory/BuildingManage` 用 table；流式详情页 `cardflow/OrchestrationDetailPage` 用 flow）。

| prop | 默认 | 说明 |
| --- | --- | --- |
| `variant` | `'table'` | `table`=渲染全局 `.page-container`，保留表格 flex 滚动链（单表列表页，表体独立滚动）；`flow`=渲染独立 `.page-flow`（token 内边距+卡片间距+整页滚动），不施加表格链（多卡片纵向流式详情页） |

- flow variant 用独立类避开全局表格滚动链，**收编各页手写的「解除全局 .page-container 锁定」覆写**（禁止页面再 scoped 覆写 `.page-container` 或 `:deep(.ant-spin-*)` 解锁）。
- 与 PageHeader 正交：标题→面包屑、操作→PageHeader、内容→PageLayout。
```

- [ ] **Step 2：§一 容器约定注记**（在 §三 令牌速查里 `.page-container { padding... }` 那条之后追加一句）

> 多卡片纵向流式页（详情/概览）用 `<PageLayout variant="flow">`（见 §二），不要 scoped 覆写 `.page-container`。

- [ ] **Step 3：提交**
```bash
git add web/docs/PATTERNS.md
git commit -m "docs(ux): PATTERNS 增 PageLayout 契约（table/flow）"
```
（尾行 Co-Authored-By 同上）

---

## 完成标准（本批次）

- `npm run build` 绿；OrchestrationDetailPage 不再 scoped 覆写 `.page-container`、不再有 `:deep(.ant-spin-*)` 解锁 hack；裸 hex 违规数未增加。
- `PageLayout` 成为页面容器唯一基元；PATTERNS 契约就绪。
- 新基建三件套（DataTable / StatFilterTabs / PageLayout）齐备。
- preview（用户已登录）：OrchestrationDetailPage 多卡片正常纵向流式、整页可滚、卡片不被裁切。
