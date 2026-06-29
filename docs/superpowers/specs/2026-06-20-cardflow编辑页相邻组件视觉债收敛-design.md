# CardFlow 编辑页相邻组件视觉债收敛（橙化 + 去绿 + 分类色令牌化）— 设计

> Follow-up of [[2026-06-19-cardflow流程编辑器视觉债收敛-design]]。上一轮收敛了流程定义编辑器**主页 + 8 designer + 列表页（10 文件）**；编辑页**内联渲染**的相邻组件不在那轮范围，仍残留约 190 处裸 hex。本轮把它们对齐同一令牌体系。
> 日期：2026-06-20。复用 [[stotop-theme-token-system]] 的 TOKENS.md 契约与上一轮 §5「三分映射」口径（**不重做**，直接引用）。
> 上游契约（只消费不重做）：`web/docs/TOKENS.md`（令牌真源）、上一轮 spec §3-§5（三分映射表）。

---

## 1. 范围与文件清单（6 文件）

| 文件 | 量级 | 颜色画像 | 本轮做什么 |
|---|---|---|---|
| `components/cardflow/runtime/CardComponentRenderer.vue` | ~52 | **绿青伪 primary 同族**（`#1f6f5f`/`#3d7d5c`/`rgba(31,111,95,*)` + 绿染中性 `#23322c`/`#1f3029`/`#7a8781`…） | **完整橙化 + 去绿**（同上一轮 §5 口径） |
| `components/cardflow/SchemaFieldEditor.vue` | ~58 | 中性灰 + danger 红 `#ef4444` + **12 色字段类型 `tone` 分类色** | 中性归一 + 语义换源 + **新增 `--cf-field-*` 分类令牌** |
| `components/cardflow/StageDefinitionEditor.vue` | ~76 | 中性灰 + 健康态 success/warning/danger + **3 色节点类型分类色**（manual蓝/auto紫/batch绿） | 中性归一 + 语义换源 + **新增 `--cf-node-*` 分类令牌** |
| `components/cardflow/StageInputFields.vue` | 3 | 纯中性 | 中性归一 |
| `components/cardflow/ConditionBuilder.vue` | 4 | 纯中性 | 中性归一 |
| `components/cardflow/SchemaRenderer.vue` | 8 | 中性 + 内联价格红 `#ee0a24` | 中性归一 + 价格红→danger |

### 任务原描述的两处订正（已与用户确认）

1. **`CardComponentRenderer` 并非「无绿青」**——它满载上一轮的伪 primary `#1f6f5f` 与绿染中性，需走**完整橙化 + 去绿**，非纯中性映射。（用户已确认：走完整橙化+去绿。）
2. **`SchemaFieldEditor` 并非「纯中性」**——`FieldType` 枚举带 12 色 `tone` 分类色（`:46-57`）。与 `StageDefinitionEditor` 的 3 色节点分类色一样，是稳定枚举绑定的数据编码色。（用户已确认：两套分类色**提升为命名令牌 `--cf-*`**。）

## 2. 用户决策（拍板记录）

1. **两套分类色 = 提升为命名令牌 `--cf-*`**（非豁免、非中性化）。在令牌真源新增静态常量，组件改 `var()` 引用——单一真源、可主题化、`rg` 色彩真清零。
2. **`CardComponentRenderer` = 完整橙化 + 去绿**，与上一轮编辑器/designer 视觉统一。
3. 沿用上一轮「零新增**语义**令牌」——success/warning/danger/info/primary 体系已够；本轮**仅新增分类令牌** `--cf-*`（数据编码用途，性质同 `--biz-*` 静态常量）。

## 3. 新增令牌（CardFlow 分类色）

性质等同既有 `--biz-*` 业务色：**静态常量**（不随 `themeConfig` 派生），登记于 `web/docs/TOKENS.md`、运行时注入于 `theme.ts` 的 `applyDesignTokensCSS()`（紧随 `--biz-*` 块，`:307` 后）。消费方以 `var()` 引用（含 SchemaFieldEditor 的 JS `tone` 字符串内联 `var()`）。

> 是否在 `web/src/styles/variables.scss` 加 `$cf-*` 编译期桥接：**镜像 `--biz-*` 的现状处理**——若 biz 无 `$` 桥接、且本轮组件均以裸 `var()` 消费（无 `$cf-*` 编译期消费者），则同样跳过桥接，仅 theme.ts + TOKENS.md 两处。实现阶段核对 biz 现状后定。

### ① 节点类型（StageDefinitionEditor，3 基色）

