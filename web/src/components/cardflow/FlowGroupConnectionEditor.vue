<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { PlusOutlined, DeleteOutlined, ArrowRightOutlined } from '@ant-design/icons-vue'
import ConditionBuilder from '@/components/cardflow/ConditionBuilder.vue'
import type { ConditionGroup, FieldOption } from '@/components/cardflow/ConditionBuilder.vue'
import { getFlowDefinitions, getFlowDraftVersion } from '@/api/cardflow'
import type {
  FlowDefinitionDto,
  FlowGroupLinkDto,
  SchemaFieldDefinition,
  SaveFlowGroupLinkRequest,
} from '@/types/cardflow'

interface MappingRow { from: string; to: string }

interface SavePayload extends SaveFlowGroupLinkRequest {
  id?: number
}

const props = defineProps<{
  visible: boolean
  connection?: FlowGroupLinkDto | null
  groupId: string | number
}>()

const emit = defineEmits<{
  'update:visible': [v: boolean]
  save: [payload: SavePayload]
}>()

const open = computed({
  get: () => props.visible,
  set: (v) => emit('update:visible', v),
})

const flows = ref<FlowDefinitionDto[]>([])
const flowsLoading = ref(false)

const sourceFlowId = ref<number | undefined>(undefined)
const targetFlowId = ref<number | undefined>(undefined)
const triggerMode = ref<'auto' | 'suggest'>('auto')
const sortOrder = ref<number>(1)
const condition = ref<ConditionGroup>({ logic: 'and', conditions: [] })
const mappingRows = ref<MappingRow[]>([])

// 字段缓存：flowId -> SchemaFieldDefinition[]
const fieldCache = ref<Map<number, SchemaFieldDefinition[]>>(new Map())
const sourceFieldsLoading = ref(false)
const targetFieldsLoading = ref(false)

const conditionBuilderRef = ref<InstanceType<typeof ConditionBuilder> | null>(null)

async function loadFlows() {
  flowsLoading.value = true
  try {
    const res = await getFlowDefinitions({ pageSize: 500 })
    flows.value = res?.items || []
  } catch {
    flows.value = []
  } finally {
    flowsLoading.value = false
  }
}

async function fetchFlowFields(flowId: number): Promise<SchemaFieldDefinition[]> {
  if (fieldCache.value.has(flowId)) return fieldCache.value.get(flowId)!
  try {
    const draft = await getFlowDraftVersion(flowId)
    const json = draft?.cardSchemaJson
    if (!json) {
      fieldCache.value.set(flowId, [])
      return []
    }
    const parsed = JSON.parse(json)
    const arr: SchemaFieldDefinition[] = Array.isArray(parsed) ? parsed : (parsed.fields || [])
    fieldCache.value.set(flowId, arr)
    return arr
  } catch {
    fieldCache.value.set(flowId, [])
    return []
  }
}

const sourceFields = ref<SchemaFieldDefinition[]>([])
const targetFields = ref<SchemaFieldDefinition[]>([])

const conditionFieldOptions = computed<FieldOption[]>(() =>
  sourceFields.value.map(f => ({ key: f.key, label: f.label, type: f.type }))
)

watch(sourceFlowId, async (id) => {
  if (!id) { sourceFields.value = []; return }
  sourceFieldsLoading.value = true
  try { sourceFields.value = await fetchFlowFields(id) }
  finally { sourceFieldsLoading.value = false }
}, { immediate: false })

watch(targetFlowId, async (id) => {
  if (!id) { targetFields.value = []; return }
  targetFieldsLoading.value = true
  try { targetFields.value = await fetchFlowFields(id) }
  finally { targetFieldsLoading.value = false }
}, { immediate: false })

function resetForm() {
  sourceFlowId.value = undefined
  targetFlowId.value = undefined
  triggerMode.value = 'auto'
  sortOrder.value = 1
  condition.value = { logic: 'and', conditions: [] }
  mappingRows.value = []
  sourceFields.value = []
  targetFields.value = []
}

function tryParseCondition(s: string | null | undefined): ConditionGroup {
  if (!s) return { logic: 'and', conditions: [] }
  try {
    const obj = JSON.parse(s)
    if (obj && obj.logic && Array.isArray(obj.conditions)) return obj
  } catch { /* swallow */ }
  return { logic: 'and', conditions: [] }
}

