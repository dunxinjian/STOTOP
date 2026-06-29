<script setup lang="ts">
/**
 * BatchUploadDialog.vue — 批次文件上传弹窗
 *
 * 1. 选择流程定义
 * 2. 上传 Excel 文件（dragger）
 * 3. 列映射预览/调整：左 Excel 表头 / 右 Schema 字段
 * 4. 提交：FormData(file + flowDefinitionId + columnMappingJson)
 */
import { ref, computed, watch } from 'vue'
import { Modal, message } from 'ant-design-vue'
import { InboxOutlined } from '@ant-design/icons-vue'
import type {
  FlowDefinitionDto,
  SchemaFieldDefinition,
  TriggerConfig,
} from '@/types/cardflow'
import {
  getFlowDefinitions,
  getFlowDraftVersion,
  getFlowVersionDetail,
  uploadBatch,
} from '@/api/cardflow'
import { parseCardSchemaFields } from '@/utils/cardflowSchema'

// ==================== Props & Emits ====================
interface Props {
  visible: boolean
  /** 预选流程定义 ID（从 WorkHub 入口传入时） */
  flowDefinitionId?: number | null
}

const props = withDefaults(defineProps<Props>(), {
  flowDefinitionId: null,
})

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'success', batchId: number): void
}>()

// ==================== State ====================
const innerVisible = computed({
  get: () => props.visible,
  set: (v) => emit('update:visible', v),
})

const submitting = ref(false)
const flowsLoading = ref(false)
const flowOptions = ref<FlowDefinitionDto[]>([])
const selectedFlowId = ref<number | undefined>(undefined)
const schemaFields = ref<SchemaFieldDefinition[]>([])
const triggerConfig = ref<TriggerConfig | null>(null)

const fileList = ref<any[]>([])
const excelHeaders = ref<string[]>([])
const columnMapping = ref<Record<string, string>>({})

// ==================== Watch ====================
watch(
  () => innerVisible.value,
  (visible) => {
    if (visible) {
      resetState()
      loadFlows()
      if (props.flowDefinitionId) {
        selectedFlowId.value = props.flowDefinitionId
        loadFlowSchema(props.flowDefinitionId)
      }
    }
  },
)

watch(selectedFlowId, (id) => {
  if (id) loadFlowSchema(id)
})

// ==================== Methods ====================
function resetState() {
  selectedFlowId.value = undefined
  schemaFields.value = []
  triggerConfig.value = null
  fileList.value = []
  excelHeaders.value = []
  columnMapping.value = {}
  submitting.value = false
}

async function loadFlows() {
  flowsLoading.value = true
  try {
    const res = await getFlowDefinitions({ status: 'published', pageSize: 200 })
    flowOptions.value = res.items || []
  } catch {
    message.error('加载流程定义失败')
  } finally {
    flowsLoading.value = false
  }
}

function parseSchemaJson(json?: string | null): SchemaFieldDefinition[] {
  return parseCardSchemaFields(json)
}

function parseTriggerConfig(flow: any): TriggerConfig | null {
  const raw = flow?.triggerConfigJson
  if (!raw) return null
  try {
    return typeof raw === 'string' ? JSON.parse(raw) : raw
  } catch {
    return null
  }
}

async function loadFlowSchema(id: number) {
  try {
    let ver
    try {
      // 优先取已发布版本（通过流程详情拿到 currentVersionId 的代价较高，直接用 draft 兜底）
      ver = await getFlowDraftVersion(id)
    } catch {
      ver = null
    }
    if (!ver) {
      // 回退到取版本列表的当前版本
      const flow: any = flowOptions.value.find((f) => f.id === id)
      if (flow?.currentVersionId) {
        ver = await getFlowVersionDetail(id, flow.currentVersionId)
      }
    }
    schemaFields.value = parseSchemaJson(ver?.cardSchemaJson)
    const flow: any = flowOptions.value.find((f) => f.id === id)
    triggerConfig.value = parseTriggerConfig(flow)
    // 应用默认映射
    const defaultMapping = triggerConfig.value?.fileUpload?.defaultMapping || {}
    columnMapping.value = { ...defaultMapping }
  } catch {
    schemaFields.value = []
  }
}

/** 解析 Excel 文件首行，提取表头列名（前端预览） */
async function parseExcelHeaders(file: File) {
  excelHeaders.value = []
  try {
    // 动态导入 xlsx（可选依赖，未安装时仅跳过预览）
    // @vite-ignore
    const moduleName = 'xlsx'
    const XLSX: any = await import(/* @vite-ignore */ moduleName).catch(() => null)
    if (!XLSX) {
      // 没有 xlsx 库时，跳过预览，由后端解析
      return
    }
    const buf = await file.arrayBuffer()
    const wb = XLSX.read(buf, { type: 'array' })
    const ws = wb.Sheets[wb.SheetNames[0]]
    if (!ws) return
    const rows: any[][] = XLSX.utils.sheet_to_json(ws, { header: 1, defval: '' })
    const header = (rows[0] || []).map((v: any) => String(v ?? '').trim()).filter((v: string) => v)
    excelHeaders.value = header
    // 自动匹配同名字段
    const m = { ...columnMapping.value }
    for (const col of header) {
      if (m[col]) continue
      const hit = schemaFields.value.find((f) => f.label === col || f.key === col)
      if (hit) m[col] = hit.key
    }
    columnMapping.value = m
  } catch (err) {
    console.warn('[BatchUploadDialog] parse excel failed', err)
  }
}

