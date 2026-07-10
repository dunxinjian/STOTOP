import { describe, it, expect } from 'vitest'
import {
  CC_CHANNEL_OPTIONS,
  CC_TIMING_OPTIONS,
  emptyCcConfig,
  parseCcConfig,
  serializeCcConfig,
} from './ccConfigShared'

/**
 * ccConfigShared 纯逻辑门禁（M8-B 件③）。
 * parseCcConfig/serializeCcConfig 镜像引擎侧消费口径（onEnter/onApprove/onReject 三触点）：
 * users 空 = 不触发抄送（序列化为 undefined，不落配置）；channels 缺省 ["system"]；timing 缺省 "onEnter"。
 */

describe('emptyCcConfig', () => {
  it('默认时机 onEnter，默认渠道仅 system，对象为空', () => {
    expect(emptyCcConfig()).toEqual({ users: [], timing: 'onEnter', channels: ['system'] })
  })
})

describe('parseCcConfig', () => {
  it('json 缺失（null/undefined/空串）→ 回退空配置', () => {
    expect(parseCcConfig(null)).toEqual(emptyCcConfig())
    expect(parseCcConfig(undefined)).toEqual(emptyCcConfig())
    expect(parseCcConfig('')).toEqual(emptyCcConfig())
  })

  it('合法 json → 原样解析 users/timing/channels', () => {
    const json = JSON.stringify({
      users: [{ userId: 123, userName: '张三' }, { userId: 456, userName: '李四' }],
      timing: 'onApprove',
      channels: ['system', 'dingtalk'],
    })
    expect(parseCcConfig(json)).toEqual({
      users: [{ userId: 123, userName: '张三' }, { userId: 456, userName: '李四' }],
      timing: 'onApprove',
      channels: ['system', 'dingtalk'],
    })
  })

  it('users 中 userId 非正数的条目被过滤（防脏数据回显幽灵抄送人）', () => {
    const json = JSON.stringify({
      users: [{ userId: 123, userName: '张三' }, { userId: 0, userName: '脏数据' }, { userId: -1, userName: '脏数据2' }],
      timing: 'onEnter',
      channels: ['system'],
    })
    expect(parseCcConfig(json).users).toEqual([{ userId: 123, userName: '张三' }])
  })

  it('缺字段 → 各自回退默认值（timing 缺失回退 onEnter，channels 缺失回退 ["system"]，users 缺失回退 []）', () => {
    expect(parseCcConfig(JSON.stringify({}))).toEqual(emptyCcConfig())
    expect(parseCcConfig(JSON.stringify({ users: [{ userId: 1, userName: 'A' }] }))).toEqual({
      users: [{ userId: 1, userName: 'A' }],
      timing: 'onEnter',
      channels: ['system'],
    })
    expect(parseCcConfig(JSON.stringify({ timing: 'always' }))).toEqual({
      users: [],
      timing: 'always',
      channels: ['system'],
    })
  })

  it('非法 JSON → 静默降级为空配置，不抛异常', () => {
    expect(parseCcConfig('{bad json')).toEqual(emptyCcConfig())
    expect(() => parseCcConfig('{bad json')).not.toThrow()
  })
})

describe('serializeCcConfig', () => {
  it('users 为空 → 返回 undefined（不落配置，引擎语义：不触发抄送）', () => {
    expect(serializeCcConfig(emptyCcConfig())).toBeUndefined()
    expect(serializeCcConfig({ users: [], timing: 'always', channels: ['system', 'dingtalk'] })).toBeUndefined()
  })

  it('users 非空 → JSON 序列化整对象', () => {
    const cfg = { users: [{ userId: 123, userName: '张三' }], timing: 'onReject', channels: ['dingtalk'] }
    expect(serializeCcConfig(cfg)).toBe(JSON.stringify(cfg))
  })

  it('parse → serialize round-trip 保值', () => {
    const original = JSON.stringify({
      users: [{ userId: 9, userName: '王五' }],
      timing: 'always',
      channels: ['system', 'dingtalk'],
    })
    const roundTripped = serializeCcConfig(parseCcConfig(original))
    expect(JSON.parse(roundTripped!)).toEqual(JSON.parse(original))
  })
})

describe('选项常量', () => {
  it('CC_TIMING_OPTIONS 四项时机齐全', () => {
    expect(CC_TIMING_OPTIONS.map(o => o.value)).toEqual(['onEnter', 'onApprove', 'onReject', 'always'])
  })

  it('CC_CHANNEL_OPTIONS 企微/bot 标记禁用（未实装渠道不可选）', () => {
    const byValue = Object.fromEntries(CC_CHANNEL_OPTIONS.map(o => [o.value, o.disabled]))
    expect(byValue.system).toBe(false)
    expect(byValue.dingtalk).toBe(false)
    expect(byValue.wecom).toBe(true)
    expect(byValue.bot).toBe(true)
  })
})
