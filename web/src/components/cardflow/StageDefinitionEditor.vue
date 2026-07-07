<script setup lang="ts">
/**
 * StageDefinitionEditor —— 节点链可视化编辑器（左右分栏）
 *
 * 设计原则：
 *  - 左栏(280px)：拖拽排序时间线 + 添加按钮（容器持有 stages 整表与选中态）
 *  - 右栏：StageConfigPanel（选中节点的 5-tab 属性面板，见 StageConfigPanel.vue，可被画布抽屉共用）
 *  - 右栏就地编辑 stages[selectedIndex]，经容器对 stages 的 deep watch 统一 emitUpdate，无需保存按钮
 */
import { computed, ref, watch, nextTick } from 'vue'
import {
  DeleteOutlined,
  HolderOutlined,
  UserOutlined,
  RobotOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons-vue'
import draggable from 'vuedraggable'
import StageConfigPanel from './StageConfigPanel.vue'
import type { CardComponentDefinition, SchemaFieldDefinition } from '@/types/cardflow'
import { Modal } from 'ant-design-vue'
import { DEFAULT_ACTIONS, getStageHealth as computeStageHealth } from './stageDefinitionShared'

// ==================== 类型 ====================

export type StageNodeType = 'manual' | 'auto'
export type StageApprovalMode = 'single' | 'countersign' | 'orsign' | 'sequential'
export type StageAccessMode = 'hidden' | 'masked' | 'readonly' | 'editable' | 'required'
export type AssigneeFallbackType = 'failSubmit' | 'flowAdmin' | 'fixedUsers'

/** 处理粒度：card=卡片级，batch=批次级 */
export type ProcessingGranularity = 'card' | 'batch'

export interface StageAccessRuleDraft {
  access: StageAccessMode
  required?: boolean
  maskPattern?: string | null
}

export interface StageViewProfileDraft {
  profileName?: string
  fieldAccess?: Record<string, StageAccessRuleDraft>
  detailAccess?: Record<string, StageAccessRuleDraft>
  componentAccess?: Record<string, StageAccessRuleDraft>
  summary?: { fields: string[] }
}

export interface StageActionPolicyDraft {
  allowedActions: string[]
  /** 需填写处理意见的动作（approve/reject/returnToStage/transfer） */
  opinionRequiredActions?: string[]
}

/** 自动裁决：条件命中时自动通过/自动拒绝（无 UI，保存链透传信封 autoDecision） */
export interface StageAutoDecisionDraft {
  mode: 'none' | 'autoApprove' | 'autoReject'
  conditionJson?: string
}

export interface StageDefinition {
  /** 本地 id，仅前端使用 */
  id: string
  /** 节点名称 */
  name: string
  /** 节点类型 */
  type: StageNodeType
  /** 处理粒度（仅 auto 节点使用） */
  processingGranularity?: ProcessingGranularity
  /** 排序号 */
  sortOrder: number

  // ===== 人工 =====
  approvalMode?: StageApprovalMode
  assigneeStrategy?: string
  assigneeConfigJson?: string
  inputFields?: string[]
  viewProfile?: StageViewProfileDraft
  actionPolicy?: StageActionPolicyDraft
  autoDecision?: StageAutoDecisionDraft
  ccConfigJson?: string
  timeoutHours?: number

  // ===== 自动 =====
  /** 插件注册引用的 FID（CF自动插件注册.FID） */
  pluginRegistryId?: number
  /** 插件规则引用的 FID（CF自动插件规则.FID，可选） */
  pluginRuleId?: number
  failurePolicy?: 'skip' | 'halt' | 'retry'

  // ===== 通用 =====
  conditionJson?: string
  /** 优先级模板（无编辑 UI，透传保存防止后端全删全建时被置空） */
  priorityTemplate?: number
}

const props = defineProps<{
  modelValue: StageDefinition[]
  schemaFields?: SchemaFieldDefinition[]
  detailSchemaFields?: SchemaFieldDefinition[]
  cardComponents?: CardComponentDefinition[]
}>()

const emit = defineEmits<{
  'update:modelValue': [val: StageDefinition[]]
  'change': [val: StageDefinition[]]
}>()

// ==================== 内部状态 ====================

const stages = ref<StageDefinition[]>(clone(props.modelValue || []))
// 从父级同步 modelValue 进镜像期间抑制回抛：外部变更（含画布抽屉直接改 state.stages）不应被本组件
// 再 emit 出去——否则父级会用回抛的克隆整体替换 state.stages，抖动抽屉里同一 StageConfigPanel 的
// selectedStage 身份、误触其回显把标签页弹回"基础"（见画布抽屉接入）。
let suppressEmit = false

watch(() => props.modelValue, (v) => {
  if (JSON.stringify(v) === JSON.stringify(stages.value)) return
  const selectedId = selectedIndex.value >= 0 ? stages.value[selectedIndex.value]?.id : undefined
  suppressEmit = true
  stages.value = clone(v || [])
  // 外部整体替换（撤销/重做）后按 id 重新定位选中节点；右栏面板经 watch(selectedStage) 自动回显编辑态，
  // 否则右侧面板可能静默指向错位节点，后续编辑会把配置写进别的节点
  if (selectedId) {
    const idx = stages.value.findIndex(s => s.id === selectedId)
    selectedIndex.value = idx >= 0 ? idx : -1
  }
  nextTick(() => { suppressEmit = false })
}, { deep: true })

function clone<T>(v: T): T { return JSON.parse(JSON.stringify(v)) }

function emitUpdate() {
  // 重排 sortOrder
  stages.value.forEach((s, i) => { s.sortOrder = i + 1 })
  emit('update:modelValue', clone(stages.value))
  emit('change', clone(stages.value))
}

function genId() {
  return 'stg_' + Math.random().toString(36).slice(2, 10)
}

function newStage(type: StageNodeType): StageDefinition {
  return {
    id: genId(),
    name: type === 'manual' ? '审批节点' : '自动节点',
    type,
    sortOrder: stages.value.length + 1,
    processingGranularity: type === 'auto' ? 'card' : undefined,
    approvalMode: type === 'manual' ? 'single' : undefined,
    assigneeStrategy: type === 'manual' ? 'role' : undefined,
    inputFields: type === 'manual' ? [] : undefined,
    viewProfile: type === 'manual' ? { fieldAccess: {}, detailAccess: {}, componentAccess: {}, summary: { fields: [] } } : undefined,
    actionPolicy: type === 'manual' ? { allowedActions: [...DEFAULT_ACTIONS] } : undefined,
    failurePolicy: type === 'auto' ? 'halt' : undefined,
  }
}

// ==================== 选中与增删 ====================

const selectedIndex = ref<number>(-1)

function selectStage(index: number) {
  selectedIndex.value = index
}

function addStage(type: StageNodeType) {
  const s = newStage(type)
  stages.value.push(s)
  emitUpdate()
  selectedIndex.value = stages.value.length - 1
}

function removeStage(idx: number) {
  Modal.confirm({
    title: '删除节点',
    content: `确定删除节点「${stages.value[idx]?.name || '未命名节点'}」？该节点的路由/配置将一并移除。`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk: () => doRemoveStage(idx),
  })
}

function doRemoveStage(idx: number) {
  stages.value.splice(idx, 1)
  if (selectedIndex.value === idx) {
    selectedIndex.value = -1
  } else if (selectedIndex.value > idx) {
    selectedIndex.value--
  }
  emitUpdate()
}

function onDragEnd() {
  // 拖拽后维护 selectedIndex（通过 id 重新定位）
  if (selectedIndex.value >= 0) {
    const currentId = stages.value[selectedIndex.value]?.id
    if (currentId) {
      const newIdx = stages.value.findIndex(s => s.id === currentId)
      if (newIdx !== selectedIndex.value) {
        selectedIndex.value = newIdx
      }
    }
  }
  emitUpdate()
}

const stageCount = computed(() => stages.value.length)

// 节点健康（左栏徽标；复用 stageDefinitionShared，与右栏面板横幅同源）
function getStageHealth(stage: StageDefinition) {
  return computeStageHealth(stage, props.detailSchemaFields)
}

// stages 变化时自动 emit（右栏 StageConfigPanel 就地编辑经此统一上抛）；
// 从父级同步来的镜像重建不回抛（suppressEmit），避免外部编辑被回抛放大成整表替换。
watch(stages, () => {
  if (suppressEmit) return
  emitUpdate()
}, { deep: true })
</script>

<template>
  <section class="sde">
    <!-- 左栏：标题区 + 节点列表 + 添加按钮 -->
    <aside class="sde__left">
      <!-- 父组件通过 slot 传入的步骤标题与依赖信息栏 -->
      <slot name="left-header" />

      <!-- 节点链小标题 -->
      <header class="sde__head">
        <div class="sde__head-left">
          <span class="sde__title">节点链</span>
          <span class="sde__count">{{ stageCount }}</span>
        </div>
      </header>

      <!-- 节点列表区 -->
      <div v-if="stageCount === 0" class="sde__left-empty">
        <p>尚未添加节点</p>
        <span>点击下方按钮添加</span>
      </div>

      <draggable
        v-else
        v-model="stages"
        :animation="200"
        item-key="id"
        handle=".sde-node__handle"
        ghost-class="sde-node--ghost"
        class="sde__left-list"
        @end="onDragEnd"
      >
        <template #item="{ element, index }">
          <div
            class="sde-line"
            :class="{ 'sde-line--active': selectedIndex === index }"
            @click="selectStage(index)"
          >
            <!-- 序号圆圈 + 竖线 -->
            <div class="sde-line__rail">
              <div
                class="sde-line__dot"
                :class="[
                  `sde-line__dot--${element.type}`,
                  element.type === 'auto' && element.processingGranularity === 'batch' ? 'sde-line__dot--batch' : ''
                ]"
              >
                {{ index + 1 }}
              </div>
              <div v-if="index < stages.length - 1" class="sde-line__bar" />
            </div>

            <!-- 节点卡片 -->
            <div class="sde-node-wrap">
              <article
                class="sde-node"
                :class="[
                  `sde-node--${element.type}`,
                  element.type === 'auto' && element.processingGranularity === 'batch' ? 'sde-node--batch' : '',
                  { 'sde-node--selected': selectedIndex === index }
                ]"
              >
                <span class="sde-node__icon">
                  <UserOutlined v-if="element.type === 'manual'" />
                  <ThunderboltOutlined v-else-if="element.processingGranularity === 'batch'" />
                  <RobotOutlined v-else />
                </span>
                <span class="sde-node__body">
                  <span class="sde-node__name">{{ element.name || '未命名' }}</span>
                  <a-tooltip :title="[...getStageHealth(element).issues, ...getStageHealth(element).warnings].join('；') || '节点健康正常'">
                    <span
                      class="sde-node-health"
                      :class="`sde-node-health--${getStageHealth(element).status}`"
                    >
                      {{ getStageHealth(element).label }}
                    </span>
                  </a-tooltip>
                </span>
                <div class="sde-node__actions" @click.stop>
                  <span class="sde-node__handle"><HolderOutlined /></span>
                  <button class="sde-node__del" @click.stop="removeStage(index)">
                    <DeleteOutlined />
                  </button>
                </div>
              </article>
            </div>
          </div>
        </template>
      </draggable>

      <!-- 底部添加按钮 -->
      <div class="sde__foot">
        <button class="sde__add sde__add--manual" @click="addStage('manual')">
          <UserOutlined />
          <span>人工</span>
        </button>
        <button class="sde__add sde__add--auto" @click="addStage('auto')">
          <RobotOutlined />
          <span>自动</span>
        </button>
      </div>
    </aside>

    <!-- 右栏：选中节点属性面板（可复用于画布节点抽屉） -->
    <StageConfigPanel
      :stages="stages"
      :selected-index="selectedIndex"
      :schema-fields="schemaFields"
      :detail-schema-fields="detailSchemaFields"
      :card-components="cardComponents"
    />
  </section>
