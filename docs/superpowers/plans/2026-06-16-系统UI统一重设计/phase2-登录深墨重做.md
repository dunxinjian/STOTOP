> 前置假设：阶段0已就绪——`--color-primary` 等权威令牌已作为 CSS 自定义属性（:root）定义并可在 `login/index.vue`、`OrgSelectModal.vue` 中以 `var(--token)` 引用。本阶段不再重复定义令牌，只消费它们。
> 当前事实（已读真实代码确认）：`web/src/views/login/index.vue` 共 1475 行，`@use '@/styles/variables.scss' as *`；目前 `#1677ff` 在 login 内 0 命中、在 `OrgSelectModal.vue` 命中 2 处（20、43）；login 内含 `#FF6700/#FF8533/#FF9A44/#FFAA44/#35b65a/rgba(255,103,0,…)` 共 36 处硬编码。`web/src/styles` 下尚无 `:root` 令牌块（由阶段0引入）。
> 验证命令统一用 PowerShell 语法（`;` 串联、`$null` 非 `/dev/null`）。构建从 `web` 目录执行 `npm run build`。每个 Task 末尾 `git add` 指定文件 + `git commit`，message 中文，结尾固定一行 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`。

---

### Task 1: 删除模板装饰层（四季粒子 / 物流路线 SVG / 几何装饰环）并解绑 season class/style

**Files:**
- Modify `web/src/views/login/index.vue`（模板 7：`:class`/`:style` 绑定；9-22：season-particles；23-37：logistics-route-map；64-71：brand-decoration）

- [ ] **Step 1: 删除四季粒子层（模板 9-22）。** 当前：
```html
        <!-- 季节粒子动画层 -->
        <div class="season-particles" :class="seasonTheme.particleClass" aria-hidden="true">
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
          <span class="particle"></span>
        </div>
```
目标：整段删除（连同上方 `<!-- 季节粒子动画层 -->` 注释）。

- [ ] **Step 2: 删除物流路线 SVG（模板 23-37）。** 当前：
```html
        <div class="logistics-route-map" aria-hidden="true">
          <svg viewBox="0 0 720 220" focusable="false">
            <path class="route-path" d="M18 166 C118 84, 202 196, 312 108 S494 60, 682 126" />
            <path class="route-path route-path--secondary" d="M92 42 C192 82, 254 28, 348 82 S508 174, 648 58" />
            <line class="warehouse-line" x1="36" y1="198" x2="210" y2="198" />
            <line class="warehouse-line" x1="36" y1="178" x2="210" y2="178" />
            <line class="warehouse-line" x1="52" y1="160" x2="52" y2="210" />
            <line class="warehouse-line" x1="96" y1="160" x2="96" y2="210" />
            <line class="warehouse-line" x1="140" y1="160" x2="140" y2="210" />
            <circle class="hub-node" cx="18" cy="166" r="5" />
            <circle class="hub-node" cx="312" cy="108" r="5" />
            <circle class="hub-node" cx="682" cy="126" r="5" />
            <circle class="hub-node" cx="648" cy="58" r="5" />
          </svg>
        </div>
```
目标：整段删除。

- [ ] **Step 3: 删除几何装饰环（模板 64-71）。** 当前：
```html
        <!-- 抽象几何装饰图形（左栏中下部） -->
        <div class="brand-decoration" aria-hidden="true">
          <div class="deco-ring deco-ring-1"></div>
          <div class="deco-ring deco-ring-2"></div>
          <div class="deco-ring deco-ring-3"></div>
          <div class="deco-line deco-line-1"></div>
          <div class="deco-line deco-line-2"></div>
        </div>
```
目标：整段删除（连同上方注释）。

- [ ] **Step 4: 解绑左品牌区的 season 动态 class 与 style（模板 7）。** 当前：
```html
      <div class="login-brand" :class="`brand-${seasonTheme.season}`" :style="brandStyle">
```
目标：
```html
      <div class="login-brand">
