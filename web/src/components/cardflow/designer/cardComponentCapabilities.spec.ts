import { describe, it, expect } from 'vitest'
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { CARD_COMPONENT_CAPABILITIES } from './cardComponentCapabilities'

/**
 * CardFlow 组件能力↔实现 一致性门禁（B9 组件注册表化）。
 *
 * 不变量：每个「可发布」能力必须且只能落在一个实现通道里，否则即"真声明假实现"——
 * 声明为 publishable 却没有任何运行态渲染。三个互斥通道：
 *   ① runtime      —— 能力表 `runtimeComponent` 字段（独立业务/明细 SFC，componentFor 查表分发）
 *   ② cardField    —— CardComponentRenderer 的 `component.binding?.source === 'cardField'` 通用控件块
 *   ③ inline       —— CardComponentRenderer 的 `component.type === '<type>'` 专属内联分支
 *
 * 纯函数门禁：只读能力表 + 读渲染器源码/磁盘文件，不挂载 Vue 组件（environment: node）。
 * 新增可发布组件却忘了接实现，或删了内联分支/SFC 却仍标 publishable，本门禁即报红。
 */

// 通道 ②：由通用 cardField 控件块（is*Field/controlKind 分发）渲染的字段控件。
const CARD_FIELD_CONTROLS = new Set([
  'text', 'textarea', 'number', 'money', 'date', 'dateRange',
  'radio', 'checkbox', 'idCard', 'phone', 'attachment',
])

// 通道 ③：各有 `component.type === '<type>'` 专属内联分支的可发布组件。
const DEDICATED_INLINE = new Set([
  'sectionTitle', 'textBlock', 'imageList', 'signature', 'rating',
  'placeholderControl', 'relationLookup',
])

const caps = Object.values(CARD_COMPONENT_CAPABILITIES)
const rendererSource = readFileSync(
  fileURLToPath(new URL('../runtime/CardComponentRenderer.vue', import.meta.url)),
  'utf8',
)
const pascal = (k: string) => k.charAt(0).toUpperCase() + k.slice(1)

describe('CardFlow 组件能力↔实现 一致性门禁', () => {
  it('每个可发布能力恰好落在一个实现通道（runtime / cardField / 专属内联）', () => {
    const offenders: string[] = []
    for (const cap of caps) {
      if (!cap.publishable) continue
      const channels = [
        cap.runtimeComponent ? 'runtime' : null,
        CARD_FIELD_CONTROLS.has(cap.type) ? 'cardField' : null,
        DEDICATED_INLINE.has(cap.type) ? 'inline' : null,
      ].filter(Boolean)
      if (channels.length !== 1) {
        offenders.push(`${cap.key}(type=${cap.type}) → 命中 ${channels.length} 个通道 [${channels.join(',')}]`)
      }
    }
    expect(offenders, `以下可发布组件的实现通道不唯一（0=真声明假实现，>1=通道歧义）：\n${offenders.join('\n')}`).toEqual([])
  })

  it('runtimeComponent 仅可发布组件持有，值唯一，且不与 requiresRuntimeIntegration 并存', () => {
    const seen = new Map<string, string>()
    for (const cap of caps) {
      if (!cap.runtimeComponent) continue
      expect(cap.publishable, `不可发布组件 ${cap.key} 不应声明 runtimeComponent`).toBe(true)
      expect(cap.requiresRuntimeIntegration ?? false, `需运行时集成的 ${cap.key} 不应声明 runtimeComponent`).toBe(false)
      const prev = seen.get(cap.runtimeComponent)
      expect(prev, `runtimeComponent '${cap.runtimeComponent}' 被 ${cap.key} 与 ${prev} 重复占用`).toBeUndefined()
      seen.set(cap.runtimeComponent, cap.key)
    }
  })

  it('每个 runtimeComponent 都有对应 SFC 文件与 RUNTIME_COMPONENTS 表项', () => {
    for (const cap of caps) {
      if (!cap.runtimeComponent) continue
      const sfc = fileURLToPath(new URL(`../runtime/components/${pascal(cap.runtimeComponent)}Component.vue`, import.meta.url))
      expect(existsSync(sfc), `缺少 SFC 文件：${pascal(cap.runtimeComponent)}Component.vue（能力 ${cap.key} 声明的 runtimeComponent）`).toBe(true)
      // componentFor 经 RUNTIME_COMPONENTS 查表分发；缺表项则运行时落空退回内联/未知分支
      expect(rendererSource.includes(`${cap.runtimeComponent}:`), `CardComponentRenderer.RUNTIME_COMPONENTS 缺少键 '${cap.runtimeComponent}'`).toBe(true)
    }
  })

  it('专属内联通道的每个 type 在 CardComponentRenderer 有 component.type 分支', () => {
    for (const type of DEDICATED_INLINE) {
      expect(rendererSource.includes(`component.type === '${type}'`), `CardComponentRenderer 缺少 '${type}' 的内联渲染分支`).toBe(true)
    }
  })

  it('cardField 通道存在且其成员均支持 cardField 绑定', () => {
    expect(rendererSource.includes(`component.binding?.source === 'cardField'`), 'CardComponentRenderer 缺少 cardField 通用控件块').toBe(true)
    for (const type of CARD_FIELD_CONTROLS) {
      const cap = CARD_COMPONENT_CAPABILITIES[type]
      expect(cap, `cardField 通道成员 ${type} 无能力记录`).toBeTruthy()
      expect(cap.supportedBindings.includes('cardField'), `${type} 未支持 cardField 绑定`).toBe(true)
    }
  })

  it('内联通道白名单不含过时条目（均为现存可发布能力）', () => {
    for (const type of [...CARD_FIELD_CONTROLS, ...DEDICATED_INLINE]) {
      const cap = CARD_COMPONENT_CAPABILITIES[type]
      expect(cap, `白名单 type '${type}' 无能力记录`).toBeTruthy()
      expect(cap.publishable, `白名单 type '${type}' 已非可发布，应从内联通道白名单移除`).toBe(true)
    }
  })
})
