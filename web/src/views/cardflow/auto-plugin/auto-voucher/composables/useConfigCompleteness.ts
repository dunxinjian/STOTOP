import { computed, type Ref } from 'vue'
import type { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'

// ==================== 类型定义 ====================

export interface LineCompleteness {
  score: number            // 0-100
  required: string[]       // 必填项列表
  missing: string[]        // 未填项
  warnings: string[]       // 非致命警告
}

export interface GroupCompleteness {
  score: number
  lineScores: LineCompleteness[]
}

export interface RuleCompleteness {
  score: number
  groupScores: GroupCompleteness[]
  totalLines: number
  completeLines: number    // score === 100
  incompleteLines: number  // score < 100
}

// ==================== 辅助函数 ====================

/** 检查值是否为有效非空 */
function isFilled(value: any): boolean {
  if (value == null) return false
  if (typeof value === 'string') return value.trim().length > 0
  if (Array.isArray(value)) return value.length > 0
  if (typeof value === 'number') return true
  return !!value
}

/** 计算单行分录完整度 */
function computeLineCompleteness(
  line: any,
  stagingColumnNames: string[],
  accountIds: Set<number | string>,
): LineCompleteness {
  const required: string[] = []
  const missing: string[] = []
  const warnings: string[] = []

  // 1. direction — 始终必填
  required.push('direction')
  if (!isFilled(line.direction)) missing.push('direction')

  // 2. amountField — 始终必填
  required.push('amountField')
  if (!isFilled(line.amountField)) missing.push('amountField')

  // 3. accountId — 当 accountMode === 'fixed' 时必填
  if (line.accountMode === 'fixed') {
    required.push('accountId')
    if (!isFilled(line.accountId)) missing.push('accountId')
  }

  // 4. accountMatchField + accountMatchRules — 当 accountMode === 'dynamic' 时必填
  if (line.accountMode === 'dynamic') {
    required.push('accountMatchField+Rules')
    const hasField = isFilled(line.accountMatchField)
    const hasRules = Array.isArray(line.accountMatchRules) && line.accountMatchRules.length >= 1
    if (!hasField || !hasRules) {
      missing.push('accountMatchField+Rules')
    }
  }

  // 5. conditionValues — 当已填 conditionField 时必填
  if (isFilled(line.conditionField)) {
    required.push('conditionValues')
    if (!Array.isArray(line.conditionValues) || line.conditionValues.length === 0) {
      missing.push('conditionValues')
    }
  }

  // 6. summaryTemplate — 始终必填
  required.push('summaryTemplate')
  if (!isFilled(line.summaryTemplate)) missing.push('summaryTemplate')

  // ==================== 交叉校验（生成 warnings） ====================

  // amountField 必须存在于当前 stagingTable 的列名列表中
  if (isFilled(line.amountField) && stagingColumnNames.length > 0) {
    if (!stagingColumnNames.includes(line.amountField)) {
      warnings.push(`amountField "${line.amountField}" 不在暂存表列中`)
    }
  }

  // conditionField 必须存在于当前 stagingTable 的列名列表中
  if (isFilled(line.conditionField) && stagingColumnNames.length > 0) {
    if (!stagingColumnNames.includes(line.conditionField)) {
      warnings.push(`conditionField "${line.conditionField}" 不在暂存表列中`)
    }
  }

  // accountMatchField 必须存在于当前 stagingTable 的列名列表中
  if (isFilled(line.accountMatchField) && stagingColumnNames.length > 0) {
    if (!stagingColumnNames.includes(line.accountMatchField)) {
      warnings.push(`accountMatchField "${line.accountMatchField}" 不在暂存表列中`)
    }
  }

  // 动态科目的 accountMatchRules 中每条 value→accountId 映射的科目必须存在于当前账套科目树中
  if (line.accountMode === 'dynamic' && Array.isArray(line.accountMatchRules) && accountIds.size > 0) {
    for (const rule of line.accountMatchRules) {
      if (rule.accountId && !accountIds.has(rule.accountId) && !accountIds.has(Number(rule.accountId))) {
        warnings.push(`动态科目规则中 accountId=${rule.accountId} 不在科目树中`)
      }
    }
  }

  // 计分
  const applicable = required.length
  const filled = applicable - missing.length
  const score = applicable > 0 ? Math.round(filled / applicable * 100) : 0

  return { score, required, missing, warnings }
}

/** 提取科目树中所有科目ID（递归） */
function extractAccountIds(tree: any[]): Set<number | string> {
  const ids = new Set<number | string>()
  function walk(nodes: any[]) {
    if (!Array.isArray(nodes)) return
    for (const node of nodes) {
      if (node.id != null) ids.add(node.id)
      if (node.accountId != null) ids.add(node.accountId)
      if (Array.isArray(node.children)) walk(node.children)
    }
  }
  walk(tree)
  return ids
}

// ==================== Composable 导出 ====================

export function useConfigCompleteness(
  formData: Ref<any>,
  store: ReturnType<typeof useAutoVoucherRuleStore>,
) {
  const ruleCompleteness = computed<RuleCompleteness>(() => {
    const data = formData.value
    if (!data || !Array.isArray(data.ruleGroups)) {
      return { score: 0, groupScores: [], totalLines: 0, completeLines: 0, incompleteLines: 0 }
    }

    // 获取暂存表列名
    const tableName = data.stagingTable || data.tableName || ''
    const columns = store.stagingColumns[tableName] ?? []
    const stagingColumnNames: string[] = columns.map((c: any) =>
      typeof c === 'string' ? c : (c.columnName || c.name || ''),
    )

    // 获取科目树ID集合
    const accountSetId = data.accountSetId
    const tree = accountSetId ? (store.accountTree[accountSetId] ?? []) : []
    const accountIds = extractAccountIds(tree)

    // 计算每组完整度
    const groupScores: GroupCompleteness[] = []
    let totalLines = 0
    let completeLines = 0
    let incompleteLines = 0

    for (const group of data.ruleGroups) {
      const lines: any[] = Array.isArray(group.lines) ? group.lines : (Array.isArray(group.entries) ? group.entries : [])
      const lineScores: LineCompleteness[] = []

      for (const line of lines) {
        const lc = computeLineCompleteness(line, stagingColumnNames, accountIds)
        lineScores.push(lc)
        totalLines++
        if (lc.score === 100) completeLines++
        else incompleteLines++
      }

      const groupScore = lineScores.length > 0
        ? Math.round(lineScores.reduce((sum, ls) => sum + ls.score, 0) / lineScores.length)
        : 0

      groupScores.push({ score: groupScore, lineScores })
    }

    // 规则完整度 = 所有组完整度平均值
    const ruleScore = groupScores.length > 0
      ? Math.round(groupScores.reduce((sum, gs) => sum + gs.score, 0) / groupScores.length)
      : 0

    return { score: ruleScore, groupScores, totalLines, completeLines, incompleteLines }
  })

  return { ruleCompleteness }
}
