<template>
  <div class="page-container">
    <PageHeader title="编码规则管理" description="管理系统各业务模块的自动编码规则">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <PlusOutlined />新增规则
        </a-button>
      </template>
    </PageHeader>

    <div class="table-wrapper">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        :loading="loading"
        rowKey="id"
        bordered
        :pagination="false"
        :scroll="{ x: 1400, y: tableScrollY }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'dateFormat'">
            {{ dateFormatLabel(record.dateFormat) }}
          </template>
          <template v-if="column.dataIndex === 'resetPeriod'">
            {{ resetPeriodLabel(record.resetPeriod) }}
          </template>
          <template v-if="column.dataIndex === 'preview'">
            <span class="code-preview">{{ buildPreview(record as CodeRuleDto) }}</span>
          </template>
          <template v-if="column.dataIndex === 'enabled'">
            <a-tag :color="record.enabled ? 'success' : 'default'">
              {{ record.enabled ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-space :size="4">
              <a-button type="link" size="small" @click="handleEdit(record as CodeRuleDto)">
                <EditOutlined />编辑
              </a-button>
              <a-popconfirm
                :title="`确定删除规则 '${record.ruleName}' 吗？`"
                ok-text="确定"
                cancel-text="取消"
                @confirm="handleDelete(record.id)"
              >
                <a-button type="link" size="small" danger>
                  <DeleteOutlined />删除
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无编码规则数据" />
        </template>
      </a-table>
    </div>

    <!-- 编辑/新增弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="isEdit ? '编辑编码规则' : '新增编码规则'"
      :width="720"
      :destroyOnClose="true"
      :bodyStyle="{ maxHeight: '60vh', overflowY: 'auto', padding: '16px 24px' }"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '90px' } }"
      >
        <!-- 第1行：规则编码 + 规则名称（新增时可编辑；编辑时只读） -->
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="规则编码" name="ruleCode">
              <a-input v-model:value="formData.ruleCode" :disabled="isEdit" placeholder="请输入规则编码" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="规则名称" name="ruleName">
              <a-input v-model:value="formData.ruleName" :disabled="isEdit" placeholder="请输入规则名称" :maxlength="100" />
            </a-form-item>
          </a-col>
        </a-row>
        <!-- 第2行：业务实体 + 编码字段 -->
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="业务实体" name="businessEntity">
              <a-input v-model:value="formData.businessEntity" :disabled="isEdit" placeholder="请输入业务实体" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="编码字段" name="codeField">
              <a-input v-model:value="formData.codeField" :disabled="isEdit" placeholder="请输入编码字段" :maxlength="100" />
            </a-form-item>
          </a-col>
        </a-row>
        <!-- 第3行：前缀 + 分隔符 -->
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="前缀" name="prefix">
              <a-input v-model:value="(formData.prefix as string | undefined)" placeholder="请输入前缀" :maxlength="20" allowClear />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="分隔符" name="separator">
              <a-input v-model:value="(formData.separator as string | undefined)" placeholder="请输入分隔符" :maxlength="5" allowClear />
            </a-form-item>
          </a-col>
        </a-row>
        <!-- 第4行：日期格式 + 流水号长度 -->
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="日期格式" name="dateFormat">
              <a-select v-model:value="(formData.dateFormat as string | undefined)" placeholder="请选择日期格式" style="width: 100%">
                <a-select-option :value="null">无</a-select-option>
                <a-select-option value="yyyy">yyyy</a-select-option>
                <a-select-option value="yyyyMM">yyyyMM</a-select-option>
                <a-select-option value="yyyyMMdd">yyyyMMdd</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="流水号长度" name="seqLength">
              <a-input-number v-model:value="formData.seqLength" :min="1" :max="10" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <!-- 第5行：重置周期 + 组织隔离 -->
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="重置周期" name="resetPeriod">
              <a-select v-model:value="formData.resetPeriod" style="width: 100%">
                <a-select-option value="never">不重置</a-select-option>
                <a-select-option value="year">按年</a-select-option>
                <a-select-option value="month">按月</a-select-option>
                <a-select-option value="day">按日</a-select-option>
              </a-select>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="启用状态" name="enabled">
              <a-switch v-model:checked="formData.enabled" />
            </a-form-item>
          </a-col>
        </a-row>
        <!-- 第6行：组织隔离（单列） -->
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="组织隔离" name="orgIsolation">
              <a-switch v-model:checked="formData.orgIsolation" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="编码预览">
              <span class="code-preview code-preview-large">{{ editPreview }}</span>
            </a-form-item>
          </a-col>
        </a-row>
        <!-- 说明（全宽） -->
        <a-form-item label="说明" name="description" :label-col="{ style: { width: '90px' } }">
          <a-textarea
            v-model:value="(formData.description as string | undefined)"
            :rows="3"
            placeholder="请输入说明"
            :maxlength="200"
            showCount
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { EditOutlined, PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { getCodeRules, updateCodeRule, createCodeRule, deleteCodeRule, type CodeRuleDto, type CodeRuleUpdateDto, type CodeRuleCreateDto } from '@/api/codeRule'

const columns = [
  { title: '规则编码', dataIndex: 'ruleCode', key: 'ruleCode', width: 100 },
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', width: 120 },
  { title: '业务实体', dataIndex: 'businessEntity', key: 'businessEntity', width: 130 },
  { title: '前缀', dataIndex: 'prefix', key: 'prefix', width: 80 },
  { title: '日期格式', dataIndex: 'dateFormat', key: 'dateFormat', width: 110 },
  { title: '流水号长度', dataIndex: 'seqLength', key: 'seqLength', width: 100, align: 'center' as const },
  { title: '分隔符', dataIndex: 'separator', key: 'separator', width: 80, align: 'center' as const },
  { title: '重置周期', dataIndex: 'resetPeriod', key: 'resetPeriod', width: 100 },
  { title: '编码预览', dataIndex: 'preview', key: 'preview', width: 200 },
  { title: '启用状态', dataIndex: 'enabled', key: 'enabled', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 130, align: 'center' as const, fixed: 'right' as const },
]

const dateFormatOptions: Record<string, string> = {
  '': '无',
  'yyyy': 'yyyy',
  'yyyyMM': 'yyyyMM',
  'yyyyMMdd': 'yyyyMMdd',
}

const resetPeriodOptions: Record<string, string> = {
  'never': '不重置',
  'year': '按年',
  'month': '按月',
  'day': '按日',
}

function dateFormatLabel(val: string | null) {
  return val ? (dateFormatOptions[val] || val) : '无'
}

function resetPeriodLabel(val: string) {
  return resetPeriodOptions[val] || val
}

function formatDateExample(format: string | null): string {
  if (!format) return ''
  const now = new Date()
  const y = now.getFullYear().toString()
  const m = (now.getMonth() + 1).toString().padStart(2, '0')
  const d = now.getDate().toString().padStart(2, '0')
  if (format === 'yyyy') return y
  if (format === 'yyyyMM') return y + m
  if (format === 'yyyyMMdd') return y + m + d
  return ''
}

function buildPreview(rule: { prefix: string | null; separator: string | null; dateFormat: string | null; seqLength: number }): string {
  const parts: string[] = []
  if (rule.prefix) parts.push(rule.prefix)
  const datePart = formatDateExample(rule.dateFormat)
  if (datePart) parts.push(datePart)
  const seq = '1'.padStart(rule.seqLength, '0')
  parts.push(seq)
  return parts.join(rule.separator || '')
}

const tableScrollY = ref(400)

function updateTableHeight() {
  // 视口高度 - PageHeader(约80px) - 表头(约55px) - 边距(约32px)
  tableScrollY.value = Math.max(300, window.innerHeight - 80 - 55 - 32)
}
const loading = ref(false)
const tableData = ref<CodeRuleDto[]>([])

// 弹窗
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentRecord = ref<CodeRuleDto | null>(null)
const isEdit = ref(false)

const formData = reactive<CodeRuleUpdateDto & { ruleCode: string; ruleName: string; businessEntity: string; codeField: string }>({
  ruleCode: '',
  ruleName: '',
  businessEntity: '',
  codeField: '',
  prefix: null,
  dateFormat: null,
  seqLength: 4,
  separator: null,
  resetPeriod: 'never',
  orgIsolation: false,
  enabled: true,
  description: null,
})

const formRules: Record<string, any[]> = {
  ruleCode: [{ required: true, message: '请输入规则编码', trigger: 'blur' }],
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  businessEntity: [{ required: true, message: '请输入业务实体', trigger: 'blur' }],
  codeField: [{ required: true, message: '请输入编码字段', trigger: 'blur' }],
  seqLength: [{ required: true, message: '请输入流水号长度', trigger: 'change' }],
  resetPeriod: [{ required: true, message: '请选择重置周期', trigger: 'change' }],
}

const editPreview = computed(() => {
  return buildPreview({
    prefix: formData.prefix || null,
    separator: formData.separator || null,
    dateFormat: formData.dateFormat || null,
    seqLength: formData.seqLength || 4,
  })
})

async function fetchData() {
  loading.value = true
  try {
    const res = await getCodeRules()
    tableData.value = (res as any) || []
  } finally {
    loading.value = false
  }
}

function handleEdit(record: CodeRuleDto) {
  isEdit.value = true
  currentRecord.value = record
  formData.ruleCode = record.ruleCode
  formData.ruleName = record.ruleName
  formData.businessEntity = record.businessEntity
  formData.codeField = record.codeField
  formData.prefix = record.prefix
  formData.dateFormat = record.dateFormat
  formData.seqLength = record.seqLength
  formData.separator = record.separator
  formData.resetPeriod = record.resetPeriod
  formData.orgIsolation = record.orgIsolation
  formData.enabled = record.enabled
  formData.description = record.description
  dialogVisible.value = true
}

function handleAdd() {
  isEdit.value = false
  currentRecord.value = null
  formData.ruleCode = ''
  formData.ruleName = ''
  formData.businessEntity = ''
  formData.codeField = 'F编码'
  formData.prefix = null
  formData.dateFormat = null
  formData.seqLength = 4
  formData.separator = '-'
  formData.resetPeriod = 'never'
  formData.orgIsolation = false
  formData.enabled = true
  formData.description = null
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  submitLoading.value = true
  try {
    if (isEdit.value) {
      if (!currentRecord.value) return
      await updateCodeRule(currentRecord.value.id, {
        prefix: formData.prefix,
        dateFormat: formData.dateFormat,
        seqLength: formData.seqLength,
        separator: formData.separator,
        resetPeriod: formData.resetPeriod,
        orgIsolation: formData.orgIsolation,
        enabled: formData.enabled,
        description: formData.description,
      })
      message.success('更新成功')
    } else {
      await createCodeRule({
        ruleCode: formData.ruleCode,
        ruleName: formData.ruleName,
        businessEntity: formData.businessEntity,
        codeField: formData.codeField,
        prefix: formData.prefix,
        dateFormat: formData.dateFormat,
        seqLength: formData.seqLength,
        separator: formData.separator,
        resetPeriod: formData.resetPeriod,
        orgIsolation: formData.orgIsolation,
        description: formData.description,
      })
      message.success('新增成功')
    }
    dialogVisible.value = false
    fetchData()
  } finally {
    submitLoading.value = false
  }
}

async function handleDelete(id: number) {
  try {
    await deleteCodeRule(id)
    message.success('删除成功')
    fetchData()
  } catch {
    message.error('删除失败')
  }
}

onMounted(() => {
  updateTableHeight()
  window.addEventListener('resize', updateTableHeight)
  fetchData()
})

onUnmounted(() => {
  window.removeEventListener('resize', updateTableHeight)
})
</script>

<style scoped lang="scss">
.page-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.table-wrapper {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  padding: 0 16px 16px;
}

.code-preview {
  display: inline-block;
  padding: 2px 8px;
  background: #f0f5ff;
  border: 1px solid #adc6ff;
  border-radius: 4px;
  font-weight: 600;
  color: #1d39c4;
  font-family: 'Courier New', Courier, monospace;
  font-size: 13px;
}

.code-preview-large {
  padding: 4px 12px;
  font-size: 15px;
}
</style>
