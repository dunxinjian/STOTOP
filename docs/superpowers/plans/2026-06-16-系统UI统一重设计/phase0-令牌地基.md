## 阶段0：配色/主题令牌单一真源 + CSS 变量桥

> 本阶段是其它所有阶段的前置。**不做** 221 处具体替换（阶段1）。仅建立令牌真源、桥接、删除死文件、加 stylelint 门禁、写文档。
> 验证：ripgrep 静态断言（PowerShell 语法，`;` 串联、`$null`）；`cd web; npm run build` 须过；`npm run type-check` 基线红不作门禁仅确认无新增；可视化用 preview_start/preview_screenshot 逐屏；运行时令牌验证=改 --color-primary 看全局变色。每个 Task 末尾 `git add` + `git commit`（中文 message，结尾带 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`）。

---

### Task 1: 删除死文件 web/src/config/theme.ts 并验证零引用

**Files:**
- Delete: `web/src/config/theme.ts`（全 30 行）

**说明**：该文件导出 `antThemeConfig`（colorPrimary `#409eff` 等），与权威令牌冲突且零外部引用（rg 仅命中其自身定义行）。它是历史遗留（Element/AntD 旧主色），必须删。

- [ ] **Step 1: 确认零外部 import**
  运行：
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "config/theme|antThemeConfig" src
  ```
  期望输出（仅自身定义，无 import 语句）：
  ```
  src\config\theme.ts:3:export const antThemeConfig: ThemeConfig = {
  ```
  若出现任何 `import ... from '@/config/theme'` 行，停止并先处理该引用。

- [ ] **Step 2: 删除文件**
  删除 `web/src/config/theme.ts`。若 `web/src/config/` 目录因此变空，保留空目录不动（不在本阶段处理目录清理）。

- [ ] **Step 3: 复验零命中 + 构建**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "antThemeConfig" src; npm run build
  ```
  期望：`rg` 零命中（无输出）；`vite build` 退出码 0，末尾出现 `✓ built in`。

- [ ] **Step 4: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/src/config/theme.ts; git commit -m @'
chore(theme): 删除死文件 config/theme.ts（零引用、与权威令牌冲突）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

### Task 2: stores/theme.ts 默认状态色改为方案权威值

**Files:**
- Modify: `web/src/stores/theme.ts:52-78`（`defaultThemeConfig` 对象）

**说明**：把运行时权威默认值改成方案值。仅改字面量，不改结构。

- [ ] **Step 1: 读取并改 colorSuccess/colorError/colorInfo**
  当前（`web/src/stores/theme.ts:54-57`）：
  ```ts
  colorSuccess: '#52C41A',
  colorWarning: '#E6A700',
  colorError: '#FF4D4F',
  colorInfo: '#13C2C2',
  ```
  目标：
  ```ts
  colorSuccess: '#2BA471',
  colorWarning: '#E6A700',
  colorError: '#E5484D',
  colorInfo: '#3A6FB0',
  ```
  （warning 值不变，仍 `#E6A700`，与权威一致。）

- [ ] **Step 2: 改 sidebarBgColor / sidebarActiveBgColor**
  当前（`web/src/stores/theme.ts:75-76`）：
  ```ts
  sidebarBgColor: '#e4e7ef',
  sidebarActiveBgColor: 'rgba(255, 103, 0, 0.06)',
  ```
  目标（对齐 `--sidebar-bg #EDEEF1` 与 `--sidebar-item-active-bg = --color-primary-light #FFF3EA`）：
  ```ts
  sidebarBgColor: '#EDEEF1',
  sidebarActiveBgColor: '#FFF3EA',
  ```

- [ ] **Step 3: 改 colorPrimary 默认**
  当前（`web/src/stores/theme.ts:53`）：
  ```ts
  colorPrimary: '#FF6700',
  ```
  目标（权威主色 `--color-primary #E85E00`；`#FF6700` 改为 hover 态语义）：
  ```ts
  colorPrimary: '#E85E00',
  ```

- [ ] **Step 4: 静态断言旧值已清除**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "#52C41A|#FF4D4F|#13C2C2|#e4e7ef" src/stores/theme.ts
  ```
  期望：零命中（无输出）。

- [ ] **Step 5: 构建验证**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run build
  ```
  期望：退出码 0，`✓ built in`。

- [ ] **Step 6: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/src/stores/theme.ts; git commit -m @'
feat(theme): 默认状态色改为方案权威值（success #2BA471 / danger #E5484D / info 蓝 #3A6FB0）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

### Task 3: stores/theme.ts 新增 applyDesignTokensCSS 注入完整令牌集到 :root

**Files:**
- Modify: `web/src/stores/theme.ts:216-239`（在 `applySidebarCSS` 之后、其 watch 之后新增函数 + watch）
- Modify: `web/src/stores/theme.ts:241-252`（return 块追加 `applyDesignTokensCSS`）

**说明**：复用现有 `applySidebarCSS`（`web/src/stores/theme.ts:217-224`）的 `document.documentElement.style.setProperty` 模式。静态令牌（中性/状态浅底/业务色/圆角/阴影/字号/间距）为常量；动态令牌（主色、各状态主色）由 `themeConfig.value` 派生。

- [ ] **Step 1: 在 applySidebarCSS 的 watch（结束于 :239）之后插入 applyDesignTokensCSS**
  新增（紧接 `web/src/stores/theme.ts:239` 之后）：
  ```ts
  /** 动态注入完整设计令牌集到 :root（静态令牌为常量，动态主色/状态色由 themeConfig 派生） */
  function applyDesignTokensCSS() {
    const s = document.documentElement.style
    const c = themeConfig.value
    // —— 动态：主色（派生 hover/active/light/border 由 antd 算法吃 colorPrimary，这里仅暴露权威色阶常量）
    s.setProperty('--color-primary', c.colorPrimary || '#E85E00')
    s.setProperty('--color-primary-hover', '#FF6700')
    s.setProperty('--color-primary-active', '#C94E00')
    s.setProperty('--color-primary-light', '#FFF3EA')
    s.setProperty('--color-primary-border', 'rgba(232,94,0,0.30)')
    // —— 动态：状态主色由 themeConfig 派生；浅底/文字为常量
    s.setProperty('--color-success', c.colorSuccess || '#2BA471')
    s.setProperty('--color-success-light', '#E7F5EF')
    s.setProperty('--color-success-text', '#0F6E56')
    s.setProperty('--color-warning', c.colorWarning || '#E6A700')
    s.setProperty('--color-warning-light', '#FBF1D8')
    s.setProperty('--color-warning-text', '#8A6200')
    s.setProperty('--color-danger', c.colorError || '#E5484D')
    s.setProperty('--color-danger-light', '#FCEBEC')
    s.setProperty('--color-danger-text', '#A3282C')
    s.setProperty('--color-info', c.colorInfo || '#3A6FB0')
    s.setProperty('--color-info-light', '#E9F0F8')
    s.setProperty('--color-info-text', '#1C4366')
    // —— 静态：文字
    s.setProperty('--text-1', '#1F2329')
    s.setProperty('--text-2', '#5A6068')
    s.setProperty('--text-3', '#8A9099')
    s.setProperty('--text-disabled', '#BFC3C9')
    // —— 静态：表面/边框
    s.setProperty('--bg-page', '#F5F6F8')
    s.setProperty('--bg-card', '#FFFFFF')
    s.setProperty('--bg-muted', '#EEF0F3')
    s.setProperty('--border', '#E6E8EB')
    s.setProperty('--border-strong', '#D6D9DD')
    // —— 静态：外壳
    s.setProperty('--topbar-ink', '#1F2430')
    s.setProperty('--topbar-ink-admin', '#171A22')
    s.setProperty('--topbar-border', 'rgba(255,255,255,0.10)')
    // 注：--sidebar-bg / --sidebar-item-active-bg 由 applySidebarCSS 按 themeConfig 注入，此处补静态项
    s.setProperty('--sidebar-item-hover', 'rgba(0,0,0,0.05)')
    s.setProperty('--sidebar-item-active-text', 'var(--color-primary)')
    // —— 静态：业务色
    s.setProperty('--biz-waybill', '#6B4FB0')
    s.setProperty('--biz-contract', '#8A6D3B')
    s.setProperty('--biz-quality', '#D9603A')
    s.setProperty('--biz-approval', '#3A6FB0')
    s.setProperty('--biz-points', '#C99A2E')
    s.setProperty('--biz-finance', '#B8860B')
    // —— 静态：圆角
    s.setProperty('--radius-sm', '4px')
    s.setProperty('--radius-md', '6px')
    s.setProperty('--radius-lg', '8px')
    s.setProperty('--radius-modal', '12px')
    s.setProperty('--radius-pill', '999px')
    // —— 静态：阴影
    s.setProperty('--shadow-sm', '0 1px 2px rgba(18,31,53,0.05)')
    s.setProperty('--shadow-md', '0 4px 12px rgba(18,31,53,0.08)')
    s.setProperty('--shadow-lg', '0 8px 24px rgba(18,31,53,0.10)')
    // —— 静态：字号刻度
    s.setProperty('--font-xs', '11px')
    s.setProperty('--font-sm', '12px')
    s.setProperty('--font-sm2', '13px')
    s.setProperty('--font-base', '14px')
    s.setProperty('--font-lg', '16px')
    s.setProperty('--font-xl', '18px')
    s.setProperty('--font-2xl', '24px')
    // —— 静态：间距 4 基数
    s.setProperty('--space-2xs2', '2px')
    s.setProperty('--space-xs4', '4px')
    s.setProperty('--space-sm8', '8px')
    s.setProperty('--space-md12', '12px')
    s.setProperty('--space-lg16', '16px')
    s.setProperty('--space-xl24', '24px')
    s.setProperty('--space-2xl32', '32px')
  }

  // 监听动态主色/状态色变化，实时重注入令牌集（静态项每次一并写入，幂等）
  watch(
    () => [
      themeConfig.value.colorPrimary,
      themeConfig.value.colorSuccess,
      themeConfig.value.colorWarning,
      themeConfig.value.colorError,
      themeConfig.value.colorInfo,
    ],
    () => {
      applyDesignTokensCSS()
    },
    { immediate: true }
  )
  ```

- [ ] **Step 2: 修正 applySidebarCSS 的 active-bg 与权威 light 对齐**
  当前（`web/src/stores/theme.ts:221-223`）：
  ```ts
  style.setProperty('--sidebar-bg', themeConfig.value.sidebarBgColor)
  style.setProperty('--sidebar-active-bg', themeConfig.value.sidebarActiveBgColor)
  style.setProperty('--sidebar-active-indicator', themeConfig.value.colorPrimary || '#FF6700')
  ```
  目标（新增权威名 `--sidebar-item-active-bg`，并把 indicator 回退色改为权威主色；保留旧 `--sidebar-active-bg` 别名避免破坏现有引用）：
  ```ts
  style.setProperty('--sidebar-bg', themeConfig.value.sidebarBgColor)
  style.setProperty('--sidebar-active-bg', themeConfig.value.sidebarActiveBgColor)
  style.setProperty('--sidebar-item-active-bg', themeConfig.value.sidebarActiveBgColor)
  style.setProperty('--sidebar-active-indicator', themeConfig.value.colorPrimary || '#E85E00')
  ```

- [ ] **Step 3: return 块导出新函数**
  当前（`web/src/stores/theme.ts:241-251`）末尾：
  ```ts
    applyTableDensityCSS,
    applyPagePaddingCSS,
    applySidebarCSS,
  }
  ```
  目标：
  ```ts
    applyTableDensityCSS,
    applyPagePaddingCSS,
    applySidebarCSS,
    applyDesignTokensCSS,
  }
  ```

- [ ] **Step 4: 构建 + 类型确认**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run build; npm run type-check
  ```
  期望：`build` 退出码 0；`type-check` 仍是基线红（不计入门禁），但不得新增对 `stores/theme.ts` 的报错（用 `rg "stores/theme.ts" <type-check 输出>` 确认无本文件新报错）。

- [ ] **Step 5: 运行时令牌验证（preview）**
  用 `preview_start` 启动前端，`preview_eval` 执行：
  ```js
  getComputedStyle(document.documentElement).getPropertyValue('--color-primary').trim()
  ```
  期望返回 `#E85E00`。再执行 `document.documentElement.style.setProperty('--color-primary','#0000ff')`，`preview_screenshot` 确认主色相关元素变蓝（验证 :root 注入生效）。

- [ ] **Step 6: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/src/stores/theme.ts; git commit -m @'
feat(theme): 新增 applyDesignTokensCSS 注入完整令牌集到 :root（静态常量 + 动态主色/状态色派生）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

### Task 4: App.vue + antdTheme 加 cssVar，让 antd 组件吃 CSS 变量

**Files:**
- Modify: `web/src/stores/theme.ts:98-134`（`antdTheme` computed 返回对象增加 `cssVar`/`hashed`）

**说明**：AntD Vue 4.x 的 ConfigProvider theme 支持 `cssVar` 与 `hashed:false`，使组件样式以 CSS 变量形式输出，便于与本方案 :root 令牌协同、减少 hash 类。`App.vue:47` 已通过 `:theme="themeStore.antdTheme"` 绑定，故只需改 computed，无需改模板；但需在 App.vue 加一行注释固化契约。

- [ ] **Step 1: antdTheme 返回对象顶层加 cssVar 与 hashed**
  当前（`web/src/stores/theme.ts:98-133`，返回对象顶层字段顺序 `token / components / algorithm`）：
  ```ts
    return {
      token: {
        ...
      },
      components: {
        ...
      },
      algorithm: algorithms,
    }
  ```
  目标（在 `algorithm` 后追加两字段）：
  ```ts
    return {
      token: {
        ...
      },
      components: {
        ...
      },
      algorithm: algorithms,
      cssVar: { prefix: 'sto' },
      hashed: false,
    }
  ```

- [ ] **Step 2: App.vue 固化契约注释**
  当前（`web/src/App.vue:47`）：
  ```vue
    <a-config-provider :locale="zhCN" :theme="themeStore.antdTheme">
  ```
  目标（上方加一行注释，说明 antdTheme 已带 cssVar，组件样式走 CSS 变量）：
  ```vue
    <!-- antdTheme 已启用 cssVar(prefix='sto') 与 hashed:false，组件样式以 CSS 变量输出，与 :root 设计令牌协同 -->
    <a-config-provider :locale="zhCN" :theme="themeStore.antdTheme">
  ```

- [ ] **Step 3: 构建验证**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run build
  ```
  期望：退出码 0，`✓ built in`。

- [ ] **Step 4: preview 验证 antd CSS 变量已输出**
  `preview_start` 后 `preview_eval`：
  ```js
  Array.from(document.documentElement.style).filter(p => p.startsWith('--sto')).length
  ```
  期望返回 > 0（antd 已注入 `--sto*` 前缀变量）。`preview_screenshot` 逐屏确认按钮/表格视觉无塌陷。

- [ ] **Step 5: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/src/stores/theme.ts web/src/App.vue; git commit -m @'
feat(theme): ConfigProvider 启用 cssVar(prefix=sto) + hashed:false，antd 组件改吃 CSS 变量

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

### Task 5: variables.scss 的 $ 字面量改为 var(--token, 同值回退) 桥接

**Files:**
- Modify: `web/src/styles/variables.scss:1-6`（颜色）、`:33-39`（字号）、`:42-49`（间距）、`:52-54`（圆角）、`:57-59`（阴影）、`:74-87`（文字/边框/背景）

**说明**：把编译期 `$` 变量改为引用 `var(--token, 回退值)`。回退值取**权威值**（不是旧值），确保即便 :root 未注入也是新视觉；且与运行时令牌同值。本阶段只改 variables.scss 本身，**不改** 任何消费方（消费方仍写 `$color-primary`，自动透传 var）。

- [ ] **Step 1: 颜色主轨改桥接**
  当前（`web/src/styles/variables.scss:1-6`）：
  ```scss
  $color-primary: #FF6700;       // 主色 - STO品牌橙
  $color-success: #52C41A;       // 绿
  $color-warning: #E6A700;       // 金黄
  $color-danger: #FF4D4F;        // 红
  $color-info: #13C2C2;          // 青色（信息）
  ```
  目标：
  ```scss
  // ===== 颜色变量（桥接到 :root 设计令牌，回退值=权威值）=====
  $color-primary: var(--color-primary, #E85E00);   // 主色 - STO品牌橙
  $color-success: var(--color-success, #2BA471);   // 绿
  $color-warning: var(--color-warning, #E6A700);   // 金黄
  $color-danger:  var(--color-danger, #E5484D);    // 红
  $color-info:    var(--color-info, #3A6FB0);      // 蓝（信息，由青改蓝）
  ```

- [ ] **Step 2: 主色衍生改桥接**
  当前（`web/src/styles/variables.scss:16-19`）：
  ```scss
  $color-primary-light: #FFF1E8; // 浅色背景底色
  $color-primary-hover: #FF8533; // 悬停态
  $color-primary-bg: rgba(255, 103, 0, 0.06); // 极浅底色
  $color-accent: #FF6700;        // 强调色
  ```
  目标：
  ```scss
  $color-primary-light: var(--color-primary-light, #FFF3EA); // 浅色背景底色
  $color-primary-hover: var(--color-primary-hover, #FF6700); // 悬停态
  $color-primary-bg: var(--color-primary-light, #FFF3EA);    // 极浅底色（并入 light）
  $color-accent: var(--color-primary, #E85E00);              // 强调色
  ```

- [ ] **Step 3: 字号刻度改桥接**
  当前（`web/src/styles/variables.scss:33-39`）：
  ```scss
  $font-size-xs: 11px;    // 徽标、角标
  $font-size-sm: 12px;    // 辅助文字、分组标签
  $font-size-sm2: 13px;   // 导航项、紧凑内容
  $font-size-base: 14px;  // 正文基准
  $font-size-lg: 16px;    // 小标题
  $font-size-xl: 18px;    // 页面主标题
  $font-size-2xl: 24px;   // 大标题
  ```
  目标：
  ```scss
  $font-size-xs: var(--font-xs, 11px);    // 徽标、角标
  $font-size-sm: var(--font-sm, 12px);    // 辅助文字、分组标签
  $font-size-sm2: var(--font-sm2, 13px);  // 导航项、紧凑内容
  $font-size-base: var(--font-base, 14px);// 正文基准
  $font-size-lg: var(--font-lg, 16px);    // 小标题
  $font-size-xl: var(--font-xl, 18px);    // 页面主标题
  $font-size-2xl: var(--font-2xl, 24px);  // 大标题
  ```

- [ ] **Step 4: 圆角与阴影改桥接**
  当前（`web/src/styles/variables.scss:52-59`）：
  ```scss
  $border-radius-sm: 4px;
  $border-radius-md: 6px;
  $border-radius-lg: 8px;

  // ===== 阴影（统一柔和系）=====
  $shadow-sm: 0 2px 8px rgba(0,0,0,0.06);
  $shadow-md: 0 4px 12px rgba(0,0,0,0.08);
  $shadow-lg: 0 8px 24px rgba(0,0,0,0.1);
  ```
  目标：
  ```scss
  $border-radius-sm: var(--radius-sm, 4px);
  $border-radius-md: var(--radius-md, 6px);
  $border-radius-lg: var(--radius-lg, 8px);

  // ===== 阴影（统一柔和系，桥接权威阴影令牌）=====
  $shadow-sm: var(--shadow-sm, 0 1px 2px rgba(18,31,53,0.05));
  $shadow-md: var(--shadow-md, 0 4px 12px rgba(18,31,53,0.08));
  $shadow-lg: var(--shadow-lg, 0 8px 24px rgba(18,31,53,0.10));
  ```

- [ ] **Step 5: 文字/边框/背景改桥接**
  当前（`web/src/styles/variables.scss:74-87`）：
  ```scss
  $text-primary: rgba(0,0,0,0.88);
  $text-regular: rgba(0,0,0,0.65);
  $text-secondary: rgba(0,0,0,0.45);
  $text-placeholder: rgba(0,0,0,0.25);

  // ===== 边框颜色（统一分隔线）=====
  $border-color: #E8E8E8;
  $border-color-light: #E8E8E8;
  $border-color-lighter: #F0F0F0;

  // ===== 背景颜色 =====
  $bg-page: #fafafa;
  $bg-card: #ffffff;
  $bg-header: #ffffff;
  ```
  目标：
  ```scss
  $text-primary: var(--text-1, #1F2329);
  $text-regular: var(--text-2, #5A6068);
  $text-secondary: var(--text-3, #8A9099);
  $text-placeholder: var(--text-disabled, #BFC3C9);

  // ===== 边框颜色（统一分隔线，桥接权威边框令牌）=====
  $border-color: var(--border, #E6E8EB);
  $border-color-light: var(--border, #E6E8EB);
  $border-color-lighter: var(--border, #E6E8EB);

  // ===== 背景颜色 =====
  $bg-page: var(--bg-page, #F5F6F8);
  $bg-card: var(--bg-card, #FFFFFF);
  $bg-header: var(--bg-card, #FFFFFF);
  ```

- [ ] **Step 6: 构建验证（SCSS 编译 var() 不报错）**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run build
  ```
  期望：退出码 0；无 `SassError`。注意：本阶段不在 `color.adjust()` 内传入这些 var 变量（button-styles.scss 仍用旧值，由 Task 6 处理白名单），故无 “argument must be a color” 报错。

- [ ] **Step 7: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/src/styles/variables.scss; git commit -m @'
refactor(theme): variables.scss 的 $ 变量改为 var(--token, 权威回退值) 桥接

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

### Task 6: 间距双轨对齐（$spacing-* 与 antd marginXS/SM/.. 同值，4 基数）

**Files:**
- Modify: `web/src/styles/variables.scss:42-49`（`$spacing-*`）
- Modify: `web/src/stores/theme.ts:65-69`（`marginXS/SM/margin/MD/LG` 默认值）

**说明**：现状 antd 轨 `marginXS8/SM12/16/MD20/LG24`，spacing 轨 `2/4/8/16/24/32`。两轨在 16/24 处一致，但 `$spacing-sm=8` vs `marginSM=12`、`$spacing-md=16` vs `margin=16`（一致）存在错位。本任务以 4 基数刻度 `2/4/8/12/16/24/32` 统一两轨，并把 SCSS 间距桥接到 `--space-*` 令牌。`marginMD` 由 20 改 24 会与 marginLG 重叠——保留 antd 自身 MD 语义但对齐到 4 基数（MD=20→保持 20 不动以免破坏 antd 内部刻度，仅文档登记其不在 spacing 轨）。

- [ ] **Step 1: $spacing-* 改桥接到 --space-* 令牌（择 4 基数刻度）**
  当前（`web/src/styles/variables.scss:42-49`）：
  ```scss
  $spacing-2xs: 2px;
  $spacing-xs: 4px;
  $spacing-sm: 8px;
  $spacing-md: 16px;
  $spacing-lg: 24px;
  $spacing-xl: 32px;
  $spacing-3xl: 48px;
  $spacing-4xl: 64px;
  ```
  目标（新增 `$spacing-md12` 桥接 12 基数，原 `$spacing-md` 维持 16 但改桥接；3xl/4xl 无对应令牌保留字面量）：
  ```scss
  // ===== 间距（4 基数刻度，桥接 --space-* 令牌；与 antd marginXS/SM/.. 同值对齐）=====
  $spacing-2xs: var(--space-2xs2, 2px);   // = antd 无
  $spacing-xs: var(--space-xs4, 4px);     // ≈ 4 基数最小
  $spacing-sm: var(--space-sm8, 8px);     // = antd marginXS(8)
  $spacing-md12: var(--space-md12, 12px); // = antd marginSM(12)（新增对齐项）
  $spacing-md: var(--space-lg16, 16px);   // = antd margin(16)
  $spacing-lg: var(--space-xl24, 24px);   // = antd marginLG(24)
  $spacing-xl: var(--space-2xl32, 32px);  // 4 基数 ×8
  $spacing-3xl: 48px;                      // 无令牌，保留
  $spacing-4xl: 64px;                      // 无令牌，保留
  ```

- [ ] **Step 2: stores/theme.ts 确认 antd margin 轨与刻度一致**
  当前（`web/src/stores/theme.ts:65-69`）：
  ```ts
  marginXS: 8,
  marginSM: 12,
  margin: 16,
  marginMD: 20,
  marginLG: 24,
  ```
  目标（marginXS/SM/margin/LG 已对齐 8/12/16/24，无需改；仅在 `marginMD: 20` 行尾加注释登记其不在 spacing 轨，避免误用）：
  ```ts
  marginXS: 8,   // = $spacing-sm  = --space-sm8
  marginSM: 12,  // = $spacing-md12 = --space-md12
  margin: 16,    // = $spacing-md  = --space-lg16
  marginMD: 20,  // antd 内部刻度，不在 spacing 双轨（文档已登记）
  marginLG: 24,  // = $spacing-lg  = --space-xl24
  ```

- [ ] **Step 3: 静态断言 spacing 桥接已生效**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "space-sm8|space-md12|space-xl24" src/styles/variables.scss
  ```
  期望命中 3 行（每个令牌至少 1 次）。

- [ ] **Step 4: 构建验证**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run build
  ```
  期望：退出码 0。注意 `spacing.scss`/`index.scss` 仍引用 `$spacing-md`/`$spacing-lg`/`$spacing-sm`/`$section-gap`，均仍存在（未删名），编译正常。

- [ ] **Step 5: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/src/styles/variables.scss web/src/stores/theme.ts; git commit -m @'
refactor(theme): 间距双轨对齐 4 基数（$spacing-* 桥接 --space-*，与 antd margin 同值）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

### Task 7: 新建 web/docs/TOKENS.md 令牌文档

**Files:**
- Create: `web/docs/TOKENS.md`

**说明**：单一真源文档。列出全部令牌名/值/用途，说明运行时（stores/theme.ts → :root）与编译期（variables.scss → var()）双轨，并给验证法。

- [ ] **Step 1: 写文档骨架与权威令牌表**
  内容（节选结构，需完整填入全部令牌）：
  ```markdown
  # STOTOP 设计令牌（单一真源）

  令牌有两条注入轨道，**值必须一致**：
  - **运行时**：`web/src/stores/theme.ts` 的 `applyDesignTokensCSS()` 把令牌写入 `:root`（动态主色/状态色由 `themeConfig` 派生，其余为静态常量）。
  - **编译期**：`web/src/styles/variables.scss` 的 `$` 变量以 `var(--token, 权威回退值)` 桥接，消费方继续写 `$color-primary` 即自动透传 CSS 变量。

  > 阶段0 只建立真源与桥接，**不替换** 组件内 221 处裸值（阶段1）。

  ## 主色
  | 令牌 | 值 | 用途 |
  |---|---|---|
  | `--color-primary` | `#E85E00` | 品牌主色（动态，随 themeConfig.colorPrimary） |
  | `--color-primary-hover` | `#FF6700` | 主色悬停 |
  | `--color-primary-active` | `#C94E00` | 主色按下 |
  | `--color-primary-light` | `#FFF3EA` | 主色浅底 |
  | `--color-primary-border` | `rgba(232,94,0,0.30)` | 主色描边 |

  ## 状态色（成功/警告/危险/信息，各带 -light/-text）
  | 令牌 | 值 | | 令牌 | 值 | | 令牌 | 值 |
  |---|---|---|---|---|---|---|---|
  | `--color-success` | `#2BA471` | | `--color-success-light` | `#E7F5EF` | | `--color-success-text` | `#0F6E56` |
  | `--color-warning` | `#E6A700` | | `--color-warning-light` | `#FBF1D8` | | `--color-warning-text` | `#8A6200` |
  | `--color-danger` | `#E5484D` | | `--color-danger-light` | `#FCEBEC` | | `--color-danger-text` | `#A3282C` |
  | `--color-info` | `#3A6FB0` | | `--color-info-light` | `#E9F0F8` | | `--color-info-text` | `#1C4366` |

  ## 文字 / 表面 / 边框
  `--text-1 #1F2329` `--text-2 #5A6068` `--text-3 #8A9099` `--text-disabled #BFC3C9`
  `--bg-page #F5F6F8` `--bg-card #FFFFFF` `--bg-muted #EEF0F3` `--border #E6E8EB` `--border-strong #D6D9DD`

  ## 外壳 / 业务色 / 圆角 / 阴影 / 字号 / 间距
  （此处需逐项补全：外壳 6 项、业务色 6 项、圆角 5 项、阴影 3 项、字号 7 项 --font-xs/-sm/-sm2/-base/-lg/-xl/-2xl、间距 7 项 --space-2xs2/-xs4/-sm8/-md12/-lg16/-xl24/-2xl32）

  ## 间距双轨对齐（4 基数）
  | SCSS | CSS 令牌 | antd token | 值 |
  |---|---|---|---|
  | `$spacing-sm` | `--space-sm8` | `marginXS` | 8 |
  | `$spacing-md12` | `--space-md12` | `marginSM` | 12 |
  | `$spacing-md` | `--space-lg16` | `margin` | 16 |
  | `$spacing-lg` | `--space-xl24` | `marginLG` | 24 |
  | — | — | `marginMD` | 20（antd 内部刻度，不在双轨） |

  ## 验证
  - 静态断言（PowerShell + ripgrep）：`cd web; rg -n "#1890ff" src`，令牌文件外应趋零（阶段1 完成后）。
  - 运行时变色：浏览器控制台 `document.documentElement.style.setProperty('--color-primary','#0000ff')`，全局应变蓝。
  ```

- [ ] **Step 2: 校验文档令牌名与权威集逐一对齐**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "--color-primary\b|--biz-finance|--space-2xl32|--shadow-lg" docs/TOKENS.md
  ```
  期望：4 个令牌名均命中（确保关键边界令牌已写入）。

- [ ] **Step 3: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/docs/TOKENS.md; git commit -m @'
docs(theme): 新增 TOKENS.md 记录权威令牌集与运行时/编译期双轨

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

### Task 8: 接入 stylelint，禁止组件 .vue/.scss 裸十六进制色（token 文件白名单）

**Files:**
- Create: `web/.stylelintrc.json`
- Modify: `web/package.json:6-11`（scripts）、`web/package.json:40-53`（devDependencies）

**说明**：本项目无前端测试运行器，**严禁引入测试框架**；stylelint 是 lint 工具非测试框架，允许。规则只禁裸 hex（`color-no-hex`），token 文件（variables.scss、ant-override.scss 局部蓝、layout.scss 外壳色）通过 `overrides` 白名单豁免，避免本阶段误报阻断。安装走 npm（注意：MEMORY 记录禁止擅自安装会改系统状态——本计划仅“描述”命令，由执行者运行）。

- [ ] **Step 1: 新建 .stylelintrc.json**
  内容：
  ```json
  {
    "extends": ["stylelint-config-standard-scss"],
    "rules": {
      "color-no-hex": true,
      "scss/dollar-variable-pattern": null,
      "selector-class-pattern": null,
      "custom-property-pattern": null,
      "scss/at-rule-no-unknown": null,
      "no-descending-specificity": null
    },
    "overrides": [
      {
        "files": [
          "src/styles/variables.scss",
          "src/styles/ant-override.scss",
          "src/styles/layout.scss",
          "src/styles/button-styles.scss",
          "src/views/finance/AmoebaPLTemplate/tokens.scss"
        ],
        "rules": { "color-no-hex": null }
      }
    ]
  }
  ```
  说明：token 与外壳/局部样式文件豁免 `color-no-hex`（这些是阶段1 之后才逐步令牌化的，本阶段不阻断）。`.vue` 文件由 stylelint-config-standard-scss 通过 `<style>` 提取覆盖，无需额外 customSyntax（如执行时报 .vue 不解析，再补 `postcss-html` 与 `overrides[files=**/*.vue].customSyntax`）。

- [ ] **Step 2: package.json scripts 加 lint:style**
  当前（`web/package.json:6-11`）：
  ```json
    "scripts": {
      "dev": "vite",
      "build": "vite build",
      "type-check": "vue-tsc -b",
      "preview": "vite preview"
    },
  ```
  目标：
  ```json
    "scripts": {
      "dev": "vite",
      "build": "vite build",
      "type-check": "vue-tsc -b",
      "preview": "vite preview",
      "lint:style": "stylelint \"src/**/*.{scss,vue}\"",
      "lint:style:fix": "stylelint \"src/**/*.{scss,vue}\" --fix"
    },
  ```

- [ ] **Step 3: package.json devDependencies 加 stylelint 依赖**
  当前（`web/package.json:40-41`）开头：
  ```json
    "devDependencies": {
      "@types/node": "^24.12.0",
  ```
  目标（新增三项，保持字母序大致）：
  ```json
    "devDependencies": {
      "@types/node": "^24.12.0",
      "postcss-html": "^1.7.0",
      "stylelint": "^16.10.0",
      "stylelint-config-standard-scss": "^14.0.0",
  ```

- [ ] **Step 4: 安装并首次运行 lint（执行者运行，非本计划自动）**
  ```powershell
  cd E:\STOTOP_Fable\web; npm install; npm run lint:style
  ```
  期望：安装成功；`lint:style` 报告组件内裸 hex 数量（这是阶段1 待替换的清单，本阶段允许非零，仅确认 token 白名单文件 0 报错）。若 .vue 解析失败，按 Step 1 说明补 `postcss-html` 的 `customSyntax` override 后复跑。

- [ ] **Step 5: 静态确认白名单文件不报色**
  ```powershell
  cd E:\STOTOP_Fable\web; npx stylelint "src/styles/variables.scss" "src/styles/ant-override.scss"
  ```
  期望：退出码 0（白名单豁免 `color-no-hex` 生效，无 color 报错）。

- [ ] **Step 6: 提交**
  ```powershell
  cd E:\STOTOP_Fable; git add web/.stylelintrc.json web/package.json web/package-lock.json; git commit -m @'
chore(lint): 接入 stylelint 禁组件裸 hex 色（token 文件白名单），加 lint:style 脚本

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```

---

## 阶段验收（全部 Task 完成后）

- [ ] **构建**：`cd E:\STOTOP_Fable\web; npm run build` 退出码 0。
- [ ] **死文件**：`rg -n "antThemeConfig" web/src` 零命中。
- [ ] **令牌真源**：`rg -n "applyDesignTokensCSS" web/src/stores/theme.ts` 命中（定义 + watch + return）。
- [ ] **桥接**：`rg -n "var\(--color-primary" web/src/styles/variables.scss` 命中。
- [ ] **运行时变色**：preview 中改 `--color-primary` 全局变色生效。
- [ ] **门禁**：`npx stylelint "web/src/styles/variables.scss"` 退出码 0；`npm run lint:style` 能列出组件内待替换裸 hex（移交阶段1）。
- [ ] **类型**：`npm run type-check` 无新增本阶段文件报错（基线红不计）。
- [ ] **未越界**：本阶段未替换组件内 221 处裸值（仅建真源/桥接/门禁/文档）。