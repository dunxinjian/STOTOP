## 阶段6：移动端（oa-mobile / vant）令牌对齐

> 前置：阶段0（令牌单一真源 + `:root` 注入完整令牌集 + antd `ConfigProvider cssVar:true`）与阶段1（去蓝/拆紫桌面端）已完成。本阶段只做移动端 vant 桥接与 oa-mobile 硬编码色令牌化，**不**改业务逻辑。
>
> 现状关键事实（已核对真实代码）：
> - vant 版本 `^4.9.24`（Vant 4 用 `--van-*` CSS 变量做主题，无需 SCSS 编译期变量覆盖）。
> - oa-mobile 表单（`web/src/views/oa-mobile/approve/forms/*`）经 `web/src/forms/index.ts` 注册，由 `web/src/components/workflow/FormRenderer.vue` 在**主 antd 应用内**动态渲染——因此桥接文件必须被主应用 `styles/index.scss` 引入。
> - 另有独立移动端 app `web/src/mobile/main.ts`（挂 `#mobile-app`，`import 'vant/lib/index.css'`），也需引入同一桥接。
> - 真实硬编码色样本：主蓝 `#4d8cf7`/`#6fa3fb`（渐变头/激活下划线/链接/节点点，13 处跨 5 文件）、vant 绿 `#07c160`、红 `#ee0a24`/`#f56c6c`、中性 `#323233`/`#969799`/`#c8c9cc`/`#646566`/`#f7f8fa`。
> - `MobileTimeline.vue` 有写死 `active-color="#07c160"`。
>
> 范围说明：`web/src/mobile/**` 各页与 `views/cardflow-mobile/**` 内仍有大量硬编码色，属设计 spec §5.5「逐组件像素级精修可留后续」。本阶段对这两处只做"接桥让主色/状态自动随令牌变色"，逐页精修不在本阶段。

---

### Task 1: 建立 vant 变量桥 vant-bridge.scss

把 vant 的 `--van-*` 全部映射到统一令牌，使所有 vant 组件（按钮/标签/步骤/单元格/字段/弹层）自动吃 `--color-*` 单一真源。Vant 4 的变量是 CSS 自定义属性，定义在 `:root` 即可全局覆盖（无 `@use` 编译期依赖）。

**Files:**
- Create `web/src/styles/vant-bridge.scss`（新文件，约 60 行）

- [ ] **Step 1: 确认 vant 默认变量名**。Vant 4 默认在 `.van-theme-light`/`:root` 暴露：`--van-primary-color`（默认 `#1989fa` 蓝）、`--van-success-color #07c160`、`--van-danger-color #ee0a24`、`--van-warning-color #ff976a`、`--van-text-color #323233`、`--van-text-color-2 #969799`、`--van-text-color-3 #c8c9cc`、`--van-background #f7f8fa`、`--van-background-2 #fff`、`--van-border-color #ebedf0`、`--van-border-radius-md 6px`、`--van-border-radius-lg 8px`。这些就是要在桥里被令牌覆盖的目标键（无需读文件，属 vant 公开约定）。
- [ ] **Step 2: 写主色/状态映射块**。新建文件，写入 `:root` 块的主色与状态部分：

```scss
/**
 * Vant 变量桥
 * 把 vant 的 --van-* CSS 变量映射到统一令牌（stores/theme.ts 注入到 :root 的 --color-* 等）。
 * 目的：移动端 vant 组件与桌面端 antd 吃同一套单一真源，改 --color-primary 即全局变色。
 * 仅做令牌桥接，不做逐组件像素级精修（见 spec §5.5）。
 */
:root {
  /* 主色 */
  --van-primary-color: var(--color-primary);
  --van-blue: var(--color-primary);
  --van-orange: var(--color-primary);
  /* 状态色 */
  --van-success-color: var(--color-success);
  --van-green: var(--color-success);
  --van-danger-color: var(--color-danger);
  --van-red: var(--color-danger);
  --van-warning-color: var(--color-warning);
}
```

- [ ] **Step 3: 续写文字/表面/圆角映射块**。在同一 `:root` 内追加：

```scss
  /* 文字 */
  --van-text-color: var(--text-1);
  --van-text-color-2: var(--text-2);
  --van-text-color-3: var(--text-3);
  /* 表面 */
  --van-background: var(--bg-page);
  --van-background-2: var(--bg-card);
  --van-border-color: var(--border);
  /* 圆角 */
  --van-border-radius-md: var(--radius-md);
  --van-border-radius-lg: var(--radius-lg);
```

