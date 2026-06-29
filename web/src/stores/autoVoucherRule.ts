import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getAccountTree, getAccountSets, getAuxiliaryItemsByAccountSet } from '@/api/finance'
import { getStagingTables, getStagingTableColumns, getAutoPluginRuleSilent, createAutoPluginRule, updateAutoPluginRule } from '@/api/cardflow'
import type { AutoPluginRuleDto } from '@/api/cardflow'

// ==================== 辅助核算类型映射 ====================
const AUX_TYPE_MAP: Record<string, string> = {
  '客户': 'customer',
  '供应商': 'supplier',
  '部门': 'department',
  '项目': 'project',
  '员工': 'employee',
  '经营单元': 'business_unit',
  '快递品牌': 'express_brand',
  '网点': 'outlet',
}
const AUX_CODE_MAP: Record<string, string> = Object.fromEntries(
  Object.entries(AUX_TYPE_MAP).map(([k, v]) => [v, k])
)
function auxTypeToCode(name: string): string {
  return AUX_TYPE_MAP[name] || name
}
function auxCodeToName(code: string): string {
  return AUX_CODE_MAP[code] || code
}

// ==================== 类型定义 ====================
export interface AuxiliaryConfig {
  name: string
  type: 'fixed' | 'field'
  value?: string
  field?: string
  matchMode?: 'exact_code' | 'exact_name' | 'source_contains_name' | 'name_contains_source'
}

export interface AccountMatchRule {
  value: string
  accountId?: number
}

export interface EntryLine {
  id: string
  direction: '借' | '贷'
  accountMode: 'fixed' | 'dynamic'
  accountId?: number | null
  accountCode?: string  // V2新字段
  accountMatchField?: string | null
  accountMatchRules: AccountMatchRule[]
  amountField: string
  amountAggregation: 'SUM' | 'ROW'
  summaryTemplate: string
  conditionField?: string | null
  conditionValues: string[]
  auxiliaryConfigs: AuxiliaryConfig[]
  defaultAuxiliaryConfigs?: AuxiliaryConfig[] // [方案B] 科目契约声明、行内未覆盖的维度的补齐配置(透传后端 EntryLineV2.DefaultAuxiliaryConfigs)
  status: number // 1=启用 0=禁用
  remark?: string
}

export interface RuleGroup {
  id: string
  name: string
  order?: number
  exactCodes?: string[]       // V2: 精确匹配科目编码
  exactCategories?: string[]  // V2: 精确匹配费用类别
  categoryKeywords?: string[] // V2: 类别关键字
  summaryKeywords?: string[]  // V2: 摘要关键字
  amountAggregation?: string  // V2: 组级别金额聚合
  lines: EntryLine[]
}

export interface FormData {
  mode: 'rulesBased'
  ruleName: string
  description: string
  status: number
  stagingTable: string
  accountSetId: number | undefined
  groupBy: string
  voucherWord: string
  dateField: string
  keyFields: string[]
  unmatchedAction: string
  matchingLayers: {
    exactMatchField: string
    categoryField: string
    summaryField: string
  }
  ruleGroups: RuleGroup[]
  _isV2?: boolean  // 标记是否为V2格式
}

function createEmptyFormData(): FormData {
  return {
    mode: 'rulesBased',
    ruleName: '',
    description: '',
    status: 1,
    stagingTable: '',
    accountSetId: undefined,
    groupBy: '',
    voucherWord: '记',
    dateField: 'F业务日期',
    keyFields: [],
    unmatchedAction: 'skip',
    matchingLayers: {
      exactMatchField: '',
      categoryField: '',
      summaryField: '',
    },
    ruleGroups: [],
  }
}

