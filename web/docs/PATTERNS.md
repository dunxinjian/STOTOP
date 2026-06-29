# STOTOP 前端页面骨架范式契约（PATTERNS）

> 阶段7 设计系统层产物。本文件是「页面长什么样」的硬约束：四类页面标准骨架 + 共享组件契约 + 令牌速查 + 红线验收命令。
> 令牌真源见 `web/docs/TOKENS.md` 与 `src/stores/theme.ts` 的 `applyDesignTokensCSS()`；本文件只规定**怎么用**。
>
> 三通道自动继承：任何页面只要用了 `.page-container`/`.page-card`/`.page-toolbar` 全局类、antd 组件、或被 `<a-config-provider>` 包裹，
> 就自动继承新内外边距、卡片范式、表头/行高/hover/校验态、主色 token——**旧页无需逐页改即继承新视觉**。

---

## 一、四类页面标准骨架

### 1. 列表页（样板：dormitory/BuildingManage、vehicle/VehicleManage —— DataTable 范式）

**页头布局规则**（首行不放标题/面包屑——标题在 Tab 上）：

```
PageHeader 首行（toolbar-primary，恒显示）：
├─ #left（左）：状态快筛 <StatFilterTabs inline>（有则放）；无快筛则放筛选输入
└─ #right（右）：筛选输入 / 重置 + 主操作「新增」
<DataTable>（内建：序号列 + 分页「共 N 条」+ 空态；状态列 <StatusTag>）
```

- **填满首行、别想藏**：首行恒渲染（`toolbarStore.register` 总置 `hasRow1=true`），故把内容填进去而非试图收起。默认**单行**：左=状态Tab（或筛选）、右=筛选+新增，中间自然留白。
  - 有状态快筛：`#left` 放 `<StatFilterTabs inline>`，`#right` 放筛选输入 + 重置 + 新增（样板 VehicleManage）。
  - 无状态快筛：`#left` 放筛选输入 + 重置，`#right` 放新增（样板 BuildingManage）。
- **拥挤降级**：`toolbar-primary` 已加 `flex-wrap`，控件多/窄屏时右簇整组自动换行（零跳变）；控件特别多的页可显式把筛选放 `#toolbar` 第二行。
- **不再用 `#toolbar`/`.page-toolbar` 第二行放筛选**（旧「布局 B」作废）。首行筛选/操作控件统一 `size="middle"`（32px，对齐首行按钮）。
- **表格**用 `<DataTable>`（见 §二），内建分页/序号列/空态；放 `.page-container` 直接子级获 flex 滚动链。
- **密度**：操作列按钮无需写 `size`（继承全局 small）；首行筛选/操作用 `size="middle"`。
- 空数据由 DataTable 内建 `<EmptyState>`（占位行 hover 不高亮）；不手写 `<a-empty>`/`#emptyText`。

**列表页迁移配方**：① 筛选条 + 状态快筛 + 新增 全进 PageHeader 首行（`#left`/`#right`，见上规则），不用 `#toolbar`；② `a-card`+`a-table` → `<DataTable v-model:pagination @change>`，删 `paginationConfig`/`handleTableChange`/序号列/序号 bodyCell；③ `a-tag :color` → `<StatusTag :type>`；④ 去 `bordered`；⑤ 分页 `ref`，读 `.value.*`；⑥ 首行控件 `size="middle"`。

**容器与表格观感（最终标准，样板：`system/UserManage` 数据多·验长列表）**：

- **贴边容器**：列表页根用 `page-container page-container--flush`（或 `<PageLayout flush>`）。`--flush` 去掉页容器**四周留白（含顶部）**——表格满宽、并**直接贴住顶部工具栏**（靠工具栏自带底边线 + 浅阴影分隔），消除工具栏与表格间露出 `--bg-page` 的「灰带缝」。
- **高度自适应**：`--flush` 下表格 `flex: 0 1 auto`——少数据按内容高（不撑满、无大片白）；数据超一屏时**表体内部滚动、列头与分页固定**。
- **表格观感统一在全局 `ant-override.scss`**（所有 `a-table` 自动继承，**无需逐页**，且不要在页面或 DataTable 里 scoped 覆写）：
  - 列头：白底融入数据行 + 中性墨字（`--text-2`）+ 字间距 + **耳语级柔线 `--border-faint`**（白表头白首行不留「缝」）。
  - 行：**去斑马纹**，仅留一条极细行底线（`--border`）撑行结构；逐行扫读交给 hover。
  - 行 hover：**中性浅灰 `--bg-muted`**（非浅橙）。
  - 操作列：表体内 `a-button type="link"` 横向 padding 收到 4px，多操作（编辑/重置/删除）并排更紧凑。
  - 分页：顶线降到 `--border-faint`、页码块去边框去底色、仅当前页留主色描边。
