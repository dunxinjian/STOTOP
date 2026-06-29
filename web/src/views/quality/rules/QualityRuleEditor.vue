<template>
  <a-modal
    :open="open"
    :title="isEdit ? '编辑质量规则' : '新建质量规则'"
    width="780px"
    :destroy-on-close="true"
    :mask-closable="false"
    @update:open="(v: boolean) => emit('update:open', v)"
    @cancel="handleCancel"
  >
    <a-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      :label-col="{ style: { width: '100px' } }"
      style="padding: 4px 8px; max-height: 65vh; overflow-y: auto"
    >
      <div class="section-title">基本信息</div>
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="规则名称" name="ruleName">
            <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" :maxlength="100" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="规则编码" name="ruleCode">
            <a-input v-model:value="formData.ruleCode" placeholder="如：NOT_NULL_CUSTOMER" :maxlength="80" />
          </a-form-item>
        </a-col>
      </a-row>

      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="关联管道" name="pipelineId">
            <a-select
              v-model:value="formData.pipelineId"
              placeholder="可选，按管道关联"
              allow-clear
              show-search
              :filter-option="filterOption"
            >
              <a-select-option v-for="p in pipelines" :key="p.id" :value="p.id">{{ p.name }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="目标表" name="targetTable">
            <a-select
              v-model:value="formData.targetTable"
              placeholder="可选，按目标表关联"
              allow-clear
              show-search
              :filter-option="filterOption"
              @change="onTargetTableChange"
            >
              <a-select-option v-for="t in stagingTables" :key="t.tableName" :value="t.tableName">{{ t.tableName }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>

      <div class="section-title">规则配置</div>
      <a-row :gutter="16">
        <a-col :span="8">
          <a-form-item label="规则级别" name="ruleLevel">
            <a-select v-model:value="formData.ruleLevel" placeholder="请选择规则级别">
              <a-select-option value="Field">字段级</a-select-option>
              <a-select-option value="Row">行级</a-select-option>
              <a-select-option value="Batch">批次级</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="检查类型" name="checkType">
            <a-select v-model:value="formData.checkType" placeholder="请选择检查类型">
              <a-select-option value="NotNull">非空</a-select-option>
              <a-select-option value="Format">格式</a-select-option>
              <a-select-option value="Range">范围</a-select-option>
              <a-select-option value="Expression">表达式</a-select-option>
              <a-select-option value="SqlCondition">SQL条件</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="严重级别" name="severityLevel">
            <a-select v-model:value="formData.severityLevel" placeholder="请选择严重级别">
              <a-select-option value="Error">错误</a-select-option>
              <a-select-option value="Warning">警告</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>

      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="目标字段" name="targetField">
            <a-select
              v-if="columnOptions.length > 0"
              v-model:value="formData.targetField"
              placeholder="选择目标字段"
              allow-clear
              show-search
              :filter-option="filterOption"
            >
              <a-select-option v-for="c in columnOptions" :key="c.columnName" :value="c.columnName">
                {{ c.columnName }} <span style="color:#999;">({{ c.dataType }})</span>
              </a-select-option>
            </a-select>
            <a-input
              v-else
              v-model:value="formData.targetField"
              placeholder="请输入目标字段名（字段级/行级必填）"
              allow-clear
            />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="质量维度" name="qualityDimension">
            <a-select v-model:value="formData.qualityDimension" placeholder="可选" allow-clear>
              <a-select-option value="Completeness">完整性</a-select-option>
              <a-select-option value="Accuracy">准确性</a-select-option>
              <a-select-option value="Consistency">一致性</a-select-option>
              <a-select-option value="Validity">有效性</a-select-option>
              <a-select-option value="Uniqueness">唯一性</a-select-option>
              <a-select-option value="Timeliness">及时性</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>

      <a-row :gutter="16">
        <a-col :span="24">
          <a-form-item label="参数配置" name="parameters">
            <a-textarea
              v-model:value="formData.parameters"
              :placeholder="parametersPlaceholder"
              :rows="4"
              :maxlength="2000"
              show-count
            />
            <div class="hint">{{ parametersHint }}</div>
          </a-form-item>
        </a-col>
      </a-row>

      <div class="section-title">错误处理</div>
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="错误类型编码" name="errorTypeCode">
            <a-input v-model:value="formData.errorTypeCode" placeholder="可选，关联问题分类编码" :maxlength="80" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="排序" name="sortOrder">
            <a-input-number
              v-model:value="formData.sortOrder"
              :min="0"
              :max="9999"
              style="width: 100%"
              placeholder="数字越小越优先"
            />
          </a-form-item>
        </a-col>
      </a-row>

      <a-row :gutter="16">
        <a-col :span="24">
          <a-form-item label="错误消息" name="messageTemplate">
            <a-textarea
              v-model:value="formData.messageTemplate"
              placeholder="支持占位符 {field}、{value}，例如：字段 {field} 不能为空"
              :rows="2"
              :maxlength="500"
            />
          </a-form-item>
        </a-col>
      </a-row>

      <a-row :gutter="16">
        <a-col :span="24">
          <a-form-item label="修复建议" name="suggestedFix">
            <a-textarea
              v-model:value="formData.suggestedFix"
              placeholder="可选，向用户展示的修复建议"
              :rows="2"
              :maxlength="500"
            />
          </a-form-item>
        </a-col>
      </a-row>

      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="阻断模式">
            <a-switch
              v-model:checked="formData.isBlocking"
              checked-children="阻断"
              un-checked-children="放行"
            />
            <span class="hint" style="margin-left: 12px;">阻断时违规数据将拦截入库</span>
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="启用状态">
            <a-switch
              v-model:checked="formData.isEnabled"
              checked-children="启用"
              un-checked-children="禁用"
            />
          </a-form-item>
        </a-col>
      </a-row>
    </a-form>

    <template #footer>
      <a-button @click="handleCancel">取消</a-button>
      <a-button :loading="testLoading" @click="handleTest">测试</a-button>
      <a-button type="primary" :loading="submitLoading" @click="handleSubmit">
        {{ isEdit ? '保存' : '创建' }}
      </a-button>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import type { SelectValue } from 'ant-design-vue/es/select'
import {
  createQualityRule,
  updateQualityRule,
  testQualityRule,
} from '@/api/qualityRule'
import type { QualityRuleDto, QualityRuleCreateDto } from '@/api/qualityRule'
import {
  getPipelines,
  getStagingTables,
  getStagingTableColumns,
} from '@/api/cardflow'
import type { PipelineDto, StagingTableInfo, StagingColumnInfo } from '@/api/cardflow'

interface Props {
  open: boolean
  rule: QualityRuleDto | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'update:open', v: boolean): void
  (e: 'saved'): void
}>()

const isEdit = computed(() => !!props.rule?.id)

// ==================== 表单数据 ====================
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const testLoading = ref(false)

// 本地表单模型：把作为下拉/输入框 v-model 的可空字段收窄为可选（去 null），
// 避免 AntD value prop 不接受 null；提交时仍兼容共享 QualityRuleCreateDto（这些字段均为可选）。
type QualityRuleForm = Omit<
  QualityRuleCreateDto,
  'pipelineId' | 'targetTable' | 'targetField' | 'qualityDimension' | 'parameters' | 'errorTypeCode' | 'messageTemplate' | 'suggestedFix'
> & {
  pipelineId?: number
  targetTable?: string
  targetField?: string
  qualityDimension?: string
  parameters?: string
  errorTypeCode?: string
  messageTemplate?: string
  suggestedFix?: string
}

const formData = reactive<QualityRuleForm>({
  ruleName: '',
  ruleCode: '',
  pipelineId: undefined,
  targetTable: undefined,
  orgId: null,
  ruleLevel: 'Field',
  checkType: 'NotNull',
  targetField: undefined,
  parameters: undefined,
  errorTypeCode: undefined,
  severityLevel: 'Error',
  qualityDimension: undefined,
  messageTemplate: undefined,
  suggestedFix: undefined,
  isBlocking: false,
  isEnabled: true,
  sortOrder: 0,
})

const formRules: Record<string, Rule[]> = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  ruleCode: [{ required: true, message: '请输入规则编码', trigger: 'blur' }],
  ruleLevel: [{ required: true, message: '请选择规则级别', trigger: 'change' }],
  checkType: [{ required: true, message: '请选择检查类型', trigger: 'change' }],
  severityLevel: [{ required: true, message: '请选择严重级别', trigger: 'change' }],
  targetField: [
    {
      validator: async (_rule, value) => {
        if ((formData.ruleLevel === 'Field' || formData.ruleLevel === 'Row') && !value) {
          return Promise.reject('字段级/行级规则必须指定目标字段')
        }
        return Promise.resolve()
      },
      trigger: 'blur',
    },
  ],
}