</template>

<style scoped lang="scss">
.sde {
  display: flex;
  flex-direction: row;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 10px;
  overflow: hidden;
  height: 100%;
  min-height: 0;
}

.sde__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
}
.sde__head-left { display: flex; align-items: center; gap: 10px; }
.sde__title { font-size: 14px; font-weight: 600; color: var(--text-1); letter-spacing: 0.4px; }
.sde__count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 20px;
  padding: 0 6px;
  border-radius: 10px;
  background: var(--text-1);
  color: var(--text-on-accent);
  font-size: 11px;
  font-weight: 600;
}
.sde__head-hint {
  font-size: 11px;
  color: var(--text-3);
  font-style: italic;
}

/* ============ 左栏 ============ */
.sde__left {
  width: 280px;
  min-width: 280px;
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  background: var(--bg-muted);
  overflow-y: auto;
}

.sde__left-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 16px;
  color: var(--text-3);
  text-align: center;
  flex: 1;
  p { margin: 0; font-size: 13px; color: var(--text-2); }
  span { font-size: 12px; margin-top: 4px; }
}

.sde__left-list {
  flex: 1;
  padding: 8px;
  min-height: 0;
}

/* ============ 时间线节点 ============ */
.sde-line {
  display: flex;
  align-items: stretch;
  gap: 8px;
  position: relative;
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 6px;
  transition: background .15s;

  &:hover { background: var(--color-primary-light); }
  &--active { background: var(--bg-muted); }
  &--ghost { opacity: 0.4; }
}