```

- [ ] **Step 5: 模板自检。** 运行 `rg -n "season-particles|logistics-route-map|brand-decoration|brandStyle|seasonTheme\.season|seasonTheme\.particleClass" web/src/views/login/index.vue`，期望输出 0 行（脚本里的 `seasonTheme` 变量将在 Task 2 删除，此处只校验模板引用已清零）。

- [ ] **Step 6: 提交。** 运行 `git add web/src/views/login/index.vue; git commit`，message：`登录页：删除四季粒子/物流路线SVG/几何装饰环模板`，附固定 Co-Authored-By 行。

---

### Task 2: 删除 seasonTheme 脚本依赖（import + brandStyle 注入）

**Files:**
- Modify `web/src/views/login/index.vue`（脚本 254：import；264-273：seasonTheme + brandStyle）

- [ ] **Step 1: 删除 seasonTheme 的 import（脚本 254）。** 当前：
```ts
import type { UserOrganizationDto } from '@/types/organization'
import { getCurrentSeasonTheme } from '@/utils/seasonTheme'

const router = useRouter()
```
目标（删掉第 254 行 import）：
```ts
import type { UserOrganizationDto } from '@/types/organization'

const router = useRouter()
```

- [ ] **Step 2: 删除 seasonTheme 取值与 brandStyle 注入（脚本 264-273）。** 当前：
```ts
// 季节主题（按当前月份自动切换）
const seasonTheme = getCurrentSeasonTheme()
const brandStyle = {
  '--season-bg-1': seasonTheme.bgGradient[0],
  '--season-bg-2': seasonTheme.bgGradient[1],
  '--season-bg-3': seasonTheme.bgGradient[2],
  '--season-glow-primary': seasonTheme.glowPrimary,
  '--season-glow-secondary': seasonTheme.glowSecondary,
  '--season-dot-1': seasonTheme.dotGradient[0],
  '--season-dot-2': seasonTheme.dotGradient[1],
} as Record<string, string>

