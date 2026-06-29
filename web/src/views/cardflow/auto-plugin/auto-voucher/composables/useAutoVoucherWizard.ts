/**
 * AutoVoucher 规则配置向导 — Composable 状态管理
 * 管理当前规则、批次、字段分析数据、DryRun 结果、乐观锁等全局状态
 */
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'
import {
  getFieldAnalysis,
  postFieldAnalysis,
  postDryRun,
  getRule,
  putRule,
  validateRule,
} from '../api'
import type { FieldAnalysisResult, RuleValidationResult } from '../api'

export function useAutoVoucherWizard() {
  const store = useAutoVoucherRuleStore()

  // ==================== 当前规则与批次 ====================
  const currentRuleId = ref<number | null>(null)
  const currentBatchId = ref<number | undefined>(undefined)
  const ruleNotFound = ref(false)

  // ==================== 字段分析数据 ====================
  const fieldAnalysis = ref<FieldAnalysisResult | null>(null)
  const fieldAnalysisLoading = ref(false)

  // ==================== DryRun 结果 ====================
  const dryRunResult = ref<any>(null)
  const dryRunLoading = ref(false)

  // ==================== 规则校验 ====================
  const validationResult = ref<RuleValidationResult | null>(null)

  // ==================== 乐观锁 ====================
  const lastModifiedAt = ref<string | undefined>(undefined)

  // ==================== 计算属性：覆盖率统计 ====================
  const coverageStats = computed(() => {
    const analysis = fieldAnalysis.value
    if (!analysis) {
      return { totalCoverage: 0, layer1Rows: 0, layer2Rows: 0, layer3Rows: 0, unmatched: 0, routedNoOutput: 0 }
    }

    const totalRows = analysis.totalRows || 0
    let coveredRows = 0
    let layer1Rows = 0
    let layer2Rows = 0
    let layer3Rows = 0

    for (const layer of analysis.layers) {
      if (layer.layerName === 'Layer1') layer1Rows = layer.coveredRows
      if (layer.layerName === 'Layer2') layer2Rows = layer.coveredRows
      if (layer.layerName === 'Layer3') layer3Rows = layer.coveredRows
      // 取各层覆盖的最大值作为总覆盖
      if (layer.coveredRows > coveredRows) coveredRows = layer.coveredRows
    }

    const unmatched = totalRows - coveredRows
    const totalCoverage = totalRows > 0 ? Math.round((coveredRows / totalRows) * 100) : 0

    return { totalCoverage, layer1Rows, layer2Rows, layer3Rows, unmatched, routedNoOutput: 0 }
  })

  // ==================== 计算属性：未匹配值列表（按层） ====================
  const unmatchedByLayer = computed(() => {
    const analysis = fieldAnalysis.value
    if (!analysis) return []
    return analysis.layers.map(layer => ({
      layerName: layer.layerName,
      fieldName: layer.fieldName,
      values: layer.unmatchedValues,
    }))
  })

  // ==================== 加载规则 ====================
  async function loadRule(ruleId: number) {
    currentRuleId.value = ruleId
    ruleNotFound.value = false
    // 1. 获取乐观锁信息（非关键路径，允许失败）
    try {
      const res: any = await getRule(ruleId)
      lastModifiedAt.value = res?.concurrencyStamp ?? undefined
    } catch (e) {
      // 规则不存在时不显示错误，仅记录日志
      console.warn('[Wizard] getRule 获取版本信息失败，不影响主流程:', e)
    }
    // 2. 加载规则配置（关键路径）
    try {
      await store.loadRule(ruleId)
    } catch (e: any) {
      // 规则不存在时设置友好状态，不弹错误提示
      if (e?.message === 'RULE_NOT_FOUND' || e?.response?.status === 404) {
        ruleNotFound.value = true
        currentRuleId.value = null
      } else {
        // 其他错误（网络/服务器等）才提示
        message.error('加载规则失败，请稍后重试')
      }
    }
  }

  // ==================== 加载字段分析 ====================
  async function loadFieldAnalysis(batchId: number, ruleId?: number) {
    if (!batchId) return
    fieldAnalysisLoading.value = true
    try {
      let res: any
      if (ruleId) {
        res = await getFieldAnalysis(batchId, ruleId)
      } else {
        // 无规则 ID 时使用当前 store 中的配置
        const configJson = store.buildConfigJson()
        res = await postFieldAnalysis(batchId, { configJson })
      }
      fieldAnalysis.value = res
    } catch (e) {
      console.error('加载字段分析失败:', e)
      message.error('加载字段分析失败')
    } finally {
      fieldAnalysisLoading.value = false
    }
  }

  // ==================== 分录行必填项前端校验 ====================
  /**
   * 校验所有分录行的必填字段，返回是否通过。
   * 侧效：将错误信息写入 store.entryErrors。
   * 返回值：{ valid, firstErrorLineId, firstErrorGroupId }
   */
  function validateEntryLines(): { valid: boolean; firstErrorLineId: string | null; firstErrorGroupId: string | null } {
    const errors: Record<string, Record<string, string>> = {}
    let firstErrorLineId: string | null = null
    let firstErrorGroupId: string | null = null

    for (const group of store.formData.ruleGroups) {
      for (const line of group.lines) {
        if (line.status !== 1) continue // 跳过禁用行
        const lineErrors: Record<string, string> = {}

        // direction 必填
        if (!line.direction) {
          lineErrors.direction = '请选择方向'
        }
        // amountField 必填
        if (!line.amountField || !line.amountField.trim()) {
          lineErrors.amountField = '请选择金额字段'
        }
        // summaryTemplate 必填
        if (!line.summaryTemplate || !line.summaryTemplate.trim()) {
          lineErrors.summaryTemplate = '请填写摘要模板'
        }
        // 科目校验
        if (line.accountMode === 'fixed') {
          if (!line.accountId) {
            lineErrors.accountId = '请选择科目'
          }
        } else if (line.accountMode === 'dynamic') {
          if (!line.accountMatchField) {
            lineErrors.accountMatchField = '请选择匹配字段'
          }
          if (!Array.isArray(line.accountMatchRules) || line.accountMatchRules.length === 0) {
            lineErrors.accountMatchRules = '请添加动态映射规则'
          }
        }
        // conditionValues：当 conditionField 已填时必填
        if (line.conditionField && line.conditionField.trim()) {
          if (!Array.isArray(line.conditionValues) || line.conditionValues.length === 0) {
            lineErrors.conditionValues = '请输入条件值'
          }
        }

        if (Object.keys(lineErrors).length > 0) {
          errors[line.id] = lineErrors
          if (!firstErrorLineId) {
            firstErrorLineId = line.id
            firstErrorGroupId = group.id
          }
        }
      }
    }

    store.setEntryErrors(errors)
    return { valid: Object.keys(errors).length === 0, firstErrorLineId, firstErrorGroupId }
  }

  // ==================== 保存规则 ====================
  async function saveRule() {
    // 数据完整性校验
    if (!store.formData.ruleGroups?.length) {
      message.error('规则组不能为空，请添加规则组或重新加载')
      throw new Error('validation_failed')
    }
    if (!store.formData.stagingTable) {
      message.error('暂存表未设置，无法保存')
      throw new Error('validation_failed')
    }
    // 已有规则必须配置账套，新建规则允许不配置
    if (currentRuleId.value && !store.formData.accountSetId) {
      message.error('账套未设置，无法保存')
      throw new Error('validation_failed')
    }

    // 分录行必填项内联校验
    const { valid, firstErrorLineId, firstErrorGroupId } = validateEntryLines()
    if (!valid) {
      // 自动切换到第一个错误所在规则组并展开对应分录行
      if (firstErrorGroupId) {
        store.selectedGroupId = firstErrorGroupId
      }
      if (firstErrorLineId) {
        store.selectedLineId = firstErrorLineId
      }
      message.warning('请完善标记的必填项后再保存')
      throw new Error('validation_failed')
    }

    try {
      const configJson = store.buildConfigJson()
      if (currentRuleId.value) {
        const res: any = await putRule(currentRuleId.value, configJson, lastModifiedAt.value)
        // 更新并发戳
        if (res?.concurrencyStamp) {
          lastModifiedAt.value = res.concurrencyStamp
        }
      } else {
        await store.saveRule()
      }
      store.isDirty = false
      store.clearAllEntryErrors()
      message.success('保存成功')
    } catch (e: any) {
      if (e?.message === 'validation_failed') throw e
      // 409 已被全局拦截器提示，不重复弹窗
      if (e?.response?.status !== 409) {
        message.error('保存失败')
      }
      throw e
    }
  }

  // ==================== 执行 DryRun ====================
  async function runDryRun(batchId: number) {
    if (!batchId) return
    dryRunLoading.value = true
    dryRunResult.value = null
    try {
      const configJson = store.buildConfigJson()
      const res: any = await postDryRun({
        batchId,
        configJson,
        ruleId: currentRuleId.value ?? undefined,
      })
      dryRunResult.value = res
    } catch (e: any) {
      message.error(e?.response?.data?.message || '预演失败')
    } finally {
      dryRunLoading.value = false
    }
  }

  // ==================== 校验规则 ====================
  async function validateCurrentRule() {
    if (!currentRuleId.value) return
    try {
      const configJson = store.buildConfigJson()
      const res: any = await validateRule(currentRuleId.value, configJson)
      validationResult.value = res
      return res
    } catch {
      validationResult.value = null
    }
  }

  // ==================== 重置 ====================
  function $reset() {
    currentRuleId.value = null
    currentBatchId.value = undefined
    ruleNotFound.value = false
    fieldAnalysis.value = null
    fieldAnalysisLoading.value = false
    dryRunResult.value = null
    dryRunLoading.value = false
    validationResult.value = null
    lastModifiedAt.value = undefined
    store.$reset()
  }

  return {
    // 状态
    currentRuleId,
    currentBatchId,
    ruleNotFound,
    fieldAnalysis,
    fieldAnalysisLoading,
    dryRunResult,
    dryRunLoading,
    validationResult,
    lastModifiedAt,
    // 计算属性
    coverageStats,
    unmatchedByLayer,
    // 方法
    loadRule,
    loadFieldAnalysis,
    saveRule,
    runDryRun,
    validateCurrentRule,
    validateEntryLines,
    $reset,
    // store 透传
    store,
  }
}
