本阶段共 6 个 Task。假定阶段0 的 `:root` 权威令牌层（`--color-primary #E85E00`、`--topbar-ink #1F2430`、`--topbar-ink-admin #171A22`、`--sidebar-bg #EDEEF1` 等）已注入全局且阶段1 已完成。所有外壳样式只允许引用权威令牌名，禁止再出现裸十六进制紫/橙。

验证基线说明（首个改动前先建立）：当前四个外壳文件中的硬编码命中点已核实——`#722ED1` 在 AdminLayout.vue 第 15、123、224 行；`#9254DE` 第 15 行；`#B37FEB` 第 123、162 行；`rgba(114, 46, 209` 第 223 行；`#FF8533` 在 TopBar.vue 第 427 行；`#E85E00` 在 SmartSidebar.vue 第 466、491 行。退役完成后这些命中应清零（令牌定义文件除外）。

---

### Task 1: 后台顶栏退役紫主题——改用 --topbar-ink-admin + 令牌化文案标识

**Files:**
- Modify `web/src/layouts/AdminLayout.vue`（模板 4-20 行；样式 102-175 行）

- [ ] **Step 1: 读取并确认顶栏当前色值**
  确认 `web/src/layouts/AdminLayout.vue` 第 105 行 `background: #434352;`、第 116-125 行 `.admin-topbar::after` 紫色渐变底线、第 160-163 行 `.admin-shield-icon { color: #B37FEB; }`。这三处是后台顶栏的"紫"来源。

- [ ] **Step 2: 顶栏底色改 --topbar-ink-admin（深一档）**
  当前（102-113 行）：
  ```css
  .admin-topbar {
    height: 48px;
    min-height: 48px;
    background: #434352;
    border-bottom: none;
  ```
  目标：
  ```css
  .admin-topbar {
    height: 48px;
    min-height: 48px;
    background: var(--topbar-ink-admin);
    border-bottom: 1px solid var(--topbar-border);
  ```
  说明：后台顶栏比前台 `--topbar-ink #1F2430` 深一档（`--topbar-ink-admin #171A22`），这是前后台的唯一环境区分；底线用 `--topbar-border`（略可见）取代原 `none`。

- [ ] **Step 3: 删除紫色渐变底线 ::after**
  删除当前 115-125 行整段：
  ```css
  /* 紫色渐变底线 */
  .admin-topbar::after {
    content: '';
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    height: 2px;
    background: linear-gradient(90deg, #722ED1 0%, #B37FEB 60%, transparent 100%);
    pointer-events: none;
  }
  ```
  整段移除（底线职责已由 Step 2 的 `border-bottom` 承担）。

- [ ] **Step 4: 盾牌图标去紫**
  当前（160-163 行）：
  ```css
  .admin-shield-icon {
    font-size: 15px;
    color: #B37FEB;
  }
  ```
  目标：
  ```css
  .admin-shield-icon {
    font-size: 15px;
    color: var(--text-3);
  }
  ```
  说明：后台标识改为中性灰图标 + "系统管理后台"文案（模板第 11 行文案保留不动），不再用紫色强调。

- [ ] **Step 5: 头像紫渐变改令牌中性底**
  模板当前（14-17 行）：
  ```html
  <a-avatar v-else :size="24" style="background: linear-gradient(135deg, #722ED1, #9254DE); font-size: 12px;">
  ```
  目标：
  ```html
  <a-avatar v-else :size="24" :style="{ background: 'var(--color-primary)', fontSize: '12px' }">
  ```
  说明：与前台一致的品牌主色头像，紫渐变退役；改用对象式 `:style` 以正确解析 `var()`。

- [ ] **Step 6: 校验本 Task 不残留紫**
  运行（PowerShell）：`cd E:/STOTOP_Fable/web; rg -n "#722ED1|#9254DE|#B37FEB" src/layouts/AdminLayout.vue`
  期望输出：仅剩 222-224 行的菜单激活态命中（Task 2 处理），其余顶栏命中为 0。