// ==================== 下拉选项 ====================
const pipelines = ref<PipelineDto[]>([])
const stagingTables = ref<StagingTableInfo[]>([])
const columnOptions = ref<StagingColumnInfo[]>([])

function filterOption(input: string, option: any) {
  const text = option?.children?.[0]?.children ?? option?.label ?? option?.value ?? ''
  return String(text).toLowerCase().includes(input.toLowerCase())
}

async function loadPipelines() {
  try {
    const res: any = await getPipelines()
    pipelines.value = res?.data ?? res ?? []
  } catch { /* silent */ }
}

async function loadStagingTables() {
  try {
    const res: any = await getStagingTables()
    stagingTables.value = res?.data ?? res ?? []
  } catch { /* silent */ }
}

async function loadColumnsFor(tableName: string | null | undefined) {
  if (!tableName) {
    columnOptions.value = []
    return
  }
  // 优先从已加载的列表中取
  const hit = stagingTables.value.find(t => t.tableName === tableName)
  if (hit?.columns?.length) {
    columnOptions.value = hit.columns
    return
  }
  try {
    const res: any = await getStagingTableColumns(tableName)
    columnOptions.value = res?.data ?? res ?? []
  } catch {
    columnOptions.value = []
  }
}

function onTargetTableChange(val: SelectValue) {
  loadColumnsFor(typeof val === 'string' ? val : null)
}