const TRANSITION_STEP_TEXTS = [
```
目标（整块删除，保留下方 TRANSITION_STEP_TEXTS）：
```ts
const TRANSITION_STEP_TEXTS = [
```

- [ ] **Step 3: 脚本自检。** 运行 `rg -n "seasonTheme|getCurrentSeasonTheme|brandStyle|--season-" web/src/views/login/index.vue`，期望 0 行（含 SCSS 内 `--season-*` 兜底变量将在 Task 3-5 一并清除，此步先确认脚本段无残留 import/变量）。

- [ ] **Step 4: 提交。** 运行 `git add web/src/views/login/index.vue; git commit`，message：`登录页：移除 seasonTheme import 与 brandStyle 注入`，附固定 Co-Authored-By 行。

---

### Task 3: 删除粒子/路线/装饰环 SCSS 及全部 keyframe

**Files:**
- Modify `web/src/views/login/index.vue`（SCSS 676-688 season-particles；690-726 logistics-route-map；728-915 四季粒子样式与 8 个 keyframe；929-997 brand-decoration）

- [ ] **Step 1: 删除 `.season-particles` 基样式（676-688）。** 当前：
```scss
.season-particles {
  position: absolute;
  inset: 0;
  overflow: hidden;
  pointer-events: none;
  z-index: 0;

  .particle {
    position: absolute;
    display: block;
    will-change: transform, opacity;
  }
}
```
目标：整块删除（连同上方 686 注释区块标题 `// 季节粒子动画层`）。

- [ ] **Step 2: 删除 `.logistics-route-map`（690-726）。** 当前为 `.logistics-route-map { … .route-path / .route-path--secondary / .hub-node / .warehouse-line … }` 整块（含 `stroke: var(--season-glow-primary, …)`、`fill: var(--season-dot-1, #FF6700)`）。目标：整块删除。

- [ ] **Step 3: 删除四季粒子样式与 keyframe（728-915）。** 范围覆盖 `.particles-spring` / `@keyframes spring-rise-faint` / `@keyframes spring-rise` / `.particles-summer` / `@keyframes summer-twinkle-faint` / `@keyframes summer-twinkle` / `.particles-autumn` / `@keyframes autumn-fall-faint` / `@keyframes autumn-fall` / `.particles-winter` / `@keyframes winter-snow-faint` / `@keyframes winter-snow`（连同 `// --- 春/夏/秋/冬 ---` 注释）。目标：整段（728-915）删除。

- [ ] **Step 4: 删除 `.brand-decoration`（929-997）。** 当前为区块标题 `// 抽象几何装饰图形（左栏中下部，增加层次与科技感）` 起、含 `.deco-ring / .deco-ring-1/2/3 / .deco-line / .deco-line-1/2`（均用 `var(--season-glow-primary, rgba(255,103,0,0.18))`）的整块（926-997，含上方注释横线）。目标：整段删除。

- [ ] **Step 5: SCSS 自检。** 运行 `rg -n "particles-|\.particle|route-path|hub-node|warehouse-line|brand-decoration|deco-ring|deco-line|spring-rise|summer-twinkle|autumn-fall|winter-snow" web/src/views/login/index.vue`，期望 0 行。

- [ ] **Step 6: 提交。** 运行 `git add web/src/views/login/index.vue; git commit`，message：`登录页：删除粒子/路线/装饰环SCSS与全部keyframe`，附固定 Co-Authored-By 行。

---

### Task 4: 左品牌区改静态深墨 + Logo 纯白 + 渐变小圆点改实色令牌

**Files:**
- Modify `web/src/views/login/index.vue`（SCSS 557-596 `.login-brand` 含 ::before/::after；611-628 `.brand-logo-img`/`.brand-logo-text`；660-671 `.brand-feature-dot`）

- [ ] **Step 1: `.login-brand` 背景改静态深墨、删除 ::before/::after 光斑（557-596）。** 当前：
```scss
.login-brand {
  flex: 0 0 45%;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 56px 52px 40px;
  position: relative;
  overflow: hidden;
  // 季节主题渐变背景（变量由脚本注入）
  background: linear-gradient(
    145deg,
    var(--season-bg-1, #1E1F26) 0%,
    var(--season-bg-2, #23242D) 60%,
    var(--season-bg-3, #1A1B22) 100%
  );
  transition: background 0.6s ease;

  // 右上装饰光晓
  &::before {
    content: '';
    position: absolute;
    width: 500px;
    height: 500px;
    background: radial-gradient(ellipse at 90% 10%, var(--season-glow-primary, rgba(255, 103, 0, 0.18)), transparent 60%);
    top: -120px;
    right: -80px;
    pointer-events: none;
  }

  &::after {
    content: '';
    position: absolute;
    width: 350px;
    height: 350px;
    background: radial-gradient(ellipse at 10% 90%, var(--season-glow-secondary, rgba(99, 102, 241, 0.12)), transparent 55%);
    bottom: -80px;
    left: -60px;
    pointer-events: none;
  }
}
```
目标（实色深墨、去渐变与光斑伪元素、去过渡）：
```scss
.login-brand {
  flex: 0 0 45%;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 56px 52px 40px;
  position: relative;
  overflow: hidden;
  // 静态精致深墨外壳，跟随权威外壳令牌
  background: var(--topbar-ink, #1F2430);
}
```

- [ ] **Step 2: Logo 纯白文字、去渐变剪裁与橙色投影（611-628）。** 当前 `.brand-logo-img` 含 `filter: drop-shadow(0 4px 16px rgba(255, 103, 0, 0.25));`，`.brand-logo-text` 含渐变剪裁。目标：
```scss
.brand-logo-img {
  max-height: 56px;
  max-width: 240px;
  object-fit: contain;
}

.brand-logo-text {
  font-size: 36px;
  font-weight: 800;
  color: #ffffff;
  letter-spacing: 5px;
}
```
（删除 `.brand-logo-img` 的 `filter` 行；`.brand-logo-text` 删除 `background`/`-webkit-background-clip`/`-webkit-text-fill-color`/`background-clip`/`text-shadow` 共 5 行，仅保留纯白文字。）

- [ ] **Step 3: 业务亮点小圆点改实色橙令牌（660-671）。** 当前：
```scss
.brand-feature-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: linear-gradient(
    135deg,
    var(--season-dot-1, #FF6700),
    var(--season-dot-2, #FFAA44)
  );
  flex: 0 0 auto;
  box-shadow: 0 0 6px var(--season-dot-1, rgba(255, 103, 0, 0.5));
}
```
目标（实色、去发光）：
```scss
.brand-feature-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-primary, #E85E00);
  flex: 0 0 auto;
}
```

- [ ] **Step 4: 校验左栏不再引用 season 兜底。** 运行 `rg -n "--season-" web/src/views/login/index.vue`，期望 0 行（确认左栏所有 season 变量已清除）。

- [ ] **Step 5: 提交。** 运行 `git add web/src/views/login/index.vue; git commit`，message：`登录页：左品牌区改静态深墨，Logo纯白，亮点圆点实色令牌`，附固定 Co-Authored-By 行。

---

### Task 5: 表单区焦点环改橙令牌 + 主按钮实色 + 顶部渐变线令牌化

**Files:**
- Modify `web/src/views/login/index.vue`（SCSS 1010-1021 `.login-form-panel::before`；1089-1099 输入框边框/焦点环；1112-1122 forgot-password hover；1126-1130 focus-visible；1132-1147 `.login-btn`；1190-1192 spin 点色；1463-1474 全局 `.login-btn.ant-btn-primary`）

- [ ] **Step 1: 表单区顶部渐变线改实色令牌（1010-1021）。** 当前：
```scss
  &::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 3px;
    background: linear-gradient(90deg, #FF6700, #FF9A44);
    border-radius: 0;
    z-index: 2;
  }
```
目标（实色橙，去渐变）：
```scss
  &::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 3px;
    background: var(--color-primary, #E85E00);
    z-index: 2;
  }
```

- [ ] **Step 2: 输入框边框 + 焦点环改令牌（1089-1099）。** 当前：
```scss
  :deep(.ant-input-affix-wrapper) {
    transition: border-color 0.3s ease, box-shadow 0.3s ease;
    border: 1px solid #d9d9d9;
    border-radius: 6px;
  }

  :deep(.ant-input-affix-wrapper:focus),
  :deep(.ant-input-affix-wrapper.ant-input-affix-wrapper-focused) {
    border-color: #FF6700;
    box-shadow: 0 0 0 2px rgba(255, 103, 0, 0.1);
  }
```
目标（边框/圆角/焦点环用令牌；焦点环由蓝/旧橙改权威橙 light）：
```scss
  :deep(.ant-input-affix-wrapper) {
    transition: border-color 0.3s ease, box-shadow 0.3s ease;
    border: 1px solid var(--border, #E6E8EB);
    border-radius: var(--radius-md, 6px);
  }

  :deep(.ant-input-affix-wrapper:focus),
  :deep(.ant-input-affix-wrapper.ant-input-affix-wrapper-focused) {
    border-color: var(--color-primary, #E85E00);
    box-shadow: 0 0 0 2px var(--color-primary-border, rgba(232, 94, 0, 0.30));
  }
```

- [ ] **Step 3: 忘记密码 hover 与 focus-visible 改令牌（1112-1122、1126-1130）。** 当前 `.forgot-password:hover { color: #FF6700; }`，以及：
```scss
.login-form :deep(*:focus-visible) {
  outline: 2px solid rgba(255, 103, 0, 0.5);
  outline-offset: 2px;
  border-radius: 4px;
}
```
目标：`.forgot-password:hover` 改 `color: var(--color-primary, #E85E00);`；focus-visible 改：
```scss
.login-form :deep(*:focus-visible) {
  outline: 2px solid var(--color-primary, #E85E00);
  outline-offset: 2px;
  border-radius: var(--radius-sm, 4px);
}
```

- [ ] **Step 4: 主按钮 scoped 样式改实色令牌（1132-1147）。** 当前：
```scss
.login-btn {
  width: 100%;
  height: 40px;
  font-size: 15px;
  font-weight: 600;
  background: linear-gradient(135deg, #FF6700, #FF8533);
  border: none;
  border-radius: 8px;
  letter-spacing: 2px;
  transition: all 0.2s ease;

  &:hover {
    transform: translateY(-1px);
    box-shadow: 0 4px 12px rgba(255, 103, 0, 0.3);
  }
}
```
目标（实色 + 去位移/辉光，hover 用 active/hover 令牌换底色）：
```scss
.login-btn {
  width: 100%;
  height: 40px;
  font-size: 15px;
  font-weight: 600;
  background: var(--color-primary, #E85E00);
  border: none;
  border-radius: var(--radius-lg, 8px);
  letter-spacing: 2px;
  transition: background 0.2s ease;

  &:hover {
    background: var(--color-primary-hover, #FF6700);
  }
}
```

- [ ] **Step 5: 全局穿透主按钮去渐变辉光（1463-1474）。** 当前（第二个无 scoped 的 `<style>` 块内）：
```scss
.login-form-panel .login-btn.ant-btn-primary {
  background: linear-gradient(135deg, #FF6700, #FF8533) !important;
  border: none;
  box-shadow: 0 4px 12px rgba(255, 103, 0, 0.3);

  &:hover,
  &:focus {
    background: linear-gradient(135deg, #FF8533, #FF9A44) !important;
    box-shadow: 0 6px 16px rgba(255, 103, 0, 0.4);
    transform: translateY(-1px);
  }
}
```
目标（实色令牌、去辉光与位移；hover/active 换底色）：
```scss
.login-form-panel .login-btn.ant-btn-primary {
  background: var(--color-primary, #E85E00) !important;
  border: none;

  &:hover,
  &:focus {
    background: var(--color-primary-hover, #FF6700) !important;
  }

  &:active {
    background: var(--color-primary-active, #C94E00) !important;
  }
}
```

- [ ] **Step 6: 加载遮罩 spin 点色改令牌（1190-1192）。** 当前 `:deep(.ant-spin-dot-item) { background-color: $color-primary; }`（`$color-primary` 现为 `#FF6700`）。目标：`background-color: var(--color-primary, #E85E00);`（与权威橙一致）。

- [ ] **Step 7: 表单区硬编码自检。** 运行 `rg -n "#FF6700|#FF8533|#FF9A44|rgba\(255, ?103, ?0" web/src/views/login/index.vue`，预期此时仅剩"过渡屏"相关的橙色（Task 6 处理），表单区无残留；记录命中行号供 Task 6 收尾比对。

- [ ] **Step 8: 提交。** 运行 `git add web/src/views/login/index.vue; git commit`，message：`登录页：焦点环改橙令牌、主按钮实色、表单顶线令牌化`，附固定 Co-Authored-By 行。

---

### Task 6: 过渡屏——去光斑、步骤切换 450→700ms、收尾橙色令牌化与结束淡入浅色

**Files:**
- Modify `web/src/views/login/index.vue`（脚本 281 `TRANSITION_STEP_DELAYS`；SCSS 1212-1237 `.login-transition-screen` 含 ::before/::after；1277-1296 transition-ring；1347-1372 stage-dot 完成/激活态；1387-1396 transition-dots）

- [ ] **Step 1: 步骤切换节奏 450→700ms（脚本 281）。** 当前：
```ts
const TRANSITION_STEP_DELAYS = [0, 450, 900, 1350] as const
const MIN_TRANSITION_DURATION = 1500
```
目标（每步间隔 700ms，4 步累计 2100ms；同步抬高最短停留时长以覆盖播放）：
```ts
const TRANSITION_STEP_DELAYS = [0, 700, 1400, 2100] as const
const MIN_TRANSITION_DURATION = 2300
```
说明：`startTransitionStepPlayback()`（391-399）按 `TRANSITION_STEP_DELAYS.slice(1)` 设定 setTimeout，改值即生效，无需改逻辑；`MIN_TRANSITION_DURATION` 抬到 2300 确保最后一步"即将进入工作台"可见。

- [ ] **Step 2: 过渡屏背景静态深墨 + 删除 ::before/::after 光斑（1212-1237）。** 当前：
```scss
.login-transition-screen {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #1A1B23;
  z-index: 9999;
  overflow: hidden;

  &::before,
  &::after {
    content: '';
    position: absolute;
    border-radius: 50%;
    background: rgba(255, 103, 0, 0.05);
  }

  &::before {
    width: 600px;
    height: 600px;
    top: -200px;
    right: -100px;
  }

  &::after {
    width: 400px;
    height: 400px;
    bottom: -150px;
    left: -100px;
  }
}
```
目标（深墨令牌、去两个光斑伪元素）：
```scss
.login-transition-screen {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--topbar-ink, #1F2430);
  z-index: 9999;
  overflow: hidden;
}
```

- [ ] **Step 3: 旋转环改单色橙令牌（1277-1296）。** 当前 `.transition-ring` 用 `conic-gradient(... rgba(255,103,0,…) / rgba(255,154,68,…) …)` 且 `box-shadow / &::after` 含橙发光。目标：保留旋转动画（`ring-spin` 属功能性 loading 指示，非装饰，保留），仅把配色换令牌——
```scss
  background: conic-gradient(
    from 0deg,
    transparent 0deg,
    var(--color-primary, #E85E00) 315deg,
    transparent 360deg
  );
```
并把 `box-shadow: 0 0 24px rgba(255, 103, 0, 0.18);` 删除（去辉光）；`&::after` 块（1289-1296 的 blur 橙光晕）删除。

- [ ] **Step 4: 步骤圆点完成/激活态改令牌（1347-1372）。** 当前完成态用 `rgba(53,182,90,…)`/`#35b65a`，激活态用 `#FF8533` 且含 `box-shadow: 0 0 16px rgba(255,103,0,0.35)`。目标：完成态绿色改 `var(--color-success, #2BA471)`（边框 `var(--color-success, #2BA471)`、底 `var(--color-success-light, #E7F5EF)`、`&::after` 实心 `var(--color-success, #2BA471)`）；激活态边框/实心改 `var(--color-primary, #E85E00)`、底 `var(--color-primary-light, #FFF3EA)`，删除 `box-shadow` 辉光行。

- [ ] **Step 5: 底部进度点改令牌（1387-1396）。** 当前 `.dot.active / .dot.done` 用 `background: #FF6700;`。目标：两处均改 `background: var(--color-primary, #E85E00);`（`.active` 保留 `transform: scale(1.3)`，`.done` 保留 `opacity: 0.5`）。

- [ ] **Step 6: 结束淡入浅色——leave 过渡改为淡出到浅底（1402-1410）。** 当前：
```scss
.screen-fade-enter-active,
.screen-fade-leave-active {
  transition: opacity 0.35s ease;
}

.screen-fade-enter-from,
.screen-fade-leave-to {
  opacity: 0;
}
```
目标（过渡结束时整屏淡出，露出其下浅色工作台/页面背景，实现"结束淡入浅色"——离场时长拉长更柔和）：
```scss
.screen-fade-enter-active {
  transition: opacity 0.35s ease;
}

.screen-fade-leave-active {
  transition: opacity 0.6s ease;
}

.screen-fade-enter-from,
.screen-fade-leave-to {
  opacity: 0;
}
```
说明：`runPostLoginTransition()`（437-441）在 redirect 分支不会把 `transitioning` 置回 false（直接 `router.push`），故卸载即离场——离场 fade 露出新路由的浅色 `--bg-page`，达成"结束淡入浅色"；org-selection 分支（445）会 `transitioning.value=false` 触发同一离场 fade，回到浅色表单区。无需新增逻辑。

- [ ] **Step 7: 过渡屏硬编码自检。** 运行 `rg -n "#1A1B23|#FF6700|#FF8533|#FF9A44|#35b65a|rgba\(255, ?103, ?0|rgba\(53, ?182, ?90" web/src/views/login/index.vue`，期望 0 行（全文件橙/绿/深墨硬编码清零，仅余 `var(--token)` 引用）。再运行 `rg -n "#ffffff|#FAFAFA|#FFFFFF|#F0F0F0|#1A1B22|#1E1F26" web/src/views/login/index.vue` 复核：剩余应仅为响应式 768px 段（1430-1449）的深色兜底与卡片白底等"非令牌覆盖范围"的中性色（这些不在本阶段令牌集内，保留即可，不报错）。

- [ ] **Step 8: 提交。** 运行 `git add web/src/views/login/index.vue; git commit`，message：`登录过渡屏：去光斑、步骤700ms、橙绿令牌化、结束淡入浅色`，附固定 Co-Authored-By 行。

---

### Task 7: OrgSelectModal 去 #1677ff → 令牌

**Files:**
- Modify `web/src/components/OrgSelectModal.vue`（模板 20 BankOutlined 图标色；43 CheckCircleFilled 选中图标色）

- [ ] **Step 1: 顶部说明图标改令牌（模板 20）。** 当前：
```html
        <BankOutlined :style="{ fontSize: '20px', color: '#1677ff' }" />
```
目标：
```html
        <BankOutlined :style="{ fontSize: '20px', color: 'var(--color-primary)' }" />
```

- [ ] **Step 2: 选中态对勾图标改令牌（模板 43）。** 当前：
```html
          <CheckCircleFilled v-if="selectedOrgId === org.orgId" class="selected-icon" :style="{ color: '#1677ff' }" />
```
目标：
```html
          <CheckCircleFilled v-if="selectedOrgId === org.orgId" class="selected-icon" :style="{ color: 'var(--color-primary)' }" />
```
说明：模板第 38 行 `<a-tag … color="blue">主组织</a-tag>` 的 `color="blue"` 是 antd 预设语义色（蓝色"主组织"徽标），非品牌色硬编码，本阶段不动；`.org-select-item` 的 `$color-primary`（133/137/138）在 SCSS 中已是变量引用，跟随 `variables.scss`，不在本任务范围。

- [ ] **Step 3: 全仓 #1677ff 自检。** 运行 `rg -n "#1677ff" web/src`，期望 0 命中（令牌定义文件除外——本阶段不新增令牌定义文件，故应为纯 0）。

- [ ] **Step 4: 提交。** 运行 `git add web/src/components/OrgSelectModal.vue; git commit`，message：`组织选择弹窗：图标蓝色 #1677ff 改主色令牌`，附固定 Co-Authored-By 行。

---

### Task 8: 退役 seasonTheme.ts + 权威色值落入 stores/theme.ts 默认配置

**Files:**
- Delete `web/src/utils/seasonTheme.ts`
- Modify `web/src/stores/theme.ts`（52-78 `defaultThemeConfig`，仅改 colorSuccess/colorError/colorInfo 三处）

- [ ] **Step 1: 确认 seasonTheme.ts 已无任何引用。** 运行 `rg -n "seasonTheme|getCurrentSeasonTheme|getSeasonTheme|getSeasonByMonth|SeasonTheme" web/src`，期望 0 命中（Task 1-4 已清除 login 内引用；若有其它命中需先处理再删文件）。

- [ ] **Step 2: 删除文件。** 运行 `git rm web/src/utils/seasonTheme.ts`（read-only 规划阶段仅记录命令；执行阶段由实施者运行）。

- [ ] **Step 3: stores/theme.ts 默认色值改权威令牌值（52-78）。** 当前：
```ts
const defaultThemeConfig: ThemeConfig = {
  colorPrimary: '#FF6700',
  colorSuccess: '#52C41A',
  colorWarning: '#E6A700',
  colorError: '#FF4D4F',
  colorInfo: '#13C2C2',
```
目标（success→#2BA471、error/danger→#E5484D、info 青→蓝 #3A6FB0；warning 维持 #E6A700；colorPrimary 是否改 #E85E00 由阶段0统一，本阶段保持现值不动，避免与阶段0冲突）：
```ts
const defaultThemeConfig: ThemeConfig = {
  colorPrimary: '#FF6700',
  colorSuccess: '#2BA471',
  colorWarning: '#E6A700',
  colorError: '#E5484D',
  colorInfo: '#3A6FB0',
```
说明：这是 antd `ConfigProvider` 的 token 源（85-115 `antdTheme` 直接读取），改此处即让全局组件语义色与权威令牌对齐；`sidebarActiveBgColor`/`sidebarBgColor` 等布局值不在本阶段令牌集内，保持不动。

- [ ] **Step 4: 自检。** 运行 `rg -n "#52C41A|#FF4D4F|#13C2C2" web/src/stores/theme.ts`，期望 0 命中（确认三处旧值已替换）；再运行 `Test-Path web/src/utils/seasonTheme.ts`，期望输出 `False`。

- [ ] **Step 5: 提交。** 运行 `git add web/src/stores/theme.ts; git commit`（删除已由 `git rm` 暂存），message：`退役 seasonTheme.ts；默认主题 success/error/info 改权威令牌值`，附固定 Co-Authored-By 行。

---

### Task 9: 构建 + 类型基线 + 逐屏 preview 截图验证

**Files:**
- 无文件改动（仅验证）

- [ ] **Step 1: 构建。** 在 `web` 目录运行 `npm run build`（vite build），期望以 `built in …` 成功结束、无 error；若报 "Cannot find module '@/utils/seasonTheme'" 说明仍有残留 import，回 Task 1-2/8 修正。

- [ ] **Step 2: 类型基线确认（非门禁）。** 运行 `npm run type-check`（vue-tsc -b）。本仓基线本就大量报错，仅确认输出里不含 `login/index.vue`、`OrgSelectModal.vue`、`stores/theme.ts` 三个本次改动文件的新增报错（用 `rg "login/index.vue|OrgSelectModal.vue|stores/theme.ts"` 过滤其输出比对，预期 0 行涉及本次文件）。

- [ ] **Step 3: 启动 preview。** 用 `preview_start` 拉起前端，导航至 `/login`。

- [ ] **Step 4: 截图——登录初始屏。** 用 `preview_screenshot` 截全屏，肉眼核对：左品牌区为静态深墨纯色（无渐变/无光斑/无粒子/无路线 SVG/无装饰环）、Logo 纯白文字、3 条业务亮点 + 实色橙小圆点、右表单区顶部 3px 实色橙线、登录按钮实色橙。

- [ ] **Step 5: 截图——输入框聚焦态。** 用 `preview_click` 聚焦账号输入框后 `preview_screenshot`，核对焦点环为橙色（`--color-primary-border`），非旧蓝 `rgba(22,119,255,0.1)`。

- [ ] **Step 6: 截图——过渡屏。** 触发登录（admin/admin123）后用 `preview_screenshot` 抓过渡屏，核对：背景静态深墨无光斑、旋转环单色橙、步骤项激活/完成态为橙/绿令牌色、步骤切换节奏明显放慢（约 700ms/步）、结束时整屏淡出露浅色。

- [ ] **Step 7: 截图——多组织选择弹窗。** 若账号属多组织则弹 OrgSelectModal，`preview_screenshot` 核对顶部 BankOutlined 图标与选中态对勾为橙色（非 #1677ff 蓝）。

- [ ] **Step 8: 运行时令牌联动验证。** 用 `preview_eval` 临时设 `document.documentElement.style.setProperty('--color-primary', '#00AA00')`，再 `preview_screenshot`，确认登录按钮/焦点环/顶线/小圆点同步变绿——证明全页配色已由权威令牌驱动；验证后用 `preview_eval` 移除该覆盖恢复橙色。

- [ ] **Step 9: 收尾。** 用 `preview_stop` 关闭预览。本任务无代码改动、不提交（验证型任务）；若任一截图发现残留装饰/硬编码色，回对应 Task 修正后重跑 Step 1。