- [ ] **Step 7: 提交**
  `cd E:/STOTOP_Fable; git add web/src/layouts/AdminLayout.vue; git commit -m "阶段5：后台顶栏退役紫主题，改 --topbar-ink-admin + 令牌文案标识"`（提交信息末尾追加一行 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`）。

---

### Task 2: 后台侧栏退役紫主题——底色/分组标题/菜单密度/激活态对齐前台令牌

**Files:**
- Modify `web/src/layouts/AdminLayout.vue`（样式 184-245 行）

- [ ] **Step 1: 侧栏深紫底改令牌 + 收口单一底色**
  当前（184-191 行）：
  ```css
  .admin-sidebar {
    width: 220px;
    flex-shrink: 0;
    background: #2B2D3A;
    overflow-y: auto;
    padding-top: 4px;
  }
  ```
  目标：
  ```css
  .admin-sidebar {
    width: 220px;
    flex-shrink: 0;
    background: var(--sidebar-bg);
    overflow-y: auto;
    padding-top: 4px;
  }
  ```
  说明：后台侧栏与前台共用 `--sidebar-bg #EDEEF1`，不再是深紫灰。下面文字色随之全部从"白系"翻转为"深色系"（Step 3-5）。

- [ ] **Step 2: 分组标题字号/色令牌化**
  当前（197-203 行）：
  ```css
  .group-title {
    color: rgba(255, 255, 255, 0.62); /* 提升对比度，达 WCAG AA（原 0.45 不达标） */
    font-size: 13px;                  /* 统一使用 $font-size-sm2 对应山（12px 在深色背景可读性差 */
    letter-spacing: 0.5px;
    padding: 16px 20px 8px;
    user-select: none;
  }
  ```
  目标：
  ```css
  .group-title {
    color: var(--text-3);
    font-size: var(--font-sm);
    letter-spacing: 0.5px;
    padding: var(--space-lg16) var(--space-xl24) var(--space-sm8);
    user-select: none;
  }
  ```
  说明：浅底下分组标题用 `--text-3 #8A9099` + `--font-sm 12px`，与前台 SmartSidebar 分组标题档位一致。

- [ ] **Step 3: 菜单项常规态——浅底深字 + 高度密度对齐 36/圆角6**
  当前（205-214 行）：
  ```css
  .menu-item {
    display: flex;
    align-items: center;
    padding: 10px 20px;
    cursor: pointer;
    color: rgba(255, 255, 255, 0.68);
    font-size: 13px;
    transition: all 0.2s;
    border-left: 3px solid transparent;
  }
  ```
  目标：
  ```css
  .menu-item {
    display: flex;
    align-items: center;
    height: 36px;
    padding: 0 var(--space-md12);
    margin: 1px var(--space-sm8);
    border-radius: var(--radius-md);
    cursor: pointer;
    color: var(--text-2);
    font-size: var(--font-sm2);
    transition: all 0.15s ease;
    position: relative;
  }
  ```
  说明：高度统一 36px、圆角 `--radius-md 6`、整行底色高亮（去掉 `border-left` 竖条，改用 Step 5 的左侧指示条与前台 SmartSidebar 第 432-442 行一致）。

- [ ] **Step 4: 菜单 hover 改 --sidebar-item-hover（可感知）**
  当前（216-219 行）：
  ```css
  .menu-item:hover {
    color: rgba(255, 255, 255, 0.92);
    background: rgba(255, 255, 255, 0.05);
  }
  ```
  目标：
  ```css
  .menu-item:hover {
    color: var(--text-1);
    background: var(--sidebar-item-hover);
  }
  ```
  说明：浅底下用 `--sidebar-item-hover rgba(0,0,0,0.05)`，与前台同一可感知反馈。