- [ ] **Step 4: 补 Steps 激活态选择器**。`van-step` 的激活圆点/连线默认吃 `--van-primary-color`，桥接后已自动变色；但 `MobileTimeline.vue` 用 `active-color="#07c160"` 覆盖了它（Task 4 改），此处无需额外选择器。仅追加一条兜底，确保 `van-button--success/--danger` 文字对比足够（vant 已用 white，留空注释说明）：

```scss
/* van 按钮 success/danger 走桥接背景 + 内置白字，对比满足 AA，无需额外覆盖 */
```

- [ ] **Step 5: 确认无语法错误**。桥文件仅 `:root{}` + 注释，不依赖任何 `@use`/变量，独立可编译。本步不单独构建，留 Task 8 统一 `npm run build` 验证。
- [ ] **Step 6: 提交**。`git add web/src/styles/vant-bridge.scss && git commit`，message：`feat(mobile): 新增 vant 变量桥 vant-bridge.scss（--van-*→统一令牌）`，末行 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`。

---

### Task 2: 两个入口引入桥接

让主 antd 应用与独立移动端 app 都加载 `vant-bridge.scss`。

**Files:**
- Modify `web/src/styles/index.scss`（第1-5行 `@use` 区）
- Modify `web/src/mobile/main.ts`（第8行后）

- [ ] **Step 1: 主应用入口接桥**。读取确认 `web/src/styles/index.scss` 头部当前为：

```scss
@use './variables.scss' as *;
@use './ant-override.scss';
@use './button-styles.scss';
@use './spacing.scss';
@use './layout.scss';
```

改为在末尾追加一行（桥放最后，保证覆盖 vant 默认）：

```scss
@use './variables.scss' as *;
@use './ant-override.scss';
@use './button-styles.scss';
@use './spacing.scss';
@use './layout.scss';
@use './vant-bridge.scss';
```

注意：主应用 `main.ts` 未全局 `import 'vant/lib/index.css'`，vant 样式由各 oa-mobile 组件 `import 'vant/es/.../style'` 局部引入；桥是 `:root` 变量定义，先于组件样式存在即生效，顺序安全。

- [ ] **Step 2: 独立移动端入口接桥**。读取确认 `web/src/mobile/main.ts` 第7-8行当前为：

```ts
// Vant 全局样式
import 'vant/lib/index.css'
```

改为：

```ts
// Vant 全局样式
import 'vant/lib/index.css'
// Vant 变量桥：映射到统一令牌
import '@/styles/vant-bridge.scss'
```

- [ ] **Step 3: 提交**。`git add web/src/styles/index.scss web/src/mobile/main.ts && git commit`，message：`feat(mobile): 主应用与独立移动端入口引入 vant 变量桥`，末行加 Co-Authored-By。

---

### Task 3: MobileApproveBar 中性色令牌化

审批操作条的 success/danger 按钮已随 Task 1 桥自动变色，仅需令牌化弹层标题文本色。

**Files:**
- Modify `web/src/views/oa-mobile/approve/components/MobileApproveBar.vue`（第93行）

- [ ] **Step 1: 读确认现状**。第88-94行 `.popup-title` 当前：

```css
.popup-title {
  font-size: 16px;
  font-weight: 600;
  text-align: center;
  margin-bottom: 16px;
  color: #323233;
}
```

- [ ] **Step 2: 令牌化标题色与字号**。改为：

```css
.popup-title {
  font-size: var(--font-lg);
  font-weight: 600;
  text-align: center;
  margin-bottom: var(--space-lg);
  color: var(--text-1);
}
```

- [ ] **Step 3: 提交**。`git add web/src/views/oa-mobile/approve/components/MobileApproveBar.vue && git commit -m "refactor(mobile): MobileApproveBar 弹层标题改用令牌色"`，末行加 Co-Authored-By。

---

### Task 4: MobileTimeline 状态色与中性色令牌化

时间线的激活绿、节点文本/底色全部接令牌。

**Files:**
- Modify `web/src/views/oa-mobile/approve/components/MobileTimeline.vue`（第55行模板 + 第82-103行 style）

- [ ] **Step 1: 激活色绑令牌**。第55行当前：

```html
<VanSteps direction="vertical" :active="currentStep" active-color="#07c160">
```

`active-color` 是运行时 prop，CSS `var()` 不能直接作为属性字面量；用 `:style` 思路不行，改为绑定一个从 CSS 变量读出的常量。最稳妥：删掉 `active-color`，让 vant 步骤吃桥接后的 `--van-primary-color`（即主色橙）——但语义上"已完成"应为成功绿。改为显式绑定 success 令牌：在 `<script setup>` 顶部已 import 的基础上新增读取：

```ts
const activeColor = 'var(--color-success)'
```

第55行改为：

```html
<VanSteps direction="vertical" :active="currentStep" :active-color="activeColor">
```

（vant `active-color` 接受任意 CSS 颜色字符串，`var(--color-success)` 经内联 style 注入到 `.van-step` 上，会解析到 `:root` 的 `--color-success #2BA471`。）

