export type FlowStatus = 'draft' | 'published' | 'disabled' | 'archived'

export type StatusTagType = 'success' | 'warning' | 'danger' | 'info' | 'default'

export interface FlowStatusMeta {
  text: string
  tagType: StatusTagType
}

/** 流程定义状态单一真源：列表页与编辑页共用，杜绝文案/颜色分叉 */
export const FLOW_STATUS_META: Record<FlowStatus, FlowStatusMeta> = {
  draft:     { text: '草稿',   tagType: 'default' },
  published: { text: '已发布', tagType: 'success' },
  disabled:  { text: '已停用', tagType: 'warning' },
  archived:  { text: '已归档', tagType: 'default' },
}

export const FLOW_STATUS_OPTIONS: Array<{ label: string; value: FlowStatus }> =
  (Object.keys(FLOW_STATUS_META) as FlowStatus[]).map((value) => ({
    label: FLOW_STATUS_META[value].text,
    value,
  }))
