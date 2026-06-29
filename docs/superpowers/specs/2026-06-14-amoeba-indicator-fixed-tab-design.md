# 阿米巴指标分区：固定特别区设计

- 日期：2026-06-14
- 状态：设计已与用户确认，待评审
- 涉及范围：模板编辑器（主要）、报表展示页（已满足，仅验证）

## 1. 背景与目标

阿米巴损益模板里"指标分区"承载 KPI 指标项（票量、重量、入库率等）。

**现状问题**：编辑器把全局唯一的指标分区**钉在每一个普通 Tab 树的顶部**（`treeData` 第 1 步全局渲染），用户在某个 Tab 新建指标分区后，它在所有 Tab 里都出现，体感是"分组跑到了所有 Tab"。

**历史**：
- V3 迁移把所有"含 indicator 子项的分组"误标为指标分区（含 Tab 内的 出港指标/进港指标）。
- V4 迁移（2026-06-12）反向收敛：强制"指标分区 = 全局唯一、根级（父ID=0）"，把 Tab 内的指标组降级为普通分组。

**本次决策**：**保持 V4 的全局单一指标分区模型不变**（不迁移、不改库、不做按 Tab 拆分），改为在**展示层**把这个唯一的指标分区固定成独立区域：

- **编辑器**：指标分区 = 一个**固定的特别 Tab**，指标项目在该 Tab 内配置。
- **报表**：指标分区**固定在左栏**展示，右栏是各 Tab 的损益数据。（现已是此布局。）

## 2. 范围

**做：**
- 编辑器：指标分区做成固定特别 Tab；移除"+指标分区"按钮；移除"钉在每个 Tab 顶部"的全局渲染；无分区时懒创建。
- 报表：验证现有左右两栏布局符合预期（功能上不改）。

**不做（明确排除）：**
- 按 Tab 的多个指标分区。
- 任何数据迁移（无 V5）。
- 后端数据模型 / 报表接口变更。

## 3. 模型（不变）

全局唯一指标分区仍是**根级分组**：`FParentId=0`、`F节点角色='group'`、`F是否指标分区=1`，其直接子项为 indicator 项。判定函数 `hasIndicatorSection` / `isIndicatorSectionNode`（根级唯一）保持不变。

## 4. 详细设计

### 4.1 模板编辑器 `web/src/views/finance/AmoebaPLTemplate.vue`

**A. 移除"+指标分区"按钮**
- 删除左树工具栏中的按钮（`AmoebaPLTemplate.vue:141` 一带，`v-if="!hasIndicatorSection"` 的 `a-button`）。
- `handleAddIndicatorSection` 的"建分区"逻辑抽成可复用的内部函数（供懒创建调用），不再由按钮触发。

**B. 固定"指标分区"特别 Tab**
- 新增常量哨兵 id（如 `INDICATOR_TAB_ID = -1`）代表"指标分区尚不存在时"的固定 Tab。
- 计算指标 Tab 项：查找根级 `isIndicatorSection` 节点；存在则 id = 该节点 id，不存在则 id = 哨兵。
- 标签栏渲染：固定排在**最左**、配独立图标/配色以区分；**无改名/删除图标**（固定系统 Tab）。Tab 文案统一为"运营指标"（与报表左栏标题一致）。
- `tabNodes`（普通 Tab，`AmoebaPLTemplate.vue:1281`）保持只含普通根级分组；指标 Tab 单独拼接，不混入普通 Tab 列表的排序。

**C. 去掉"钉在每个 Tab 顶部"的全局渲染**
- `treeData`（`AmoebaPLTemplate.vue:1355`）**删除第 1 步**（`indicatorSection` 全局置顶 push）。
- 当 `activeTabId` 为指标 Tab：
  - 分区已存在 → 渲染该分区的指标子项（沿用第 2 步 `parentId===activeTabId`）。
  - 分区不存在（哨兵）→ 空树 + 引导文案"点击上方『新增项目』添加指标"。
- 普通 Tab 下不再出现指标分区。

**D. 懒创建**
- 在指标 Tab 下"新增项目"提交时（`handleAddItemSubmit`）：
  - 若指标分区尚不存在，先创建根级指标分区（`parentId=0, nodeRole='group', isIndicatorSection=true`），拿到新 id；
  - 再把指标项以新分区 id 作为 `parentId` 提交；
  - 重新加载后 `activeTabId` 切到真实分区 id。
- 已存在指标分区的模板：直接挂到现有分区，无懒创建。
- 指标 Tab 内项目类别锁定为 indicator（沿用 `checkAncestorIsIndicatorSection` / `isUnderIndicatorSection`）。

**E. activeTab 合法性**
- 把指标 Tab（真实 id 或哨兵）纳入合法 `activeTabId` 集合，`watch(tabNodes)` 的回退逻辑不得把它当非法 Tab 清掉。
- 默认选中：`loadTemplateItems` 仍默认选第一个**普通** Tab；指标 Tab 由用户点击进入。

### 4.2 报表展示 `web/src/views/finance/AmoebaPL.vue`（已满足）

现状已是左右两栏：
- 左栏 `.indicator-panel`（`AmoebaPL.vue:61`）渲染全局 `indicatorSections`，标题硬编码"运营指标"，由 `hasIndicators`（`:623`）控制显隐；
- 右栏 `.pnl-panel`（`:103`）含方向 Tab 与各 Tab 损益数据；
- 左栏是右栏同级，切换右栏 Tab 时左栏保持不变。

**功能上无需改动**，实现阶段仅验证上述行为；如左栏样式需与编辑器特别 Tab 视觉呼应可做轻微 CSS 调整，不改数据流。

### 4.3 后端（无改动）

报表接口已分别返回全局 `IndicatorSections`（`TabAncestorId=0`）与 per-tab `Sections`；`AddItemAsync` 对"指标分区子项必须为 indicator"的校验（`AmoebaService.cs:262`）保持。懒创建复用既有损益项 CRUD 接口，无需新增端点。

## 5. 边界情况

- **模板无指标分区**：编辑器仍显示固定指标 Tab（空），首次加指标项懒创建；报表 `hasIndicators=false` 时左栏隐藏（可接受）。
- **历史脏数据：多个根级指标分区**：取排序最前的一个作为固定 Tab 来源；其余暂保持原样（清理不在本次范围）。
- **指标 Tab 不可删/改名**：固定系统 Tab；如需清空指标，逐项删除其子项。

## 6. 测试

**前端契约（`scripts/tests/*.mjs`，沿用现有契约测试风格）：**
- 普通 Tab 的 `treeData` 不再包含指标分区节点。
- 固定指标 Tab 始终存在、排最左、无删除/改名入口。
- "+指标分区"按钮已移除。
- 懒创建：无分区时在指标 Tab 加指标项 → 先建根级指标分区、再挂项。

**报表：**
- 验证左栏在右栏 Tab 切换时保持显示；`hasIndicators` 显隐正确。

## 7. 实现顺序建议

1. 编辑器：移除按钮 + 删除 treeData 第 1 步 + 固定指标 Tab（先支持"已存在分区"路径）。
2. 编辑器：懒创建（无分区路径）。
3. 报表：验证（必要时轻微 CSS）。
4. 契约测试补齐。