- 个别超宽超密大报表若去斑马后难扫读，可**按页例外**恢复斑马（`:nth-child(even)` 浅底），不动整体克制风格。

### 2. 详情页（样板：TaskDetail）

```
.page-container
├─ <PageHeader :backTo="...">（返回按钮 → #page-toolbar-left）
└─ <a-row>
   ├─ <a-col :md=主区16>  正文 / 描述 / 操作
   └─ <a-col :md=信息栏8>  侧栏 sticky 元信息
```

- 返回按钮由 `PageHeader` 的 `backTo` prop 渲染，主色 hover 浅橙，禁止页面自造返回按钮。

### 3. 表单页（样板：ExpenseReimburseSubmit）

```
.page-container
├─ <PageHeader>（#actions 保存/提交）
└─ <BaseCard> 或 <a-card>
   └─ <a-form>（统一 :label-col；冒号由 ConfigProvider 接管）
```

- 必填星 / 校验错误文案由 `ant-override.scss` 统一令牌化（`--color-danger` / `--color-danger-text`），页面不重复定义。
- label 字号统一 `--font-sm2`（13）。

### 4. 仪表盘（样板：express/Dashboard）

```
.page-container（或自定义 flex 容器）
├─ <PageHeader>（#toolbar 周期/组织筛选）
├─ KPI 条（.kpi-bar：--bg-card + --radius-lg + --shadow-sm）
└─ <a-tabs>
   └─ 内容 panel（.content-panel）/ <StatCard> 网格
```

- KPI / StatCard 数值色走 `--color-info`/`--biz-*`/`--color-success` 等令牌；echarts 序列色用十六进制字面量（canvas 不吃 CSS 变量，属唯一豁免）。

---

## 二、共享组件契约

### PageHeader —— Teleport 四槽

| 插槽 | 传送目标 id | 用途 |
| --- | --- | --- |
| `#left` / `#title-extra` | `#page-toolbar-left` | 标题左侧附加 |
| `#center` | `#page-toolbar-center` | 居中标题（`--font-lg`/`--text-1`） |
| `#right` / `#actions` | `#page-toolbar-actions` | 右侧主操作 |
| `#toolbar` | `#page-toolbar-row2` | 第二行筛选区 |
| `backTo` prop | `#page-toolbar-left` | 返回按钮（主色 hover 浅橙） |

- 标题不在页内显示，由面包屑承担；不改 keep-alive token 注册机制（`registerToolbar`/`instanceToken`）。
- **页面 scoped `:deep` 够不到工具栏内容**：`#left`/`#actions`/`#toolbar` 被 Teleport 到面包屑（`AppBreadcrumb`）外层 DOM，页面 `<style scoped>`（含 `:deep`）无法命中被传送走的控件。要调工具栏控件的高度/样式，用**组件 prop**（如分段控件 `size="middle"` 拿原生 32px，对齐右簇筛选/按钮）或在 **`AppBreadcrumb` 的 `.toolbar-right :deep(...)`** 加全局规则；写进页面 scoped 里不生效（曾踩：分段 Tab scoped 高度无效，实测仍 24px）。

### EmptyState

| prop | 说明 |
| --- | --- |
| `size` | `default`（整页 min-height 300）/ `small`（表格内嵌 min-height 160，图 60） |
| `title` / `description` | 文案，色用 `--text-1` / `--text-3` |
| `icon` / `iconColor` | 自定义图标，默认色 `--text-disabled` |
| `actionText` + `actionRoute` + `showAction` | 操作按钮 |

表格空态：`<template #emptyText><EmptyState size="small" title="暂无数据" /></template>`。

### BaseCard

| prop | 默认 | 说明 |
| --- | --- | --- |
| `title` / `#title` / `#extra` | — | 卡头标题与操作区 |
| `bordered` | `true` | 边框 `--border` |
| `hoverable` | `false` | hover 升 `--shadow-md` |
| `noPadding` | `false` | 主体去内边距（内嵌表格） |

轻量纯容器仍可直接用全局 `.page-card` 类；带标题/操作/hover 变体用 `BaseCard`。

### StatusTag

