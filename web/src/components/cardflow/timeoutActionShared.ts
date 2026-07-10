/**
 * 人工节点「超时升级链」配置（CfStageDefinition.FTimeoutActionJson）单一真源。
 * 引擎侧消费见 StageTimeoutReminderJob（M8-C）：levels 按 multiplier（超时时长倍数）升序逐级判定，
 * 命中级别执行对应 action；levels 为空 = 仅提醒（现有行为，M2-7），与 timeoutHours 配合——
 * timeoutHours=0 或留空时整条升级链不生效（引擎无超时基准可比对）。
 */

export interface TimeoutLevel {
  multiplier: number
  action: string
}

export interface TimeoutActionConfig {
  levels: TimeoutLevel[]
}

export const TIMEOUT_ACTION_OPTIONS = [
  { value: 'remind', label: '提醒处理人' },
  { value: 'autoApprove', label: '自动通过' },
  { value: 'autoReject', label: '自动驳回' },
  { value: 'escalate', label: '升级到上级' },
] as const

export function emptyTimeoutActionConfig(): TimeoutActionConfig {
  return { levels: [] }
}

/** 解析 timeoutActionJson（容错：非法 JSON / 缺字段一律回退空配置，不抛异常）。 */
export function parseTimeoutAction(json?: string | null): TimeoutActionConfig {
  if (!json) return emptyTimeoutActionConfig()
  try {
    const raw = JSON.parse(json)
    const levels = Array.isArray(raw?.levels)
      ? raw.levels
          .filter((l: any) => Number(l?.multiplier) > 0 && typeof l?.action === 'string' && l.action)
          .map((l: any) => ({ multiplier: Number(l.multiplier), action: l.action }))
      : []
    return { levels }
  } catch {
    return emptyTimeoutActionConfig()
  }
}

/** 序列化写回 timeoutActionJson：级别为空则不落配置（引擎语义：无 levels = 仅提醒）。 */
export function serializeTimeoutAction(cfg: TimeoutActionConfig): string | undefined {
  return cfg.levels.length > 0 ? JSON.stringify(cfg) : undefined
}
