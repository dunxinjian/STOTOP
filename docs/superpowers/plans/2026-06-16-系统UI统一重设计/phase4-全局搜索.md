> 前置假定：阶段0 已在全局 `:root` 定义本阶段引用的全部 CSS 自定义属性（令牌），阶段1 已完成。本阶段所有颜色/字号/圆角/阴影/间距一律以 `var(--token)` 引用，禁止再出现本地 `$brand`/`$brand-bg` 与裸十六进制/像素硬编码（动画 keyframe 的 scale 数值、`box-sizing` 等非主题量除外）。
>
> 真实代码事实（已逐行核对）：
> - `GlobalSearch.vue` 有两段样式：第一段 `<style lang="scss">`（非 scoped，645-706，含 `.ant-modal-content` 圆角 `12px` 与阴影 `0 16px 48px rgba(0,0,0,0.15)`），第二段 `<style scoped lang="scss">`（708-985，顶部 `$brand: #FF6700;` / `$brand-bg: #FFF1E8;`）。
> - `$brand` 在 707-984 共被引用 9 处：807（section-title 圆点）、821/825（hover/active 底色 `$brand-bg`）、826（active 左边框）、839/960（item-module-tag、match-hl 文字色 `$brand`，及 839 的 `rgba(255,103,0,0.06)` 底色）、868/872（module-header hover/active）、926（empty-keyword）、955（recommended-item 渐变）、960-961（match-hl）、973（recommended-icon）、977-978（recommend-reason）。
> - active 位移 hack 出现在 826-827（`border-left: 3px` + `padding-left: 15px`）、854-856（sub-item active `padding-left: 37px`）、872-874（module-header active）。
> - 硬截断在 345：`.slice(0, 20)`；`groupedResults`（358-378）依据 `filteredResults.value` 计算，`getSearchIndex`（380-382）用 `filteredResults.value.indexOf`，`totalNavigableItems`（428-431）搜索态返回 `filteredResults.value.length`。
> - 焦点视觉反馈在 718-720（`.search-header:focus-within .search-icon`），搜索/浏览两套索引切换在 428-443（`totalNavigableItems` + `moveUp`/`moveDown`）。
> - `allFlatMenus`（296-325）依赖 `visibleModules.value`（244-259，读 `permissionStore.getModuleVisibility`）与 `permissionStore.getCurrentModuleMenus`（permission.ts 331-349，读 `menus.value` ref）。

---

### Task 1: 核查并固化 allFlatMenus 权限缓存依赖链（只读核查 + 注释）

**Files:**
- Modify: `web/src/components/GlobalSearch.vue`（核查 244-325，仅在 296 上方补注释，不改逻辑）
- Read（只读核查，不改）：`web/src/stores/permission.ts` 303/313/331-367、`web/src/stores/sidebar.ts` 131

- [ ] **Step 1: 读 permission.ts 确认响应式源头。** 读 `web/src/stores/permission.ts` 第 300-367 行，确认：`const menus = ref<MenuItem[]>([])`（303），登录后 `menus.value = flatMenus`（313），登出 `menus.value = []`（488）；`getModuleVisibility`（355-367）内部 `menus.value.some(...)`，`getCurrentModuleMenus`（331-349）内部 `buildMenuTree(menus.value)`。结论：三者均在调用时同步读取同一个 `menus` ref，无内部本地缓存绕过响应式。
- [ ] **Step 2: 推导 allFlatMenus 的依赖追踪是否完整。** 在 GlobalSearch.vue 中，`allFlatMenus`（296-325）的 `for (const mod of visibleModules.value)`（320）追踪 `visibleModules`（→ `getModuleVisibility` → `menus.value`），循环体 `permissionStore.getCurrentModuleMenus(mod.code)`（321）再次读 `menus.value`。两条路径都让该 computed 把 `menus` ref 收集为依赖，故后端菜单刷新 / 登出清空时会自动重算。结论：依赖链正确，无需改逻辑。
- [ ] **Step 3: 确认组织切换不依赖菜单引用变更也能刷新。** 读 GlobalSearch.vue 603-607 的 `watch(() => orgContextStore.orgSwitchVersion, ...)`：组织切换时清空 `expandedModules/keyword/activeIndex`。即使切换后 `menus.value` 被替换为新数组引用（permission.ts 313 整体赋值），computed 也会因引用变化重算；该 watch 额外负责重置本地展开/选中态。结论：无遗漏。
- [ ] **Step 4: 在 allFlatMenus 上方补一行依赖说明注释（唯一改动）。** 当前 295-296：
  ```
  /** 所有菜单项展平（带模块信息），递归到所有层级 */
  const allFlatMenus = computed<FlatMenuItem[]>(() => {
  ```
  目标（在第 295 注释后追加一行依赖说明）：
  ```
  /** 所有菜单项展平（带模块信息），递归到所有层级 */
  // 依赖链：visibleModules(→getModuleVisibility→menus.ref) + getCurrentModuleMenus(→menus.ref)
  // 两路径均同步读取 permission store 的 menus ref，后端刷新/登出/换组织时自动重算，无需手动失效。
  const allFlatMenus = computed<FlatMenuItem[]>(() => {
  ```