- [ ] **Step 5: 菜单激活态退役紫，改令牌激活底 + 左指示条**
  当前（221-225 行）：
  ```css
  .menu-item.active {
    color: #fff;
    background: rgba(114, 46, 209, 0.14);
    border-left-color: #722ED1;
  }
  ```
  目标：
  ```css
  .menu-item.active {
    color: var(--sidebar-item-active-text);
    background: var(--sidebar-item-active-bg);
    font-weight: 600;
  }
  .menu-item.active::before {
    content: '';
    position: absolute;
    left: 0;
    top: 10px;
    bottom: 10px;
    width: 3px;
    border-radius: 0 2px 2px 0;
    background: var(--sidebar-active-indicator);
  }
  ```
  说明：激活底色 `--sidebar-item-active-bg`(=`--color-primary-light`)、文字 `--sidebar-item-active-text`(=`--color-primary`)、左侧 3px 指示条 `--sidebar-active-indicator`，与前台 SmartSidebar 第 432-468 行像素级对齐。

- [ ] **Step 6: 菜单图标 opacity 与内容区底色令牌化**
  当前（227-231 行）`.menu-icon { ... opacity: 0.85; }` 在浅底下偏淡，改 `opacity: 1; color: var(--text-3);`；当前（240-245 行）`.admin-content { ... background: #F4F5F7; padding: 16px; }` 改 `background: var(--bg-page); padding: var(--space-lg16);`。激活时图标随父级 `currentColor` 走主色，新增：
  ```css
  .menu-item.active .menu-icon { color: var(--sidebar-item-active-text); }
  ```

- [ ] **Step 7: 校验后台彻底无紫 + 提交**
  `cd E:/STOTOP_Fable/web; rg -n "#722ED1|#9254DE|#B37FEB|rgba\(114, 46, 209" src/layouts/AdminLayout.vue` 期望 0 命中。
  `cd E:/STOTOP_Fable; git add web/src/layouts/AdminLayout.vue; git commit -m "阶段5：后台侧栏退役紫主题，底色/分组/菜单密度/激活态对齐前台令牌"`（追加 Co-Authored-By 行）。

---

### Task 3: 前台 TopBar 工作台激活色对齐 --color-primary，外壳橙令牌化

**Files:**
- Modify `web/src/layouts/TopBar.vue`（样式 405-635 行）

- [ ] **Step 1: 工作台按钮 active 色对齐主色（修 #FF8533）**
  当前（425-430 行）：
  ```scss
    &.active {
      background: rgba(255, 103, 0, 0.15);
      color: #FF8533;
      font-weight: 600;
    }
  ```
  目标：
  ```scss
    &.active {
      background: var(--color-primary-light);
      color: var(--color-primary);
      font-weight: 600;
    }
  ```
  说明：原 `#FF8533`（hover 档）与侧栏激活的 `#E85E00` 不一致，统一收口到 `--color-primary #E85E00` + `--color-primary-light #FFF3EA`。

- [ ] **Step 2: 搜索框 hover 边框橙令牌化**
  当前（609-613 行）：
  ```scss
    &:hover {
      background: rgba(255, 255, 255, 0.18);
      border-color: rgba(255, 103, 0, 0.5);
      color: rgba(255, 255, 255, 0.9);
    }
  ```
  目标：
  ```scss
    &:hover {
      background: rgba(255, 255, 255, 0.18);
      border-color: var(--color-primary-border);
      color: rgba(255, 255, 255, 0.9);
    }
  ```
  说明：深色顶栏内的橙色高亮收口到 `--color-primary-border rgba(232,94,0,0.30)`。

- [ ] **Step 3: 待办徽章底色令牌化**
  当前（549-564 行）`.workhub-todo-count { ... background: #FF6700; ... }` 改 `background: var(--color-primary);`。

- [ ] **Step 4: 公告脉冲圆点令牌化**
  当前（626-634 行）`.topbar-announcement::before { ... background: #FF6700; ... }` 改 `background: var(--color-primary);`。