- [ ] **Step 2: 令牌化 step 标题色**。第82-86行 `.step-title` 当前 `color: #323233;` → 改为 `color: var(--text-1);`，并把 `font-size: 14px;` → `font-size: var(--font-base);`。
- [ ] **Step 3: 令牌化 meta/time 辅助色**。第87-94行：`.step-meta { color: #969799; font-size:12px }` → `color: var(--text-3); font-size: var(--font-sm);`；`.step-time { color: #c8c9cc; }` → `color: var(--text-disabled);`。
- [ ] **Step 4: 令牌化 comment 块**。第95-103行 `.step-comment`：`color: #646566;` → `color: var(--text-2);`，`font-size:13px` → `var(--font-sm2)`，`background: #f7f8fa;` → `background: var(--bg-muted);`，`border-radius: 4px;` → `var(--radius-sm)`。
- [ ] **Step 5: 提交**。`git add web/src/views/oa-mobile/approve/components/MobileTimeline.vue && git commit -m "refactor(mobile): MobileTimeline 激活色/中性色改用令牌"`，末行加 Co-Authored-By。

---

### Task 5: 四个大表单去蓝（主色渐变/激活下划线/链接/节点）

`MExpenseRequestForm`、`MExpenseReimburseForm`、`MExternalPaymentForm`、`MPettyCashReimburseForm` 共享同一套硬编码蓝 `#4d8cf7`/`#6fa3fb`。逐文件按语义改令牌（渐变头=主色品牌带，下划线/链接/节点=交互主色）。

**Files:**
- Modify `web/src/views/oa-mobile/approve/forms/MExpenseRequestForm.vue`（第436、461、611、625、484、671行附近 + .section-card 圆角第416行）
- Modify `web/src/views/oa-mobile/approve/forms/MExpenseReimburseForm.vue`（第363、430、478、509、645、659行附近）
- Modify `web/src/views/oa-mobile/approve/forms/MExternalPaymentForm.vue`（第199、450、478、509行附近）
- Modify `web/src/views/oa-mobile/approve/forms/MPettyCashReimburseForm.vue`（第186、370、398、429行附近）

- [ ] **Step 1: MExpenseRequestForm 总额卡片渐变**。第435-439行当前：

```css
.total-amount-card {
  background: linear-gradient(135deg, #4d8cf7 0%, #6fa3fb 100%);
  border-radius: 10px;
  padding: 16px 20px;
}
```

改为（蓝品牌带→橙主色带；圆角令牌）：

```css
.total-amount-card {
  background: linear-gradient(135deg, var(--color-primary) 0%, var(--color-primary-hover) 100%);
  border-radius: var(--radius-lg);
  padding: 16px 20px;
}
```

