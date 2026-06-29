## 阶段7 实施计划：设计系统层（共享组件 + 全局覆盖，全站自动继承）

### 探查结论（基于真实文件，非假设）

逐文件读取后确认了 7 个对实施有决定性影响的现状：

1. **令牌桥不存在（最关键风险）**：`rg --color-primary|--bg-page|--biz-*|--radius-lg` 在 `web/src` 全树 **0 命中**。`git log` 仅见两条 `docs(ui)` 设计 spec 提交，**阶段0/1 的实现尚未落地**。`variables.scss` 仍是旧调色板（`$color-primary: #FF6700`、`$color-info: #13C2C2`、大量 `#1890ff/#1677ff`）。→ 本阶段若直接写 `var(--color-primary)`，构建产物里这些变量会解析为空。**必须在消费前先以 Task 1 建立 `tokens.scss` 兜底桥**（若阶段0真已落地，则 Task 1 退化为"校验+补缺"）。

2. **pagePadding 双轨实锤**：`index.scss:58` `.page-container { padding: $page-padding }`（`variables.scss:105` `$page-padding: 0`）是静态轨；`theme.ts:191-200` `applyPagePaddingCSS` 又向 `.page-container` 注入 `padding: …!important`（默认 `pagePaddingX/Y: 0`）是动态轨。两轨都把页面内边距压到 0，业务页只能各自在 scoped 里补 `padding`（如 `ExpenseReimburseSubmit.vue:58`、`TaskDetail.vue:481`、`Dashboard.vue:306` 各写各的）。→ 收口方案：动态轨改写 `--page-pad-x/y` CSS 变量，静态轨 `.page-container` 单一规则消费 `var(--page-pad-x/y)`，删除 `!important`。

3. **`.page-container` 是事实标准容器**：194 个文件引用，且全局定义只此一处（`index.scss`），自带完整 flex 滚动链（`.ant-card`→`.ant-table-body`）。→ 全站自动继承的"主通道"已就位，本阶段只需把它的视觉令牌化，无需逐页改。

4. **PageHeader 已是 Teleport 架构**：内容传送到 `AppBreadcrumb.vue` 的 `#page-toolbar-left/-center/-actions/-row2` 四槽。硬编码蓝在 `PageHeader.vue:129` `color: #1677ff` 和 `:135` `rgba(22,119,255,0.06)`。→ 标准化只换色，不动 Teleport 机制。

5. **EmptyState 已存在但未令牌化**：`#d9d9d9`、`rgba(0,0,0,0.85/0.45)` 硬编码（`EmptyState.vue:55/93/99`）。而业务页自造空态规模庞大：`<a-empty>` 命中 **167 文件 378 处**，`class="*empty*"` 手写空态命中 **43 文件 99 处**。→ 标准化 EmptyState 后给出 rg 摸排清单，分批替换（本阶段先建组件 + 替换 3 个样板页，剩余进逐模块巡检）。

6. **ConfigProvider Table token 已存在**：`theme.ts:116-131` 已配 `Table.headerBg/#fafafa`、`rowHoverBg/#f5f7fa`、`Button.controlHeight/32`。→ 不新建，只把字面量对齐 `--bg-muted/--bg-card` 口径，斑马/对齐补在 `ant-override.scss`。

7. **卡片三轨并存**：全局类 `.page-card`（index.scss）、`a-card` 覆盖（ant-override.scss）、各页自造（`StatCard.vue` `border-radius:8px;box-shadow:…` 写死、`Dashboard.vue .content-panel` 写死）。`StatCard.vue:51/63` 默认 `#1677ff`。→ **评估结论：抽 `BaseCard.vue`**。理由：带标题/操作区/无内边距变体靠全局类无法表达（`a-card` 的 head padding 与 hover 已被 ant-override 接管，但自造卡片游离在外），抽组件可承接渐进替换；同时保留全局 `.page-card` 类供轻量场景。

### 自动继承机制（三通道，全站零逐页改即生效）