.sde-line__rail {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 24px;
  flex-shrink: 0;
}

.sde-line__dot {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
  background: var(--bg-card);
  border: 2px solid var(--text-1);
  color: var(--text-1);
  font-variant-numeric: tabular-nums;

  &--manual { border-color: var(--cf-node-manual); color: var(--cf-node-manual); }
  &--auto   { border-color: var(--cf-node-auto); color: var(--cf-node-auto); }
  &--batch  { border-color: var(--cf-node-batch); color: var(--cf-node-batch); }
}
.sde-line__bar {
  flex: 1;
  width: 2px;
  background: repeating-linear-gradient(
    to bottom,
    var(--border) 0,
    var(--border) 3px,
    transparent 3px,
    transparent 6px
  );
  min-height: 12px;
  margin-top: 2px;
}

.sde-node-wrap {
  flex: 1;
  padding-bottom: 6px;
  min-width: 0;
}

.sde-node {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 6px;
  transition: border-color .18s, box-shadow .18s;
  font-size: 13px;

  &--manual { border-left: 3px solid var(--cf-node-manual); }
  &--auto   { border-left: 3px solid var(--cf-node-auto); }
  &--batch  { border-left: 3px solid var(--cf-node-batch); }

  &--selected {
    background: var(--bg-muted);
    border-color: var(--border-strong);
    box-shadow: 0 0 0 2px var(--border-strong);
  }

  &:hover {
    border-color: var(--text-1);
    .sde-node__del { opacity: 1; transform: translateX(0); }
  }
}