- [ ] **Step 5: page-tab 圆角令牌化**
  当前（473 行）`border-radius: 8px 8px 0 0;` 改 `border-radius: var(--radius-lg) var(--radius-lg) 0 0;`；`.page-tab-icon`（508-512 行）`color: $color-primary;` 保留语义但改 `color: var(--color-primary);` 以统一来源。

- [ ] **Step 6: 顶栏内联圆角统一 --radius-md**
  将 `.topbar-brand`(329)、`:deep(.topbar-org ...) .org-current`(381)、`.topbar-workhub-btn`(413)、`.topbar-back-btn/.topbar-forward-btn`(438) 的 `border-radius: 6px;` 改 `border-radius: var(--radius-md);`（值不变，来源统一）。

- [ ] **Step 7: 校验 TopBar 橙令牌化 + 提交**
  `cd E:/STOTOP_Fable/web; rg -n "#FF8533|#FF6700|rgba\(255, 103, 0" src/layouts/TopBar.vue` 期望 0 命中。
  `cd E:/STOTOP_Fable; git add web/src/layouts/TopBar.vue; git commit -m "阶段5：TopBar 工作台激活色对齐 --color-primary，外壳橙令牌化"`（追加 Co-Authored-By 行）。

---

### Task 4: 前台 SmartSidebar 激活/hover/指示条令牌化（与后台同规则）

**Files:**
- Modify `web/src/layouts/SmartSidebar.vue`（样式 409-572 行）

- [ ] **Step 1: 激活态底色/文字令牌化（修裸 #E85E00）**
  当前（464-468 行）：
  ```scss
    &.active {
      background: rgba(255, 103, 0, 0.10);
      color: #E85E00;
      font-weight: 600;
    }
  ```
  目标：
  ```scss
    &.active {
      background: var(--sidebar-item-active-bg);
      color: var(--sidebar-item-active-text);
      font-weight: 600;
    }
  ```
  说明：与 AdminLayout 第 221-223 行（Task 2 改后）完全相同的令牌，前后台激活态统一。

- [ ] **Step 2: 激活图标色令牌化**
  当前（490-492 行）：
  ```scss
  .nav-item.active .nav-icon {
    color: #E85E00;
  }
  ```
  目标：
  ```scss
  .nav-item.active .nav-icon {
    color: var(--sidebar-item-active-text);
  }
  ```

- [ ] **Step 3: 激活指示条令牌化**
  当前（432-442 行）`&.active::before { ... background: $color-primary; }` 改 `background: var(--sidebar-active-indicator);`，与后台指示条同源。

- [ ] **Step 4: hover 反馈令牌化（可感知）**
  当前（459-462 行）：
  ```scss
    &:hover {
      background: rgba(0, 0, 0, 0.05);
      color: rgba(0, 0, 0, 0.88);
    }
  ```
  目标：
  ```scss
    &:hover {
      background: var(--sidebar-item-hover);
      color: var(--text-1);
    }
  ```
  说明：`--sidebar-item-hover rgba(0,0,0,0.05)` 与原值一致，来源统一；与后台 hover 同令牌。

- [ ] **Step 5: dirty 点 / 拖拽手柄 / ghost 橙令牌化**
  `.dirty-dot`（544-556 行）`background: #FF6700;` 改 `background: var(--color-primary);`，`box-shadow: 0 0 0 2px rgba(255, 103, 0, 0.15);` 改 `box-shadow: 0 0 0 2px var(--color-primary-border);`；
  `.sidebar-resize-handle:hover`（559-565 行）`background: rgba(255, 103, 0, 0.3);` 改 `background: var(--color-primary-border);`；
  `.nav-item-ghost`（568-572 行）`background: rgba(255, 103, 0, 0.08);` 改 `background: var(--color-primary-light);`，`border-left: 2px solid $color-primary;` 改 `border-left: 2px solid var(--color-primary);`。