- **通道A 全局类/CSS 变量**：`tokens.scss` 的 `:root` 变量 + `index.scss` 的 `.page-container/.page-card/.page-toolbar` → 任何用了这些类的页面（194 个）自动继承新内外边距、卡片范式、工具栏范式。
- **通道B ant-override.scss**：`.ant-table/.ant-card/.ant-btn/.ant-form-item` 全局覆盖 → 所有 antd 组件（几乎全站）自动继承表头/行高/hover/圆角/校验态。
- **通道C ConfigProvider token**：`App.vue:47` 已包裹全树的 `<a-config-provider :theme>` → `theme.ts` 的 token 改动一处生效全站表格/按钮。
- 三通道叠加：组件级（PageHeader/EmptyState/BaseCard/StatusTag）只是给"愿意显式用"的页提供一致骨架，**不依赖逐页替换即可让旧页继承新色**。

### Task 列表

---

### Task 1: 建立令牌桥防御性兜底 tokens.scss（消费前的地基）

**Files:**
- Create `web/src/styles/tokens.scss`
- Modify `web/src/styles/index.scss:1-5`（@use 顺序）

- [ ] **Step 1** 先验证阶段0是否真已落地：运行 `cd web; rg "color-primary" src/styles/ -l`，预期当前输出仅 `variables.scss`（SCSS `$color-primary`，非 CSS 变量），证明 `:root` 桥缺失，需本 Task 兜底。
- [ ] **Step 2** 在 `tokens.scss` 的 `:root{}` 内逐条声明唯一权威令牌集全部 CSS 变量。颜色/圆角/阴影/字号/间距严格对齐 prompt 给定的精确值与精确名（如 `--color-primary:#E85E00; --color-primary-hover:#FF6700; --bg-page:#F5F6F8; --biz-waybill:#6B4FB0; --radius-lg:8px; --shadow-sm:0 1px 2px rgba(18,31,53,0.05); --space-md12:12px; --toolbar-height:40px;` 等）。新增 `--page-pad-x/--page-pad-y` 初值（建议 `16px`/`12px`，替代当前 0 的拥挤）。
- [ ] **Step 3** 若 `rg` 在 Step 1 发现阶段0已建桥（命中非 variables 的 `:root --color-primary`），则把本文件改为"仅补缺"模式（只声明缺失项），避免与阶段0真源重复定义；在文件头注释写明"阶段0真源优先，此处仅兜底"。
- [ ] **Step 4** `index.scss` 首行改为 `@use './tokens.scss';`（在 `variables` 之前），确保变量在全局样式编译期可用。
- [ ] **Step 5** 验证：`cd web; npm run build`，预期 exit 0；构建后 `rg "var\(--color-primary\)" dist/ -l` 应能命中且 dist css 中存在 `--color-primary:#E85E00`（变量被打包而非丢空）。
- [ ] **Step 6** `git add web/src/styles/tokens.scss web/src/styles/index.scss; git commit`，message：`feat(ui): 建立设计令牌 CSS 变量桥 tokens.scss(阶段7地基/防御兜底)` + 结尾 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`。

---

### Task 2: 收口 pagePadding 双轨 + .page-container 令牌化

**Files:**
- Modify `web/src/styles/index.scss:57-66`
- Modify `web/src/stores/theme.ts:191-200`、`:52-78`（默认值）

- [ ] **Step 1** 读现状：`index.scss:59` 当前 `padding: $page-padding;`（解析为 0）。目标改为 `padding: var(--page-pad-y) var(--page-pad-x);`，并新增 `background: var(--bg-page);`。
- [ ] **Step 2** 读现状：`theme.ts:199` 当前 `style.textContent = \`.page-container { padding: ${paddingY}px ${paddingX}px !important; }\``。目标改为写 CSS 变量而非规则：`document.documentElement.style.setProperty('--page-pad-x', paddingX+'px'); document.documentElement.style.setProperty('--page-pad-y', paddingY+'px')`，函数体不再生成 `<style>` 注入（删除 `!important` 双轨）。
- [ ] **Step 3** `theme.ts:71-72` 默认 `pagePaddingX/Y: 0` 改为与 tokens 初值一致（如 `16`/`12`），使未配置主题的环境也有合理留白。
- [ ] **Step 4** 静态断言：`rg "padding:.*!important" src/stores/theme.ts` 预期 0 命中（双轨已拆）；`rg "var\(--page-pad" src/styles/index.scss` 预期命中 1 行。
- [ ] **Step 5** `npm run build` 过；用 preview 抽检一个列表页（VoucherList）确认内容区四周有留白且无横向滚动溢出。
- [ ] **Step 6** `git add -A; git commit -m "refactor(ui): 收口 pagePadding 双轨为 --page-pad-x/y 单一变量"` + Co-Authored 尾。