- [ ] **Step 2: MExpenseRequestForm 下划线/链接/节点/危险色**。第461行 `.detail-tab-text` `border-bottom: 2px solid #4d8cf7;` → `border-bottom: 2px solid var(--color-primary);`；第611行 `.process-link color: #4d8cf7;` → `color: var(--color-primary);`；第625行 `.node-dot background: #4d8cf7;` → `background: var(--color-primary);`；第484行 `.required-dot background: #f56c6c;` → `background: var(--color-danger);`；第671行 `color: #f56c6c;` → `color: var(--color-danger);`。`.section-card` 第417行 `border-radius: 12px;` → `border-radius: var(--radius-lg);`。
- [ ] **Step 3: MExpenseReimburseForm 渐变 + 下划线 + 链接 + 节点**。第430行 `.total-amount-card background: linear-gradient(135deg, #4d8cf7 0%, #6fa3fb 100%);` → `linear-gradient(135deg, var(--color-primary) 0%, var(--color-primary-hover) 100%)`，紧邻 `border-radius: 10px` → `var(--radius-lg)`；第478行 `border-bottom: 2px solid #4d8cf7;` → `var(--color-primary)`；第645行 `.process-link color: #4d8cf7;` → `var(--color-primary)`；第659行 `.node-dot background: #4d8cf7;` → `var(--color-primary)`。
- [ ] **Step 4: MExpenseReimburseForm 标签/危险色**。第363行模板 `<VanTag color="#07c160" ...>` → `<VanTag color="var(--color-success)" ...>`（cc-tag 头像底色=成功绿）；第181行模板 `<VanIcon name="cross" size="16" color="#f56c6c" />` → `color="var(--color-danger)"`；第509行 `.del-badge background: #f56c6c;` → `background: var(--color-danger);`。
- [ ] **Step 5: MExternalPaymentForm 去蓝**。第450行 `.total-amount-card background: linear-gradient(135deg, #4d8cf7 0%, #6fa3fb 100%);` → 主色渐变；同块圆角 → `var(--radius-lg)`；第478行 `border-bottom: 2px solid #4d8cf7;` → `var(--color-primary)`；第199行模板 `<VanIcon name="cross" size="16" color="#f56c6c" />` → `color="var(--color-danger)"`；第509行 `.del-badge background: #f56c6c;` → `var(--color-danger)`。
- [ ] **Step 6: MPettyCashReimburseForm 去蓝**。第370行 `.total-amount-card` 渐变 → 主色渐变 + 圆角令牌；第398行 `border-bottom: 2px solid #4d8cf7;` → `var(--color-primary)`；第186行模板 `color="#f56c6c"` → `color="var(--color-danger)"`；第429行 `.del-badge background: #f56c6c;` → `var(--color-danger)`。
- [ ] **Step 7: 提交**。`git add web/src/views/oa-mobile/approve/forms/MExpenseRequestForm.vue web/src/views/oa-mobile/approve/forms/MExpenseReimburseForm.vue web/src/views/oa-mobile/approve/forms/MExternalPaymentForm.vue web/src/views/oa-mobile/approve/forms/MPettyCashReimburseForm.vue && git commit -m "refactor(mobile): 四个大表单去蓝改主色/危险色令牌"`，末行加 Co-Authored-By。

---

### Task 6: 小表单与冲销表单危险色/成功色令牌化

5 个小表单只用 `#ee0a24`（金额负值红）；冲销表单另有内联 `'#07c160'/'#ee0a24'` 与一处渐变。

**Files:**
- Modify `web/src/views/oa-mobile/approve/forms/MPettyCashWriteOffForm.vue`（第133、230行）
- Modify `web/src/views/oa-mobile/approve/forms/MPettyCashApplyForm.vue`（第182行）
- Modify `web/src/views/oa-mobile/approve/forms/MPettyCashReturnForm.vue`（第135行）
- Modify `web/src/views/oa-mobile/approve/forms/MSalaryAdvanceForm.vue`（第177行）
- Modify `web/src/views/oa-mobile/approve/forms/MLoanApplyForm.vue`（第182行）

- [ ] **Step 1: WriteOff 内联差额色**。第133行模板当前：

```html
<div class="field-value amount-value" :style="{ color: Number(formData.difference) >= 0 ? '#07c160' : '#ee0a24' }">
```

改为：

```html
<div class="field-value amount-value" :style="{ color: Number(formData.difference) >= 0 ? 'var(--color-success)' : 'var(--color-danger)' }">
```

- [ ] **Step 2: WriteOff 渐变头**。第230行 `.xxx background: linear-gradient(135deg, #4d8cf7 0%, #6fa3fb 100%);` → `linear-gradient(135deg, var(--color-primary) 0%, var(--color-primary-hover) 100%)`。
- [ ] **Step 3: 四个小表单金额红**。逐文件把 `.amount`/`.amount-highlight .amount` 的 `color: #ee0a24;` → `color: var(--color-danger);`：`MPettyCashApplyForm.vue` 第182行、`MPettyCashReturnForm.vue` 第135行、`MSalaryAdvanceForm.vue` 第177行、`MLoanApplyForm.vue` 第182行。
- [ ] **Step 4: 提交**。`git add web/src/views/oa-mobile/approve/forms/MPettyCashWriteOffForm.vue web/src/views/oa-mobile/approve/forms/MPettyCashApplyForm.vue web/src/views/oa-mobile/approve/forms/MPettyCashReturnForm.vue web/src/views/oa-mobile/approve/forms/MSalaryAdvanceForm.vue web/src/views/oa-mobile/approve/forms/MLoanApplyForm.vue && git commit -m "refactor(mobile): 小表单金额红/差额色改用令牌"`，末行加 Co-Authored-By。

