// ==================== 卡片流程编排中心 类型定义 ====================
//
// 对应后端模块：Orchestration（编排模板 / 编排实例 / 节点实例 / 派发记录 / 自由派发）
// 与 cardflow.ts 中的 PagedResult 共用分页结构

import type { PagedResult } from './cardflow'

// 重导出，便于本模块统一引用
export type { PagedResult }

// ==================== 编排模板 ====================

/** 编排模板状态 */
export type OrchestrationTemplateStatus = 'draft' | 'published' | 'disabled'

/** 编排模板 */
export interface OrchestrationTemplate {
  id: number
  code: string
  name: string
  description?: string
  /** DAG 节点 JSON（序列化后的 DagNode[]） */
  nodesJson?: string
  /** DAG 边 JSON（序列化后的 DagEdge[]） */
  edgesJson?: string
  status: OrchestrationTemplateStatus
  /** 单实例最大触发次数（兜底防御） */
  maxTriggerCount: number
  creatorId: number
  createdTime: string
  updatedTime?: string
}

// ==================== DAG 节点/边定义（JSON 内部结构） ====================

/** DAG 节点类型 */
export type DagNodeType = 'start' | 'cardflow' | 'join' | 'end'

/** 节点完成模式（仅 cardflow 节点需要） */
export type DagCompletionMode = 'single' | 'batch'

/** 汇聚模式（仅 join 节点需要） */
export type DagJoinMode = 'all' | 'any'

/** DAG 节点 */
export interface DagNode {
  id: string
  type: DagNodeType
  name: string
  /** 关联卡片流程 FlowCode（cardflow 节点必填） */
  flowCode?: string
  completionMode?: DagCompletionMode
  joinMode?: DagJoinMode
}

/** 边的判断条件 */
export interface DagEdgeCondition {
  field: string
  op: string
  value: string | number | boolean
}

/** 数据传递协议级别 */
export type DagDataProtocolLevel = 'signal' | 'inline' | 'ref'

/** 边的数据传递协议 */
export interface DagEdgeDataProtocol {
  level: DagDataProtocolLevel
  /** inline 模式下的字段映射（源字段 → 目标字段） */
  mapping?: Record<string, string>
  /** ref 模式下的引用描述 */
  ref?: { table: string; filterExpr: string }
}

/** DAG 边 */
export interface DagEdge {
  id: string
  from: string
  to: string
  condition?: DagEdgeCondition
  dataProtocol?: DagEdgeDataProtocol
}

// ==================== 编排实例 ====================

/** 编排实例状态 */
export type OrchestrationInstanceStatus =
  | 'running'
  | 'completed'
  | 'terminated'
  | 'failed'
  | 'cancelled'
  | 'paused'

/** 编排实例 */
export interface OrchestrationInstance {
  id: number
  templateId: number
  templateName?: string
  status: OrchestrationInstanceStatus
  completionReason?: string
  /** 启动时模板节点快照 */
  snapshotNodesJson?: string
  /** 启动时模板边快照 */
  snapshotEdgesJson?: string
  /** 运行时上下文 JSON */
  contextJson?: string
  triggerCount: number
  initiatorId: number
  initiatedTime: string
  completedTime?: string
  failureReason?: string
}

// ==================== 编排节点实例 ====================

/** 节点实例状态 */
export type OrchestrationNodeInstanceStatus =
  | 'pending'
  | 'running'
  | 'completed'
  | 'skipped'
  | 'failed'

/** 编排节点实例 */
export interface OrchestrationNodeInstance {
  id: number
  orchestrationInstanceId: number
  nodeId: string
  status: OrchestrationNodeInstanceStatus
  /** 终态类型（end 节点上记录） */
  endStatusType?: string
  /** 关联卡片 ID（single 完成模式） */
  relatedCardId?: number
  /** 关联批次 ID（batch 完成模式） */
  relatedBatchId?: number
  resultJson?: string
  startTime?: string
  completedTime?: string
}

// ==================== 派发记录 ====================

/** 派发类型 */
export type DispatchType = 'auto' | 'manual'

/** 派发状态 */
export type DispatchStatus = 'pending' | 'triggered' | 'skipped' | 'failed'

/** 派发记录 */
export interface DispatchRecord {
  id: number
  /** 自由派发时可为空 */
  orchestrationInstanceId?: number
  dispatchType: DispatchType
  sourceNodeId?: string
  sourceCardId?: number
  sourceFlowCode?: string
  targetNodeId?: string
  targetCardId?: number
  targetFlowCode?: string
  /** 派发时携带的数据载荷 JSON */
  dataPayloadJson?: string
  status: DispatchStatus
  operatorId?: number
  createdTime: string
  triggeredTime?: string
  failureReason?: string
}

// ==================== 自由派发 ====================

/** 可派发目标选项 */
export interface DispatchOption {
  targetFlowCode: string
  name: string
  /** 目标流程要求的数据协议 JSON（可选，用于前端引导用户填写 customData） */
  dataProtocolJson?: string
}

// ==================== 请求/响应 DTO ====================

/** 创建编排模板请求 */
export interface CreateTemplateRequest {
  code: string
  name: string
  description?: string
  nodesJson?: string
  edgesJson?: string
  maxTriggerCount?: number
}

/** 更新编排模板请求 */
export interface UpdateTemplateRequest {
  name?: string
  description?: string
  nodesJson?: string
  edgesJson?: string
  maxTriggerCount?: number
}

/** 模板列表查询请求 */
export interface OrchestrationTemplateQueryRequest {
  keyword?: string
  status?: OrchestrationTemplateStatus
  page?: number
  pageSize?: number
}

/** 启动编排实例请求 */
export interface StartInstanceRequest {
  templateId: number
  inputData?: any
}

/** 实例列表查询请求 */
export interface OrchestrationInstanceQueryRequest {
  templateId?: number
  status?: OrchestrationInstanceStatus
  page?: number
  pageSize?: number
}

/** 自由派发请求 */
export interface AdHocDispatchRequest {
  sourceCardId: number
  targetFlowCode: string
  customData?: any
}

/** 启动编排实例响应 */
export interface StartInstanceResponse {
  instanceId: number
}

/** 自由派发响应 */
export interface AdHocDispatchResponse {
  cardId: number
}

/** 编排实例详情（含节点实例列表） */
export interface OrchestrationInstanceDetail extends OrchestrationInstance {
  nodeInstances: OrchestrationNodeInstance[]
}
