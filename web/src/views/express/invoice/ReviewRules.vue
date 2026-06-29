<template>
  <div class="page-container">
    <PageHeader title="审核规则">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增规则
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="false"
        row-key="id"
        bordered
        :scroll="{ x: 1000 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'ruleType'">
            {{ getRuleTypeText(record.ruleType) }}
          </template>
          <template v-if="column.dataIndex === 'enabled'">
            <a-tag :color="record.enabled ? 'success' : 'default'">
              {{ record.enabled ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as ReviewRuleDto)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm title="确定删除该规则吗？" ok-text="确定" cancel-text="取消" @confirm="handleDelete(record as ReviewRuleDto)">
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增审核规则' : '编辑审核规则'"
      width="600px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="规则名称" name="ruleName">
          <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" :maxlength="50" />
        </a-form-item>
        <a-form-item label="规则类型" name="ruleType">
          <a-select v-model:value="formData.ruleType" placeholder="请选择规则类型" :options="ruleTypeOptions" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="最小值" name="minValue">
              <a-input-number v-model:value="formData.minValue" style="width: 100%" placeholder="最小值" :precision="2" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="最大值" name="maxValue">
              <a-input-number v-model:value="formData.maxValue" style="width: 100%" placeholder="最大值" :precision="2" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="阈值" name="threshold">
          <a-input-number v-model:value="formData.threshold" style="width: 100%" placeholder="阈值百分比" :precision="2" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="客户ID">
              <a-input-number v-model:value="formData.clientId" style="width: 100%" placeholder="可选" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="品牌编码">
              <a-input v-model:value="formData.brandCode" style="width: 100%" placeholder="可选" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="优先级" name="priority">
              <a-input-number v-model:value="formData.priority" :min="0" style="width: 100%" placeholder="优先级" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="状态" name="enabled">
              <a-switch v-model:checked="formData.enabled" checked-children="启用" un-checked-children="停用" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getReviewRules,
  createReviewRule,
  updateReviewRule,
  deleteReviewRule,
  type ReviewRuleDto,
  type CreateReviewRuleRequest,
} from '@/api/express'

const loading = ref(false)
const tableData = ref<ReviewRuleDto[]>([])

const tableColumns = [
  { title: '规则名称', dataIndex: 'ruleName', width: 160 },
  { title: '规则类型', dataIndex: 'ruleType', width: 140 },
  { title: '最小值', dataIndex: 'minValue', width: 100, align: 'right' as const },
  { title: '最大值', dataIndex: 'maxValue', width: 100, align: 'right' as const },
  { title: '阈值', dataIndex: 'threshold', width: 100, align: 'right' as const },
  { title: '优先级', dataIndex: 'priority', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'enabled', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

const ruleTypeOptions = [
  { label: '单票均价范围', value: 1 },
  { label: '总额偏差比', value: 2 },
  { label: '单量偏差比', value: 3 },
  { label: '异常运单比例', value: 4 },
  { label: '均重范围', value: 5 },
]

function getRuleTypeText(t: number) {
  return ruleTypeOptions.find(o => o.value === t)?.label ?? '未知'
}

async function fetchList() {
  loading.value = true
  try {
    tableData.value = await getReviewRules()
  } catch {
    message.error('获取审核规则失败')
  } finally {
    loading.value = false
  }
}

// 弹窗
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  ruleName: '',
  ruleType: undefined as number | undefined,
  minValue: undefined as number | undefined,
  maxValue: undefined as number | undefined,
  threshold: undefined as number | undefined,
  clientId: undefined as number | undefined,
  brandCode: undefined as string | undefined,
  priority: 0,
  enabled: true,
})

const formRules: Record<string, Rule[]> = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  ruleType: [{ required: true, message: '请选择规则类型', trigger: 'change' }],
  priority: [{ required: true, message: '请输入优先级', trigger: 'blur' }],
}

function resetForm() {
  formData.ruleName = ''
  formData.ruleType = undefined
  formData.minValue = undefined
  formData.maxValue = undefined
  formData.threshold = undefined
  formData.clientId = undefined
  formData.brandCode = undefined
  formData.priority = 0
  formData.enabled = true
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

function handleEdit(row: ReviewRuleDto) {
  dialogType.value = 'edit'
  currentId.value = row.id
  formData.ruleName = row.ruleName
  formData.ruleType = row.ruleType
  formData.minValue = row.minValue
  formData.maxValue = row.maxValue
  formData.threshold = row.threshold
  formData.clientId = row.clientId != null ? Number(row.clientId) : undefined
  formData.brandCode = row.brandCode
  formData.priority = row.priority
  formData.enabled = row.enabled
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  submitLoading.value = true
  try {
    const data: CreateReviewRuleRequest = {
      ruleName: formData.ruleName,
      ruleType: formData.ruleType!,
      minValue: formData.minValue,
      maxValue: formData.maxValue,
      threshold: formData.threshold,
      clientId: formData.clientId != null ? String(formData.clientId) : undefined,
      brandCode: formData.brandCode,
      priority: formData.priority,
      enabled: formData.enabled,
    }
    if (dialogType.value === 'add') {
      await createReviewRule(data)
      message.success('新增成功')
    } else {
      await updateReviewRule(currentId.value!, data)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } catch { /* handled */ } finally {
    submitLoading.value = false
  }
}

async function handleDelete(row: ReviewRuleDto) {
  try {
    await deleteReviewRule(row.id)
    message.success('删除成功')
    fetchList()
  } catch { /* handled */ }
}

onMounted(() => fetchList())
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