- [ ] **Step 5: 提交。** 运行 `cd web; git add src/components/GlobalSearch.vue; git commit -m "核查并注释 GlobalSearch allFlatMenus 权限依赖链`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望输出含 `1 file changed`。

---

### Task 2: 第一段非 scoped 样式令牌化（圆角 12→--radius-modal、阴影→--shadow-lg）

**Files:**
- Modify: `web/src/components/GlobalSearch.vue` 645-669（`.ant-modal-content`）

- [ ] **Step 1: 读取当前 645-669。** 确认 651-656：
  ```scss
  .ant-modal-content {
    border-radius: 12px;
    overflow: hidden;
    padding: 0;
    box-shadow: 0 16px 48px rgba(0, 0, 0, 0.15);
  }
  ```
- [ ] **Step 2: 迁圆角与阴影到令牌。** 将 652、655 改为：
  ```scss
  .ant-modal-content {
    border-radius: var(--radius-modal);
    overflow: hidden;
    padding: 0;
    box-shadow: var(--shadow-lg);
  }
  ```
  说明：原 `0 16px 48px` 比权威 `--shadow-lg`（`0 8px 24px rgba(18,31,53,0.10)`）更重，统一为令牌后弹层阴影与全站一致。
- [ ] **Step 3: 静态断言该段已无硬编码圆角/阴影。** 运行 `cd web; rg -n "border-radius: 12px|0 16px 48px" src/components/GlobalSearch.vue`。期望输出：0 命中（空）。
- [ ] **Step 4: 提交。** `cd web; git add src/components/GlobalSearch.vue; git commit -m "GlobalSearch 模态壳层圆角/阴影迁令牌`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed`。

---

### Task 3: 删本地 $brand/$brand-bg 并迁 scoped 样式基础色/字号/间距/圆角令牌

**Files:**
- Modify: `web/src/components/GlobalSearch.vue` 708-934（708-710 变量删除；search-header/section/search-item/module-header/search-empty 段）

- [ ] **Step 1: 读取 708-766 并删除本地变量。** 当前 708-710：
  ```scss
  <style scoped lang="scss">
  $brand: #FF6700;
  $brand-bg: #FFF1E8;
  ```
  目标（删除两行本地变量；颜色后续步骤改用令牌）：
  ```scss
  <style scoped lang="scss">
  ```
- [ ] **Step 2: search-header 边框/字号/圆角令牌化（712-765）。** 将 716 `border-bottom: 1px solid #f0f0f0` → `border-bottom: 1px solid var(--border)`；723 `font-size: 18px` → `font-size: var(--font-lg)`（16，搜索图标无 18 令牌，归并到 lg）；734 input `font-size: 15px` → `font-size: var(--font-base)`（14）；735 `color: rgba(0,0,0,0.85)` → `color: var(--text-1)`；739 placeholder `color: #bfbfbf` → `color: var(--text-disabled)`；744 clear `font-size: 14px` → `var(--font-base)`；757 hint `font-size: 10px` → `var(--font-xs)`（11，最小档归并）；758 `color: rgba(0,0,0,0.3)` → `var(--text-3)`；761 `border: 1px solid #e8e8e8` → `border: 1px solid var(--border)`；762 `border-radius: 4px` → `var(--radius-sm)`。
- [ ] **Step 3: search-body 滚动条与 padding（768-780）。** 771 `padding: 6px 0` 保留（非主题量，可选改 `var(--space-2xs2) 0` 不强制）；778 `background: #ddd` → `background: var(--border-strong)`。
- [ ] **Step 4: section / section-title 令牌化（782-810）。** 785 `border-top: 1px solid #f5f5f5` → `var(--border)`；792 `font-size: 11px` → `var(--font-xs)`；794 `color: rgba(0,0,0,0.4)` → `var(--text-3)`；807 圆点 `background: $brand` → `background: var(--color-primary)`。
- [ ] **Step 5: search-item 基础态字号/颜色/圆角（812-844，active/hover 留到 Task 4）。** 831 item-name `font-size: 13.5px` → `font-size: var(--font-sm2)`（13）；832 `color: rgba(0,0,0,0.85)` → `var(--text-1)`；837 tag `font-size: 11px` → `var(--font-xs)`；838 `color: $brand` → `var(--color-primary)`；839 `background: rgba(255,103,0,0.06)` → `background: var(--color-primary-light)`；842 `border-radius: 3px` → `var(--radius-sm)`；851 sub-item `font-size: 13px` → `var(--font-sm2)`。
- [ ] **Step 6: module-header 字号/颜色（860-905，active/hover 留到 Task 4）。** 878 expand-icon `font-size: 10px` → `var(--font-xs)`；879 `color: rgba(0,0,0,0.4)` → `var(--text-3)`；890 module-name `font-size: 13.5px` → `var(--font-sm2)`；891 `color: rgba(0,0,0,0.85)` → `var(--text-1)`；897 count `font-size: 10px` → `var(--font-xs)`；898 `color: rgba(0,0,0,0.4)` → `var(--text-3)`；900 `background: #f5f5f5` → `var(--bg-muted)`；903 `border-radius: 8px` → `var(--radius-lg)`。
- [ ] **Step 7: search-empty 与 result-group-title 令牌化（907-951）。** 911 `font-size: 13.5px` → `var(--font-sm2)`；915 icon `color: rgba(0,0,0,0.12)` → `var(--text-disabled)`；921 `font-size: 13.5px` → `var(--font-sm2)`；921 `color: rgba(0,0,0,0.45)` → `var(--text-2)`；926 empty-keyword `color: $brand` → `var(--color-primary)`；931 hint `font-size: 12px` → `var(--font-sm)`；932 `color: rgba(0,0,0,0.25)` → `var(--text-3)`；941 group-title `font-size: 11px` → `var(--font-xs)`；944 `color: rgba(0,0,0,0.35)` → `var(--text-3)`；948 `border-top: 1px solid #f5f5f5` → `var(--border)`。
- [ ] **Step 8: 静态断言本地变量与裸橙已大幅消除。** 运行 `cd web; rg -n "\$brand|\$brand-bg|#FF6700|rgba\(255, ?103, ?0" src/components/GlobalSearch.vue`。期望仅剩 Task 4 待处理的 active/hover/渐变/match-hl 引用（recommended-item 955、match-hl 959-960、recommend-reason 977-978、item hover/active 821-825、868-872、973）；section-title/empty/tag/icon 行应为 0 命中。
- [ ] **Step 9: 构建。** 运行 `cd web; npm run build`。期望以 `built in` 结尾、无报错（SCSS `$brand` 未定义会在此处暴露，确保全部已替换）。
- [ ] **Step 10: 提交。** `cd web; git add src/components/GlobalSearch.vue; git commit -m "GlobalSearch 删本地 brand 变量并迁基础色/字号/间距/圆角令牌`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed`。

---

### Task 4: 修 active 边框位移 hack → 整行底色 + 无位移指示条

**Files:**
- Modify: `web/src/components/GlobalSearch.vue` 812-905（search-item / sub-item / module-header 的 hover/active）

- [ ] **Step 1: 读取当前 search-item hover/active（820-828）。** 当前：
  ```scss
  &:hover {
    background: $brand-bg;
  }

  &.active {
    background: $brand-bg;
    border-left: 3px solid $brand;
    padding-left: 15px; // 原18px减3px
  }
  ```
- [ ] **Step 2: 用伪元素指示条替代 border-left，消除 padding 补偿。** 改 search-item 容器为相对定位并改写 hover/active（817 行附近 `position: relative` 若无则补）：
  ```scss
  .search-item {
    position: relative;
    /* …其余不变… */
    &:hover {
      background: var(--bg-muted);
    }

    &.active {
      background: var(--color-primary-light);
    }

    &.active::before {
      content: '';
      position: absolute;
      left: 0;
      top: 0;
      bottom: 0;
      width: 3px;
      background: var(--color-primary);
    }
  ```
  说明：指示条用绝对定位伪元素，不占据流内宽度，故删除 827 的 `padding-left: 15px` 补偿；hover 用中性 `--bg-muted` 与 active 的 `--color-primary-light` 拉开层级（解决 hover 与推荐/选中底色雷同）。
- [ ] **Step 3: 删除 sub-item active 的 padding 补偿（854-857）。** 当前：
  ```scss
  &.active {
    padding-left: 37px;
  }
  ```
  目标：整段删除（sub-item 的 `padding-left: 40px` 在 847 保持不变；指示条由 `.search-item.active::before` 统一提供，子项缩进不再需要单独补偿）。
- [ ] **Step 4: 改写 module-header hover/active（867-875）。** 当前：
  ```scss
  &:hover {
    background: $brand-bg;
  }

  &.active {
    background: $brand-bg;
    border-left: 3px solid $brand;
    padding-left: 15px; // 原18px减3px
  }
  ```
  目标（同样改伪元素指示条，860 容器补 `position: relative`）：
  ```scss
  &:hover {
    background: var(--bg-muted);
  }

  &.active {
    background: var(--color-primary-light);
  }

  &.active::before {
    content: '';
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: var(--color-primary);
  }
  ```
- [ ] **Step 5: 静态断言无 border-left 位移 hack 残留。** 运行 `cd web; rg -n "border-left: 3px|padding-left: 15px|padding-left: 37px" src/components/GlobalSearch.vue`。期望 0 命中。
- [ ] **Step 6: 构建。** `cd web; npm run build`。期望 `built in` 结尾、无报错。
- [ ] **Step 7: preview 验证位移消失。** 用 `preview_start` 起本地服务（端口见 stotop-dev-workflow，前端 9000），登录后 `Ctrl+K` 打开命令面板；`preview_screenshot` 截浏览模式空态，肉眼确认：上下方向键在模块/子项间移动时，行内文字左缘不再随 active 切换左右抖动（旧 3px 边框补偿已消除），active 行有左侧 3px 橙色指示条 + `--color-primary-light` 整行底色。
- [ ] **Step 8: 提交。** `cd web; git add src/components/GlobalSearch.vue; git commit -m "GlobalSearch active 改伪元素指示条消除边框位移`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed`。

---

### Task 5: 区分“推荐 vs hover”并修推荐项/高亮/标签令牌（955/958-984）

**Files:**
- Modify: `web/src/components/GlobalSearch.vue` 953-984（recommended-item / match-hl / recommended-icon / recommend-reason）

- [ ] **Step 1: 读取当前 953-984。** 当前推荐渐变（953-956）：
  ```scss
  .recommended-item {
    position: relative;
    background: linear-gradient(90deg, rgba(255, 103, 0, 0.03) 0%, transparent 100%);
  }
  ```
  问题：`rgba(255,103,0,0.03)` 与 hover 的 `#FFF1E8` 几乎无差，推荐区辨识度极低。
- [ ] **Step 2: 推荐项改用左侧色条 + 浅底，区别于 hover/active。** 目标：
  ```scss
  .recommended-item {
    position: relative;
    background: var(--color-primary-light);
    box-shadow: inset 2px 0 0 var(--color-primary);
  }
  .recommended-item:hover {
    background: var(--bg-muted);
  }
  ```
  说明：推荐项静态即用 `--color-primary-light` + 内嵌左色条（与 active 的伪元素指示条视觉同源但常驻，表达“系统推荐”而非“当前选中”）；其 hover 落到 `--bg-muted`，与 active 的 `--color-primary-light` 仍可区分。三态层级：普通(透明) < hover(`--bg-muted`) < 推荐常驻/选中(`--color-primary-light` + 橙条)。
- [ ] **Step 3: match-hl 高亮令牌化（958-964）。** 当前：
  ```scss
  :deep(.match-hl) {
    background: rgba(255, 103, 0, 0.15);
    color: $brand;
    padding: 0 1px;
    border-radius: 2px;
    font-weight: 500;
  }
  ```
  目标：
  ```scss
  :deep(.match-hl) {
    background: var(--color-primary-light);
    color: var(--color-primary);
    padding: 0 1px;
    border-radius: var(--radius-sm);
    font-weight: 500;
  }
  ```
- [ ] **Step 4: recommended-icon 与 recommend-reason 令牌化（966-984）。** 967 item-icon `font-size: 13px` → `var(--font-sm2)`；973 `color: $brand` → `var(--color-primary)`；977-981 recommend-reason：`background: $brand-bg` → `var(--color-primary-light)`，`color: $brand` → `var(--color-primary)`，`font-size: 10px` → `var(--font-xs)`，`border-radius: 2px` → `var(--radius-sm)`。
- [ ] **Step 5: 静态断言 scoped 段已彻底无裸橙/本地变量。** 运行 `cd web; rg -n "\$brand|#FF6700|rgba\(255, ?103, ?0|#FFF1E8" src/components/GlobalSearch.vue`。期望 0 命中。
- [ ] **Step 6: 构建。** `cd web; npm run build`。期望 `built in` 结尾。
- [ ] **Step 7: preview 验证三态可辨。** 复用已起 preview，打开命令面板空态（需存在推荐项；若无推荐数据，先访问 2-3 个不同菜单制造导航链，再重开面板）；`preview_screenshot` 对比“为你推荐”区与下方“最近使用/全部模块”：确认推荐项有常驻浅橙底+左橙条；鼠标 hover 一个非推荐普通项，其底色为中性灰（`--bg-muted`），与推荐浅橙明显不同；键盘选中态为浅橙+伪元素橙条。截图留证。
- [ ] **Step 8: 提交。** `cd web; git add src/components/GlobalSearch.vue; git commit -m "GlobalSearch 区分推荐/hover/选中三态并令牌化高亮与推荐标签`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed`。

---

### Task 6: 修焦点视觉反馈（718-720）与搜索/浏览双索引切换聚焦同步（428-443）

**Files:**
- Modify: `web/src/components/GlobalSearch.vue` 16-30（input 模板）、428-443（totalNavigableItems/moveUp/moveDown）、609-621（keyword/visible watch）、718-720（focus-within）

- [ ] **Step 1: 焦点视觉反馈令牌化并增强（718-720）。** 当前：
  ```scss
  &:focus-within .search-icon {
    color: rgba(0, 0, 0, 0.45);
  }
  ```
  目标（聚焦时图标转主色，并给 header 增加底部主色描边，明确“输入已聚焦”）：
  ```scss
  &:focus-within {
    border-bottom-color: var(--color-primary);
  }

  &:focus-within .search-icon {
    color: var(--color-primary);
  }
  ```
  配套：将 716 的静态 `border-bottom: 1px solid var(--border)`（Task 3 Step 2 已迁）确认存在，使聚焦时颜色过渡有基线；714-727 段已有 `.search-icon { transition: color 0.2s }`（726），无需新增过渡。
- [ ] **Step 2: 读取双索引切换源 428-443，定位聚焦未重置问题。** 当前 `totalNavigableItems`（428-431）搜索态返回 `filteredResults.length`、浏览态返回 `browseItemsList.length`；`moveUp/moveDown`（435-443）以 `activeIndex` 在该总数内增减。已有 watch（610-612）在 `keyword` 变化时 `activeIndex.value = 0`。问题：keyword 从有→无（清空）切回浏览模式、或浏览模式展开/折叠致 `browseItemsList` 长度变化时，`activeIndex` 可能越界且输入框焦点不一定回到 input，键盘导航“看不见选中”。
- [ ] **Step 3: keyword watch 同步重置选中并回焦输入框（610-612）。** 当前：
  ```js
  // keyword 变化时重置 activeIndex
  watch(keyword, () => {
    activeIndex.value = 0
  })
  ```
  目标：
  ```js
  // keyword 变化时重置 activeIndex，并把焦点保持在输入框，保证两套索引切换后键盘导航连续
  watch(keyword, () => {
    activeIndex.value = 0
    nextTick(() => {
      searchInputRef.value?.focus({ preventScroll: true })
    })
  })
  ```
- [ ] **Step 4: activeIndex 越界自愈（在 moveUp 上方新增 watch totalNavigableItems）。** 在 433-434（`// ---- 键盘导航 ----` 注释下、`function moveUp` 之前）插入：
  ```js
  // 列表长度变化（清空搜索 / 展开折叠模块）后，夹紧 activeIndex 避免越界丢失高亮
  watch(totalNavigableItems, (total) => {
    if (activeIndex.value > total - 1) {
      activeIndex.value = Math.max(0, total - 1)
    }
  })
  ```
- [ ] **Step 5: moveUp/moveDown 边界确认（无需改逻辑，仅核对）。** 核对 435-443：`moveUp` 在 `activeIndex>0` 时自减、`moveDown` 在 `<total-1` 时自增，配合 Step 4 的夹紧，越界已不可达。确认 `scrollIntoView`（523-530）按 `[data-index]` 滚动，模板中搜索态分组项 `:data-index="getSearchIndex(item)"`（54）与浏览态各项 `:data-index="getGlobalIndex(...)"` 一致，键盘高亮可正确滚入视口。
- [ ] **Step 6: 构建 + 类型基线确认。** 运行 `cd web; npm run build`（期望 `built in`），再运行 `cd web; npm run type-check`（vue-tsc 基线红，仅确认输出中**不含** `GlobalSearch.vue` 的新增报错行；用 `rg "GlobalSearch.vue"` 过滤本文件确认无新错）。
- [ ] **Step 7: preview 键盘导航验证。** 复用 preview：打开面板→在输入框 `preview_fill` 输入“结算”等关键词→连按 ↓ 数次→`preview_screenshot` 确认高亮随键移动且滚动跟随→`preview_fill` 清空关键词切回浏览模式→立即按 ↓→截图确认选中从顶部第一项开始（未越界、焦点仍在输入框、可继续键盘导航）；Tab 在“推荐/最近/模块”三区间跳转（cycleSection 507-521）正常；聚焦输入框时搜索图标与底部描边变橙。
- [ ] **Step 8: 提交。** `cd web; git add src/components/GlobalSearch.vue; git commit -m "GlobalSearch 修焦点视觉反馈与搜索浏览双索引聚焦同步`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed`。

---

### Task 7: slice(0,20) 硬截断 → “显示更多”按钮 + 截断提示

**Files:**
- Modify: `web/src/components/GlobalSearch.vue` 42-80（结果模板）、200-205（state）、327-378（filteredResults/groupedResults）、609-612（watch keyword）

- [ ] **Step 1: 新增展开状态与上限常量（200-205 附近）。** 当前 200-204：
  ```js
  const keyword = ref('')
  const activeIndex = ref(0)
  const searchInputRef = ref<HTMLInputElement | null>(null)
  const scrollContainerRef = ref<HTMLElement | null>(null)
  const expandedModules = ref<string[]>([])
  ```
  目标（追加上限常量与展开开关）：
  ```js
  const keyword = ref('')
  const activeIndex = ref(0)
  const searchInputRef = ref<HTMLInputElement | null>(null)
  const scrollContainerRef = ref<HTMLElement | null>(null)
  const expandedModules = ref<string[]>([])
  /** 搜索结果默认显示上限；超出后折叠并提供“显示更多” */
  const SEARCH_LIMIT = 20
  const showAllResults = ref(false)
  ```
- [ ] **Step 2: 拆分“全部匹配”与“受限结果”（327-346）。** 当前 `filteredResults` 末尾 `.slice(0, 20)`（345）。改为先算全量 `allMatched`，再按开关切片。将 328-346 改为：
  ```js
  /** 全部模糊匹配结果（未截断），供计数与“显示更多”使用 */
  const allMatchedResults = computed<FlatMenuItem[]>(() => {
    const kw = keyword.value.trim()
    if (!kw) return []
    const lowerKw = kw.toLowerCase()
    return allFlatMenus.value
      .filter(item =>
        pinyinMatch(item.name, kw) ||
        item.code.toLowerCase().includes(lowerKw) ||
        pinyinMatch(item.moduleName, kw)
      )
      .sort((a, b) => getMatchScore(b, lowerKw) - getMatchScore(a, lowerKw))
  })

  /** 实际渲染的结果：默认截断到 SEARCH_LIMIT，点击“显示更多”后展开全部 */
  const filteredResults = computed<FlatMenuItem[]>(() => {
    return showAllResults.value
      ? allMatchedResults.value
      : allMatchedResults.value.slice(0, SEARCH_LIMIT)
  })

  /** 被折叠隐藏的结果条数（>0 时显示“显示更多”） */
  const hiddenResultCount = computed(() =>
    Math.max(0, allMatchedResults.value.length - filteredResults.value.length)
  )
  ```
  注意：原 `getMatchScore`（348-355）排序内联逻辑等价改为 `getMatchScore(b,...) - getMatchScore(a,...)`，行为不变；`groupedResults`（358-378）/`getSearchIndex`（380-382）/`totalNavigableItems`（429）继续基于 `filteredResults` 工作，故键盘可导航范围 = 当前可见结果，符合预期。
- [ ] **Step 3: keyword 变化时重置展开开关（610-612，与 Task 6 Step 3 合并）。** 在 Task 6 改写后的 watch(keyword) 内追加一行：
  ```js
  watch(keyword, () => {
    activeIndex.value = 0
    showAllResults.value = false
    nextTick(() => {
      searchInputRef.value?.focus({ preventScroll: true })
    })
  })
  ```
- [ ] **Step 4: 模板加“显示更多”按钮（74-80 之间，result-list 结束后、search-empty 之前）。** 当前 74-75：
  ```html
        </div>
        <div class="search-empty" v-else>
  ```
  目标（在 `</div>`(result-list 闭合，74) 后、`search-empty`(75) 前插入）：
  ```html
        </div>
        <div
          v-if="hiddenResultCount > 0"
          class="search-more"
          @click="showAllResults = true"
        >
          显示更多 {{ hiddenResultCount }} 条结果
        </div>
        <div class="search-empty" v-else-if="!filteredResults.length">
  ```
  说明：原 75 的 `v-else` 改为 `v-else-if="!filteredResults.length"`，因为新增的 `search-more` 分支位于 `filteredResults.length` 为真的同一 `template v-if="keyword.trim()"`（42）作用域内；空态仍只在无结果时出现。
- [ ] **Step 5: 新增 search-more 样式（scoped 段末尾，984 之后）。** 追加：
  ```scss
  .search-more {
    padding: var(--space-sm8) var(--space-lg16);
    text-align: center;
    font-size: var(--font-sm);
    color: var(--color-primary);
    cursor: pointer;
    border-top: 1px solid var(--border);

    &:hover {
      background: var(--bg-muted);
    }
  }
  ```
- [ ] **Step 6: 静态断言硬截断常量已移除内联。** 运行 `cd web; rg -n "slice\(0, 20\)" src/components/GlobalSearch.vue`。期望 0 命中（已改为 `slice(0, SEARCH_LIMIT)`）。再 `cd web; rg -n "SEARCH_LIMIT|hiddenResultCount|showAllResults" src/components/GlobalSearch.vue`，期望多处命中确认接线完整。
- [ ] **Step 7: 构建。** `cd web; npm run build`。期望 `built in` 结尾、无报错。
- [ ] **Step 8: preview 验证“显示更多”。** 复用 preview：输入易命中大量结果的关键词（如“管理”或单字母拼音首字母“b”）→`preview_screenshot` 确认列表底部出现“显示更多 N 条结果”→`preview_click` 该按钮→截图确认全部结果展开、按钮消失、键盘 ↓ 可继续滚动到新出现项。
- [ ] **Step 9: 提交。** `cd web; git add src/components/GlobalSearch.vue; git commit -m "GlobalSearch 搜索结果硬截断改为显示更多与剩余条数提示`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed`。

---

### Task 8: TopBar 搜索触发器与残留橙色硬编码迁令牌（支撑运行时验证）

**Files:**
- Modify: `web/src/layouts/TopBar.vue` 425-429（workhub active）、557-564（todo 徽章）、566-614（topbar-search）、626-634（公告脉冲点）

- [ ] **Step 1: 读取并迁 topbar-search 残留橙色（609-613）。** 当前：
  ```scss
  &:hover {
    background: rgba(255, 255, 255, 0.18);
    border-color: rgba(255, 103, 0, 0.5);
    color: rgba(255, 255, 255, 0.9);
  }
  ```
  目标（hover 边框用主色边框令牌；572 `border-radius: 14px` → `var(--radius-pill)`）：
  ```scss
  &:hover {
    background: rgba(255, 255, 255, 0.18);
    border-color: var(--color-primary-border);
    color: rgba(255, 255, 255, 0.9);
  }
  ```
  并将 572 `border-radius: 14px;` → `border-radius: var(--radius-pill);`；604 kbd `border-radius: 3px` → `var(--radius-sm)`。说明：白色半透明（顶栏深底上的玻璃感）属外壳态，保留；仅主色相关迁令牌以满足“改 --color-primary 全局变色”。
- [ ] **Step 2: workhub active 主色迁令牌（425-429）。** 当前：
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
    color: var(--color-primary-hover);
    font-weight: 600;
  }
  ```
  说明：`#FF8533` 为浅亮橙，最接近权威 `--color-primary-hover #FF6700`；深底上 `--color-primary-light` 浅橙作为 active 底（与命令面板 active 同源）。
