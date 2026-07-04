/**
 * 节点视图访问级别的前端归一（镜像后端 StageAccessLevels）：Trim + 小写，未知/拼错/null/空 一律 fail-closed → masked。
 * 供预览（B3 preview-presentation）与运行时渲染（SchemaRenderer/CardComponentRenderer）统一判定可见性，
 * 避免各处散点比较字符串导致大小写/空白口径漂移。
 */
export type StageAccess = 'hidden' | 'masked' | 'readonly' | 'editable' | 'required'

export function normalizeAccess(access?: string | null): StageAccess {
  switch ((access || '').trim().toLowerCase()) {
    case 'hidden':
      return 'hidden'
    case 'masked':
      return 'masked'
    case 'readonly':
      return 'readonly'
    case 'editable':
      return 'editable'
    case 'required':
      return 'required'
    default:
      return 'masked'
  }
}

export function isHiddenAccess(access?: string | null): boolean {
  return normalizeAccess(access) === 'hidden'
}

export function isMaskedAccess(access?: string | null): boolean {
  return normalizeAccess(access) === 'masked'
}

/** 可编辑（editable 或 required）。 */
export function isEditableAccess(access?: string | null): boolean {
  const normalized = normalizeAccess(access)
  return normalized === 'editable' || normalized === 'required'
}

export function isRequiredAccess(access?: string | null): boolean {
  return normalizeAccess(access) === 'required'
}