| prop | 取值 | 映射 |
| --- | --- | --- |
| `type` | `success`/`warning`/`danger`/`info`/`default` | `--color-*-light` 底 + `--color-*-text` 字 |
| `biz` | `waybill`/`contract`/`quality`/`approval`/`points`/`finance` | 业务色字 + 浅底（优先于 type） |
| `dot` | 布尔 | 前置状态圆点 |

统一替代手写 `a-tag :color="'success'|'processing'|'error'"`。

### DataTable

列表页表格封装（样板 `dormitory/BuildingManage`），消除各页重复的 paginationConfig + 序号列 + 空态。

| prop | 默认 | 说明 |
| --- | --- | --- |
| `columns` / `data-source` / `loading` | — | 透传 a-table；`columns` 不含序号列 |
| `pagination`（v-model） | `{pageIndex,pageSize,total}` | `v-model:pagination` 绑响应式对象（父用 `ref`）；翻页 emit `update:pagination` 新对象 + emit `change`；传 `false` 关闭分页 |
| `index-column` | `true` | 最左自动加「序号」列并按分页算行号 |
| `bordered` | `false` | 克制收敛去边框 |
| `row-key` / `scroll` / `empty-text` | `'id'` / — / `'暂无数据'` | 透传 / 内建空态主标题（绑 EmptyState `:title`） |

- 翻页：`@change` 触发后父组件重新取数；不再每页手写 `paginationConfig`/`handleTableChange`。
- 其余列单元格用父组件 `#bodyCell` 作用域插槽（序号列由组件内部渲染）。
- 密度：不显式传 `size`，继承 `ConfigProvider` 全局 `small`。
- **观感不在此封装**：列头/行线/斑马/hover/分页等视觉统一在全局 `ant-override.scss`（见 §一.1）；DataTable 自身**无 scoped 样式**，只管行为（分页 v-model / 序号列 / 空态）。

### StatFilterTabs

带计数的状态快筛条（样板 `vehicle/VehicleManage`），把「KPI 统计卡 + 状态下拉」合一为一行可点击过滤的 Tab。

| prop | 说明 |
| --- | --- |
| `tabs` | `[{key,label,count?,color?}]`；`key=''` 常表示「全部」；`color` 传 `var(--token)` 渲染状态圆点 |
| `active`（v-model） | 当前选中 key；点击 emit `update:active` + `change(key)` |
| `inline` | 布尔，默认 false。置于 PageHeader 首行 `#left` 内时传 `inline` 去掉底部外边距 |

- **标准用法**（见 §一.1 列表页头规则）：放进 PageHeader 首行 `#left`，筛选输入 + 主操作入 `#right`：
  ```
  <PageHeader>
    <template #left>
      <StatFilterTabs inline v-model:active="searchForm.status" :tabs="statusTabs" @change="handleSearch" />
    </template>
    <template #right> 搜索 / 下拉 / 重置（size=middle）+ 新增 </template>
  </PageHeader>
  ```
  替代顶部 a-statistic 卡片与独立状态下拉，与筛选/新增同处首行；首行 `flex-wrap` 拥挤时自动换行。
- 全令牌：选中态走 `--color-primary`/`--color-primary-light`，计数 `tabular-nums`；禁裸 hex。

### PageLayout

页面容器基元（样板：列表页 `dormitory/BuildingManage` 用 table；流式详情页 `cardflow/OrchestrationDetailPage` 用 flow）。

| prop | 默认 | 说明 |
| --- | --- | --- |
| `variant` | `'table'` | `table`=渲染全局 `.page-container`，保留表格 flex 滚动链（单表列表页，表体独立滚动）；`flow`=渲染独立 `.page-flow`（token 内边距+卡片间距+整页滚动），不施加表格链（多卡片纵向流式详情页） |
| `flush` | `false` | 列表贴边：去 `.page-container` 四周留白（含顶部）让表格满宽并贴住工具栏 + 高度自适应（仅 `table` variant 生效，见 §一.1） |

- flow variant 用独立类避开全局表格滚动链，**收编各页手写的「解除全局 .page-container 锁定」覆写**（禁止页面再 scoped 覆写 `.page-container` 或 `:deep(.ant-spin-*)` 解锁）。
- 注意：flow（`.page-flow`）下裸 `<a-card>` 不受全局 `.page-container .ant-card{border-radius:0}` 压制，会保留自身圆角——这是预期差异。
- 与 PageHeader 正交：标题→面包屑、操作→PageHeader、内容→PageLayout。

---

