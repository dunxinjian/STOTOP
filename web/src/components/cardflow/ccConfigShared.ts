/**
 * 人工节点「抄送/通知」配置（CfStageDefinition.FCcConfigJson）单一真源。
 * 引擎侧消费见 onEnter/onApprove/onReject 三处触点：channels 缺省 ["system"]，timing 缺省 "onEnter"，
 * users 为空则不触发抄送。与「抄送节点」（auto 节点 + AlertNotify 插件预设，见 stageDefinitionShared.ts
 * 的 isCcStage/NOTIFY_PLUGIN_REGISTRY_ID）是两套并行机制：本文件仅服务人工节点内联抄送配置。
 */

export interface CcUser {
  userId: number
  userName: string
}

export interface CcNotifyConfig {
  users: CcUser[]
  timing: string
  channels: string[]
}

export const CC_TIMING_OPTIONS = [
  { value: 'onEnter', label: '进入节点时' },
  { value: 'onApprove', label: '审批通过时' },
  { value: 'onReject', label: '审批驳回时' },
  { value: 'always', label: '全部时机' },
] as const

export const CC_CHANNEL_OPTIONS = [
  { value: 'system', label: '应用内待办', disabled: false },
  { value: 'dingtalk', label: '钉钉', disabled: false },
  { value: 'wecom', label: '企微', disabled: true },
  { value: 'bot', label: 'Bot/Webhook', disabled: true },
] as const

export function emptyCcConfig(): CcNotifyConfig {
  return { users: [], timing: 'onEnter', channels: ['system'] }
}

/** 解析 ccConfigJson（容错：非法 JSON / 缺字段一律回退默认值，不抛异常）。 */
export function parseCcConfig(json?: string | null): CcNotifyConfig {
  if (!json) return emptyCcConfig()
  try {
    const raw = JSON.parse(json)
    return {
      users: Array.isArray(raw?.users) ? raw.users.filter((u: any) => u?.userId > 0) : [],
      timing: typeof raw?.timing === 'string' ? raw.timing : 'onEnter',
      channels: Array.isArray(raw?.channels) ? raw.channels : ['system'],
    }
  } catch {
    return emptyCcConfig()
  }
}

/** 序列化写回 ccConfigJson：抄送对象为空则不落配置（引擎语义：无 users = 不触发抄送）。 */
export function serializeCcConfig(cfg: CcNotifyConfig): string | undefined {
  return cfg.users.length > 0 ? JSON.stringify(cfg) : undefined
}
