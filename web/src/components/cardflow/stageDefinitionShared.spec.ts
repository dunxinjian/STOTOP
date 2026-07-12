import { describe, it, expect } from 'vitest'
import type { StageDefinition } from './StageDefinitionEditor.vue'
import type { SchemaFieldDefinition } from '@/types/cardflow'
import { DEFAULT_ACTIONS, parseAssigneeConfig, getStageHealth, stageVisualKind, isCcStage, NOTIFY_PLUGIN_REGISTRY_ID, formatAssigneeSummary } from './stageDefinitionShared'

/**
 * stageDefinitionShared 纯逻辑门禁（B9 StageConfigPanel 抽取的共享助手）。
 * 锁定从 StageDefinitionEditor 抽出的 parseAssigneeConfig / getStageHealth 行为不变，
 * 二者被容器（左栏徽标）与面板（右栏横幅）共用，任一漂移即回归。
 */

const stage = (over: Partial<StageDefinition>): StageDefinition => ({
  id: 's1', name: '节点', type: 'manual', sortOrder: 1, ...over,
})

describe('parseAssigneeConfig', () => {
  it('无配置 / 非法 JSON → null；合法 JSON → 对象', () => {
    expect(parseAssigneeConfig(stage({ assigneeConfigJson: undefined }))).toBeNull()
    expect(parseAssigneeConfig(stage({ assigneeConfigJson: '{bad' }))).toBeNull()
    expect(parseAssigneeConfig(stage({ assigneeConfigJson: '{"roleCode":"r1"}' }))).toEqual({ roleCode: 'r1' })
  })
})

describe('getStageHealth', () => {
  it('人工节点缺名称/审批模式/处理人策略/允许动作 → error 且逐条问题', () => {
    const h = getStageHealth(stage({
      name: '  ', type: 'manual', approvalMode: undefined, assigneeStrategy: undefined,
      actionPolicy: { allowedActions: [] },
    }))
    expect(h.status).toBe('error')
    expect(h.issues).toContain('节点名称未配置')
    expect(h.issues).toContain('审批模式未配置')
    expect(h.issues).toContain('处理人策略未配置')
    expect(h.issues).toContain('允许动作未配置')
    expect(h.label).toBe(`${h.issues.length} 个问题`)
  })

  it('role 缺 roleCode / fixed 缺 users / fieldUsers 缺 fieldKey 各自报问题', () => {
    expect(getStageHealth(stage({ approvalMode: 'single', assigneeStrategy: 'role', actionPolicy: { allowedActions: ['approve'] } })).issues)
      .toContain('角色处理人未选择')
    expect(getStageHealth(stage({ approvalMode: 'single', assigneeStrategy: 'fixed', actionPolicy: { allowedActions: ['approve'] } })).issues)
      .toContain('固定处理人未选择')
    expect(getStageHealth(stage({ approvalMode: 'single', assigneeStrategy: 'fieldUsers', actionPolicy: { allowedActions: ['approve'] } })).issues)
      .toContain('人员字段未选择')
  })

  it('role 有 roleCode + 有动作 → 无处理人问题；未配字段权限 → warning', () => {
    const h = getStageHealth(stage({
      approvalMode: 'single', assigneeStrategy: 'role',
      assigneeConfigJson: '{"roleCode":"r1"}',
      actionPolicy: { allowedActions: ['approve'] },
    }))
    expect(h.issues).toEqual([])
    expect(h.warnings).toContain('未单独配置卡片字段权限')
    expect(h.status).toBe('warning')
  })

  it('detailSchemaFields 存在但未配明细权限 → 追加明细提醒；无 detailSchema 则不追加', () => {
    const base = stage({
      approvalMode: 'single', assigneeStrategy: 'role', assigneeConfigJson: '{"roleCode":"r1"}',
      actionPolicy: { allowedActions: ['approve'] },
      viewProfile: { fieldAccess: { a: { access: 'editable' } }, detailAccess: {} },
    })
    const detail: SchemaFieldDefinition[] = [{ key: 'amt', label: '金额', type: 'money' }]
    expect(getStageHealth(base, detail).warnings).toContain('未单独配置明细字段权限')
    expect(getStageHealth(base, []).warnings).not.toContain('未单独配置明细字段权限')
  })

  it('auto 节点缺处理粒度/插件 → error；缺失败策略 → warning', () => {
    const h = getStageHealth(stage({ type: 'auto', name: '自动', processingGranularity: undefined, pluginRegistryId: undefined, failurePolicy: undefined }))
    expect(h.issues).toContain('处理粒度未配置')
    expect(h.issues).toContain('自动插件未选择')
    expect(h.warnings).toContain('失败策略未配置')
  })

  it('进入条件 JSON 非法 / 结构异常 → 报问题；全配齐 → ok', () => {
    expect(getStageHealth(stage({ type: 'auto', name: 'a', processingGranularity: 'card', pluginRegistryId: 1, failurePolicy: 'halt', conditionJson: '{bad' })).issues)
      .toContain('进入条件 JSON 解析失败')
    expect(getStageHealth(stage({ type: 'auto', name: 'a', processingGranularity: 'card', pluginRegistryId: 1, failurePolicy: 'halt', conditionJson: '{"x":1}' })).issues)
      .toContain('进入条件格式异常')
    const ok = getStageHealth(stage({ type: 'auto', name: 'a', processingGranularity: 'card', pluginRegistryId: 1, failurePolicy: 'halt' }))
    expect(ok.status).toBe('ok')
    expect(ok.label).toBe('正常')
  })

  it('DEFAULT_ACTIONS 稳定（newStage / ensureStageConfigDefaults 共用）', () => {
    expect(DEFAULT_ACTIONS).toEqual(['approve', 'reject', 'returnToStage', 'transfer', 'addSignAfter', 'cc'])
  })
})

