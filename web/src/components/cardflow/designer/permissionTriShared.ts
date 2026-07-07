/**
 * PermissionTri 三态权限胶囊的纯逻辑（设计 A6/C2）。
 * 锁定项渲染划线+tooltip 原因、点击无效；M2 抽屉与 M4 矩阵共用。
 */
export type PermissionValue = 'edit' | 'read' | 'hidden'

export const PERMISSION_TRI_LABELS: Record<PermissionValue, string> = {
  edit: '可编辑',
  read: '只读',
  hidden: '隐藏',
}

export interface TriState {
  value: PermissionValue
  label: string
  active: boolean
  locked: boolean
}

const ORDER: PermissionValue[] = ['edit', 'read', 'hidden']

export function buildTriStates(current: PermissionValue, lockedStates: PermissionValue[]): TriState[] {
  return ORDER.map((value) => ({
    value,
    label: PERMISSION_TRI_LABELS[value],
    active: value === current,
    locked: lockedStates.includes(value),
  }))
}