- [ ] **Step 6: item-action / pin-marker 主色令牌化**
  `.item-action:hover`（515-518 行）`color: $color-primary;` 改 `color: var(--color-primary);`；`.pin-marker`（448-457 行）`color: $color-primary;` 改 `color: var(--color-primary);`；`.section-title .section-icon`（375-378 行）`color: $color-primary;` 改 `color: var(--color-primary);`。

- [ ] **Step 7: 校验 SmartSidebar 橙令牌化 + 提交**
  `cd E:/STOTOP_Fable/web; rg -n "#E85E00|#FF6700|rgba\(255, 103, 0" src/layouts/SmartSidebar.vue` 期望 0 命中。
  `cd E:/STOTOP_Fable; git add web/src/layouts/SmartSidebar.vue; git commit -m "阶段5：SmartSidebar 激活/hover/指示条令牌化，与后台同规则"`（追加 Co-Authored-By 行）。

---

### Task 5: 收口侧栏单一底色与外壳基底——layout.scss / variables.scss / theme.ts 三处同源

**Files:**
- Modify `web/src/styles/layout.scss`（24-29、178-181、230-234、241-287 行）
- Modify `web/src/styles/variables.scss`（120-140 行）
- Modify `web/src/stores/theme.ts`（52-78、216-224 行）

- [ ] **Step 1: 定位侧栏底色三处分叉**
  确认三处来源：layout.scss 第 180 行 `.smart-sidebar { background: $sidebar-bg; }`（实际渲染值）；variables.scss 第 120 行 `$sidebar-bg: #E2E2E2;`（被上行消费）；theme.ts 第 221 行 `style.setProperty('--sidebar-bg', themeConfig.value.sidebarBgColor)` + 第 75 行 `sidebarBgColor: '#e4e7ef'`（注入了一个无人消费的旁路 `--sidebar-bg`）。本 Task 把三者收口到权威 `--sidebar-bg #EDEEF1`。

- [ ] **Step 2: layout.scss 顶栏与侧栏改消费权威令牌**
  当前（19-29 行）：
  ```scss
  .topbar {
    display: flex;
    align-items: stretch;
    height: $topbar-height;
    padding: 0 12px;
    background: $topbar-bg;
    border-bottom: $topbar-bottom-border;
  ```
  目标：
  ```scss
  .topbar {
    display: flex;
    align-items: stretch;
    height: $topbar-height;
    padding: 0 12px;
    background: var(--topbar-ink);
    border-bottom: 1px solid var(--topbar-border);
  ```
  侧栏当前（178-181 行）`width: ...; background: $sidebar-bg;` 的 `background` 改 `background: var(--sidebar-bg);`。说明：前台顶栏底线由 `--topbar-border rgba(255,255,255,0.10)` 略可见（比原 0.06 更明确）。

- [ ] **Step 3: layout.scss nav-item 基础规则令牌化（前后台共用）**
  当前（241-266 行）`.nav-item` 的 `font-size: $font-size-base;`→`font-size: var(--font-sm2);`、`color: $sidebar-text;`→`color: var(--text-2);`、`&:hover:not(.active)` 背景 `rgba(0,0,0,0.04)`→`var(--sidebar-item-hover)`、`color: $sidebar-text-hover;`→`color: var(--text-1);`、`&.active` 背景 `$sidebar-item-active-bg`→`var(--sidebar-item-active-bg)` 与 `color: $sidebar-text-active;`→`color: var(--sidebar-item-active-text);`、`border-radius: 4px;`→`border-radius: var(--radius-md);`、`&:focus-visible outline` `$color-primary`→`var(--color-primary)`。section-divider（230-234 行）`background: $sidebar-divider;` 改 `background: var(--border);`。