---

### Task 7: 静态断言去蓝/去硬编码绿红

用 ripgrep 确认 oa-mobile 内目标硬编码色已清零（桥定义文件除外）。

**Files:**（无文件改动，纯验证）

- [ ] **Step 1: 去蓝断言**。运行 `rg -n "#4d8cf7|#6fa3fb" web/src/views/oa-mobile`，期望输出为空（0 命中）。
- [ ] **Step 2: 去硬编码状态色断言**。运行 `rg -n "#07c160|#ee0a24|#f56c6c" web/src/views/oa-mobile`，期望 0 命中。
- [ ] **Step 3: vant 蓝兜底断言**。运行 `rg -n "#1989fa|#1677ff" web/src/views/oa-mobile ; rg -n "#1989fa" web/src/styles/vant-bridge.scss`，两条均期望 0 命中（桥用 `var()` 不写裸 hex）。
- [ ] **Step 4: 桥变量存在断言**。运行 `rg -n "--van-primary-color: var\(--color-primary\)" web/src/styles/vant-bridge.scss`，期望恰好 1 命中。
- [ ] **Step 5: 若有残留**。针对命中行回到对应 Task 修复后重跑；全清后无需提交（本 Task 无改动）。

---

### Task 8: 构建 + 类型基线 + 窄屏 preview 截图验证

确认改动可编译、未新增类型报错，并用窄屏 preview 逐屏目视主色/状态/表面已对齐。

**Files:**（无文件改动，纯验证）

- [ ] **Step 1: 构建**。运行 `cd web; npm run build`，期望 vite build 成功（exit 0、产出 `dist/`）；若 SCSS 报 `Undefined variable` 说明令牌名拼错或桥未被引入，回 Task 1/2 修。
- [ ] **Step 2: 类型基线**。运行 `cd web; npm run type-check`，与改动前对比：本就基线红不作门禁，仅确认 oa-mobile 表单文件未新增报错行（`:active-color` 绑定字符串、`color="var(...)"` 均为合法 string prop，不应新增 TS 错）。
- [ ] **Step 3: 启动 preview**。用 preview 工具 `preview_start` 拉起 web 开发服。
- [ ] **Step 4: 切窄屏**。`preview_resize` 设为移动端宽度 390×844（iPhone 视口），导航到独立移动端 `/m/home` 一屏，确认 tabbar 选中色、按钮、链接为橙主色（非 vant 默认蓝），无残留蓝。
- [ ] **Step 5: oa-mobile 表单截图**。导航到承载 `FormRenderer` 的 OA 审批移动表单页（如费用报销 view 模式），`preview_screenshot` 截图，目视核对：总额卡片为橙渐变、激活下划线/链接/节点为橙、必填点/负金额为 `#E5484D` 红、时间线"已完成"为 `#2BA471` 绿、卡片底为 `--bg-page`/`--bg-muted`。
- [ ] **Step 6: 运行时令牌联动验证**。在 preview 控制台执行 `document.documentElement.style.setProperty('--color-primary', '#1E90FF')`，目视移动端按钮/链接/激活/总额卡片应同步变蓝（证明桥真正吃单一真源）；验证后执行 `document.documentElement.style.removeProperty('--color-primary')` 复原。
- [ ] **Step 7: 收尾提交（如有验证期微调）**。若截图暴露遗漏硬编码（如某图标 `color="#999"` 影响观感，可令牌化为 `var(--text-3)`），就地修该文件后 `git add <file> && git commit -m "refactor(mobile): preview 复核补令牌化中性图标色"`，末行加 Co-Authored-By；无遗漏则本 Task 不产生 commit。

> 留后续（非本阶段）：`web/src/mobile/**`（Home/Dashboard/Mine/各 Report 等）与 `views/cardflow-mobile/**` 的逐页硬编码中性色与图标色精修；这些页面已通过 Task 1/2 的桥获得主色/状态自动变色，像素级精修按 spec §5.5 排入后续阶段。