---

### Task 3: 标准化 PageHeader（去硬编码蓝、四要素令牌化）

**Files:**
- Modify `web/src/components/PageHeader.vue:124-137`

- [ ] **Step 1** 读现状：`:129` `color: #1677ff;`、`:135` `background: rgba(22, 119, 255, 0.06);`。目标 `.toolbar-back-btn` 改 `color: var(--color-primary);` 与 hover `background: var(--color-primary-light);`，圆角 `border-radius: var(--radius-sm);`。
- [ ] **Step 2** `:151` `.page-toolbar-center-content` 的 `color: $text-primary;` 改 `var(--text-1);`，字号 `16px`→`var(--font-lg)`。
- [ ] **Step 3** 确认 Teleport 四槽（left/center/actions/row2 + 返回按钮）逻辑与 `AppBreadcrumb.vue` 的 `#page-toolbar-*` id 一一对应，不改 keep-alive token 注册机制（`registerToolbar`/`instanceToken`）。
- [ ] **Step 4** 静态断言：`rg "#1677ff|22, 119, 255|22,119,255" src/components/PageHeader.vue` 预期 0 命中。
- [ ] **Step 5** `npm run build` 过；preview 进入任一带返回按钮的详情页（如 VoucherEntry），截图确认返回按钮为橙色 hover 浅橙。
- [ ] **Step 6** `git add -A; git commit -m "feat(ui): PageHeader 去硬编码蓝改用令牌(返回/标题/居中)"` + Co-Authored 尾。

---

### Task 4: 全局类 .page-card + .page-toolbar 范式（index.scss）

**Files:**
- Modify `web/src/styles/index.scss:132-217`

- [ ] **Step 1** 读现状：`:133-139` `.page-card { background:#fff; border-radius:0; padding:20px; box-shadow:$shadow-sm; }`。目标令牌化：`background: var(--bg-card); border-radius: var(--radius-lg); border: 1px solid var(--border); padding: var(--space-lg16); box-shadow: var(--shadow-sm); margin-bottom: var(--space-lg16);`（卡片范式：圆角+边框+阴影+内边距四要素一齐）。
- [ ] **Step 2** 新增 `.page-toolbar` 工具栏范式类：`min-height: var(--toolbar-height); padding: var(--space-sm8) var(--space-lg16); display:flex; align-items:center; gap:var(--space-sm8);` 并含 `&__group`(按钮组 gap)、`&__filters`(筛选区 margin-left:auto) 子结构，对齐 VoucherList 当前手写的 `.toolbar-row/.filter-group`。
- [ ] **Step 3** `.search-bar:142`、`.page-section:223`、`a:273` 的 `#fff`/`#303133`/`$color-primary` 字面量统一换 `var(--bg-card)/var(--text-1)/var(--color-primary)`。
- [ ] **Step 4** 静态断言：`rg "border-radius: 0" src/styles/index.scss` 仅保留确需直角处；`rg "\.page-toolbar" src/styles/index.scss` 命中。
- [ ] **Step 5** `npm run build` 过。
- [ ] **Step 6** `git add -A; git commit -m "feat(ui): 新增 .page-card/.page-toolbar 全局范式类(令牌化)"` + Co-Authored 尾。

---

### Task 5: ant-override.scss 表格/卡片/表单/按钮令牌化（通道B+C）

