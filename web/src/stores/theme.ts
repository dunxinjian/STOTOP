import { defineStore } from 'pinia'
import { ref, computed, watch } from 'vue'
import { theme } from 'ant-design-vue'
import type { MappingAlgorithm, ThemeConfig as AntdThemeConfig } from 'ant-design-vue/es/config-provider/context'
import { getThemeSettings, updateThemeSettings } from '@/api/theme'

/** 表格行高密度选项 */
export type TableRowDensity = 'compact' | 'standard' | 'relaxed' | 'comfortable'

/** 密度选项对应的 padding 值映射 */
export const TABLE_DENSITY_MAP: Record<TableRowDensity, { block: number; inline: number; blockSM: number; inlineSM: number }> = {
  compact:     { block: 4,  inline: 6,  blockSM: 2,  inlineSM: 4 },
  standard:    { block: 6,  inline: 8,  blockSM: 4,  inlineSM: 6 },
  relaxed:     { block: 8,  inline: 10, blockSM: 6,  inlineSM: 8 },
  comfortable: { block: 12, inline: 14, blockSM: 8,  inlineSM: 10 },
}

export const TABLE_DENSITY_LABELS: Record<TableRowDensity, string> = {
  compact: '紧凑',
  standard: '标准',
  relaxed: '宽松',
  comfortable: '舒适',
}

/** 主题色预设：维持橙 / 板岩蓝（强调色双预设，均为亮色主题；见 docs/TOKENS.md） */
export type ThemePreset = 'orange' | 'slate'

/** 单套主色色阶 */
interface PrimaryShades {
  primary: string
  hover: string
  active: string
  /** 主色浅底/软底 */
  light: string
  /** 主色描边/焦点环 */
  border: string
}

interface PresetDef {
  label: string
  /** 登录品牌区深底色（随预设温度） */
  loginInk: string
  /** 亮色主题主色阶（系统恒用此套） */
  shades: PrimaryShades
  /** 登录品牌区(恒深底)上用的更亮强调色 */
  brightOnInk: string
}

/**
 * 两套强调色预设（均亮色主题）。对比度均已校验：
 * 橙 #C2410C(白字按钮5.18/亮面彩字5.04)；蓝 #3E5C82(6.9/6.8)。
 */
export const THEME_PRESETS: Record<ThemePreset, PresetDef> = {
  orange: {
    label: '申通橙',
    loginInk: '#2A2C30',
    shades: { primary: '#C2410C', hover: '#A8370A', active: '#93310A', light: '#FBEDE6', border: 'rgba(194,65,12,0.30)' },
    brightOnInk: '#FB8B3C',
  },
  slate: {
    label: '板岩蓝',
    loginInk: '#18202B',
    shades: { primary: '#3E5C82', hover: '#345074', active: '#2C4566', light: '#EAEFF5', border: 'rgba(62,92,130,0.30)' },
    brightOnInk: '#8AA6C8',
  },
}

/** 中性令牌（与预设无关：两套预设共用同一亮色灰阶） */
const NEUTRALS = {
  text1: '#1F2329', text2: '#5A6068', text3: '#8A9099', textDisabled: '#BFC3C9', textOnAccent: '#FFFFFF',
  bgPage: '#F7F8FA', bgCard: '#FFFFFF', bgMuted: '#F1F3F6',
  border: '#ECEEF1', borderStrong: '#DDE0E4', borderFaint: '#F2F4F6',
  topbarInk: '#232834', topbarInkAdmin: '#171A22', topbarBorder: 'rgba(255,255,255,0.10)',
  sidebarItemHover: 'rgba(0,0,0,0.05)',
  successLight: '#EAF4EF', successText: '#2A6B4C',
  warningLight: '#FAF1DD', warningText: '#8A6212',
  dangerLight: '#FBEEEC', dangerText: '#9E332B',
  infoLight: '#EBEFF4', infoText: '#34455A',
  shadowSm: '0 1px 2px rgba(18,31,53,0.05)', shadowMd: '0 4px 12px rgba(18,31,53,0.08)', shadowLg: '0 8px 24px rgba(18,31,53,0.10)',
}

/** hex → rgba（用于深色侧栏在品牌强调色上叠加低透明度软底） */
function hexToRgba(hex: string, alpha: number): string {
  const h = hex.replace('#', '')
  const r = parseInt(h.slice(0, 2), 16)
  const g = parseInt(h.slice(2, 4), 16)
  const b = parseInt(h.slice(4, 6), 16)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}

