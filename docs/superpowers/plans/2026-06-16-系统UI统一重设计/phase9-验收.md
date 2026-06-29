## 阶段9：验收与回归（全站一致性 + 构建 + 可视化）

> 在阶段 0–8 全部完成后执行。本阶段不改业务样式，只做**断言式验收**；任何一项不过，回到对应阶段修复。
> 验证：ripgrep（PowerShell 语法）；`cd web; npm run build`；preview 工具截图；运行时改 `--color-primary` 看全局变色。每个 Task 末尾 `git commit`（中文 message + `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`）。

---

### Task 1: 去蓝/死文件 零残留断言

**Files:**
- 只读校验，无修改（如有残留回阶段1）

- [ ] **Step 1: 三种蓝零命中**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n -i "#1677ff|#1890ff|#409eff" src
  ```
  期望：零命中（无输出）。命中项逐个回阶段1 按"交互→`var(--color-primary)` / 信息→`var(--color-info)`"修。

- [ ] **Step 2: 死文件已删**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "config/theme|antThemeConfig" src; if (Test-Path src/config/theme.ts) { "STILL EXISTS" } else { "DELETED" }
  ```
  期望：`rg` 零命中；输出 `DELETED`。

- [ ] **Step 3: 提交校验记录（如本 Task 无改动则跳过提交）**
  仅当为通过断言而修了残留时提交。

---

### Task 2: 同义多值收敛 断言

**Files:**
- 只读校验

- [ ] **Step 1: 金黄/红/内容灰 旧值零命中**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n -i "#faad14|#d4b106|#fa8c16|#f5222d|#f4f6f8|#F0F2F5|#F4F5F7|#13C2C2" src --glob '!src/styles/tokens.scss' --glob '!src/styles/variables.scss'
  ```
  期望：零命中（令牌定义文件已排除）。残留回阶段1。

- [ ] **Step 2: 后台紫主题已退役**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "#722ED1|#722ed1|#434352|#2B2D3A|#B37FEB" src
  ```
  期望：仅 `--biz-waybill` 定义处（若运单业务色取值与紫相关）或零命中；`AdminLayout.vue` 内不得再出现紫色顶栏/侧栏/激活态。残留回阶段5。

---

### Task 3: 运行时令牌单一真源 — 全局变色验证

**Files:**
- 只读校验（运行时）

- [ ] **Step 1: 启动 preview**
  用 `preview_start` 启动前端 dev server。

- [ ] **Step 2: 改主色看全局联动**
  用 `preview_eval` 执行：
  ```js
  document.documentElement.style.setProperty('--color-primary', '#1565C0')
  ```
  然后 `preview_screenshot` 截 工作台/某列表页/某表单页。
  期望：按钮、链接、侧栏激活、Tab 下滑条、搜索高亮**同步变蓝**（证明组件吃 `var(--color-primary)` 而非写死橙）。截完恢复：
  ```js
  document.documentElement.style.removeProperty('--color-primary')
  ```

- [ ] **Step 3: antd 组件吃 CSS 变量**
  `preview_eval` 检查任一 `.ant-btn-primary` 的 `getComputedStyle(...).backgroundColor` 是否随 Step 2 改变。期望：随之改变。

---

### Task 4: 登录页 去装饰 + 深墨 断言

**Files:**
- 只读校验（如残留回阶段2）

- [ ] **Step 1: 动画/装饰源码零残留**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "season|particle|route-path|deco-ring|brand-decoration|logistics-route" src/views/login src/utils/seasonTheme.ts 2>$null
  ```
  期望：零命中；`src/utils/seasonTheme.ts` 已删（`Test-Path` 为 False）。

- [ ] **Step 2: 焦点环为橙、无蓝光晕**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n "rgba\(22,\s*119,\s*255" src/views/login src/styles/ant-override.scss
  ```
  期望：零命中。

- [ ] **Step 3: 可视化**
  `preview` 打开 `/login`，`preview_screenshot`。期望：左深墨静态品牌区、无粒子/SVG/光斑、橙色实色登录按钮、橙色焦点环；深墨过渡屏 → 淡入浅色系统。

