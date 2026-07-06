import type { StageDefinition } from './StageDefinitionEditor.vue'
import type { SchemaFieldDefinition } from '@/types/cardflow'

/**
 * StageDefinitionEditor / StageConfigPanel 共用的纯逻辑与常量单一真源。
 * 抽出以避免容器（节点链左栏徽标）与面板（右栏健康横幅 / buildAssigneeConfig）两处重复。
 * 仅 import type（运行时无环）：本文件被两个组件以值方式引入，StageDefinitionEditor.vue 仅提供类型。
 */

/** 人工节点默认允许动作（newStage 与 ensureStageConfigDefaults 共用）。 */
export const DEFAULT_ACTIONS = ['approve', 'reject', 'returnToStage', 'transfer', 'addSignAfter', 'cc']

/** 解析节点处理人配置 JSON（容错，失败返回 null）。 */
export function parseAssigneeConfig(stage: StageDefinition) {
  if (!stage.assigneeConfigJson) return null
  try {
    return JSON.parse(stage.assigneeConfigJson)
  } catch {
    return null
  }
}

/** 节点健康诊断（纯函数；节点链左栏徽标与右栏面板横幅共用）。detailSchemaFields 用于明细权限提醒。 */
export function getStageHealth(stage: StageDefinition, detailSchemaFields?: SchemaFieldDefinition[]) {
  const issues: string[] = []
  const warnings: string[] = []

  if (!stage.name?.trim()) issues.push('节点名称未配置')

  if (stage.type === 'manual') {
    const config = parseAssigneeConfig(stage)
    if (!stage.approvalMode) issues.push('审批模式未配置')
    if (!stage.assigneeStrategy) {
      issues.push('处理人策略未配置')
    } else if (stage.assigneeStrategy === 'role' && !config?.roleCode) {
      issues.push('角色处理人未选择')
    } else if (stage.assigneeStrategy === 'fixed' && !(config?.users || []).length) {
      issues.push('固定处理人未选择')
    } else if (stage.assigneeStrategy === 'fieldUsers' && !config?.fieldKey) {
      issues.push('人员字段未选择')
    }
    if (!stage.actionPolicy?.allowedActions?.length) issues.push('允许动作未配置')
    if (!Object.keys(stage.viewProfile?.fieldAccess || {}).length) warnings.push('未单独配置卡片字段权限')
    if ((detailSchemaFields || []).length && !Object.keys(stage.viewProfile?.detailAccess || {}).length) {
      warnings.push('未单独配置明细字段权限')
    }
  }

  if (stage.type === 'auto') {
    if (!stage.processingGranularity) issues.push('处理粒度未配置')
    if (!stage.pluginRegistryId) issues.push('自动插件未选择')
    if (!stage.failurePolicy) warnings.push('失败策略未配置')
  }

  if (stage.conditionJson) {
    try {
      const g = JSON.parse(stage.conditionJson)
      if (!g || typeof g !== 'object' || !Array.isArray(g.conditions)) issues.push('进入条件格式异常')
    } catch {
      issues.push('进入条件 JSON 解析失败')
    }
  }

  return {
    status: issues.length ? 'error' : warnings.length ? 'warning' : 'ok',
    label: issues.length ? `${issues.length} 个问题` : warnings.length ? `${warnings.length} 个提醒` : '正常',
    issues,
    warnings,
  }
}