// ==================== 参数提示 ====================
const parametersPlaceholder = computed(() => {
  switch (formData.checkType) {
    case 'NotNull':
      return '通常无需配置；如需自定义提示可填写 JSON。'
    case 'Format':
      return '示例：{"pattern":"^\\\\d{11}$"}  // 手机号格式'
    case 'Range':
      return '示例：{"min":0,"max":100}'
    case 'Expression':
      return '示例：{"expression":"FAmount > 0 && FQuantity > 0"}'
    case 'SqlCondition':
      return '示例：{"sql":"SELECT COUNT(*) FROM @table WHERE FAmount < 0"}'
    default:
      return '请输入 JSON 配置'
  }
})

const parametersHint = computed(() => {
  switch (formData.checkType) {
    case 'NotNull':
      return '非空检查：默认按目标字段判定，不需额外参数。'
    case 'Format':
      return '格式检查：使用 pattern 正则；支持 message 自定义提示。'
    case 'Range':
      return '范围检查：使用 min / max 数值；可二选一。'
    case 'Expression':
      return '表达式检查：使用 expression 字段，支持引用记录字段。'
    case 'SqlCondition':
      return 'SQL检查：返回结果为 0 表示通过，非 0 表示存在违规。'
    default:
      return ''
  }
})

// ==================== 监听 ====================
watch(
  () => props.open,
  async (val) => {
    if (val) {
      await Promise.all([loadPipelines(), loadStagingTables()])
      if (props.rule) {
        // 编辑模式：回填
        Object.assign(formData, {
          ruleName: props.rule.ruleName,
          ruleCode: props.rule.ruleCode,
          pipelineId: props.rule.pipelineId ?? undefined,
          targetTable: props.rule.targetTable ?? undefined,
          orgId: props.rule.orgId,
          ruleLevel: props.rule.ruleLevel,
          checkType: props.rule.checkType,
          targetField: props.rule.targetField ?? undefined,
          parameters: props.rule.parameters ?? undefined,
          errorTypeCode: props.rule.errorTypeCode ?? undefined,
          severityLevel: props.rule.severityLevel,
          qualityDimension: props.rule.qualityDimension ?? undefined,
          messageTemplate: props.rule.messageTemplate ?? undefined,
          suggestedFix: props.rule.suggestedFix ?? undefined,
          isBlocking: props.rule.isBlocking,
          isEnabled: props.rule.isEnabled,
          sortOrder: props.rule.sortOrder ?? 0,
        })
        await loadColumnsFor(props.rule.targetTable)
      } else {
        resetForm()
      }
    }
  },
)

function resetForm() {
  Object.assign(formData, {
    ruleName: '',
    ruleCode: '',
    pipelineId: undefined,
    targetTable: undefined,
    orgId: null,
    ruleLevel: 'Field',
    checkType: 'NotNull',
    targetField: undefined,
    parameters: undefined,
    errorTypeCode: undefined,
    severityLevel: 'Error',
    qualityDimension: undefined,
    messageTemplate: undefined,
    suggestedFix: undefined,
    isBlocking: false,
    isEnabled: true,
    sortOrder: 0,
  })
  columnOptions.value = []
  formRef.value?.clearValidate()
}

// ==================== 操作 ====================
function handleCancel() {
  emit('update:open', false)
}

async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  // 参数 JSON 校验
  if (formData.parameters) {
    try {
      JSON.parse(formData.parameters)
    } catch {
      message.error('参数配置不是合法的 JSON')
      return
    }
  }

  submitLoading.value = true
  try {
    const payload: QualityRuleCreateDto = { ...formData }
    if (isEdit.value && props.rule) {
      await updateQualityRule(props.rule.id, payload)
      message.success('已更新')
    } else {
      await createQualityRule(payload)
      message.success('已创建')
    }
    emit('saved')
    emit('update:open', false)
  } catch (e: any) {
    message.error(e?.message ?? '保存失败')
  } finally {
    submitLoading.value = false
  }
}

async function handleTest() {
  if (formData.parameters) {
    try {
      JSON.parse(formData.parameters)
    } catch {
      message.error('参数配置不是合法的 JSON')
      return
    }
  }
  testLoading.value = true
  try {
    const res: any = await testQualityRule({
      ruleLevel: formData.ruleLevel,
      checkType: formData.checkType,
      targetField: formData.targetField,
      parameters: formData.parameters,
      targetTable: formData.targetTable,
    })
    const result = res?.data ?? res
    if (result?.success) {
      if (result.passed) {
        message.success(result.message || '测试通过')
      } else {
        message.warning(result.message || `测试未通过：${(result.errors || []).join('；')}`)
      }
    } else {
      message.error(result?.message || '测试失败')
    }
  } catch (e: any) {
    message.error(e?.message ?? '测试请求失败')
  } finally {
    testLoading.value = false
  }
}
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: $text-primary;
  margin: 4px 0 12px;
  padding-bottom: 6px;
  border-bottom: 1px solid $border-color-lighter;
}

.hint {
  font-size: 12px;
  color: $text-secondary;
  line-height: 1.6;
  margin-top: 4px;
}
</style>