**Files:**
- Modify `web/src/styles/ant-override.scss:6-36`(表格)、`:39-59`(卡片)、`:152-189`(按钮/输入/表单)、`:281-309`(toolbar-btn)
- Modify `web/src/stores/theme.ts:116-131`(Table/Button token)

- [ ] **Step 1** 表格表头：`:8` `background: #FAFAFA;`→`var(--bg-muted);`，`:11/:13/:18` 边框 `$border-color-lighter`→`var(--border);`；新增斑马 `.ant-table-tbody tr:nth-child(even) td{ background: var(--bg-page); }` 与数值列 `.num-col{ text-align:right }`（已存在则对齐）。行 hover 统一 `var(--color-primary-light)`。
- [ ] **Step 2** 卡片：`:41/:42` `border`/`box-shadow` 换 `var(--border)`/`var(--shadow-sm)`，圆角加 `var(--radius-lg)`（在非"工具栏紧贴"场景）。
- [ ] **Step 3** 按钮与聚焦光晕：`:175/:182` `rgba(22,119,255,0.1)` 与 `:284-301` `.toolbar-btn` 的 `#1890ff/rgba(24,144,255,…)` 全部换 `var(--color-primary)`/`var(--color-primary-light)`/`var(--color-primary-border)`。
- [ ] **Step 4** 表单范式：`:187` `.ant-form-item-label>label` 补 `font-size: var(--font-sm2);`；新增必填星 `.ant-form-item-required::before{ color: var(--color-danger); }`、校验态 `.ant-form-item-explain-error{ color: var(--color-danger-text); }`、统一 label 列宽建议（注释说明用 ConfigProvider Form.labelColon/页面 `:label-col` 约定）。
- [ ] **Step 5** `theme.ts:118-120` Table `headerBg:'#fafafa'`/`rowHoverBg:'#f5f7fa'` 注释标注对应 `--bg-muted`/`--color-primary-light` 口径（值保持十六进制因 antd token 不吃 var）。
- [ ] **Step 6** 静态断言：`rg "1890ff|22, ?119, ?255|24, ?144, ?255|#FAFAFA" src/styles/ant-override.scss` 预期 0 命中。
- [ ] **Step 7** `npm run build` 过；preview 抽检 VoucherList 与 ContractList 表格，确认表头灰、hover 浅橙、斑马生效。
- [ ] **Step 8** `git add -A; git commit -m "feat(ui): ant-override 表格/卡片/表单/按钮令牌化(去蓝/统一表头hover斑马校验态)"` + Co-Authored 尾。

---

### Task 6: 标准化 EmptyState + 摸排自造空态

**Files:**
- Modify `web/src/components/EmptyState.vue:54-60`(默认色)、`:71-104`(样式)

