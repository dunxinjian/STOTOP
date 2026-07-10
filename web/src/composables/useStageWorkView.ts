import { computed, type ComputedRef, type Ref } from 'vue'
import type {
  CardComponentRuntime,
  CardDetailDto,
  CustomActionDefinition,
  SchemaFieldDefinition,
  StageWorkView,
} from '@/types/cardflow'

/**
 * CardFlow 阶段工作视图（StageWorkView）应用逻辑单一真源。
 *
 * 抽自 CardFlowPanel（PC/审批面板）与 MobileCardApprovalPage（移动端）两处逐字重复的
 * fieldAccess / detailAccess / components / actionPolicy 消费逻辑，合一为本 composable，
 * 消除双份维护与逐步漂移。
 *
 * 【行为保持】本次合一严格保持两端各自现状：PC/移动的真实分歧点由 options 参数化
 * （cardRequiredFallback 与 buildStageInputFields 的 enabled/forceEditable/legacyKeys），
 * 快照测试（useStageWorkView.spec.ts）逐字对照两端旧实现锁定输出不变。
 *
 * 【已知遗留分歧——本次"重构"提交内不改语义，留待独立拍板】：
 *   - D1 卡片字段 required 兜底：PC 落 field.required、移动落 access==='required'（后端语义）。
 *     统一到 'access'（后端语义）会改变 PC 审批必填校验，属行为变更，需产品拍板，故先参数化保持。
 *   - D8 access 比较未过 normalizeAccess（utils/cardflowAccess.ts）：大小写/空白漂移 fail-open。
 *     两端旧实现皆用裸字符串 ===，本次保持；收敛为独立提交。
 *   - sections/summary：后端下发但运行态零消费（设计器 author-only），本次不新增消费。
 */

export type CardRequiredFallback = 'schema' | 'access'

export interface UseStageWorkViewOptions {
  /**
   * 卡片字段 required 兜底口径（rule.required 为 null/undefined 时）：
   *  - 'schema'：回退到 field.required（CardFlowPanel 现状）
   *  - 'access'：由 rule.access === 'required' 派生（MobileCardApprovalPage 现状 / 后端语义）
   */
  cardRequiredFallback: CardRequiredFallback
}

export interface BuildStageInputFieldsOptions {
  /** 关闭时直接返回 []（PC 以 mode==='approval' 门控；移动端不门控、由调用方模板控制）。默认开启。 */
  enabled?: boolean
  /** 命中的补充字段强制 readonly:false（CardFlowPanel 现状；移动端不设）。默认 false。 */
  forceEditable?: boolean
  /** fieldAccess 缺失时的旧版补充字段键回退（移动端 inputFieldsJson 兼容）；PC 不传 → 回退为空。 */
  legacyKeys?: () => string[]
}

function isWritableAccess(access: string | undefined): boolean {
  return access === 'editable' || access === 'required'
}

export function useStageWorkView(
  cardDetail: Ref<CardDetailDto | null> | ComputedRef<CardDetailDto | null>,
  options: UseStageWorkViewOptions,
) {
  const stageWorkView = computed<StageWorkView | null>(() => cardDetail.value?.currentStageWorkView ?? null)

  const runtimeComponents = computed<CardComponentRuntime[]>(() => stageWorkView.value?.components ?? [])
  const hasRuntimeComponents = computed(() => runtimeComponents.value.length > 0)

  /** 应用节点字段展示权限：隐藏字段过滤、非写权限转 readonly、required 兜底按 options.cardRequiredFallback。 */
  function applyFieldAccess(schema: SchemaFieldDefinition[]): SchemaFieldDefinition[] {
    const access = stageWorkView.value?.fieldAccess
    if (!access) return schema
    return schema
      .filter((field) => access[field.key]?.access !== 'hidden')
      .map((field) => {
        const rule = access[field.key]
        if (!rule) return field
        const writable = isWritableAccess(rule.access)
        return {
          ...field,
          readonly: !writable || field.readonly,
          required: options.cardRequiredFallback === 'access'
            ? (rule.required ?? (rule.access === 'required'))
            : (rule.required ?? field.required),
        }
      })
  }

  /** 应用明细字段展示权限（PC/移动一致）：按 `<表>.<字段>` 后缀匹配规则，隐藏过滤、写/必填聚合。 */
  function applyDetailAccess(schema: SchemaFieldDefinition[]): SchemaFieldDefinition[] {
    const access = stageWorkView.value?.detailAccess
    if (!access) return schema
    return schema
      .filter((field) => {
        const rules = Object.entries(access).filter(([key]) => key.endsWith(`.${field.key}`))
        return rules.length === 0 || rules.some(([, rule]) => rule.access !== 'hidden')
      })
      .map((field) => {
        const rules = Object.entries(access).filter(([key]) => key.endsWith(`.${field.key}`))
        if (rules.length === 0) return field
        const editable = rules.some(([, rule]) => isWritableAccess(rule.access))
        const required = rules.some(([, rule]) => rule.required || rule.access === 'required')
        return { ...field, readonly: !editable || field.readonly, required: required || field.required }
      })
  }

  /**
   * 构造当前节点补充输入字段（editable/required）。
   * required 兜底两端一致：rule.required ?? (rule.access === 'required')。
   * 分歧参数化：enabled(D4)/forceEditable(D6)/legacyKeys(D5)；base 由调用方提供（含移动端空 schema 兜底 D7）。
   */
  function buildStageInputFields(
    base: SchemaFieldDefinition[],
    opts: BuildStageInputFieldsOptions = {},
  ): SchemaFieldDefinition[] {
    if (opts.enabled === false) return []
    const access = stageWorkView.value?.fieldAccess
    if (access) {
      return base
        .filter((field) => isWritableAccess(access[field.key]?.access))
        .map((field) => ({
          ...field,
          ...(opts.forceEditable ? { readonly: false } : {}),
          required: access[field.key]?.required ?? (access[field.key]?.access === 'required'),
        }))
    }
    const keys = opts.legacyKeys?.() ?? []
    return keys.length ? base.filter((field) => keys.includes(field.key)) : []
  }

  const stageAllowedActions = computed<string[] | null>(() => stageWorkView.value?.actionPolicy?.allowedActions ?? null)

  /** 设计器自定义动作按钮（M8-C），审批面板动态渲染。 */
  const stageCustomActions = computed<CustomActionDefinition[]>(() => stageWorkView.value?.actionPolicy?.customActions ?? [])

  /** 节点是否允许某动作：无 allowedActions 或为空 → 全放行；否则大小写不敏感匹配。 */
  function canStageAction(action: string): boolean {
    const allowed = stageAllowedActions.value
    if (!allowed || allowed.length === 0) return true
    return allowed.some((item) => item.toLowerCase() === action.toLowerCase())
  }

  return {
    stageWorkView,
    runtimeComponents,
    hasRuntimeComponents,
    applyFieldAccess,
    applyDetailAccess,
    buildStageInputFields,
    stageAllowedActions,
    stageCustomActions,
    canStageAction,
  }
}
