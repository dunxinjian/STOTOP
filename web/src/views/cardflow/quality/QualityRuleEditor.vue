<template>
  <a-modal
    v-model:open="dialogOpen"
    :title="isEdit ? '编辑质量规则' : '新建质量规则'"
    width="800px"
    :destroyOnClose="true"
    centered
    :bodyStyle="{ maxHeight: '68vh', overflowY: 'auto', padding: '16px 24px' }"
  >
    <a-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      :labelCol="{ style: { width: '110px' } }"
      layout="horizontal"
    >
      <!-- 区域1: 基本信息 -->
      <div class="section-title">基本信息</div>
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="规则名称" name="ruleName">
            <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="规则编码" name="ruleCode">
            <a-input v-model:value="formData.ruleCode" placeholder="请输入规则编码" />
          </a-form-item>
        </a-col>
      </a-row>
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="关联管道" name="pipelineId">
            <a-select
              v-model:value="formData.pipelineId"
              placeholder="请选择关联管道"
              allowClear
              showSearch
              :loading="pipelinesLoading"
              :filterOption="filterOption"
            >
              <a-select-option v-for="p in pipelines" :key="p.id" :value="p.id">{{ p.name }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="目标表" name="targetTable">
            <a-select
              v-model:value="formData.targetTable"
              placeholder="请选择目标暂存表"
              allowClear
              showSearch
              :loading="stagingTablesLoading"
              :filterOption="filterOption"
            >
              <a-select-option v-for="t in stagingTables" :key="t.tableName" :value="t.tableName">{{ t.tableName }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>

      <!-- 区域2: 检查配置 -->
      <div class="section-title">检查配置</div>
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="规则级别" name="ruleLevel">
            <a-select v-model:value="formData.ruleLevel" placeholder="请选择规则级别">
              <a-select-option value="Field">Field（字段级）</a-select-option>
              <a-select-option value="Row">Row（行级）</a-select-option>
              <a-select-option value="Batch">Batch（批次级）</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="检查类型" name="checkType">
            <a-select v-model:value="formData.checkType" placeholder="请选择检查类型" @change="onCheckTypeChange">
              <a-select-option value="NotNull">NotNull（非空）</a-select-option>
              <a-select-option value="Format">Format（格式）</a-select-option>
              <a-select-option value="Range">Range（范围）</a-select-option>
              <a-select-option value="Expression">Expression（表达式）</a-select-option>
              <a-select-option value="SqlCondition">SqlCondition（SQL条件）</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>
      <a-form-item label="目标字段" name="targetField">
        <a-input v-model:value="formData.targetField" placeholder="请输入目标字段名，如 F金额" />
      </a-form-item>

      <!-- 动态参数区域 -->
      <template v-if="formData.checkType === 'Format'">
        <a-form-item label="格式类型">
          <a-select v-model:value="params.format" placeholder="请选择格式类型" style="width: 200px;">
            <a-select-option value="decimal">decimal（数值）</a-select-option>
            <a-select-option value="date">date（日期）</a-select-option>
            <a-select-option value="regex">regex（正则表达式）</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item v-if="params.format === 'regex'" label="正则模式">
          <a-input v-model:value="params.pattern" placeholder="请输入正则表达式" style="font-family: monospace;" />
        </a-form-item>
        <a-form-item v-if="params.format === 'decimal'" label="精度">
          <a-input-number v-model:value="params.precision" :min="0" :max="10" placeholder="小数位数" style="width: 160px;" />
        </a-form-item>
        <a-form-item v-if="params.format === 'date'" label="日期格式">
          <a-input v-model:value="params.dateFormat" placeholder="如 yyyy-MM-dd" style="width: 200px;" />
        </a-form-item>
      </template>

      <template v-if="formData.checkType === 'Range'">
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="最小值">
              <a-input-number v-model:value="params.min" placeholder="最小值" style="width: 100%;" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="最大值">
              <a-input-number v-model:value="params.max" placeholder="最大值" style="width: 100%;" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item label="包含边界">
              <a-switch v-model:checked="params.inclusive" checkedChildren="是" unCheckedChildren="否" />
            </a-form-item>
          </a-col>
        </a-row>
      </template>

      <template v-if="formData.checkType === 'Expression'">
        <a-form-item label="表达式">
          <a-textarea
            v-model:value="params.expression"
            :rows="3"
            placeholder="请输入检查表达式，如 row['F金额'] > 0"
            style="font-family: monospace;"
          />
        </a-form-item>
      </template>

      <template v-if="formData.checkType === 'SqlCondition'">
        <a-form-item label="SQL条件">
          <a-textarea
            v-model:value="params.sql"
            :rows="3"
            placeholder="请输入SQL条件，如 F金额 > 0 AND F日期 IS NOT NULL"
            style="font-family: monospace;"
          />
        </a-form-item>
      </template>

      <!-- 区域3: 错误配置 -->
      <div class="section-title">错误配置</div>
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="错误类型编码">
            <a-input v-model:value="formData.errorTypeCode" placeholder="如 NULL_CHECK_FAILED" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="严重级别" name="severityLevel">
            <a-select v-model:value="formData.severityLevel" placeholder="请选择严重级别">
              <a-select-option value="Error">Error（错误）</a-select-option>
              <a-select-option value="Warning">Warning（警告）</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>
      <a-form-item label="质量维度">
        <a-select v-model:value="formData.qualityDimension" placeholder="请选择质量维度" allowClear>
          <a-select-option value="Completeness">完整性 (Completeness)</a-select-option>
          <a-select-option value="Accuracy">准确性 (Accuracy)</a-select-option>
          <a-select-option value="Consistency">一致性 (Consistency)</a-select-option>
          <a-select-option value="Timeliness">及时性 (Timeliness)</a-select-option>
          <a-select-option value="Uniqueness">唯一性 (Uniqueness)</a-select-option>
          <a-select-option value="Validity">有效性 (Validity)</a-select-option>
        </a-select>
      </a-form-item>
      <a-form-item label="消息模板">
        <a-textarea
          v-model:value="formData.messageTemplate"
          :rows="2"
          placeholder="错误消息模板，可用变量: {Field}, {Value}, {Rule}"
        />
      </a-form-item>
      <a-form-item label="建议修复方案">
        <a-textarea
          v-model:value="formData.suggestedFix"
          :rows="2"
          placeholder="给用户的修复建议"
        />
      </a-form-item>

      <!-- 区域4: 高级选项 -->
      <div class="section-title">高级选项</div>
      <a-row :gutter="16">
        <a-col :span="8">
          <a-form-item label="是否阻断">
            <a-switch v-model:checked="formData.isBlocking" checkedChildren="阻断" unCheckedChildren="放行" />
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="启用状态">
            <a-switch v-model:checked="formData.isEnabled" checkedChildren="启用" unCheckedChildren="禁用" />
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="排序号">
            <a-input-number v-model:value="formData.sortOrder" :min="0" :max="9999" style="width: 100%;" />
          </a-form-item>
        </a-col>
      </a-row>
    </a-form>

    <template #footer>
      <div style="display: flex; justify-content: flex-end; gap: 8px;">
        <a-button @click="dialogOpen = false">取消</a-button>
        <a-button type="primary" @click="handleSubmit" :loading="submitting">
          {{ isEdit ? '保存' : '创建' }}
        </a-button>
      </div>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import {
  createQualityRule,
  updateQualityRule,
} from '@/api/qualityRule'
import type { QualityRuleDto } from '@/api/qualityRule'
import { getPipelines, getStagingTables } from '@/api/cardflow'
import type { PipelineDto, StagingTableInfo } from '@/api/cardflow'

const props = defineProps<{
  open: boolean
  rule: QualityRuleDto | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved'): void
}>()

const dialogOpen = computed({
  get: () => props.open,
  set: (val: boolean) => emit('update:open', val),
})

const isEdit = computed(() => !!props.rule)
const submitting = ref(false)
const formRef = ref<FormInstance>()

// ==================== 下拉选项 ====================
const pipelines = ref<PipelineDto[]>([])
const pipelinesLoading = ref(false)
const stagingTables = ref<StagingTableInfo[]>([])
const stagingTablesLoading = ref(false)

async function fetchPipelines() {
  pipelinesLoading.value = true
  try {
    const res: any = await getPipelines()
    pipelines.value = res.data ?? res ?? []
  } catch { /* silent */ } finally {
    pipelinesLoading.value = false
  }
}

async function fetchStagingTables() {
  stagingTablesLoading.value = true
  try {
    const res: any = await getStagingTables()
    stagingTables.value = res.data ?? res ?? []
  } catch { /* silent */ } finally {
    stagingTablesLoading.value = false
  }
}

function filterOption(input: string, option: any) {
  const label = option?.children?.[0]?.children ?? option?.label ?? ''
  return String(label).toLowerCase().includes(input.toLowerCase())
}

// ==================== 表单数据 ====================
const formData = reactive({
  ruleName: '',
  ruleCode: '',
  pipelineId: undefined as number | undefined,
  targetTable: undefined as string | undefined,
  ruleLevel: 'Field' as string,
  checkType: 'NotNull' as string,
  targetField: '',
  errorTypeCode: '',
  severityLevel: 'Error' as string,
  qualityDimension: undefined as string | undefined,
  messageTemplate: '',
  suggestedFix: '',
  isBlocking: false,
  isEnabled: true,
  sortOrder: 0,
})

const params = reactive({
  // Format
  format: 'decimal' as string,
  pattern: '',
  precision: 2 as number | undefined,
  dateFormat: 'yyyy-MM-dd',
  // Range
  min: undefined as number | undefined,
  max: undefined as number | undefined,
  inclusive: true,
  // Expression
  expression: '',
  // SqlCondition
  sql: '',
})

const formRules = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' as const }],
  ruleCode: [{ required: true, message: '请输入规则编码', trigger: 'blur' as const }],
  ruleLevel: [{ required: true, message: '请选择规则级别', trigger: 'change' as const }],
  checkType: [{ required: true, message: '请选择检查类型', trigger: 'change' as const }],
  severityLevel: [{ required: true, message: '请选择严重级别', trigger: 'change' as const }],
}

function onCheckTypeChange() {
  // 重置参数
  params.format = 'decimal'
  params.pattern = ''
  params.precision = 2
  params.dateFormat = 'yyyy-MM-dd'
  params.min = undefined
  params.max = undefined
  params.inclusive = true
  params.expression = ''
  params.sql = ''
}

// ==================== 构造参数JSON ====================
function buildParametersJson(): string | null {
  switch (formData.checkType) {
    case 'NotNull':
      return null
    case 'Format': {
      const obj: Record<string, any> = { format: params.format }
      if (params.format === 'regex') obj.pattern = params.pattern
      if (params.format === 'decimal') obj.precision = params.precision
      if (params.format === 'date') obj.dateFormat = params.dateFormat
      return JSON.stringify(obj)
    }
    case 'Range':
      return JSON.stringify({ min: params.min, max: params.max, inclusive: params.inclusive })
    case 'Expression':
      return JSON.stringify({ expression: params.expression })
    case 'SqlCondition':
      return JSON.stringify({ sql: params.sql })
    default:
      return null
  }
}

// ==================== 反序列化参数 ====================
function parseParameters(checkType: string, parametersStr: string | null) {
  onCheckTypeChange()
  if (!parametersStr) return
  try {
    const obj = JSON.parse(parametersStr)
    switch (checkType) {
      case 'Format':
        params.format = obj.format ?? 'decimal'
        params.pattern = obj.pattern ?? ''
        params.precision = obj.precision ?? 2
        params.dateFormat = obj.dateFormat ?? 'yyyy-MM-dd'
        break
      case 'Range':
        params.min = obj.min ?? undefined
        params.max = obj.max ?? undefined
        params.inclusive = obj.inclusive ?? true
        break
      case 'Expression':
        params.expression = obj.expression ?? ''
        break
      case 'SqlCondition':
        params.sql = obj.sql ?? ''
        break
    }
  } catch { /* ignore */ }
}

// ==================== 初始化/重置 ====================
function resetForm() {
  formData.ruleName = ''
  formData.ruleCode = ''
  formData.pipelineId = undefined
  formData.targetTable = undefined
  formData.ruleLevel = 'Field'
  formData.checkType = 'NotNull'
  formData.targetField = ''
  formData.errorTypeCode = ''
  formData.severityLevel = 'Error'
  formData.qualityDimension = undefined
  formData.messageTemplate = ''
  formData.suggestedFix = ''
  formData.isBlocking = false
  formData.isEnabled = true
  formData.sortOrder = 0
  onCheckTypeChange()
}

function loadFromRule(rule: QualityRuleDto) {
  formData.ruleName = rule.ruleName
  formData.ruleCode = rule.ruleCode
  formData.pipelineId = rule.pipelineId ?? undefined
  formData.targetTable = rule.targetTable ?? undefined
  formData.ruleLevel = rule.ruleLevel
  formData.checkType = rule.checkType
  formData.targetField = rule.targetField ?? ''
  formData.errorTypeCode = rule.errorTypeCode ?? ''
  formData.severityLevel = rule.severityLevel
  formData.qualityDimension = rule.qualityDimension ?? undefined
  formData.messageTemplate = rule.messageTemplate ?? ''
  formData.suggestedFix = rule.suggestedFix ?? ''
  formData.isBlocking = rule.isBlocking
  formData.isEnabled = rule.isEnabled
  formData.sortOrder = rule.sortOrder
  parseParameters(rule.checkType, rule.parameters)
}

watch(
  () => props.open,
  (val) => {
    if (!val) return
    if (pipelines.value.length === 0) fetchPipelines()
    if (stagingTables.value.length === 0) fetchStagingTables()
    if (props.rule) {
      loadFromRule(props.rule)
    } else {
      resetForm()
    }
  },
)

// ==================== 提交 ====================
async function handleSubmit() {
  if (!formRef.value) return
  await formRef.value.validate()
  submitting.value = true
  try {
    const payload = {
      ruleName: formData.ruleName,
      ruleCode: formData.ruleCode,
      pipelineId: formData.pipelineId,
      targetTable: formData.targetTable,
      ruleLevel: formData.ruleLevel,
      checkType: formData.checkType,
      targetField: formData.targetField || null,
      parameters: buildParametersJson(),
      errorTypeCode: formData.errorTypeCode || null,
      severityLevel: formData.severityLevel,
      qualityDimension: formData.qualityDimension,
      messageTemplate: formData.messageTemplate || null,
      suggestedFix: formData.suggestedFix || null,
      isBlocking: formData.isBlocking,
      isEnabled: formData.isEnabled,
      sortOrder: formData.sortOrder,
    }

    if (props.rule) {
      await updateQualityRule(props.rule.id, payload)
      message.success('规则更新成功')
    } else {
      await createQualityRule(payload)
      message.success('规则创建成功')
    }
    emit('saved')
    emit('update:open', false)
  } catch {
    message.error(props.rule ? '更新失败' : '创建失败')
  } finally {
    submitting.value = false
  }
}
</script>

<style lang="scss" scoped>
.section-title {
  font-weight: 600;
  font-size: 14px;
  color: var(--text-1);
  margin: 16px 0 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border);

  &:first-child {
    margin-top: 0;
  }
}
</style>
