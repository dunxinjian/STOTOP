/**
 * 发起范围（startPolicyJson）解析/序列化共享助手（M8-A 件②）。
 * 与后端 `STOTOP.Module.CardFlow.Models.StartPolicyCodec` 镜像同源：
 * - startPolicyJson 存在（哪怕非法）即以新列为准：合法→解析；非法→静默降级为空策略，**不回退** legacy。
 * - startPolicyJson 缺失（null/undefined/空串）才从 legacy 可发起角色JSON 派生角色维（向后兼容显示）。
 */

export interface ScopeDims {
  roles: number[]
  orgs: number[]
  positions: number[]
  users: number[]
}

export interface OnBehalfConfig {
  enabled: boolean
  agentScope: ScopeDims
}

export interface StartPolicy {
  initiatorScope: ScopeDims
  onBehalf: OnBehalfConfig
}

export function emptyScope(): ScopeDims {
  return { roles: [], orgs: [], positions: [], users: [] }
}

export function emptyStartPolicy(): StartPolicy {
  return { initiatorScope: emptyScope(), onBehalf: { enabled: false, agentScope: emptyScope() } }
}

export function isScopeEmpty(s: ScopeDims): boolean {
  return !s.roles.length && !s.orgs.length && !s.positions.length && !s.users.length
}

function nums(a: unknown): number[] {
  return Array.isArray(a) ? a.map((x) => Number(x)).filter((n) => Number.isFinite(n) && n > 0) : []
}

function parseScope(raw: unknown): ScopeDims {
  const s = (raw ?? {}) as Record<string, unknown>
  return { roles: nums(s.roles), orgs: nums(s.orgs), positions: nums(s.positions), users: nums(s.users) }
}

/**
 * 解析后端 startPolicyJson；镜像后端 StartPolicyCodec.Parse 的降级语义：
 * - startPolicyJson 非空：尝试解析；解析失败 → 静默降级为空策略（=不限制），**不回退** allowedRolesJson。
 * - startPolicyJson 为空/缺失：回退 allowedRolesJson 派生角色维（前端兼容显示，无数据回填）。
 */
export function parseStartPolicy(startPolicyJson?: string | null, allowedRolesJson?: string | null): StartPolicy {
  if (startPolicyJson) {
    try {
      const raw = JSON.parse(startPolicyJson) as Record<string, unknown>
      const p = emptyStartPolicy()
      p.initiatorScope = parseScope(raw?.initiatorScope)
      const ob = raw?.onBehalf as Record<string, unknown> | undefined
      if (ob) {
        p.onBehalf = { enabled: !!ob.enabled, agentScope: parseScope(ob.agentScope) }
      }
      return p
    } catch {
      // 新列存在但非法：静默降级为空策略，不回退 legacy（与后端 StartPolicyCodec.Parse 一致）
      return emptyStartPolicy()
    }
  }
  const p = emptyStartPolicy()
  if (allowedRolesJson) {
    try {
      p.initiatorScope.roles = nums(JSON.parse(allowedRolesJson))
    } catch { /* 非法 legacy JSON 静默降级为不限制 */ }
  }
  return p
}

/** 序列化为后端列值；全空返回 undefined（=不写策略，保持不限制）。 */
export function serializeStartPolicy(p: StartPolicy): string | undefined {
  const meaningful = !isScopeEmpty(p.initiatorScope) || p.onBehalf.enabled || !isScopeEmpty(p.onBehalf.agentScope)
  return meaningful ? JSON.stringify(p) : undefined
}