## 三、令牌速查（唯一权威，禁止再引入字面量）

> 完整清单见 `web/docs/TOKENS.md`。下表为本阶段最常消费项。

| 语义 | 变量 | 值 |
| --- | --- | --- |
| 主色 / hover / 浅底 / 边框 | `--color-primary` / `-hover` / `-light` / `-border` | `#E85E00` / `#FF6700` / `#FFF3EA` / `rgba(232,94,0,.30)` |
| 状态色（底/字） | `--color-success/warning/danger/info-light` `-text` | 见 TOKENS.md |
| 文字 1/2/3/禁用 | `--text-1` / `--text-2` / `--text-3` / `--text-disabled` | `#1F2329` / `#5A6068` / `#8A9099` / `#BFC3C9` |
| 表面 / 卡片 / 弱底 | `--bg-page` / `--bg-card` / `--bg-muted` | `#F7F8FA` / `#FFFFFF` / `#F1F3F6` |
| 边框 普通 / 更浅(耳语) / 更强 | `--border` / `--border-faint` / `--border-strong` | `#ECEEF1` / `#F2F4F6` / `#DDE0E4` |
| 强调块上文字 / 危险焦点环 | `--text-on-accent` / `--color-danger-border` | `#FFFFFF` / `rgba(214,88,78,.30)` |
| 业务色 | `--biz-waybill/contract/quality/approval/points/finance` | 见 TOKENS.md |
| 圆角 | `--radius-sm/md/lg/modal/pill` | `4/6/8/12/999px` |
| 阴影 | `--shadow-sm/md/lg` | `0 1px 2px…` / `0 4px 12px…` / `0 8px 24px…` |
| 字号 | `--font-xs/sm/sm2/base/lg/xl/2xl` | `11/12/13/14/16/18/24px` |
| 间距（4 基数） | `--space-2xs2/xs4/sm8/md12/lg16/xl24/2xl32` | `2/4/8/12/16/24/32px` |
| 布局 | `--toolbar-height` / `--page-pad-x` / `--page-pad-y` | `40px` / `16px` / `12px` |

页面内边距单一真源：`.page-container { padding: var(--page-pad-y) var(--page-pad-x) }`，
动态轨由 `theme.ts applyPagePaddingCSS` 写变量，**双轨 !important 已收口**。

> 多卡片纵向流式页（详情/概览）用 `<PageLayout variant="flow">`（见 §二），不要 scoped 覆写 `.page-container`。

---

## 四、红线 + 验收命令（PowerShell / rg）

新代码禁止写死颜色字面量；改色一律走令牌。验收命令（worktree `web/` 下执行）：

```powershell
# 1) 硬编码蓝（旧 antd 主色）应趋零（echarts 序列色、第三方桥除外）
rg "#1890ff|#1677ff|1890ff|22, ?119, ?255|24, ?144, ?255" src --glob "*.vue" --glob "*.scss"

# 2) 自造空态递减指标（替换为 <EmptyState> 后应下降）
rg "<a-empty" src/views -c          # 巡检基线：91 文件
rg -l "class=\"[^\"]*empty[^\"]*\"" src/views   # 手写空态：43 文件

# 3) page-padding 双轨断言：theme.ts 不应再注入 .page-container 的 !important
rg "page-container.*!important" src/stores/theme.ts   # 预期 0 命中

# 4) 全局范式类存在性
rg "\.page-toolbar|\.page-card" src/styles/index.scss
```

### 自造空态摸排台账（阶段7 基线，供逐模块巡检递减）

- `<a-empty>`：**91 文件**（阶段0 前为 167，已替换部分）。
- 手写 `class="*empty*"`：**43 文件**。
- 本阶段仅替换样板（Dashboard 待办/预警 2 处 → `EmptyState size="small"`；VoucherList `#emptyText` 已用 `EmptyState`）；
  其余进逐模块巡检阶段，按上表命令递减。

### preview 抽检清单（四类样板）

- 列表页：system/UserManage（贴边·长列表样板）/ VoucherList / ContractList —— 表头白 + 耳语柔线、行 hover 中性浅灰、去斑马仅极细行底线、贴边满宽贴住工具栏、超屏时表体内滚而列头+分页固定。
- 详情页：TaskDetail —— 返回按钮橙、侧栏元信息弱底。
- 表单页：ExpenseReimburseSubmit —— 必填星红、校验错误文案、label 13px。
- 仪表盘：express/Dashboard 全三 Tab —— KPI 条卡片范式、空态 small、panel 左条主色。