| 令牌 | 值 | 绑定枚举 | 用途 |
|---|---|---|---|
| `--cf-node-manual` | `#1D4ED8` | `type='manual'` | 人工节点：序号圈边/左竖条/类型徽标文字 |
| `--cf-node-auto` | `#7C3AED` | `type='auto'`(card) | 自动节点 |
| `--cf-node-batch` | `#059669` | `processingGranularity='batch'` | 批次级自动节点 |

浅底（类型徽标背景 `#eff6ff`/`#f5f3ff`/`#ecfdf5`、添加按钮 hover 底）以 `color-mix(in srgb, var(--cf-node-X) N%, transparent)` 派生（沿用上一轮 color-mix 惯例），N 取值实现时对照原色微调（原浅底约 6-8% 调）。

> **batch 绿留存说明**：上一轮「去绿」针对的是绿青**伪 primary chrome**；此处绿是 batch 枚举的**数据编码分类色**（与 manual蓝/auto紫 并列三态区分），性质同被豁免的 SVG 图论色，故保留绿调、仅收为令牌。实现后请于 live 复看，若与橙化 chrome 并置突兀再议。

### ② 字段类型（SchemaFieldEditor，12 基色，原 `tone` 直迁）

| 令牌 | 值 | | 令牌 | 值 |
|---|---|---|---|---|
| `--cf-field-text` | `#1F2937` | | `--cf-field-account` | `#0F766E` |
| `--cf-field-money` | `#B45309` | | `--cf-field-auxiliary` | `#4F46E5` |
| `--cf-field-enum` | `#7C3AED` | | `--cf-field-bankAccount` | `#0369A1` |
| `--cf-field-date` | `#0891B2` | | `--cf-field-voucherRef` | `#9333EA` |
| `--cf-field-file` | `#475569` | | `--cf-field-org` | `#2563EB` |
| `--cf-field-user` | `#16A34A` | | `--cf-field-cardRef` | `#DB2777` |

`tone` 仅作图标色 / 标签边+字色（`:285`/`:295`/`:331`/`:379`），**无 `-light` 变体需求**——12 基色即可。改 `FIELD_TYPES[].tone` 由 `'#xxxxxx'` → `'var(--cf-field-xxx)'`。

## 4. 三分映射（引用上一轮 §5，不重述）

中性/语义两轴**完全复用上一轮 §5 映射表**，本轮逐文件落点：

### CardComponentRenderer.vue（橙化 + 去绿）
- **橙化**：`#1f6f5f`→`var(--color-primary)`（勾选/单选选中填充 `:702-703`、`:658-659` checked 态）；`#3d7d5c` 聚焦边→`var(--color-primary)`；`rgba(31,111,95,.18)` 斜纹 `:776`、`rgba(61,125,92,.12)` 聚焦环 `:503` → `color-mix(in srgb, var(--color-primary) N%, transparent)` 对应档（或 `--color-primary-border`）。
- **去绿中性**：`#23322c`/`#1f3029`→`--text-1`；`#637068`/`#7a8781`/`#5f6f67`/`#314139`/`#2f3f38`/`#53645c`/`#7c8781`→`--text-2`/`--text-3`（按对比度分桶）；`#eef1ef`/`#d9dfdc`/`#dfe7e3`/`#e3eae6`/`#dfe9e4`/`#cfdcd6`/`#d6dcd8`→`--border`；`#91a49b`（勾选框边）→`--border-strong`；`#f8fbfa`/`#fbfcfb`/`#fbfdfc`→`--bg-muted`/`--bg-page`；`#fff`→`--bg-card`；`#d1d5db`→`--text-3`。
- **语义**：`#b23b3b`→`--color-danger`/`-text`；`#eef4ff`/`#1d4ed8`(`:851-852`)→`--color-info-light`/`--color-info`。

### SchemaFieldEditor.vue
- **分类**：12 `tone`→`var(--cf-field-*)`（§3②）。
- **中性**：`#111827`/`#1f2937`→`--text-1`；`#4b5563`/`#6b7280`→`--text-2`；`#9ca3af`/`#d1d5db`/`#c4c4c4`→`--text-3`；`#e6e6e6`/`#ececec`/`#efefef`/`#eee`→`--border`；`#fafafa`/`#f8f8f8`/`#f3f4f6`→`--bg-muted`/`--bg-page`；`#fff`→`--bg-card`；`linear-gradient(180deg,#fafafa,#fff)`→令牌化两端。
- **语义**：`#ef4444`→`--color-danger`；`#fef2f2`→`--color-danger-light`；`#fecaca`→`--color-danger-border`；`rgba(17,24,39,.06/.08)`→`--shadow-sm`/`-md`。