export interface ThemeConfig {
  /** 强调色预设（维持橙/板岩蓝） */
  themePreset: ThemePreset
  colorPrimary: string
  colorSuccess: string
  colorWarning: string
  colorError: string
  colorInfo: string
  borderRadius: number
  fontSize: number
  sizeStep: number
  sizeUnit: number
  wireframe: boolean
  compactMode: boolean
  marginXS: number
  marginSM: number
  margin: number
  marginMD: number
  marginLG: number
  tableRowDensity: TableRowDensity
  pagePaddingX: number
  pagePaddingY: number
  sidebarExpandedWidth: number
  sidebarCollapsedWidth: number
  sidebarBgColor: string
  sidebarActiveBgColor: string
  sidebarMaxTabs: number
}

const defaultThemeConfig: ThemeConfig = {
  themePreset: 'orange',
  colorPrimary: THEME_PRESETS.orange.shades.primary,
  colorSuccess: '#3E9E6E',
  colorWarning: '#D49A2E',
  colorError: '#D6584E',
  colorInfo: '#5B7290',
  borderRadius: 6,
  fontSize: 14,
  sizeStep: 4,
  sizeUnit: 4,
  wireframe: false,
  compactMode: false,
  marginXS: 8,   // = $spacing-sm  = --space-sm8
  marginSM: 12,  // = $spacing-md12 = --space-md12
  margin: 16,    // = $spacing-md  = --space-lg16
  marginMD: 20,  // antd 内部刻度，不在 spacing 双轨（文档已登记）
  marginLG: 24,  // = $spacing-lg  = --space-xl24
  tableRowDensity: 'standard',
  pagePaddingX: 16,  // = --page-pad-x
  pagePaddingY: 12,  // = --page-pad-y
  sidebarExpandedWidth: 180,
  sidebarCollapsedWidth: 48,
  sidebarBgColor: '#EDEEF1',
  sidebarActiveBgColor: '#FBEDE6',
  sidebarMaxTabs: 12,
}