function tryParseMapping(s: string | null | undefined): MappingRow[] {
  if (!s) return []
  try {
    const obj = JSON.parse(s)
    if (Array.isArray(obj)) {
      return obj
        .filter(r => r && typeof r === 'object')
        .map(r => ({ from: String(r.from ?? r.source ?? ''), to: String(r.to ?? r.target ?? '') }))
    }
    if (obj && typeof obj === 'object') {
      return Object.entries(obj).map(([k, v]) => ({ from: k, to: String(v) }))
    }
  } catch { /* swallow */ }
  return []
}

watch(() => props.visible, async (v) => {
  if (!v) return
  resetForm()
  if (flows.value.length === 0) await loadFlows()
  const c = props.connection
  if (c) {
    sourceFlowId.value = c.sourceFlowId
    targetFlowId.value = c.targetFlowId
    triggerMode.value = (c.triggerMode === 'suggest' ? 'suggest' : 'auto')
    sortOrder.value = c.sortOrder || 1
    condition.value = tryParseCondition(c.triggerCondition)
    mappingRows.value = tryParseMapping(c.fieldMappingJson)
  }
})

function addMappingRow() {
  mappingRows.value.push({ from: '', to: '' })
}
function removeMappingRow(idx: number) {
  mappingRows.value.splice(idx, 1)
}

const conditionSummary = computed(() => {
  return conditionBuilderRef.value?.conditionSummary || ''
})

const sourceFlowName = computed(() => flows.value.find(f => f.id === sourceFlowId.value)?.flowName || '源流程')
const targetFlowName = computed(() => flows.value.find(f => f.id === targetFlowId.value)?.flowName || '目标流程')

const triggerLabel = computed(() => triggerMode.value === 'auto' ? '自动触发' : '建议发起')

const validMappingCount = computed(() =>
  mappingRows.value.filter(r => r.from && r.to).length
)

const previewText = computed(() => {
  const cond = conditionSummary.value
  const condPart = cond ? `且 ${cond} ` : ''
  return `当 ${sourceFlowName.value} 完成${cond ? '' : ' '}${condPart}时，${triggerLabel.value} ${targetFlowName.value}，映射 ${validMappingCount.value} 个字段`
})

function handleCancel() { open.value = false }

function handleOk() {
  if (!sourceFlowId.value) return
  if (!targetFlowId.value) return
  if (sourceFlowId.value === targetFlowId.value) return

  const validRows = mappingRows.value.filter(r => r.from && r.to)
  const hasCondition = condition.value.conditions.length > 0
  const payload: SavePayload = {
    id: props.connection?.id,
    sourceFlowId: sourceFlowId.value!,
    targetFlowId: targetFlowId.value!,
    triggerCondition: hasCondition ? JSON.stringify(condition.value) : undefined,
    fieldMappingJson: validRows.length ? JSON.stringify(validRows) : undefined,
    triggerMode: triggerMode.value,
    sortOrder: sortOrder.value,
  }
  emit('save', payload)
}

const okDisabled = computed(() =>
  !sourceFlowId.value || !targetFlowId.value || sourceFlowId.value === targetFlowId.value
)
</script>

