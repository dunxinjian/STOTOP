import { describe, it, expect } from 'vitest'
import {
  TIMEOUT_ACTION_OPTIONS,
  emptyTimeoutActionConfig,
  parseTimeoutAction,
  serializeTimeoutAction,
} from './timeoutActionShared'

/**
 * timeoutActionShared 纯逻辑门禁（M8-C 件①）。
 * parseTimeoutAction/serializeTimeoutAction 镜像引擎侧消费口径（StageTimeoutReminderJob 按
 * multiplier 升序逐级判定）：levels 空 = 仅提醒（序列化为 undefined，不落配置）。
 */

describe('emptyTimeoutActionConfig', () => {
  it('默认级别列表为空', () => {
    expect(emptyTimeoutActionConfig()).toEqual({ levels: [] })
  })
})

describe('parseTimeoutAction', () => {
  it('json 缺失（null/undefined/空串）→ 回退空配置', () => {
    expect(parseTimeoutAction(null)).toEqual(emptyTimeoutActionConfig())
    expect(parseTimeoutAction(undefined)).toEqual(emptyTimeoutActionConfig())
    expect(parseTimeoutAction('')).toEqual(emptyTimeoutActionConfig())
  })

  it('合法 json → 原样解析 levels（多级）', () => {
    const json = JSON.stringify({
      levels: [
        { multiplier: 1, action: 'remind' },
        { multiplier: 2, action: 'autoApprove' },
      ],
    })
    expect(parseTimeoutAction(json)).toEqual({
      levels: [
        { multiplier: 1, action: 'remind' },
        { multiplier: 2, action: 'autoApprove' },
      ],
    })
  })

  it('levels 中 multiplier 非正数或 action 缺失的条目被过滤（防脏数据回显幽灵级别）', () => {
    const json = JSON.stringify({
      levels: [
        { multiplier: 1, action: 'remind' },
        { multiplier: 0, action: 'autoReject' },
        { multiplier: -1, action: 'escalate' },
        { multiplier: 2, action: '' },
        { multiplier: 3 },
      ],
    })
    expect(parseTimeoutAction(json).levels).toEqual([{ multiplier: 1, action: 'remind' }])
  })

  it('levels 缺失 → 回退空数组', () => {
    expect(parseTimeoutAction(JSON.stringify({}))).toEqual(emptyTimeoutActionConfig())
  })

  it('非法 JSON → 静默降级为空配置，不抛异常', () => {
    expect(parseTimeoutAction('{bad json')).toEqual(emptyTimeoutActionConfig())
    expect(() => parseTimeoutAction('{bad json')).not.toThrow()
  })
})

describe('serializeTimeoutAction', () => {
  it('levels 为空 → 返回 undefined（不落配置，引擎语义：仅提醒）', () => {
    expect(serializeTimeoutAction(emptyTimeoutActionConfig())).toBeUndefined()
    expect(serializeTimeoutAction({ levels: [] })).toBeUndefined()
  })

  it('levels 非空 → JSON 序列化整对象', () => {
    const cfg = { levels: [{ multiplier: 1, action: 'remind' }, { multiplier: 3, action: 'escalate' }] }
    expect(serializeTimeoutAction(cfg)).toBe(JSON.stringify(cfg))
  })

  it('parse → serialize round-trip 保值', () => {
    const original = JSON.stringify({
      levels: [
        { multiplier: 1, action: 'remind' },
        { multiplier: 2, action: 'autoReject' },
        { multiplier: 3, action: 'escalate' },
      ],
    })
    const roundTripped = serializeTimeoutAction(parseTimeoutAction(original))
    expect(JSON.parse(roundTripped!)).toEqual(JSON.parse(original))
  })
})

describe('选项常量', () => {
  it('TIMEOUT_ACTION_OPTIONS 四项动作齐全', () => {
    expect(TIMEOUT_ACTION_OPTIONS.map(o => o.value)).toEqual(['remind', 'autoApprove', 'autoReject', 'escalate'])
  })
})