export const useThemeStore = defineStore('theme', () => {
  const themeConfig = ref<ThemeConfig>({ ...defaultThemeConfig })
  const loading = ref(false)

  /** 当前预设定义 */
  const preset = computed(() => THEME_PRESETS[themeConfig.value.themePreset] || THEME_PRESETS.orange)
  /** 当前主色色阶（恒亮色，按预设切换） */
  const shades = computed<PrimaryShades>(() => preset.value.shades)

  /** 转换为 Ant Design ConfigProvider 的 theme prop 格式 */
  const antdTheme = computed(() => {
    const eff = shades.value
    const algorithms: MappingAlgorithm[] = [theme.defaultAlgorithm]
    if (themeConfig.value.compactMode) {
      algorithms.push(theme.compactAlgorithm)
    }

    const density = TABLE_DENSITY_MAP[themeConfig.value.tableRowDensity] || TABLE_DENSITY_MAP.standard

    return {
      token: {
        colorPrimary: eff.primary,
        colorSuccess: themeConfig.value.colorSuccess,
        colorWarning: themeConfig.value.colorWarning,
        colorError: themeConfig.value.colorError,
        colorInfo: themeConfig.value.colorInfo,
        colorLink: '#2C3340',
        colorLinkHover: eff.primary,
        colorLinkActive: eff.active,
        borderRadius: themeConfig.value.borderRadius,
        fontSize: themeConfig.value.fontSize,
        sizeStep: themeConfig.value.sizeStep,
        sizeUnit: themeConfig.value.sizeUnit,
        wireframe: themeConfig.value.wireframe,
        marginXS: themeConfig.value.marginXS,
        marginSM: themeConfig.value.marginSM,
        margin: themeConfig.value.margin,
        marginMD: themeConfig.value.marginMD,
        marginLG: themeConfig.value.marginLG,
      },
      components: {
        Table: {
          headerBg: '#fafafa',
          headerColor: 'rgba(0, 0, 0, 0.85)',
          rowHoverBg: '#f5f7fa',
          cellPaddingBlock: density.block,
          cellPaddingBlockSM: density.blockSM,
          cellPaddingBlockMD: density.block,
          cellPaddingInline: density.inline,
          cellPaddingInlineSM: density.inlineSM,
          cellPaddingInlineMD: density.inline,
        },
        Button: {
          controlHeight: 32,
          controlHeightSM: 24,
        },
        Tabs: {
          itemColor: NEUTRALS.text2,
          itemSelectedColor: NEUTRALS.text1,   // 激活页签文字中性；下划线(inkBar)留主色作标记
          itemHoverColor: eff.primary,
          inkBarColor: eff.primary,
        },
        Menu: {
          itemSelectedColor: NEUTRALS.text1,
          itemSelectedBg: '#F1F3F6',
        },
        Segmented: {
          itemSelectedBg: '#FFFFFF',
          itemSelectedColor: NEUTRALS.text1,
          itemColor: NEUTRALS.text2,
          itemHoverColor: NEUTRALS.text1,
        },
        Radio: {
          buttonSolidCheckedBg: '#5A6068',
          buttonSolidCheckedHoverBg: '#41464D',
          buttonSolidCheckedActiveBg: '#41464D',
        },
      } as AntdThemeConfig['components'],
      algorithm: algorithms,
      cssVar: { prefix: 'sto' },
      hashed: false,
    }
  })

  /** 从 API 加载主题配置（含旧配置迁移） */
  async function loadTheme() {
    try {
      const json = await getThemeSettings()
      if (json) {
        const parsed = typeof json === 'string' ? JSON.parse(json) : json
        themeConfig.value = migrateConfig({ ...defaultThemeConfig, ...parsed }, parsed)
      } else {
        themeConfig.value = { ...defaultThemeConfig }
      }
    } catch (error: unknown) {
      // API 返回 404/500 或网络异常，优雅降级为默认配置
      console.warn('Failed to load theme settings, using defaults:', error)
      themeConfig.value = { ...defaultThemeConfig }
    }
  }

  /** 旧配置迁移：无 themePreset 时由 colorPrimary 推断；锚色统一对齐预设 */
  function migrateConfig(cfg: ThemeConfig, raw: Partial<ThemeConfig>): ThemeConfig {
    if (!raw.themePreset) cfg.themePreset = raw.colorPrimary === THEME_PRESETS.slate.shades.primary ? 'slate' : 'orange'
    cfg.colorPrimary = THEME_PRESETS[cfg.themePreset]?.shades.primary ?? defaultThemeConfig.colorPrimary
    return cfg
  }

  /** 保存主题配置到 API */
  async function saveTheme() {
    loading.value = true
    try {
      await updateThemeSettings(JSON.stringify(themeConfig.value))
    } finally {
      loading.value = false
    }
  }

  /** 切换强调色预设并持久化 */
  function setPreset(p: ThemePreset) {
    themeConfig.value.themePreset = p
    themeConfig.value.colorPrimary = THEME_PRESETS[p].shades.primary
    saveTheme().catch((e: unknown) => console.warn('保存主题预设失败:', e))
  }

  /** 恢复默认主题 */
  function resetTheme() {
    themeConfig.value = { ...defaultThemeConfig }
  }

  /** 动态注入表格行高 CSS 覆盖（同步 ant-override.scss 中的 !important 规则） */
  function applyTableDensityCSS(density: TableRowDensity) {
    const id = '__theme-table-density__'
    let style = document.getElementById(id) as HTMLStyleElement | null
    if (!style) {
      style = document.createElement('style')
      style.id = id
      document.head.appendChild(style)
    }
    const d = TABLE_DENSITY_MAP[density] || TABLE_DENSITY_MAP.standard
    style.textContent = `
.ant-table .ant-table-thead > tr > th { padding: ${d.block}px ${d.inline}px !important; }
.ant-table .ant-table-tbody > tr > td { padding: ${d.block}px ${d.inline}px !important; }
.ant-table.ant-table-small .ant-table-thead > tr > th,
.ant-table.ant-table-small .ant-table-tbody > tr > td { padding: ${d.blockSM}px ${d.inlineSM}px !important; }
.ant-table.ant-table-middle .ant-table-thead > tr > th,
.ant-table.ant-table-middle .ant-table-tbody > tr > td { padding: ${d.block}px ${d.inline}px !important; }
`
  }

  /** 动态注入页面间距：写 CSS 变量而非注入 !important 规则 */
  function applyPagePaddingCSS(paddingY: number, paddingX: number) {
    const s = document.documentElement.style
    s.setProperty('--page-pad-x', paddingX + 'px')
    s.setProperty('--page-pad-y', paddingY + 'px')
  }

  // 监听 tableRowDensity 变化，实时注入 CSS
  watch(() => themeConfig.value.tableRowDensity, (val) => {
    applyTableDensityCSS(val)
  }, { immediate: true })

  // 监听页面间距变化，实时注入 CSS
  watch(
    () => [themeConfig.value.pagePaddingX, themeConfig.value.pagePaddingY],
    ([x, y]) => {
      applyPagePaddingCSS(y, x)
    },
    { immediate: true }
  )

  /** 动态注入深色侧栏 CSS 变量（底色/强调随预设品牌色切换） */
  function applySidebarCSS() {
    const style = document.documentElement.style
    const c = themeConfig.value
    const accent = preset.value.brightOnInk
    style.setProperty('--sidebar-expanded-width', c.sidebarExpandedWidth + 'px')
    style.setProperty('--sidebar-collapsed-width', c.sidebarCollapsedWidth + 'px')
    // 深色侧栏底：随预设品牌深色（申通橙=中性石墨灰 / 板岩蓝=深板岩）
    style.setProperty('--sidebar-bg', preset.value.loginInk)
    // 深底上的文字层级（白色不同透明度）
    style.setProperty('--sidebar-text', 'rgba(255, 255, 255, 0.66)')
    style.setProperty('--sidebar-text-strong', 'rgba(255, 255, 255, 0.92)')
    style.setProperty('--sidebar-text-muted', 'rgba(255, 255, 255, 0.45)')
    style.setProperty('--sidebar-item-hover', 'rgba(255, 255, 255, 0.06)')
    style.setProperty('--sidebar-border', 'rgba(255, 255, 255, 0.10)')
    style.setProperty('--sidebar-search-bg', 'rgba(255, 255, 255, 0.08)')
    // 激活：亮橙/亮蓝（随预设），软底用其低透明度
    style.setProperty('--sidebar-item-active-text', accent)
    style.setProperty('--sidebar-active-indicator', accent)
    style.setProperty('--sidebar-item-active-bg', hexToRgba(accent, 0.14))
  }

  // 监听侧栏配置 + 预设变化，实时注入 CSS
  watch(
    () => [
      themeConfig.value.sidebarExpandedWidth,
      themeConfig.value.sidebarCollapsedWidth,
      themeConfig.value.sidebarBgColor,
      themeConfig.value.sidebarActiveBgColor,
      themeConfig.value.themePreset,
    ],
    () => {
      applySidebarCSS()
    },
    { immediate: true }
  )

  /** 动态注入完整设计令牌集到 :root（主色随预设派生，其余为亮色常量） */
  function applyDesignTokensCSS() {
    const s = document.documentElement.style
    const c = themeConfig.value
    const N = NEUTRALS
    const P = shades.value

    document.documentElement.dataset.theme = 'light'
    document.documentElement.dataset.preset = c.themePreset

    // —— 动态：主色（按预设派生）
    s.setProperty('--color-primary', P.primary)
    s.setProperty('--color-primary-hover', P.hover)
    s.setProperty('--color-primary-active', P.active)
    s.setProperty('--color-primary-light', P.light)
    s.setProperty('--color-primary-border', P.border)
    s.setProperty('--login-ink', preset.value.loginInk)
    // 登录品牌区恒为深底，强调色用更亮变体
    s.setProperty('--login-accent', preset.value.brightOnInk)
    // —— 状态色：主色由 themeConfig 派生；浅底/文字为常量
    s.setProperty('--color-success', c.colorSuccess || '#3E9E6E')
    s.setProperty('--color-success-light', N.successLight)
    s.setProperty('--color-success-text', N.successText)
    s.setProperty('--color-success-border', 'rgba(62, 158, 110, 0.30)')
    s.setProperty('--color-warning', c.colorWarning || '#D49A2E')
    s.setProperty('--color-warning-light', N.warningLight)
    s.setProperty('--color-warning-text', N.warningText)
    s.setProperty('--color-warning-border', 'rgba(212, 154, 46, 0.30)')
    s.setProperty('--color-danger', c.colorError || '#D6584E')
    s.setProperty('--color-danger-light', N.dangerLight)
    s.setProperty('--color-danger-text', N.dangerText)
    s.setProperty('--color-danger-border', 'rgba(214, 88, 78, 0.30)')
    s.setProperty('--color-info', c.colorInfo || '#5B7290')
    s.setProperty('--color-info-light', N.infoLight)
    s.setProperty('--color-info-text', N.infoText)
    s.setProperty('--color-info-border', 'rgba(91, 114, 144, 0.30)')
    // —— 文字
    s.setProperty('--text-1', N.text1)
    s.setProperty('--text-2', N.text2)
    s.setProperty('--text-3', N.text3)
    s.setProperty('--text-disabled', N.textDisabled)
    s.setProperty('--text-on-accent', N.textOnAccent)
    // —— 表面/边框
    s.setProperty('--bg-page', N.bgPage)
    s.setProperty('--bg-card', N.bgCard)
    s.setProperty('--bg-muted', N.bgMuted)
    s.setProperty('--border', N.border)
    s.setProperty('--border-strong', N.borderStrong)
    s.setProperty('--border-faint', N.borderFaint)
    // —— 外壳
    s.setProperty('--topbar-ink', N.topbarInk)
    s.setProperty('--topbar-ink-admin', N.topbarInkAdmin)
    s.setProperty('--topbar-border', N.topbarBorder)
    // 注：--sidebar-bg / --sidebar-text* / --sidebar-item-active-* / --sidebar-item-hover / --sidebar-active-indicator / --sidebar-border / --sidebar-search-bg 由 applySidebarCSS 注入
    // —— 静态：业务色（数据编码枚举，不随主色派生）
    s.setProperty('--biz-waybill', '#6B4FB0')
    s.setProperty('--biz-contract', '#8A6D3B')
    s.setProperty('--biz-quality', '#D9603A')
    s.setProperty('--biz-approval', '#5B7290')
    s.setProperty('--biz-points', '#C99A2E')
    s.setProperty('--biz-finance', '#B8860B')
    s.setProperty('--biz-task', '#4F8A7B')
    s.setProperty('--biz-notification', '#8A8F99')
    // —— 静态：CardFlow 分类色（数据编码枚举）
    s.setProperty('--cf-node-manual', '#1D4ED8')
    s.setProperty('--cf-node-auto', '#7C3AED')
    s.setProperty('--cf-node-batch', '#059669')
    // —— 静态：流程设计器语义色（竖向图节点着色；色相对齐 cf-node 族与项目降饱和风格）
    s.setProperty('--color-flow-auto', '#7C3AED')
    s.setProperty('--color-flow-auto-light', '#F3EEFB')
    s.setProperty('--color-flow-auto-border', 'rgba(124, 58, 237, 0.35)')
    s.setProperty('--color-flow-cc', '#C9740F')
    s.setProperty('--color-flow-cc-light', '#FAF0E1')
    s.setProperty('--color-flow-cc-border', 'rgba(201, 116, 15, 0.35)')
    s.setProperty('--shadow-lift', '0 10px 26px rgba(18, 31, 53, 0.16)')
    s.setProperty('--cf-field-text', '#1F2937')
    s.setProperty('--cf-field-money', '#B45309')
    s.setProperty('--cf-field-enum', '#7C3AED')
    s.setProperty('--cf-field-date', '#0891B2')
    s.setProperty('--cf-field-file', '#475569')
    s.setProperty('--cf-field-user', '#16A34A')
    s.setProperty('--cf-field-org', '#2563EB')
    s.setProperty('--cf-field-cardRef', '#DB2777')
    s.setProperty('--cf-field-account', '#0F766E')
    s.setProperty('--cf-field-auxiliary', '#4F46E5')
    s.setProperty('--cf-field-bankAccount', '#0369A1')
    s.setProperty('--cf-field-voucherRef', '#9333EA')
    // —— 静态：头像色环
    s.setProperty('--avatar-palette-1', '#5B7290')
    s.setProperty('--avatar-palette-2', '#6BA292')
    s.setProperty('--avatar-palette-3', '#C99A6B')
    s.setProperty('--avatar-palette-4', '#9B8AB8')
    s.setProperty('--avatar-palette-5', '#C77B6B')
    s.setProperty('--avatar-palette-6', '#8FB07E')
    // —— 静态：圆角
    s.setProperty('--radius-sm', '4px')
    s.setProperty('--radius-md', '6px')
    s.setProperty('--radius-lg', '8px')
    s.setProperty('--radius-modal', '12px')
    s.setProperty('--radius-pill', '999px')
    // —— 阴影
    s.setProperty('--shadow-sm', N.shadowSm)
    s.setProperty('--shadow-md', N.shadowMd)
    s.setProperty('--shadow-lg', N.shadowLg)
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
    // —— 静态：布局范式
    s.setProperty('--toolbar-height', '40px')
  }

  // 监听预设/状态色变化，实时重注入令牌集（静态项每次一并写入，幂等）
  watch(
    () => [
      themeConfig.value.themePreset,
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

  return {
    themeConfig,
    loading,
    antdTheme,
    loadTheme,
    saveTheme,
    setPreset,
    resetTheme,
    applyTableDensityCSS,
    applyPagePaddingCSS,
    applySidebarCSS,
    applyDesignTokensCSS,
  }
})
