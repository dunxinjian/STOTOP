import { describe, it, expect } from 'vitest'
import {
  emptyScope,
  emptyStartPolicy,
  isScopeEmpty,
  parseStartPolicy,
  serializeStartPolicy,
  type StartPolicy,
} from './startPolicyShared'

/**
 * startPolicyShared 纯逻辑门禁（M8-A 件②）。
 * parseStartPolicy 的"present-but-malformed 不回退 legacy"分支镜像后端
 * StartPolicyCodec.Parse（tests/.../InitiatorScopeResolverTests.cs 非法startPolicyJson_有legacy_不回退legacy仍不限制），
 * 前后端漂移即回归。
 */

describe('emptyScope / isScopeEmpty', () => {
  it('emptyScope 四维皆空数组；isScopeEmpty 判定为空', () => {
    const s = emptyScope()
    expect(s).toEqual({ roles: [], orgs: [], positions: [], users: [] })
    expect(isScopeEmpty(s)).toBe(true)
    expect(isScopeEmpty({ ...s, roles: [1] })).toBe(false)
  })
})

describe('parseStartPolicy', () => {
  it('startPolicyJson 缺失 → 回退 legacy 可发起角色JSON 派生角色维', () => {
    const p = parseStartPolicy(null, '["10","11"]')
    expect(p.initiatorScope.roles).toEqual([10, 11])
    expect(p.initiatorScope.orgs).toEqual([])
  })

  it('startPolicyJson 缺失且 legacy 也缺失 → 空策略', () => {
    expect(parseStartPolicy(null, null)).toEqual(emptyStartPolicy())
    expect(parseStartPolicy(undefined, undefined)).toEqual(emptyStartPolicy())
  })

  it('startPolicyJson 合法 → 解析四维 + onBehalf', () => {
    const json = JSON.stringify({
      initiatorScope: { roles: [1, 2], orgs: [10], positions: [], users: [99] },
      onBehalf: { enabled: true, agentScope: { roles: [3], orgs: [], positions: [], users: [] } },
    })
    const p = parseStartPolicy(json, '["999"]')
    expect(p.initiatorScope).toEqual({ roles: [1, 2], orgs: [10], positions: [], users: [99] })
    expect(p.onBehalf).toEqual({ enabled: true, agentScope: { roles: [3], orgs: [], positions: [], users: [] } })
  })

  it('startPolicyJson 存在但非法 + legacy 存在 → 降级为空策略，不回退 legacy（镜像后端）', () => {
    const p = parseStartPolicy('{bad json', '["10"]')
    expect(p).toEqual(emptyStartPolicy())
    expect(p.initiatorScope.roles).toEqual([])
  })

  it('startPolicyJson 存在但非法 + legacy 缺失 → 降级为空策略', () => {
    const p = parseStartPolicy('{bad json', null)
    expect(p).toEqual(emptyStartPolicy())
  })

  it('非法 legacy JSON 单独出现 → 静默降级不抛', () => {
    expect(() => parseStartPolicy(null, '{bad')).not.toThrow()
    expect(parseStartPolicy(null, '{bad').initiatorScope.roles).toEqual([])
  })
})

describe('serializeStartPolicy', () => {
  it('全空策略 → undefined（不写策略=不限制）', () => {
    expect(serializeStartPolicy(emptyStartPolicy())).toBeUndefined()
  })

  it('仅 initiatorScope 有值 → 返回 JSON 字符串', () => {
    const p = emptyStartPolicy()
    p.initiatorScope.roles = [1]
    expect(serializeStartPolicy(p)).toBe(JSON.stringify(p))
  })

  it('round-trip：parse(serialize(x)) 保值', () => {
    const p: StartPolicy = {
      initiatorScope: { roles: [1, 2], orgs: [10, 20], positions: [30], users: [99] },
      onBehalf: { enabled: true, agentScope: { roles: [], orgs: [], positions: [], users: [5] } },
    }
    const json = serializeStartPolicy(p)
    expect(json).toBeDefined()
    expect(parseStartPolicy(json)).toEqual(p)
  })
})