- [ ] **Step 3: todo 徽章与公告脉冲点迁令牌（558、631）。** 558 `background: #FF6700` → `background: var(--color-primary)`；631 `background: #FF6700` → `background: var(--color-primary)`。
- [ ] **Step 4: 静态断言 TopBar 已无裸橙。** 运行 `cd web; rg -n "#FF6700|#FF8533|rgba\(255, ?103, ?0" src/layouts/TopBar.vue`。期望 0 命中。
- [ ] **Step 5: 构建。** `cd web; npm run build`。期望 `built in` 结尾。
- [ ] **Step 6: 运行时令牌验证（改 --color-primary 看全局变色）。** preview 打开应用后，用 `preview_eval` 执行 `document.documentElement.style.setProperty('--color-primary', '#0066FF')`；`preview_screenshot` 顶栏 + 打开命令面板：确认搜索框 hover 边框、工作台 active、待办徽章、公告脉冲点、命令面板 active 指示条/推荐色条/高亮全部变蓝（证明无遗漏硬编码）；随后 `preview_eval` 还原 `removeProperty('--color-primary')`。
- [ ] **Step 7: 提交。** `cd web; git add src/layouts/TopBar.vue; git commit -m "TopBar 搜索触发器与徽章/公告残留橙色迁令牌`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed`。

---

### Task 9: 阶段整体回归（静态断言 + 全屏逐屏 preview）

**Files:**
- Read/验证（无代码改动；如发现遗漏回到对应 Task 修复后再过）：`web/src/components/GlobalSearch.vue`、`web/src/layouts/TopBar.vue`

- [ ] **Step 1: 全文件零硬编码断言（GlobalSearch）。** 运行 `cd web; rg -n "\$brand|#FF6700|#FFF1E8|rgba\(255, ?103, ?0|13\.5px|: 10px|: 11px|border-radius: 12px|0 16px 48px|slice\(0, 20\)" src/components/GlobalSearch.vue`。期望 0 命中（字号已全部令牌化，圆角/阴影/截断已替换）。
- [ ] **Step 2: 令牌引用计数复核。** 运行 `cd web; rg -c "var\(--" src/components/GlobalSearch.vue`。期望命中数明显 >0（数十处），确认全面令牌化；再 `cd web; rg -n "var\(--font-2xl|var\(--font-xl\b" src/components/GlobalSearch.vue` 期望 0（命令面板未用到 18/24 档，避免误引）。
- [ ] **Step 3: 构建 + 类型基线。** `cd web; npm run build`（期望 `built in`）；`cd web; npm run type-check` 后 `rg "GlobalSearch.vue|TopBar.vue"` 过滤，确认两文件无**新增** vue-tsc 报错（基线红其余报错不作门禁）。
- [ ] **Step 4: 逐屏 preview 终检（含键盘导航）。** 复用 preview，依次截图存证：(a) 命令面板空态浏览模式（推荐/最近/全部模块三区辨识清晰）；(b) ↓↑ 键盘导航无位移、高亮随滚动跟随；(c) Tab 三区跳转；(d) 输入关键词→分组结果→“显示更多”展开；(e) 聚焦输入框图标+底边变主色；(f) `preview_eval` 改 `--color-primary` 全局变色后还原。每屏 `preview_screenshot` 留图。
- [ ] **Step 5: 收尾提交（若 Step 1-3 触发任何修补）。** 如本 Task 未改动代码则跳过提交；若有修补：`cd web; git add src/components/GlobalSearch.vue src/layouts/TopBar.vue; git commit -m "阶段4 命令面板令牌化回归修补`n`nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。期望 `1 file changed` 或 `2 files changed`。

---

### Critical Files for Implementation
- E:/STOTOP_Fable/web/src/components/GlobalSearch.vue
- E:/STOTOP_Fable/web/src/layouts/TopBar.vue
- E:/STOTOP_Fable/web/src/stores/permission.ts
- E:/STOTOP_Fable/web/src/stores/theme.ts
- E:/STOTOP_Fable/web/src/styles/variables.scss