- [ ] **Step 1** 读现状：`:55` `iconColor:'#d9d9d9'`→`'var(--text-disabled)'`；`:93` `.empty-title color: rgba(0,0,0,0.85)`→`var(--text-1)`；`:99` `.empty-description rgba(0,0,0,0.45)`→`var(--text-3)`。
- [ ] **Step 2** 新增 `size` prop(`'small'|'default'`)：`small` 时 `padding: var(--space-xl24)`、`min-height: 160px`、`imageSize:60`，供表格 `#emptyText` 内嵌；`default` 保持整页空态。标题字号 `var(--font-lg)`。
- [ ] **Step 3** 摸排自造空态命令(PowerShell)：
  - `cd web; rg -l "class=`"[^`"]*empty[^`"]*`"" src/views` → 43 文件手写空态清单
  - `cd web; rg -l "<a-empty" src/views` → 167 文件 a-empty 清单
  - 交集即"应替换为 `<EmptyState>`"的优先页；输出存为巡检台账（注：本阶段不全量替换，仅样板）。
- [ ] **Step 4** 样板替换：在 VoucherList(`:205` `#emptyText`)已用 `<EmptyState />`，确认其继承新色；再替换 Dashboard(`:95/:108` `<a-empty description="暂无待办">`)为 `<EmptyState size="small" title="暂无待办" />`。
- [ ] **Step 5** 静态断言：`rg "#d9d9d9|rgba\(0, ?0, ?0, ?0\.85\)|rgba\(0, ?0, ?0, ?0\.45\)" src/components/EmptyState.vue` 预期 0 命中。
- [ ] **Step 6** `npm run build` 过；preview 抽检 Dashboard"待办与预警"Tab 空态。
- [ ] **Step 7** `git add -A; git commit -m "feat(ui): EmptyState 令牌化+size 变体,样板页替换自造空态(附摸排台账)"` + Co-Authored 尾。

---

### Task 7: 卡片范式定型——BaseCard 组件 + StatCard 样板令牌化

**Files:**
- Create `web/src/components/BaseCard.vue`
- Modify `web/src/views/conference/components/StatCard.vue:45-64`(默认色)、`:73-118`(样式)

- [ ] **Step 1** 评估记录(写入组件头注释)：仅全局 `.page-card` 类无法表达"带标题+操作区+无内边距+hover"变体，故抽 `BaseCard.vue`：props `title/bordered/hoverable/noPadding`，`#title`/`#extra`/默认插槽；根 `.base-card` 套 `.page-card` 范式(`var(--radius-lg)/var(--border)/var(--shadow-sm)/var(--space-lg16)`)，`hoverable` hover 升 `var(--shadow-md)`。
- [ ] **Step 2** `StatCard.vue:51/63` 默认 `#1677ff`→`var(--color-info)`；`:60-64` 阈值色 `#52c41a/#1677ff/#fa8c16`→`var(--color-success)/var(--color-info)/var(--color-warning)`。
- [ ] **Step 3** `StatCard.vue:75-118` 样式：`border-radius:8px`→`var(--radius-lg)`，`box-shadow:0 2px 8px…`→`var(--shadow-sm)`，hover→`var(--shadow-md)`，`#8c8c8c`→`var(--text-3)`，`#fff`→`var(--bg-card)`，字号 `24/14/13`→`var(--font-2xl/-base/-sm2)`。
- [ ] **Step 4** 静态断言：`rg "#1677ff|#8c8c8c|0 2px 8px" src/views/conference/components/StatCard.vue` 预期 0 命中；`rg "defineProps" src/components/BaseCard.vue` 命中。
- [ ] **Step 5** `npm run build` 过；preview 抽检含 StatCard 的看板页。
- [ ] **Step 6** `git add -A; git commit -m "feat(ui): 抽 BaseCard 卡片范式组件 + StatCard 令牌化样板"` + Co-Authored 尾。

---

### Task 8: 标签/徽章统一 StatusTag + 仪表盘/详情样板令牌化

**Files:**
- Create `web/src/components/StatusTag.vue`
- Modify `web/src/views/express/dashboard/Dashboard.vue:160-164`(KPI色)、`:300-458`(样式)
- Modify `web/src/views/task/TaskDetail.vue:248-254`(statusMap)、`:479-583`(样式)

- [ ] **Step 1** 建 `StatusTag.vue`：props `type`(`success|warning|danger|info|default`)与可选 `biz`(`waybill|contract|quality|approval|points|finance`)；内部映射到 `var(--color-success-light)`底+`var(--color-success-text)`字（及对应 biz 色），统一替代各页 `a-tag :color="'success'|'processing'|'error'"` 字面量。
- [ ] **Step 2** Dashboard KPI `:160-164` 颜色 `#1890ff/#722ed1/#1890ff/#fa8c16/#52c41a`→`var(--color-info)/var(--biz-waybill)/var(--color-info)/var(--color-warning)/var(--color-success)`。
- [ ] **Step 3** Dashboard 样式 `:307/:319/:411-431` 的 `#f0f2f5/box-shadow/#e6f4ff/#91caff/#1890ff` 改 `var(--bg-page)/var(--shadow-sm)/var(--color-primary-light)/var(--color-primary-border)/var(--color-primary)`；`.panel-title` 左条 `#1890ff`→`var(--color-primary)`。
- [ ] **Step 4** TaskDetail 样式 `:514/:523/:528/:556/:566` 的 `#333/#595959/#fafafa/#8c8c8c/#bfbfbf` 改 `var(--text-1)/--text-2/--bg-muted/--text-3/--text-disabled`，圆角 6/8→`var(--radius-md/-lg)`；`statusMap` 保留 antd 语义色(由 ConfigProvider 接管)，仅注释标注对应 `--color-*`。
- [ ] **Step 5** 静态断言：`rg "#1890ff|#722ed1|#fa8c16|#e6f4ff|#91caff" src/views/express/dashboard/Dashboard.vue` 与 `rg "#333|#595959|#bfbfbf" src/views/task/TaskDetail.vue` 预期 0 命中。
- [ ] **Step 6** `npm run build` 过；preview 抽检 Dashboard 全三 Tab + TaskDetail。
- [ ] **Step 7** `git add -A; git commit -m "feat(ui): StatusTag 状态色统一 + 仪表盘/详情样板令牌化"` + Co-Authored 尾。

---

### Task 9: 落地 web/docs/PATTERNS.md 页面骨架范式契约

**Files:**
- Create `web/docs/PATTERNS.md`

- [ ] **Step 1** 写四类页面标准骨架：**列表页**(`.page-container > PageHeader(actions+toolbar) + .table-card/a-card > a-table(#emptyText=EmptyState) + 分页`，样板 VoucherList/ContractList)、**详情页**(`.page-container > PageHeader(backTo) + a-row[主区16/信息栏8]`，样板 TaskDetail)、**表单页**(`.page-container > PageHeader(保存/提交) + BaseCard > a-form(label-col统一)`，样板 ExpenseReimburseSubmit)、**仪表盘**(`.page-container > PageHeader(筛选) + KPI条 + a-tabs > 内容panel/StatCard`，样板 Dashboard)。
- [ ] **Step 2** 写共享组件契约：PageHeader 四要素(标题/面包屑/操作区/返回)与 Teleport 槽对应表；EmptyState size 用法；BaseCard props/插槽；StatusTag type/biz 映射。
- [ ] **Step 3** 写令牌速查表：本 phase `newTokenNames` 全量列为表格(语义→变量→值)，标注"唯一权威，禁止再引入字面量"。
- [ ] **Step 4** 写红线 + 验收命令集(PowerShell rg)：`rg "#1890ff|#1677ff|1890ff|22, ?119, ?255" src --glob "*.vue" --glob "*.scss"` 应趋零；`rg "<a-empty" src/views -c` 作为后续巡检递减指标；并约定 preview 抽检四类样板页的截图清单。
- [ ] **Step 5** `git add web/docs/PATTERNS.md; git commit -m "docs(ui): 新增 PATTERNS.md 页面骨架范式契约+令牌速查+验收命令"` + Co-Authored 尾。

---

### 风险与排序说明

- **Task 1 必须最先且不可跳过**：实测令牌桥缺失，跳过会导致 Task 3-8 的 `var(--token)` 全部解析为空、视觉塌陷。若执行时发现阶段0已真落地，Task 1 退化为校验+补缺，不重复定义真源。
- **通道顺序**：先地基(1-2)→全局类(4)→全局覆盖(5)→组件(3/6/7/8)→文档(9)，保证每步 build 可过、preview 可验。
- **不做全量替换**：167+43 个自造空态/卡片不在本阶段逐页改（属逐模块巡检阶段），本阶段只建组件+令牌化全局通道+替换样板页，靠通道A/B/C 让旧页自动继承新色。
- **type-check 不作门禁**：依记忆 `vue-tsc -b` 基线本就大量红，验证以 `vite build` 过 + rg 静态断言 + preview 抽检为准。

### 实施最关键的文件

- `web/src/styles/tokens.scss`（新建，全栈继承的根，Task 1）
- `web/src/styles/index.scss`（`.page-container/.page-card/.page-toolbar` 全局通道 A）
- `web/src/styles/ant-override.scss`（antd 全局覆盖通道 B）
- `web/src/stores/theme.ts`（ConfigProvider token 通道 C + pagePadding 双轨收口）
- `web/docs/PATTERNS.md`（新建，范式契约，后续页面的硬约束）