- [ ] **Step 4: variables.scss 桥接值回填（仅改值不改名）**
  当前（119-131 行）：
  ```scss
  $sidebar-bg: #E2E2E2;                // 申通浅灰（较深灰）
  $topbar-bg: #1A1B23;                 // 深统一色，与登录过渡屏一致
  $topbar-bottom-border: 1px solid rgba(255, 255, 255, 0.06); // 极细半透明白线，克制分隔
  ```
  目标：
  ```scss
  $sidebar-bg: #EDEEF1;                // 桥接 --sidebar-bg（收口 #E2E2E2/#e4e7ef）
  $topbar-bg: #1F2430;                 // 桥接 --topbar-ink
  $topbar-bottom-border: 1px solid rgba(255, 255, 255, 0.10); // 桥接 --topbar-border（略可见）
  ```
  同时第 128 行 `$sidebar-text-active: #E85E00;` 保留（已是权威主色值）。说明：保留 SCSS 名做兼容桥，layout.scss 已改用 var() 直引，此回填仅兜底其它仍引用 `$sidebar-bg/$topbar-bg` 的旧处。

- [ ] **Step 5: variables.scss 退役 admin 紫色族（标注 DEPRECATED）**
  当前（133-140 行）`$admin-topbar-bg / $admin-topbar-gradient-line / $admin-sidebar-bg / $admin-accent / $admin-accent-light / $admin-content-bg / $admin-text-muted` 整组紫色定义，在每行行首注释前追加 `// DEPRECATED 阶段5 退役，AdminLayout 已改令牌；保留防编译断引` 一行块注释（不删除以免编译期未清引用报错，但确保不再被外壳消费）。运行 `cd E:/STOTOP_Fable/web; rg -n "\$admin-topbar-bg|\$admin-accent|\$admin-sidebar-bg" src` 确认仅 variables.scss 自身命中（外壳已无引用）。

- [ ] **Step 6: theme.ts 默认色对齐权威令牌值 + 收口 sidebarBg**
  当前（52-77 行）defaultThemeConfig：
  ```ts
    colorPrimary: '#FF6700',
    colorSuccess: '#52C41A',
    colorWarning: '#E6A700',
    colorError: '#FF4D4F',
    colorInfo: '#13C2C2',
    ...
    sidebarBgColor: '#e4e7ef',
    sidebarActiveBgColor: 'rgba(255, 103, 0, 0.06)',
  ```
  目标：
  ```ts
    colorPrimary: '#FF6700',
    colorSuccess: '#2BA471',
    colorWarning: '#E6A700',
    colorError: '#E5484D',
    colorInfo: '#3A6FB0',
    ...
    sidebarBgColor: '#EDEEF1',
    sidebarActiveBgColor: '#FFF3EA',
  ```
  说明：success→`#2BA471`、error/danger→`#E5484D`、info 由青→蓝 `#3A6FB0`；`colorPrimary` 保持 `#FF6700`（Ant 组件主色取 hover 档，页面级 `--color-primary #E85E00` 由 :root 提供，二者分工，不在本阶段改 Ant token）；sidebarBg 收口 `#EDEEF1`、激活底收口 `#FFF3EA`(=`--color-primary-light`)。

- [ ] **Step 7: theme.ts applySidebarCSS 对齐权威令牌**
  当前（217-224 行）：
  ```ts
      style.setProperty('--sidebar-bg', themeConfig.value.sidebarBgColor)
      style.setProperty('--sidebar-active-bg', themeConfig.value.sidebarActiveBgColor)
      style.setProperty('--sidebar-active-indicator', themeConfig.value.colorPrimary || '#FF6700')
  ```
  目标：
  ```ts
      style.setProperty('--sidebar-bg', themeConfig.value.sidebarBgColor)
      style.setProperty('--sidebar-item-active-bg', themeConfig.value.sidebarActiveBgColor)
      style.setProperty('--sidebar-active-indicator', themeConfig.value.colorPrimary || '#E85E00')
  ```
  说明：把旁路 `--sidebar-active-bg` 改为权威名 `--sidebar-item-active-bg`（被 layout.scss 消费），兜底色由 `#FF6700` 改 `#E85E00` 对齐主色；`--sidebar-bg` 注入值已随 Step 6 收口为 `#EDEEF1`，与 SCSS 端不再分叉。