describe('stageVisualKind / isCcStage（竖向图节点五类视觉）', () => {
  it('人工节点 → appr', () => {
    expect(stageVisualKind(stage({ type: 'manual' }))).toBe('appr')
    expect(isCcStage(stage({ type: 'manual' }))).toBe(false)
  })

  it('普通 auto 节点（非通知插件）→ auto', () => {
    expect(stageVisualKind(stage({ type: 'auto', pluginRegistryId: 5 }))).toBe('auto')
    expect(isCcStage(stage({ type: 'auto', pluginRegistryId: 5 }))).toBe(false)
  })

  it('auto + 通知插件 → cc（抄送节点）', () => {
    const cc = stage({ type: 'auto', pluginRegistryId: NOTIFY_PLUGIN_REGISTRY_ID })
    expect(stageVisualKind(cc)).toBe('cc')
    expect(isCcStage(cc)).toBe(true)
  })

  it('人工节点即便误挂通知插件 ID 也不算抄送（cc 必须 auto）', () => {
    expect(isCcStage(stage({ type: 'manual', pluginRegistryId: NOTIFY_PLUGIN_REGISTRY_ID }))).toBe(false)
  })
})

describe('formatAssigneeSummary（竖向图节点「处理人」摘要）', () => {
  it('fixed 策略读持久化真键 userId/userName（回归 #undefined：不得读 id/name 落空）', () => {
    expect(formatAssigneeSummary(stage({
      assigneeStrategy: 'fixed',
      assigneeConfigJson: JSON.stringify({ users: [{ userId: 10, userName: '张三' }] }),
    }))).toBe('张三')
  })

  it('fixed 用户缺 userName → #userId（而非 #undefined）', () => {
    expect(formatAssigneeSummary(stage({
      assigneeStrategy: 'fixed',
      assigneeConfigJson: JSON.stringify({ users: [{ userId: 10 }] }),
    }))).toBe('#10')
  })

  it('存量策略变体 fixedusers 先归一再取名（不显示裸 fixedusers）', () => {
    expect(formatAssigneeSummary(stage({
      assigneeStrategy: 'fixedusers',
      assigneeConfigJson: JSON.stringify({ users: [{ userId: 1, userName: '李四' }] }),
    }))).toBe('李四')
  })

  it('fixed 超过 2 人 → 折叠「前两人 等 N 人」', () => {
    expect(formatAssigneeSummary(stage({
      assigneeStrategy: 'fixed',
      assigneeConfigJson: JSON.stringify({ users: [
        { userId: 1, userName: 'A' }, { userId: 2, userName: 'B' }, { userId: 3, userName: 'C' },
      ] }),
    }))).toBe('A、B 等 3 人')
  })

  it('role 有 roleCode → 按角色·角色名（回退 roleCode）', () => {
    expect(formatAssigneeSummary(stage({
      assigneeStrategy: 'role',
      assigneeConfigJson: JSON.stringify({ roleCode: 'fin_mgr', roleName: '财务经理' }),
    }))).toBe('按角色·财务经理')
  })

  it('fixed 但 users 为空 → 回退策略标签「指定人员」（不再 #undefined）', () => {
    expect(formatAssigneeSummary(stage({
      assigneeStrategy: 'fixed',
      assigneeConfigJson: JSON.stringify({ users: [] }),
    }))).toBe('指定人员')
  })

  it('未配策略 → 未配置处理人；auto 节点 → 空串', () => {
    expect(formatAssigneeSummary(stage({ assigneeStrategy: undefined }))).toBe('未配置处理人')
    expect(formatAssigneeSummary(stage({ type: 'auto', assigneeStrategy: 'fixed' }))).toBe('')
  })
})
