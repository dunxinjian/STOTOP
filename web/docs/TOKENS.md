# STOTOP 设计令牌（单一真源）

令牌有两条注入轨道，**值必须一致**：
- **运行时**：`web/src/stores/theme.ts` 的 `applyDesignTokensCSS()` 把令牌写入 `:root`（动态主色/状态色由 `themeConfig` 派生，其余为静态常量）。
- **编译期**：`web/src/styles/variables.scss` 的 `$` 变量以 `var(--token, 权威回退值)` 桥接，消费方继续写 `$color-primary` 即自动透传 CSS 变量。

> 阶段0 建立真源/桥接 + **双预设(维持橙/板岩蓝) 引擎（仅亮色主题，暗色模式已按需求移除）**（`theme.ts`），**不替换** 组件内裸值（阶段1）。

## 主色（双预设，亮色主题，由 `theme.ts` THEME_PRESETS 驱动）

强调色提供两套可切换预设（均亮色），含主色与派生 hover/active/light(软底)/border；`applyDesignTokensCSS()` 按 `themePreset` 注入：

| 令牌 | 维持橙 | 板岩蓝 |
|---|---|---|
| `--color-primary` | `#C2410C` | `#3E5C82` |
| `--color-primary-hover` | `#A8370A` | `#345074` |
| `--color-primary-active` | `#93310A` | `#2C4566` |
| `--color-primary-light` | `#FBEDE6` | `#EAEFF5` |
| `--color-primary-border` | `rgba(194,65,12,.30)` | `rgba(62,92,130,.30)` |
| `--login-ink`（登录品牌区深底） | `#1A1A19` | `#18202B` |
| `--login-accent`（深底上更亮强调） | `#FB8B3C` | `#8AA6C8` |

> 原品牌橙 `#E85E00` 作小号彩字 3.39 / 配白字 3.48 均不达 AA，已退出 UI 强调用途（仅大色块/营销可用）；UI 强调用下沉的烧橙 `#C2410C`。**仅亮色主题**。

| 描边令牌 | 值 | 用途 |
|---|---|---|
| `--color-danger-border` | `rgba(214,88,78,0.30)` | 危险态焦点环/描边 |
| `--color-success-border` | `rgba(62,158,110,0.30)` | 成功态描边（对齐 .30 口径） |
| `--color-warning-border` | `rgba(212,154,46,0.30)` | 警告态描边（对齐 .30 口径） |

## 状态色（成功/警告/危险/信息，各带 -light/-text）

| 令牌 | 值 | | 令牌 | 值 | | 令牌 | 值 |
|---|---|---|---|---|---|---|---|
| `--color-success` | `#2BA471` | | `--color-success-light` | `#E7F5EF` | | `--color-success-text` | `#0F6E56` |
| `--color-warning` | `#E6A700` | | `--color-warning-light` | `#FBF1D8` | | `--color-warning-text` | `#8A6200` |
| `--color-danger` | `#E5484D` | | `--color-danger-light` | `#FCEBEC` | | `--color-danger-text` | `#A3282C` |
| `--color-info` | `#3A6FB0` | | `--color-info-light` | `#E9F0F8` | | `--color-info-text` | `#1C4366` |

> `--color-success` / `--color-warning` / `--color-danger`(=themeConfig.colorError) / `--color-info` 为动态，随 `themeConfig` 派生；各 `-light` / `-text` 为静态常量。

## 文字 / 表面 / 边框（中性令牌，亮色，与预设无关；见 `theme.ts` NEUTRALS）

| 令牌 | 值 | | 令牌 | 值 |
|---|---|---|---|---|
| `--text-1` | `#1F2329` | | `--bg-page` | `#F7F8FA` |
| `--text-2` | `#5A6068` | | `--bg-card` | `#FFFFFF` |
| `--text-3` | `#8A9099` | | `--bg-muted` | `#F1F3F6` |
| `--text-disabled` | `#BFC3C9` | | `--border` | `#ECEEF1` |
| `--text-on-accent` | `#FFFFFF` | | `--border-strong` | `#DDE0E4` |

## 外壳（topbar / sidebar）

| 令牌 | 值 | 用途 |
|---|---|---|
| `--topbar-ink` | `#1F2430` | 前台顶栏底色 |
| `--topbar-ink-admin` | `#171A22` | 管理后台顶栏底色 |
| `--topbar-border` | `rgba(255,255,255,0.10)` | 顶栏分隔线 |
| `--sidebar-item-hover` | `rgba(0,0,0,0.05)` | 侧栏项悬停底 |
| `--sidebar-item-active-text` | `var(--color-primary)` | 侧栏激活项文字 |

> `--sidebar-bg` / `--sidebar-active-bg` / `--sidebar-item-active-bg` / `--sidebar-active-indicator` 由 `applySidebarCSS()` 按 `themeConfig`（`sidebarBgColor`=`#EDEEF1`、`sidebarActiveBgColor`=`#FFF3EA`、indicator=主色）动态注入，此处仅登记。

## 业务色