- [ ] **Step 8: 构建校验 + 提交**
  `cd E:/STOTOP_Fable/web; rg -n "#E2E2E2|#e4e7ef" src/styles src/stores/theme.ts` 期望 0 命中（侧栏底色完成收口）。
  `cd E:/STOTOP_Fable/web; npm run build` 期望以 `✓ built in` 结尾、无 SCSS 编译错误。
  `cd E:/STOTOP_Fable; git add web/src/styles/layout.scss web/src/styles/variables.scss web/src/stores/theme.ts; git commit -m "阶段5：收口侧栏单一底色与外壳基底，layout/variables/theme 三处同源令牌"`（追加 Co-Authored-By 行）。

---

### Task 6: 逐屏 preview 前后台对比验证 + 运行时令牌验证

**Files:**
- 无文件修改（纯验证 Task）

- [ ] **Step 1: 全量静态断言无残留紫/裸橙**
  `cd E:/STOTOP_Fable/web; rg -n "#722ED1|#9254DE|#B37FEB|rgba\(114, 46, 209|#FF8533" src/layouts` 期望 0 命中；
  `rg -n "#FF6700|#E85E00|rgba\(255, 103, 0" src/layouts` 期望 0 命中（外壳已全部令牌化）。

- [ ] **Step 2: 类型检查不新增报错**
  `cd E:/STOTOP_Fable/web; npm run type-check` —— 基线本就红，仅确认 `AdminLayout.vue / TopBar.vue / SmartSidebar.vue / theme.ts` 不出现本次改动新增的报错（对照改动前同命令输出，本文件无新增行即通过）。

- [ ] **Step 3: 启动 preview**
  用 preview 工具 `preview_start`（基址 `http://localhost:9000`，若开发服务未起先按 restart-dev 拉起）。等待首屏可交互。

- [ ] **Step 4: 前台外壳截图**
  导航 `/workhub`，`preview_screenshot` 命名 `phase5-front-workhub`：核对顶栏底色 `--topbar-ink #1F2430` + 底线略可见、侧栏底色 `--sidebar-bg #EDEEF1` 单一色、工作台按钮 active 为 `#E85E00`+`#FFF3EA`（非 `#FF8533`）。再导航任一业务页（如 `/express/...`），截图 `phase5-front-sidebar-active`：核对侧栏激活项底 `#FFF3EA`+左 3px 指示条 `#E85E00`、hover 浅灰可感知、菜单项高度 36/圆角 6。

- [ ] **Step 5: 后台外壳截图对比**
  导航 `/admin`，`preview_screenshot` 命名 `phase5-admin-shell`：核对顶栏 `--topbar-ink-admin #171A22`（比前台深一档、无紫渐变线、盾牌中性灰 + "系统管理后台"文案）、侧栏底色与前台同为 `#EDEEF1`、分组标题 `#8A9099`/12px、菜单 hover/激活与前台像素一致（激活底 `#FFF3EA`+指示条 `#E85E00`，无任何紫）。与 Step 4 前台截图并排核对"前后台仅顶栏深浅一档之差"。

- [ ] **Step 6: 运行时令牌验证（改 --color-primary 看全局变色）**
  用 `preview_eval` 执行 `document.documentElement.style.setProperty('--color-primary', '#1E90FF')`，截图 `phase5-token-runtime`：核对前台工作台激活、侧栏激活文字/指示条、后台侧栏激活、dirty 点、ghost 同步变蓝 —— 证明外壳已全部走单一权威令牌；随后 `preview_eval` 执行 `document.documentElement.style.removeProperty('--color-primary')` 复原。

- [ ] **Step 7: 收尾**
  `preview_stop`。本 Task 无代码改动，无需提交（若 preview 过程意外改动任何文件则 `git checkout --` 复原后再结束）。