/**
 * PermissionTri 权限胶囊的纯逻辑（设计 A6/C2）。
 * 访问级四态（editable/readonly/masked/hidden）——masked 为 CardFlow 脱敏链核心语义，
 * 对齐 StageViewProfile access 枚举（引擎语义优先于 mock 三态）。
 * 锁定项渲染划线+tooltip 原因、点击无效；M2 抽屉与 M4 矩阵共用。
 */
export type PermissionValue = 'editable' | 'readonly' | 'masked' | 'hidden'

export const PERMISSION_TRI_LABELS: Record<PermissionValue, string> = {
  editable: '可编辑',
  readonly: '只读',
  masked: '脱敏',
  hidden: '隐藏',
}

export interface TriState {
  value: PermissionValue
  label: string
  active: boolean
  locked: boolean
}

const ORDER: PermissionValue[] = ['editable', 'readonly', 'masked', 'hidden']

export function buildTriStates(
  current: PermissionValue,
  lockedStates: PermissionValue[],
  values: PermissionValue[] = ORDER,
): TriState[] {
  return values.map((value) => ({
    value,
    label: PERMISSION_TRI_LABELS[value],
    active: value === current,
    locked: lockedStates.includes(value),
  }))
}
