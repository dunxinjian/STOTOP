# 指标项拖入"指标Tab"改归属 设计

- 日期：2026-06-15
- 状态：设计已与用户确认，待评审
- 涉及范围：模板编辑器前端（`AmoebaPLTemplate.vue`）；后端无改动

## 1. 背景与目标

指标分区已做成编辑器里的固定特别 Tab（见 [2026-06-14-amoeba-indicator-fixed-tab-design.md](2026-06-14-amoeba-indicator-fixed-tab-design.md)）。但指标类型叶子项（`itemCategory=indicator`，如"发件票量"）可能散落在**普通 Tab 内的分组**里（例如出港 Tab 下"出港指标"组）。

**目标**：允许用户把普通 Tab 里的**指标项**直接**拖到标签栏的"运营指标"指标 Tab 上**，从而把它移成**全局指标分区的直接子项**（`parentId=指标分区`），快速归集指标。

## 2. 范围

**做：**
- 单向：把当前 Tab 树里可见的**指标项**拖到指标 Tab → 归入全局指标分区（无分区则懒创建）。要拖其它 Tab 的指标项需先切到该 Tab（树一次只渲染当前 Tab）。
- 拖拽中的可放/拒收视觉反馈。

**不做（明确排除）：**
- 反向（从指标 Tab 拖回普通 Tab 的分组）。
- 普通 Tab 之间互拖（跨普通 Tab）。
- 后端接口/数据模型变更。

## 3. 现状（拖拽相关）

- 左树是 `a-tree`，`:draggable`、`@drop="handleTreeDrop"`、`:allow-drop="handleAllowDrop"`，仅在**当前 Tab 的树内部**重排/改父。
- `validateDropTarget`（`AmoebaPLTemplate.vue:1516`）已有同口径校验：指标分区下只能放 indicator；非指标项不得入指标分区。
- 移动落库走现有 `updateAmoebaPLItem(templateId, itemId, payload)`；`UpdateItemAsync` 在 `ParentId` 变化时调 `EnsureValidParentAsync` 校验（含"指标分区子项必须 indicator"）。
- 指标分区固定 Tab 的标签由 `#tab` 插槽渲染（`<span class="dir-tab-label indicator-tab">…`）；分区不存在时为哨兵态，`ensureIndicatorSection()` 可懒创建。

## 4. 详细设计

### 4.1 触发与机制（纯前端 + 复用更新接口）

1. **记录被拖项**：给 `a-tree` 加 `@dragstart` → 把 `draggingItemId`（ref）设为被拖节点 id；`@dragend` → 清空 `draggingItemId`、清除 Tab 高亮。
2. **指标 Tab 作为放置目标**：给指标 Tab 标签 `<span class="dir-tab-label indicator-tab">` 加原生监听：
   - `@dragover.prevent`：若 `draggingItemId` 对应项是**指标项**，`preventDefault` 允许放置并加高亮类（如 `indicator-tab--drop-active`）；否则不允许（不 preventDefault → 浏览器显示禁止光标）。
   - `@dragleave`：移除高亮类。
   - `@drop.prevent`：执行归属变更（见 4.3）。
3. a-tree 的内部 `@drop`（落在树节点上）与指标 Tab 的原生 `@drop`（落在标签上）互不冲突：落在标签上时只触发后者。

### 4.2 校验与反馈

- **仅指标项可放**：判定 `itemCategory==='indicator' || nodeRole==='indicator'`。
- 拖**非指标项**到指标 Tab：不高亮、不接收；`@drop` 里若误触发则提示"只有指标项可移入指标分区"并返回。
- **已在指标分区下**的指标项再拖入：no-op（其 `parentId` 已是分区或分区子孙），给"已在指标分区中"轻提示或静默返回。

### 4.3 归属变更（落库）

```
onIndicatorTabDrop:
  itemId = draggingItemId; 清空 draggingItemId + 高亮
  item = flatItems.find(id===itemId); 若非指标项 → 提示并返回
  secId = await ensureIndicatorSection()        // 不存在则懒创建
  若 item.parentId === secId → 静默返回（no-op）
  await updateAmoebaPLItem(templateId, itemId,
        buildItemUpdatePayload(item, { parentId: secId, sort: computeNextSortOrder(secId) }))
  await loadTemplateItems()
  message.success('已移入指标分区')
```

- **落点**：指标分区子项**末尾**（`computeNextSortOrder(secId)` = 现有最大 sort + 10）。
- **落下后**：停留在当前 Tab（被拖项从原树消失），不自动跳指标 Tab。
- 失败（接口异常/后端拒收）：`try/catch` + `message.error`，与本文件其他处理一致。

### 4.4 后端

无改动。移动用现有 `updateAmoebaPLItem`；后端 `EnsureValidParentAsync` 已保证"指标分区子项必须 indicator"，构成第二道防线。

## 5. 实现风险与回退

ant-design-vue 的 `a-tree` 用原生 HTML5 拖拽，理论上被拖节点可在外部元素（指标 Tab 标签）上触发 `drop`。**实现首步先做最小验证**：拖树节点到指标 Tab，确认 `@dragstart` 能拿到节点、Tab 的 `@dragover/@drop` 能触发。

- 若 a-tree 不吞外部 drop（预期）：按 4.1 实现。
- 若外部 drop 不触发：**回退**为在 a-tree 的 `@dragend` 里用落点坐标（`event.clientX/Y`）命中指标 Tab 标签的包围盒来判定"拖到了指标 Tab"，再执行 4.3。

## 6. 边界情况

- 指标 Tab 当前即激活 Tab（看着自己的树）时拖入：仍允许（项来自该树内的子分组）。
- 指标分区不存在：`ensureIndicatorSection` 懒创建后再移。
- 被拖项是分组/收入/成本/利润：拒收。
- 拖拽中途松手在别处：`@dragend` 清状态，无副作用。

## 7. 测试

**契约测试**（`scripts/tests/*.mjs` 静态断言）：
- `a-tree` 带 `@dragstart`；存在 `draggingItemId` ref。
- 指标 Tab 标签带 `@drop`/`@dragover` 与放置处理函数（如 `onIndicatorTabDrop`）。
- 放置处理含指标项校验与 `ensureIndicatorSection` 调用。

**运行态人工**：
- 普通 Tab 内分组里的指标项拖到指标 Tab → 移入、计数+1、原处消失、提示成功。
- 无分区模板：拖入触发懒创建再移入。
- 非指标项拖到指标 Tab → 不接收 + 提示。

## 8. 不在本次范围

反向拖出、跨普通 Tab 互拖、后端变更——均不做。