---

### Task 5: 外壳前后台一致 + 后台中性区分 断言

**Files:**
- 只读校验（残留回阶段5）

- [ ] **Step 1: 可视化前台**
  `preview` 进前台工作台，`preview_screenshot`：顶栏 `--topbar-ink`、侧栏 `--sidebar-bg` 单一灰、激活态橙竖条 + 浅橙底、hover 可感知。

- [ ] **Step 2: 可视化后台**
  导航到管理后台页（如科目管理/主题配置），`preview_screenshot`：顶栏为 `--topbar-ink-admin`（深一档）+ "管理后台"文案标识，**无紫色**；菜单圆角/密度/分组标题字号与前台一致。

---

### Task 6: WCAG AA 抽检

**Files:**
- 只读校验

- [ ] **Step 1: 主按钮对比度**
  `preview_inspect` 或 `preview_eval` 取主按钮文字色/底色，计算对比度（白字 on `--color-primary` 填充按钮）。期望：≥ 3:1（大字/粗体按钮 AA）。不达标回阶段0 微调 `--color-primary` 深度。

- [ ] **Step 2: 正文与次要文字**
  取 `--text-1`/`--text-2` 在 `--bg-card` 上的对比度。期望：`--text-1` ≥ 7:1、`--text-2` ≥ 4.5:1。

---

### Task 7: 全站逐模块一致性抽检

**Files:**
- 只读校验（残留回阶段8）

- [ ] **Step 1: 每模块至少一页截图**
  逐模块用 `preview` 打开代表页并 `preview_screenshot`：finance（列表+表单）、express（仪表盘）、cardflow（详情）、task（详情）、crm、oa、quality、points、system（含主题配置页，确认 emoji 标签已去）、conference、dormitory、vehicle、contract。

- [ ] **Step 2: 一致性核对**
  对照 `web/docs/PATTERNS.md`：页头走 `PageHeader`、容器/卡片/工具栏/表格/空态走范式、无自造卡片样式、无硬编码色、状态色与 `--biz-*` 正确。逐项打勾；偏离页回阶段8 对应模块 Task 修。

- [ ] **Step 3: 自造空态残留摸排**
  ```powershell
  cd E:\STOTOP_Fable\web; (rg -l "class=.*empty" src/views | Measure-Object -Line).Lines
  ```
  期望：显著低于基线（基线 43 文件）；剩余为合理特例并已登记。

---

### Task 8: 移动端令牌联动 断言

**Files:**
- 只读校验（残留回阶段6）

- [ ] **Step 1: vant 变量桥生效**
  `preview_resize` 切窄屏（如 390×844），打开 oa-mobile 页，`preview_screenshot`。期望：主色/状态/表面与桌面端一致。

- [ ] **Step 2: 移动端硬编码色零残留**
  ```powershell
  cd E:\STOTOP_Fable\web; rg -n -i "#1677ff|#1890ff|#07c160|#1989fa" src/views/oa-mobile
  ```
  期望：零命中（vant 默认绿/蓝已映射到令牌）。

---

### Task 9: 构建与类型 终检

**Files:**
- 只读校验

- [ ] **Step 1: 生产构建**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run build
  ```
  期望：退出码 0，`✓ built in`。

- [ ] **Step 2: 类型无新增报错**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run type-check 2>&1 | Select-String -Pattern "error TS" | Measure-Object -Line
  ```
  与改造前基线对比（基线本就大量报错）：期望本次改造**未新增** `error TS`（数量不超过基线）。

- [ ] **Step 3: stylelint 门禁**
  ```powershell
  cd E:\STOTOP_Fable\web; npm run lint:style
  ```
  期望：通过（裸 hex 在组件中为 0，token 文件白名单除外）。

- [ ] **Step 4: 收尾提交**
  ```powershell
  cd E:\STOTOP_Fable; git add -A; git commit -m @'
chore(ui): 阶段9 全站 UI 统一验收通过（去蓝/拆紫/令牌联动/登录/外壳/移动端/全站一致性）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
  ```