| 令牌 | 值 | 用途 |
|---|---|---|
| `--biz-waybill` | `#6B4FB0` | 快递/运单 |
| `--biz-contract` | `#8A6D3B` | 合同 |
| `--biz-quality` | `#D9603A` | 质量 |
| `--biz-approval` | `#3A6FB0` | 审批 |
| `--biz-points` | `#C99A2E` | 积分 |
| `--biz-finance` | `#B8860B` | 财务 |

## CardFlow 分类色（数据编码枚举，静态常量，不随 themeConfig 派生）

> 性质同 `--biz-*`：枚举绑定的数据编码色，由 `applyDesignTokensCSS()` 注入 `:root`，组件以 `var()` 消费（含 SchemaFieldEditor 的 JS `tone` 内联 `var()`）。**无 `variables.scss` `$` 桥接**（镜像 `--biz-*` 现状）。浅底以 `color-mix(in srgb, var(--cf-node-X) N%, transparent)` 派生。

| 令牌 | 值 | 绑定 | | 令牌 | 值 | 绑定 |
|---|---|---|---|---|---|---|
| `--cf-node-manual` | `#1D4ED8` | 人工节点 | | `--cf-field-text` | `#1F2937` | 文本 |
| `--cf-node-auto` | `#7C3AED` | 自动节点 | | `--cf-field-money` | `#B45309` | 金额 |
| `--cf-node-batch` | `#059669` | 批次级节点 | | `--cf-field-enum` | `#7C3AED` | 枚举 |
| `--cf-field-date` | `#0891B2` | 日期 | | `--cf-field-file` | `#475569` | 附件 |
| `--cf-field-user` | `#16A34A` | 人员 | | `--cf-field-org` | `#2563EB` | 组织 |
| `--cf-field-cardRef` | `#DB2777` | 卡片引用 | | `--cf-field-account` | `#0F766E` | 会计科目 |
| `--cf-field-auxiliary` | `#4F46E5` | 辅助核算 | | `--cf-field-bankAccount` | `#0369A1` | 银行账户 |
| `--cf-field-voucherRef` | `#9333EA` | 凭证引用 | | | | |

## 流程设计器语义色（静态常量，`applyDesignTokensCSS()` 注入，有 `variables.scss` `$` 桥接）

> 竖向流程图节点/徽标着色。色相对齐 `--cf-node-*` 族（auto 复用 `#7C3AED` 保持"自动"语义一致）；抄送橙经降饱和对齐项目暖色风格（非 AntD 原橙 #fa8c16）。深色模式待深色主题立项时补值。

| 令牌 | 值 | 用途 | SCSS |
|---|---|---|---|
| `--color-flow-auto` | `#7C3AED` | 自动处理节点主色 | `$color-flow-auto` |
| `--color-flow-auto-light` | `#F3EEFB` | 自动节点浅底 | `$color-flow-auto-light` |
| `--color-flow-auto-border` | `rgba(124,58,237,0.35)` | 自动节点虚线边框 | `$color-flow-auto-border` |
| `--color-flow-cc` | `#C9740F` | 抄送节点主色 | `$color-flow-cc` |
| `--color-flow-cc-light` | `#FAF0E1` | 抄送节点浅底 | `$color-flow-cc-light` |
| `--color-flow-cc-border` | `rgba(201,116,15,0.35)` | 抄送节点徽标描边 | `$color-flow-cc-border` |

## 圆角

| 令牌 | 值 |
|---|---|
| `--radius-sm` | `4px` |
| `--radius-md` | `6px` |
| `--radius-lg` | `8px` |
| `--radius-modal` | `12px` |
| `--radius-pill` | `999px` |

## 阴影

| 令牌 | 值 |
|---|---|
| `--shadow-sm` | `0 1px 2px rgba(18,31,53,0.05)` |
| `--shadow-md` | `0 4px 12px rgba(18,31,53,0.08)` |
| `--shadow-lg` | `0 8px 24px rgba(18,31,53,0.10)` |
| `--shadow-lift` | `0 10px 26px rgba(18,31,53,0.16)`（拖拽抬升，SCSS `$shadow-lift`） |

## 字号刻度

| 令牌 | 值 | SCSS |
|---|---|---|
| `--font-xs` | `11px` | `$font-size-xs` |
| `--font-sm` | `12px` | `$font-size-sm` |
| `--font-sm2` | `13px` | `$font-size-sm2` |
| `--font-base` | `14px` | `$font-size-base` |
| `--font-lg` | `16px` | `$font-size-lg` |
| `--font-xl` | `18px` | `$font-size-xl` |
| `--font-2xl` | `24px` | `$font-size-2xl` |

## 间距（4 基数）

| 令牌 | 值 | SCSS |
|---|---|---|
| `--space-2xs2` | `2px` | `$spacing-2xs` |
| `--space-xs4` | `4px` | `$spacing-xs` |
| `--space-sm8` | `8px` | `$spacing-sm` |
| `--space-md12` | `12px` | `$spacing-md12` |
| `--space-lg16` | `16px` | `$spacing-md` |
| `--space-xl24` | `24px` | `$spacing-lg` |
| `--space-2xl32` | `32px` | `$spacing-xl` |

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
- 门禁：`npx stylelint "src/styles/variables.scss"` 退出码 0（白名单豁免 `color-no-hex`）；`npm run lint:style` 列出组件内待替换裸 hex（移交阶段1）。