<template>
  <a-modal
    v-model:open="open"
    :title="connection ? '编辑连接' : '新增连接'"
    :width="640"
    :ok-button-props="{ disabled: okDisabled }"
    @ok="handleOk"
    @cancel="handleCancel"
    destroy-on-close
  >
    <div class="fgce-form">
      <a-form layout="vertical" :colon="false">
        <a-row :gutter="16">
          <a-col :span="11">
            <a-form-item label="源流程" required>
              <a-select
                v-model:value="sourceFlowId"
                placeholder="选择源流程"
                :loading="flowsLoading"
                show-search
                :filter-option="(input: string, opt: any) => String(opt?.label ?? '').toLowerCase().includes(input.toLowerCase())"
                :options="flows.map(f => ({ value: f.id, label: f.flowName }))"
              />
            </a-form-item>
          </a-col>
          <a-col :span="2" class="fgce-arrow-col">
            <ArrowRightOutlined class="fgce-arrow" />
          </a-col>
          <a-col :span="11">
            <a-form-item label="目标流程" required>
              <a-select
                v-model:value="targetFlowId"
                placeholder="选择目标流程"
                :loading="flowsLoading"
                show-search
                :filter-option="(input: string, opt: any) => String(opt?.label ?? '').toLowerCase().includes(input.toLowerCase())"
                :options="flows.map(f => ({ value: f.id, label: f.flowName, disabled: f.id === sourceFlowId }))"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item label="触发条件">
          <div class="fgce-cond-wrap">
            <a-spin :spinning="sourceFieldsLoading">
              <ConditionBuilder
                ref="conditionBuilderRef"
                v-model="condition"
                :fields="conditionFieldOptions"
              />
            </a-spin>
            <div v-if="!sourceFlowId" class="fgce-cond-tip">请先选择源流程，以加载可用字段</div>
          </div>
        </a-form-item>

        <a-form-item label="字段映射">
          <div class="fgce-mapping">
            <div class="fgce-mapping-header">
              <span class="fgce-col">源字段</span>
              <span class="fgce-arrow-mini"></span>
              <span class="fgce-col">目标字段</span>
              <span class="fgce-actions"></span>
            </div>
            <div v-if="!mappingRows.length" class="fgce-mapping-empty">暂无映射，点击下方“新增映射”添加</div>
            <div
              v-for="(row, idx) in mappingRows"
              :key="idx"
              class="fgce-mapping-row"
            >
              <a-select
                v-model:value="row.from"
                placeholder="源字段"
                size="small"
                :disabled="!sourceFlowId"
                :loading="sourceFieldsLoading"
                class="fgce-col"
                :options="sourceFields.map(f => ({ value: f.key, label: f.label }))"
                allow-clear
              />
              <ArrowRightOutlined class="fgce-arrow-mini" />
              <a-select
                v-model:value="row.to"
                placeholder="目标字段"
                size="small"
                :disabled="!targetFlowId"
                :loading="targetFieldsLoading"
                class="fgce-col"
                :options="targetFields.map(f => ({ value: f.key, label: f.label }))"
                allow-clear
              />
              <a-button
                type="text"
                danger
                size="small"
                class="fgce-actions"
                @click="removeMappingRow(idx)"
              >
                <template #icon><DeleteOutlined /></template>
              </a-button>
            </div>
            <a-button
              type="dashed"
              size="small"
              block
              class="fgce-add-mapping"
              :disabled="!sourceFlowId || !targetFlowId"
              @click="addMappingRow"
            >
              <template #icon><PlusOutlined /></template>新增映射
            </a-button>
          </div>
        </a-form-item>

        <a-row :gutter="16">
          <a-col :span="14">
            <a-form-item label="触发方式">
              <a-radio-group v-model:value="triggerMode">
                <a-radio value="auto">自动触发</a-radio>
                <a-radio value="suggest">建议发起</a-radio>
              </a-radio-group>
            </a-form-item>
          </a-col>
          <a-col :span="10">
            <a-form-item label="排序号">
              <a-input-number
                v-model:value="sortOrder"
                :min="1"
                :step="1"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>

      <div class="fgce-preview">
        <span class="fgce-preview-label">效果预览</span>
        <span class="fgce-preview-text">{{ previewText }}</span>
      </div>
    </div>
  </a-modal>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.fgce-form {
  padding-top: 4px;
}

:deep(.ant-form-item) {
  margin-bottom: 14px;
}

.fgce-arrow-col {
  display: flex;
  align-items: center;
  justify-content: center;
  padding-bottom: 6px; // align with select baseline
}

.fgce-arrow {
  font-size: 18px;
  color: var(--text-2);
}

.fgce-cond-wrap {
  position: relative;
}

.fgce-cond-tip {
  margin-top: 6px;
  font-size: 12px;
  color: $text-secondary;
}

.fgce-mapping {
  border: 1px solid $border-color;
  border-radius: $border-radius-md;
  padding: 8px 10px;
  background: var(--bg-muted);
}

.fgce-mapping-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: $text-secondary;
  padding: 0 4px 6px;
  border-bottom: 1px dashed $border-color-lighter;
  margin-bottom: 8px;
}

.fgce-mapping-empty {
  text-align: center;
  color: $text-placeholder;
  font-size: 12px;
  padding: 10px 0;
}

.fgce-mapping-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.fgce-col {
  flex: 1;
  min-width: 0;
}

.fgce-arrow-mini {
  width: 18px;
  text-align: center;
  color: var(--text-3);
  font-size: 12px;
}

.fgce-actions {
  width: 32px;
  display: flex;
  justify-content: center;
}

.fgce-add-mapping {
  margin-top: 4px;
}

.fgce-preview {
  margin-top: 12px;
  background: var(--bg-muted);
  border: 1px solid $border-color-lighter;
  border-radius: $border-radius-md;
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;

  .fgce-preview-label {
    font-size: 12px;
    color: $text-secondary;
    letter-spacing: 0.4px;
  }

  .fgce-preview-text {
    font-size: 13px;
    color: $text-primary;
    line-height: 1.6;
  }
}
</style>