### StageDefinitionEditor.vue
- **分类**：`#1d4ed8`/`#7c3aed`/`#059669` + 浅底→`var(--cf-node-*)` + color-mix（§3①），覆盖 `.sde-line__dot--*`/`.sde-node--*`/`.sde-editor__type-badge--*`/`.sde__add--*`。
- **健康态语义**：ok（`#047857`/`#ecfdf5`/`#bbf7d0`/`#f0fdf4`）→`--color-success-text`/`-light`/边 color-mix；warning（`#b45309`/`#92400e`/`#fffbeb`/`#fde68a`）→`--color-warning-*`；error（`#b91c1c`/`#991b1b`/`#fef2f2`/`#fecaca`）→`--color-danger-*`。
- **danger**：`#ef4444`（必填星 `:1579`、删除键 `:1452`）→`--color-danger`；`#fef2f2`→`--color-danger-light`。
- **中性**：`#111827`/`#1f2937`→`--text-1`；`#4b5563`/`#374151`/`#6b7280`→`--text-2`；`#9ca3af`/`#d1d5db`/`#c4c4c4`→`--text-3`；`#e6e6e6`/`#e5e7eb`/`#ececec`/`#edf0f3`/`#efefef`→`--border`；`#d8dde5`→`--border-strong`；`#fafbfc`/`#fafafa`/`#f8fafc`/`#f3f4f6`→`--bg-muted`/`-page`；`#fff`→`--bg-card`；网格虚线 `#d1d5db`→`--border`/`--text-3`。

### StageInputFields.vue / ConditionBuilder.vue
- 纯中性：`#fafafa`/`#f5f5f5`→`--bg-muted`；`#e8e8e8`→`--border`；`#595959`→`--text-2`；`#999`→`--text-3`。

### SchemaRenderer.vue
- 中性：`#f0f0f0`/`#eee`→`--border`；`#333`→`--text-1`；`#666`→`--text-2`；`#999`/`#bbb`→`--text-3`。
- **价格红**：内联 `#ee0a24`（¥ 强调 `:437`）→`var(--color-danger)`（保留「金额醒目」的红调；近源红，最小观感差）。

## 5. 验证口径（同上一轮 §8）

- **改动文件 `rg -n --pcre2 '#[0-9a-fA-F]{3,8}\b|rgba?\(' <file>` 色彩清零**——含 inline `style` / JS `tone` 字符串（stylelint 盲区，本轮真元凶）。
- **新令牌定义处例外**：`theme.ts` 注入行、`TOKENS.md` 表格、`variables.scss`（若加桥接）按上一轮 stylelint 白名单豁免（这些是令牌真源，本就允许字面 hex）。
- **`npx stylelint "<file>"` 0 报错**（6 组件文件）。
- **`cd web && npx vite build` 绿**（`vue-tsc` 基线红可过滤，见 [[frontend-typecheck-baseline-red]]）。
- **纯前端无后端改动**：无 dotnet/单测；无前端测试运行器、不引入框架。
- **运行时变色自检**：控制台 `setProperty('--color-primary','#0000ff')` 后 CardComponentRenderer 选中态应变蓝（橙化生效证据）；`--cf-node-*`/`--cf-field-*` 为静态常量不随主色变（符合分类色定位）。
- **用户 9001 live HMR 逐组件复看**：CardComponentRenderer（勾选/单选/聚焦橙化、绿染中性已去）、SchemaFieldEditor（12 字段类型色保真）、StageDefinitionEditor（节点三态色 + 健康态 + batch 绿留存观感）。

## 6. 明确不做

- 不动上一轮已收敛的 10 文件。
- 不动 `--biz-*` / 既有语义令牌定义（含 theme.ts `--biz-approval=#5B7290` 与 TOKENS.md `#3A6FB0` 的既存口径差异——属另一观察项，非本轮范围）。
- 不重做上一轮 §5 三分映射表（本轮只引用 + 逐文件落点）。
- 不做结构性重构（组件拆分/外壳迁移/共享组件 swap）——本轮纯颜色令牌化。

---

> 下一步：本 spec 经用户复审后，进入 `writing-plans` 产出分阶段实现计划（新令牌 → CardComponentRenderer 橙化去绿 → 两个分类色组件 → 三个纯中性小件 → 复验）。