function beforeUpload(file: File) {
  fileList.value = [{ uid: String(Date.now()), name: file.name, status: 'done', originFileObj: file }]
  parseExcelHeaders(file)
  return false // 阻止 a-upload 自动上传
}

function removeFile() {
  fileList.value = []
  excelHeaders.value = []
}

const schemaFieldOptions = computed(() =>
  schemaFields.value.map((f) => ({ label: `${f.label} (${f.key})`, value: f.key })),
)

async function handleSubmit() {
  if (!selectedFlowId.value) {
    message.warning('请选择流程定义')
    return
  }
  const file: File | undefined = fileList.value[0]?.originFileObj
  if (!file) {
    message.warning('请上传文件')
    return
  }
  // 至少有一个映射
  const mappingEntries = Object.entries(columnMapping.value).filter(([, v]) => !!v)
  if (excelHeaders.value.length > 0 && mappingEntries.length === 0) {
    Modal.confirm({
      title: '尚未配置任何列映射',
      content: '当前未指定 Excel 列与 Schema 字段的对应关系，确定继续上传？',
      onOk: doSubmit,
    })
    return
  }
  await doSubmit()
}

async function doSubmit() {
  const file: File | undefined = fileList.value[0]?.originFileObj
  if (!file || !selectedFlowId.value) return
  submitting.value = true
  try {
    const fd = new FormData()
    fd.append('file', file)
    fd.append('flowDefinitionId', String(selectedFlowId.value))
    fd.append('columnMappingJson', JSON.stringify(columnMapping.value))
    const batch: any = await uploadBatch(fd)
    message.success('上传成功，已创建批次')
    emit('success', batch?.id ?? 0)
    innerVisible.value = false
  } catch (err: any) {
    message.error(err?.response?.data?.message || '上传失败')
  } finally {
    submitting.value = false
  }
}

const acceptTypes = computed(() => triggerConfig.value?.fileUpload?.accept || '.xlsx,.xls,.csv')
</script>

<template>
  <a-modal
    v-model:open="innerVisible"
    title="批量上传"
    :width="780"
    :confirm-loading="submitting"
    ok-text="开始上传"
    cancel-text="取消"
    destroy-on-close
    @ok="handleSubmit"
  >
    <div class="cf-batch-upload">
      <!-- 流程定义 -->
      <div class="cf-batch-upload__row">
        <span class="cf-batch-upload__label">流程定义</span>
        <a-select
          v-model:value="selectedFlowId"
          class="cf-batch-upload__flow"
          :loading="flowsLoading"
          placeholder="请选择流程定义"
          show-search
          :filter-option="(input: string, opt: any) => opt.label.toLowerCase().includes(input.toLowerCase())"
          :options="flowOptions.map((f) => ({ label: `${f.flowName} (${f.flowCode})`, value: f.id }))"
          style="flex: 1"
        />
      </div>

      <!-- 文件上传 -->
      <div class="cf-batch-upload__upload">
        <a-upload-dragger
          :file-list="fileList"
          :accept="acceptTypes"
          :max-count="1"
          :before-upload="beforeUpload"
          @remove="removeFile"
        >
          <p class="ant-upload-drag-icon">
            <inbox-outlined />
          </p>
          <p class="ant-upload-text">点击或拖拽文件到此区域上传</p>
          <p class="ant-upload-hint">
            支持 {{ acceptTypes }} 格式，单文件
          </p>
        </a-upload-dragger>
      </div>

      <!-- 列映射 -->
      <div v-if="excelHeaders.length > 0" class="cf-batch-upload__mapping">
        <div class="cf-batch-upload__mapping-title">列映射</div>
        <a-table
          :data-source="excelHeaders.map((h) => ({ key: h, header: h }))"
          :pagination="false"
          size="small"
          :columns="[
            { title: 'Excel 列头', dataIndex: 'header', width: 240 },
            { title: '映射到 Schema 字段', dataIndex: 'mapping' },
          ]"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'mapping'">
              <a-select
                v-model:value="columnMapping[record.header]"
                style="width: 100%"
                allow-clear
                show-search
                placeholder="不映射"
                :options="schemaFieldOptions"
                :filter-option="(input: string, opt: any) => opt.label.toLowerCase().includes(input.toLowerCase())"
              />
            </template>
          </template>
        </a-table>
      </div>
    </div>
  </a-modal>
</template>

<style scoped lang="scss">
.cf-batch-upload {
  display: flex;
  flex-direction: column;
  gap: 16px;

  &__row {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  &__label {
    width: 76px;
    color: var(--text-2);
    font-size: 14px;
    flex-shrink: 0;
  }

  &__upload {
    margin-top: 4px;
  }

  &__mapping-title {
    font-size: 14px;
    color: var(--text-1);
    margin-bottom: 8px;
    font-weight: 500;
  }
}
</style>