function createEmptyLine(): EntryLine {
  return {
    id: `line_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    direction: '借',
    accountMode: 'fixed',
    accountId: undefined,
    accountMatchField: null,
    accountMatchRules: [],
    amountField: '',
    amountAggregation: 'ROW',
    summaryTemplate: '',
    conditionField: null,
    conditionValues: [],
    auxiliaryConfigs: [],
    status: 1,
    remark: '',
  }
}

let _groupCounter = 0

export const useAutoVoucherRuleStore = defineStore('autoVoucherRule', () => {
  // ==================== 会话级缓存 ====================
  const stagingTables = ref<any[]>([])
  const stagingColumns = ref<Record<string, any[]>>({})
  const accountSets = ref<any[]>([])
  const accountTree = ref<Record<number, any[]>>({})
  const auxItemsCache = ref<Record<string, Array<{ value: string; label: string }>>>({})

  // ==================== 当前编辑规则 ====================
  const currentRule = ref<AutoPluginRuleDto | null>(null)
  const formData = ref<FormData>(createEmptyFormData())
  const hitStats = ref<any>(null)
  const isDirty = ref(false)
  const ruleLoading = ref(false)

  // ==================== 字段级校验错误（key = lineId，value = { fieldName: errorMsg }） ====================
  const entryErrors = ref<Record<string, Record<string, string>>>({})

  // ==================== 当前选中状态 ====================
  const selectedGroupId = ref<string | null>(null)
  const selectedLineId = ref<string | null>(null)

  // ==================== 计算属性 ====================
  const currentGroup = computed(() => {
    if (!selectedGroupId.value) return null
    return formData.value.ruleGroups.find(g => g.id === selectedGroupId.value) ?? null
  })

  const currentLines = computed(() => currentGroup.value?.lines ?? [])

  const currentLine = computed(() => {
    if (!selectedLineId.value) return null
    return currentLines.value.find(l => l.id === selectedLineId.value) ?? null
  })

  const currentStagingFields = computed(() => {
    if (!formData.value.stagingTable) return []
    const cols = stagingColumns.value[formData.value.stagingTable] ?? []
    const systemCols = new Set(['FId', 'FBatchId', 'FStatus', 'FErrorMessage',
      'FCreateTime', 'FUpdateTime', 'FImportBatchNo', 'FOtherColumnsData'])
    return cols.map((c: any) => c.columnName).filter((n: string) => !systemCols.has(n))
  })

  function buildTreeData(items: any[]): any[] {
    return items.map(item => ({
      id: item.id ?? item.fid,
      title: `${item.code ?? item.fCode ?? ''} ${item.name ?? item.fName ?? ''}`.trim(),
      children: item.children ? buildTreeData(item.children) : undefined,
      selectable: !item.children || item.children.length === 0,
    }))
  }

  const accountTreeData = computed(() => {
    if (!formData.value.accountSetId) return []
    const raw = accountTree.value[formData.value.accountSetId]
    if (!raw) return []
    return buildTreeData(raw)
  })

  // ==================== 缓存加载方法 ====================
  async function loadStagingTables() {
    if (stagingTables.value.length > 0) return
    try {
      const res: any = await getStagingTables()
      stagingTables.value = res ?? []
    } catch (e) {
      console.error('加载暂存表失败:', e)
    }
  }

  async function loadStagingColumns(tableName: string) {
    if (!tableName) return
    if (stagingColumns.value[tableName]) return
    try {
      const res: any = await getStagingTableColumns(tableName)
      stagingColumns.value[tableName] = res ?? []
    } catch (e) {
      console.error(`加载暂存表 ${tableName} 列失败:`, e)
    }
  }

  async function loadAccountSets() {
    if (accountSets.value.length > 0) return
    try {
      const res: any = await getAccountSets()
      accountSets.value = res ?? []
    } catch (e) {
      console.error('加载账套失败:', e)
    }
  }

  async function loadAccountTree(setId: number) {
    if (accountTree.value[setId]) return
    try {
      const res: any = await getAccountTree(undefined, setId)
      accountTree.value[setId] = res ?? []
    } catch (e) {
      console.error('加载科目树失败:', e)
    }
  }

  async function loadAuxItems(auxName: string, auxType: string) {
    if (auxItemsCache.value[auxName]) return
    if (!formData.value.accountSetId) return
    try {
      const res: any = await getAuxiliaryItemsByAccountSet({
        accountSetId: formData.value.accountSetId,
        auxType,
      })
      const items = (res ?? []).map((item: any) => ({
        value: `${item.code || item.id}`,
        label: `${item.code ? item.code + ' ' : ''}${item.name}`,
      }))
      auxItemsCache.value[auxName] = items
    } catch {
      auxItemsCache.value[auxName] = []
    }
  }

  // ==================== 规则加载/保存 ====================
  async function loadRule(id: number) {
    ruleLoading.value = true
    try {
      const res: any = await getAutoPluginRuleSilent(id)
      if (!res) {
        // 规则不存在，抛出错误给上层处理
        throw new Error('RULE_NOT_FOUND')
      }
      currentRule.value = res
      if (res?.configJson) {
        const rawConfig = JSON.parse(res.configJson)

        // V2 格式检测：如果有 version=2 和 ruleConfig 则从嵌套对象读取
        const isV2 = rawConfig.version === 2 && rawConfig.ruleConfig
        const config = isV2 ? rawConfig.ruleConfig : rawConfig

        formData.value = {
          mode: 'rulesBased',
          ruleName: res.ruleName ?? '',
          description: res.description ?? '',
          status: res.status ?? 1,
          stagingTable: config.targetTable || config.stagingTable || '',
          accountSetId: rawConfig.accountSetId || config.accountSetId,  // V2中顶层也有accountSetId
          groupBy: config.groupBy ?? '',
          voucherWord: config.voucherWord || '记',
          dateField: config.dateField || 'F业务日期',
          keyFields: config.keyFields || [],
          unmatchedAction: config.unmatchedAction || 'skip',
          matchingLayers: {
            exactMatchField: config.matchingLayers?.exactMatchField ?? '',
            categoryField: config.matchingLayers?.categoryField ?? '',
            summaryField: config.matchingLayers?.summaryField ?? '',
          },
          ruleGroups: (config.ruleGroups || []).map((g: any, gIdx: number) => ({
            id: g.id || `group_${gIdx + 1}`,  // V2有GUID id，保留它
            name: g.name ?? `规则组 ${gIdx + 1}`,
            order: g.order ?? gIdx + 1,
            exactCodes: g.exactCodes || [],
            exactCategories: g.exactCategories || [],
            categoryKeywords: g.categoryKeywords || [],
            summaryKeywords: g.summaryKeywords || [],
            amountAggregation: g.amountAggregation || 'ROW',
            lines: (g.lines || []).map((l: any) => ({
              id: `line_${gIdx}_${Math.random().toString(36).slice(2, 8)}`,
              direction: l.direction ?? '借',
              accountMode: l.accountId ? 'fixed' : (l.accountMatchField ? 'dynamic' : 'fixed'),
              accountId: l.accountId,
              accountCode: l.accountCode || '',
              accountMatchField: l.accountMatchField,
              accountMatchRules: (l.accountMatchRules || []).map((r: any) => ({
                value: r.matchValue || r.value || '',
                accountId: r.accountId,
                accountCode: r.accountCode,
              })),
              amountField: l.amountField ?? '',
              amountAggregation: l.amountAggregation ?? g.amountAggregation ?? 'ROW',
              summaryTemplate: l.summaryTemplate ?? '',
              conditionField: l.conditionField,
              conditionValues: l.conditionValues || [],
              auxiliaryConfigs: (l.auxiliaryConfigs || []).map((a: any) => ({
                name: auxCodeToName(a.auxType || a.name || ''),
                type: a.sourceType === 'dynamic' ? 'field' : (a.sourceType === 'fixed' ? 'fixed' : (a.type || 'fixed')),
                value: a.fixedValue || a.value || '',
                field: a.sourceField || a.field || '',
                matchMode: a.matchBy || a.matchMode || undefined,
              })),
              defaultAuxiliaryConfigs: (l.defaultAuxiliaryConfigs || []).map((a: any) => ({
                name: auxCodeToName(a.auxType || a.name || ''),
                type: a.sourceType === 'dynamic' ? 'field' : (a.sourceType === 'fixed' ? 'fixed' : (a.type || 'fixed')),
                value: a.fixedValue || a.value || '',
                field: a.sourceField || a.field || '',
                matchMode: a.matchBy || a.matchMode || undefined,
              })),
              status: l.status ?? 1,
              remark: l.remark ?? '',
            })),
          })),
          _isV2: isV2,
        }
        _groupCounter = formData.value.ruleGroups.length
        // 自动加载关联数据
        const tasks: Promise<void>[] = []
        if (formData.value.stagingTable) tasks.push(loadStagingColumns(formData.value.stagingTable))
        if (formData.value.accountSetId) tasks.push(loadAccountTree(formData.value.accountSetId))
        await Promise.all(tasks)
      } else {
        formData.value = createEmptyFormData()
        formData.value.ruleName = res?.ruleName ?? ''
        formData.value.description = res?.description ?? ''
        formData.value.status = res?.status ?? 1
      }
      isDirty.value = false
      if (formData.value.ruleGroups.length > 0) {
        selectedGroupId.value = formData.value.ruleGroups[0].id
      }
    } catch (e: any) {
      console.error('加载规则失败:', e)
      // 规则不存在时统一抛出 RULE_NOT_FOUND
      if (e?.message === 'RULE_NOT_FOUND' || e?.message?.includes('不存在') || e?.response?.status === 404) {
        throw new Error('RULE_NOT_FOUND')
      }
      throw e  // 其他错误原样重新抛出
    } finally {
      ruleLoading.value = false
    }
  }

  function buildConfigJson(): string {
    const ruleConfig = {
      mode: 'rulesBased',
      version: 2,
      stagingTable: formData.value.stagingTable,
      accountSetId: formData.value.accountSetId,
      groupBy: formData.value.groupBy || undefined,
      voucherWord: formData.value.voucherWord || '记',
      dateField: formData.value.dateField || 'F业务日期',
      keyFields: formData.value.keyFields || [],
      unmatchedAction: formData.value.unmatchedAction || 'skip',
      filterConditions: [],
      matchingLayers: {
        exactMatchField: formData.value.matchingLayers.exactMatchField || undefined,
        categoryField: formData.value.matchingLayers.categoryField || undefined,
        summaryField: formData.value.matchingLayers.summaryField || undefined,
      },
      ruleGroups: formData.value.ruleGroups.map((g, gIdx) => ({
        id: g.id?.startsWith('group_') ? undefined : g.id,  // 保留GUID格式的id，去掉临时id
        name: g.name,
        order: g.order ?? gIdx + 1,
        exactCodes: g.exactCodes?.length ? g.exactCodes : undefined,
        exactCategories: g.exactCategories?.length ? g.exactCategories : undefined,
        categoryKeywords: g.categoryKeywords?.length ? g.categoryKeywords : undefined,
        summaryKeywords: g.summaryKeywords?.length ? g.summaryKeywords : undefined,
        amountAggregation: g.amountAggregation || 'ROW',
        lines: g.lines.map((l, i) => ({
          lineNo: i + 1,
          direction: l.direction,
          accountId: l.accountId || null,
          accountCode: l.accountCode || undefined,
          accountMatchField: l.accountMatchField || null,
          accountMatchRules: l.accountMatchRules?.length
            ? l.accountMatchRules.map(r => ({
                matchValue: r.value,
                accountId: r.accountId,
                accountCode: (r as any).accountCode,
              }))
            : [],
          amountField: l.amountField,
          amountAggregation: l.amountAggregation || undefined,
          summaryTemplate: l.summaryTemplate || null,
          conditionField: l.conditionField || null,
          conditionValues: l.conditionValues?.length ? l.conditionValues : [],
          auxiliaryConfigs: l.auxiliaryConfigs?.length
            ? l.auxiliaryConfigs.map(a => ({
                auxType: auxTypeToCode(a.name),
                sourceType: a.type === 'field' ? 'dynamic' : 'fixed',
                fixedValue: a.type === 'fixed' ? a.value : undefined,
                sourceField: a.type === 'field' ? a.field : undefined,
                matchBy: a.matchMode || undefined,
              }))
            : [],
          defaultAuxiliaryConfigs: l.defaultAuxiliaryConfigs?.length
            ? l.defaultAuxiliaryConfigs.map(a => ({
                auxType: auxTypeToCode(a.name),
                sourceType: a.type === 'field' ? 'dynamic' : 'fixed',
                fixedValue: a.type === 'fixed' ? a.value : undefined,
                sourceField: a.type === 'field' ? a.field : undefined,
                matchBy: a.matchMode || undefined,
              }))
            : [],
          displayOrder: i + 1,
          status: l.status ?? 1,
        })),
      })),
    }

    // 始终输出V2格式
    const output = JSON.stringify({
      version: 2,
      accountSetId: formData.value.accountSetId,
      ruleConfig,
    })
    return output
  }

  async function saveRule() {
    const configJson = buildConfigJson()
    if (currentRule.value?.id) {
      const res: any = await updateAutoPluginRule(currentRule.value.id, {
        typeCode: 'AutoVoucher',
        ruleName: formData.value.ruleName,
        description: formData.value.description,
        status: formData.value.status,
        configJson,
        concurrencyStamp: currentRule.value.concurrencyStamp,
      })
      // 更新并发戳，避免下次保存时乐观锁冲突
      if (res?.concurrencyStamp) {
        currentRule.value.concurrencyStamp = res.concurrencyStamp
      }
    } else {
      const res: any = await createAutoPluginRule({
        typeCode: 'AutoVoucher',
        ruleName: formData.value.ruleName,
        description: formData.value.description,
        configJson,
      })
      // 新建后也设置 currentRule，使后续保存走 update 路径
      if (res?.id) {
        currentRule.value = res
      }
    }
    isDirty.value = false
  }

  // ==================== 编辑方法：规则组 ====================
  function markDirty() {
    isDirty.value = true
  }

  /** 设置分录行字段级错误 */
  function setEntryErrors(errors: Record<string, Record<string, string>>) {
    entryErrors.value = errors
  }

  /** 清除某行某字段的错误 */
  function clearEntryFieldError(lineId: string, field: string) {
    if (entryErrors.value[lineId]) {
      delete entryErrors.value[lineId][field]
      if (Object.keys(entryErrors.value[lineId]).length === 0) {
        delete entryErrors.value[lineId]
      }
    }
  }

  /** 清除所有校验错误 */
  function clearAllEntryErrors() {
    entryErrors.value = {}
  }

  function addGroup(name?: string): RuleGroup {
    _groupCounter++
    const group: RuleGroup = {
      id: `group_${_groupCounter}_${Date.now()}`,
      name: name || `规则组 ${_groupCounter}`,
      lines: [],
    }
    formData.value.ruleGroups.push(group)
    selectedGroupId.value = group.id
    selectedLineId.value = null
    markDirty()
    return group
  }

  function removeGroup(groupId: string) {
    const idx = formData.value.ruleGroups.findIndex(g => g.id === groupId)
    if (idx < 0) return
    formData.value.ruleGroups.splice(idx, 1)
    if (selectedGroupId.value === groupId) {
      selectedGroupId.value = formData.value.ruleGroups[0]?.id ?? null
      selectedLineId.value = null
    }
    markDirty()
  }

  function updateGroupName(groupId: string, name: string) {
    const g = formData.value.ruleGroups.find(x => x.id === groupId)
    if (g) { g.name = name; markDirty() }
  }

  /**
   * 将某个未匹配值加入指定规则组的匹配字段。
   * Layer1 → exactCodes；Layer2 → exactCategories；Layer3 → summaryKeywords。
   * 返回是否实际添加（重复值返回 false）。
   */
  function addMatchValueToGroup(groupId: string, layerName: string, value: string): boolean {
    const g = formData.value.ruleGroups.find(x => x.id === groupId)
    if (!g) return false
    const v = (value ?? '').trim()
    if (!v) return false
    const fieldKey = layerName === 'Layer1'
      ? 'exactCodes'
      : layerName === 'Layer2'
        ? 'exactCategories'
        : layerName === 'Layer3'
          ? 'summaryKeywords'
          : null
    if (!fieldKey) return false
    const arr = ((g as any)[fieldKey] as string[] | undefined) ?? []
    if (arr.includes(v)) return false
    ;(g as any)[fieldKey] = [...arr, v]
    markDirty()
    return true
  }

  function importGroup(json: string): boolean {
    try {
      const parsed = JSON.parse(json)
      const groups = Array.isArray(parsed) ? parsed : [parsed]
      for (const g of groups) {
        _groupCounter++
        const newGroup: RuleGroup = {
          id: `group_${_groupCounter}_${Date.now()}`,
          name: g.name || `导入组 ${_groupCounter}`,
          lines: (g.lines || []).map((l: any) => ({
            ...createEmptyLine(),
            direction: l.direction ?? '借',
            accountMode: l.accountMode ?? 'fixed',
            accountId: l.accountId,
            accountMatchField: l.accountMatchField,
            accountMatchRules: l.accountMatchRules || [],
            amountField: l.amountField ?? '',
            amountAggregation: l.amountAggregation ?? 'ROW',
            summaryTemplate: l.summaryTemplate ?? '',
            conditionField: l.conditionField,
            conditionValues: l.conditionValues || [],
            auxiliaryConfigs: l.auxiliaryConfigs || [],
            defaultAuxiliaryConfigs: l.defaultAuxiliaryConfigs || [],
            status: l.status ?? 1,
          })),
        }
        formData.value.ruleGroups.push(newGroup)
        selectedGroupId.value = newGroup.id
      }
      markDirty()
      return true
    } catch {
      return false
    }
  }

  // ==================== 编辑方法：分录行 ====================
  function addLine(): EntryLine | null {
    if (!currentGroup.value) return null
    const line = createEmptyLine()
    currentGroup.value.lines.push(line)
    selectedLineId.value = line.id
    markDirty()
    return line
  }

  function removeLine(lineId: string) {
    if (!currentGroup.value) return
    const idx = currentGroup.value.lines.findIndex(l => l.id === lineId)
    if (idx < 0) return
    currentGroup.value.lines.splice(idx, 1)
    if (selectedLineId.value === lineId) {
      selectedLineId.value = null
    }
    markDirty()
  }

  function removeLines(lineIds: string[]) {
    if (!currentGroup.value) return
    currentGroup.value.lines = currentGroup.value.lines.filter(l => !lineIds.includes(l.id))
    if (lineIds.includes(selectedLineId.value ?? '')) {
      selectedLineId.value = null
    }
    markDirty()
  }

  function toggleLineStatus(lineId: string) {
    if (!currentGroup.value) return
    const line = currentGroup.value.lines.find(l => l.id === lineId)
    if (line) {
      line.status = line.status === 1 ? 0 : 1
      markDirty()
    }
  }

  function batchToggleStatus(lineIds: string[], status: number) {
    if (!currentGroup.value) return
    for (const l of currentGroup.value.lines) {
      if (lineIds.includes(l.id)) l.status = status
    }
    markDirty()
  }

  function copyLine(lineId: string): EntryLine | null {
    if (!currentGroup.value) return null
    const src = currentGroup.value.lines.find(l => l.id === lineId)
    if (!src) return null
    const copied: EntryLine = {
      ...JSON.parse(JSON.stringify(src)),
      id: `line_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    }
    const idx = currentGroup.value.lines.findIndex(l => l.id === lineId)
    currentGroup.value.lines.splice(idx + 1, 0, copied)
    selectedLineId.value = copied.id
    markDirty()
    return copied
  }

  function updateLine(lineId: string, patch: Partial<EntryLine>) {
    if (!currentGroup.value) return
    const line = currentGroup.value.lines.find(l => l.id === lineId)
    if (line) {
      Object.assign(line, patch)
      markDirty()
    }
  }

  // ==================== 剪贴板 ====================
  const clipboard = ref<{ type: 'line' | 'group'; data: any; cut: boolean } | null>(null)

  /**
   * 将指定分录批量分发到目标规则组
   * @param lineId 源分录ID（从当前组中取）
   * @param targetGroupIds 目标规则组ID列表
   * @returns 成功复制的组数
   */
  function distributeLineToGroups(lineId: string, targetGroupIds: string[]): number {
    if (!currentGroup.value) return 0
    const src = currentGroup.value.lines.find(l => l.id === lineId)
    if (!src) return 0

    let count = 0
    for (const gid of targetGroupIds) {
      const target = formData.value.ruleGroups.find(g => g.id === gid)
      if (!target) continue
      const copied: EntryLine = {
        ...JSON.parse(JSON.stringify(src)),
        id: `line_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
      }
      target.lines.push(copied)
      count++
    }
    if (count > 0) markDirty()
    return count
  }

  /**
   * 获取各规则组按方向的分录统计（排除当前组）
   */
  function getGroupDirectionStats(): Array<{
    groupId: string
    groupName: string
    debitCount: number
    creditCount: number
  }> {
    return formData.value.ruleGroups
      .filter(g => g.id !== selectedGroupId.value)
      .map(g => ({
        groupId: g.id,
        groupName: g.name,
        debitCount: g.lines.filter(l => l.direction === '借').length,
        creditCount: g.lines.filter(l => l.direction === '贷').length,
      }))
  }

  function clipboardCopyLine(lineId: string) {
    if (!currentGroup.value) return
    const src = currentGroup.value.lines.find(l => l.id === lineId)
    if (!src) return
    clipboard.value = { type: 'line', data: JSON.parse(JSON.stringify(src)), cut: false }
  }

  function clipboardCutLine(lineId: string) {
    if (!currentGroup.value) return
    const src = currentGroup.value.lines.find(l => l.id === lineId)
    if (!src) return
    clipboard.value = { type: 'line', data: JSON.parse(JSON.stringify(src)), cut: true }
    removeLine(lineId)
  }

  function clipboardPasteLine() {
    if (!currentGroup.value || !clipboard.value || clipboard.value.type !== 'line') return
    const pasted: EntryLine = {
      ...clipboard.value.data,
      id: `line_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    }
    currentGroup.value.lines.push(pasted)
    selectedLineId.value = pasted.id
    if (clipboard.value.cut) clipboard.value = null
    markDirty()
  }

  function clipboardCopyGroup(groupId: string) {
    const g = formData.value.ruleGroups.find(x => x.id === groupId)
    if (!g) return
    clipboard.value = { type: 'group', data: JSON.parse(JSON.stringify(g)), cut: false }
  }

  function clipboardCutGroup(groupId: string) {
    const g = formData.value.ruleGroups.find(x => x.id === groupId)
    if (!g) return
    clipboard.value = { type: 'group', data: JSON.parse(JSON.stringify(g)), cut: true }
    removeGroup(groupId)
  }

  function clipboardPasteGroup() {
    if (!clipboard.value || clipboard.value.type !== 'group') return
    _groupCounter++
    const pasted: RuleGroup = {
      ...clipboard.value.data,
      id: `group_${_groupCounter}_${Date.now()}`,
      lines: (clipboard.value.data.lines || []).map((l: any) => ({
        ...l,
        id: `line_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
      })),
    }
    formData.value.ruleGroups.push(pasted)
    selectedGroupId.value = pasted.id
    if (clipboard.value.cut) clipboard.value = null
    markDirty()
  }

  function cloneGroup(groupId: string) {
    const g = formData.value.ruleGroups.find(x => x.id === groupId)
    if (!g) return
    _groupCounter++
    const cloned: RuleGroup = {
      ...JSON.parse(JSON.stringify(g)),
      id: `group_${_groupCounter}_${Date.now()}`,
      name: `${g.name} (副本)`,
      lines: g.lines.map(l => ({
        ...JSON.parse(JSON.stringify(l)),
        id: `line_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
      })),
    }
    formData.value.ruleGroups.push(cloned)
    selectedGroupId.value = cloned.id
    markDirty()
  }

  // ==================== 重置 ====================
  function $reset() {
    currentRule.value = null
    formData.value = createEmptyFormData()
    hitStats.value = null
    isDirty.value = false
    selectedGroupId.value = null
    selectedLineId.value = null
    auxItemsCache.value = {}
    entryErrors.value = {}
    _groupCounter = 0
  }

  return {
    // 缓存
    stagingTables,
    stagingColumns,
    accountSets,
    accountTree,
    auxItemsCache,
    // 编辑状态
    currentRule,
    formData,
    hitStats,
    isDirty,
    ruleLoading,
    selectedGroupId,
    selectedLineId,
    entryErrors,
    // 计算属性
    currentGroup,
    currentLines,
    currentLine,
    currentStagingFields,
    accountTreeData,
    // 缓存加载
    loadStagingTables,
    loadStagingColumns,
    loadAccountSets,
    loadAccountTree,
    loadAuxItems,
    // 规则操作
    loadRule,
    saveRule,
    buildConfigJson,
    // 组操作
    addGroup,
    removeGroup,
    updateGroupName,
    addMatchValueToGroup,
    importGroup,
    markDirty,
    // 校验错误
    setEntryErrors,
    clearEntryFieldError,
    clearAllEntryErrors,
    // 行操作
    addLine,
    removeLine,
    removeLines,
    toggleLineStatus,
    batchToggleStatus,
    copyLine,
    updateLine,
    // 剪贴板
    clipboard,
    clipboardCopyLine,
    clipboardCutLine,
    clipboardPasteLine,
    clipboardCopyGroup,
    clipboardCutGroup,
    clipboardPasteGroup,
    cloneGroup,
    // 分发
    distributeLineToGroups,
    getGroupDirectionStats,
    // 重置
    $reset,
  }
})