.sde-node__icon {
  width: 22px; height: 22px;
  display: flex; align-items: center; justify-content: center;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  font-size: 11px;
  color: var(--text-2);
  flex-shrink: 0;
}

.sde-node__body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.sde-node__name {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.sde-node-health {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  max-width: 100%;
  padding: 1px 5px;
  border-radius: 4px;
  font-size: 10.5px;
  line-height: 16px;
  border: 1px solid transparent;

  &--ok {
    color: var(--color-success-text);
    background: var(--color-success-light);
    border-color: var(--color-success-border);
  }

  &--warning {
    color: var(--color-warning-text);
    background: var(--color-warning-light);
    border-color: var(--color-warning-border);
  }

  &--error {
    color: var(--color-danger-text);
    background: var(--color-danger-light);
    border-color: var(--color-danger-border);
  }
}

.sde-node__actions {
  display: flex;
  align-items: center;
  gap: 2px;
  flex-shrink: 0;
}
.sde-node__handle {
  cursor: grab;
  color: var(--text-3);
  font-size: 12px;
  &:active { cursor: grabbing; }
}
.sde-node__del {
  border: none;
  background: transparent;
  color: var(--color-danger);
  width: 22px; height: 22px;
  border-radius: 4px;
  cursor: pointer;
  opacity: 0;
  transform: translateX(4px);
  transition: opacity .18s, transform .18s, background .18s;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  &:hover { background: var(--color-danger-light); }
}

/* ============ 添加按钮组 ============ */
.sde__foot {
  display: flex;
  gap: 6px;
  padding: 10px 8px;
  border-top: 1px dashed var(--border);
  margin-top: auto;
}
.sde__add {
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding: 6px 8px;
  border: 1px dashed var(--border);
  border-radius: 6px;
  background: var(--bg-card);
  cursor: pointer;
  font-size: 11px;
  color: var(--text-2);
  transition: all .18s ease;

  &--manual:hover { border-color: var(--cf-node-manual); color: var(--cf-node-manual); background: color-mix(in srgb, var(--cf-node-manual) 8%, transparent); }
  &--auto:hover   { border-color: var(--cf-node-auto); color: var(--cf-node-auto); background: color-mix(in srgb, var(--cf-node-auto) 8%, transparent); }
}

@media (max-width: 1080px) {
  .sde {
    flex-direction: column;
  }

  .sde__left {
    width: 100%;
    min-width: 0;
    max-height: 260px;
    border-right: none;
    border-bottom: 1px solid var(--border);
  }
}
</style>
