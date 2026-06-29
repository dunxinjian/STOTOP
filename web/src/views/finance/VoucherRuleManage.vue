<template>
  <div class="page-container">
    <PageHeader title="凭证手动规则" description="配置银行流水自动生成凭证的匹配规则">
      <template #actions>
        <a-button v-if="has(FinancePermissions.VoucherRuleManage)" type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增规则
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false" class="toolbar-section">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'channelName'">
            <a-tag v-if="record.channelName" color="blue">{{ record.channelName }}</a-tag>
            <span v-else style="color: #999">全部渠道</span>
          </template>
          <template v-if="column.dataIndex === 'matchCondition'">
            <span class="condition-brief">{{ formatConditionBrief(record.matchCondition) }}</span>
          </template>
          <template v-if="column.dataIndex === 'priority'">
            <a-input-number
              v-if="has(FinancePermissions.VoucherRuleManage)"
              :value="record.priority"
              :min="0"
              :max="999"
              size="small"
              style="width: 70px"
              @change="(val: number | string | null) => handlePriorityChange(record as VoucherRuleDto, Number(val))"
            />
            <span v-else>{{ record.priority }}</span>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button v-if="has(FinancePermissions.VoucherRuleManage)" type="link" size="small" @click="handleEdit(record as VoucherRuleDto)">
              <EditOutlined />编辑
            </a-button>
            <a-button v-if="has(FinancePermissions.VoucherRuleManage)" type="link" size="small" @click="handleToggleStatus(record as VoucherRuleDto)">
              {{ record.status === 1 ? '停用' : '启用' }}
            </a-button>
            <a-popconfirm
              v-if="has(FinancePermissions.VoucherRuleManage)"
              title="确定删除该规则吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDelete(record as VoucherRuleDto)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无凭证规则" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑规则弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增规则' : '编辑规则'"
      width="800px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <!-- 基本信息 -->
        <div class="section-title">基本信息</div>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="规则名称" name="ruleName">
              <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" :maxlength="100" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="渠道">
              <a-select
                v-model:value="formData.channelId"
                placeholder="全部渠道"
                allow-clear
                :options="channelOptions"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="优先级" name="priority">
              <a-input-number v-model:value="formData.priority" :min="0" :max="999" style="width: 100%" placeholder="数字越小优先级越高" />
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 匹配条件 -->
        <div class="section-title">
          匹配条件
          <a-button type="link" size="small" @click="addCondition">
            <PlusOutlined />添加条件
          </a-button>
        </div>
        <div v-for="(cond, idx) in formData.conditions" :key="idx" class="condition-row">
          <a-select v-model:value="cond.field" placeholder="字段" style="width: 130px" size="small">
            <a-select-option value="counterpartName">对方户名</a-select-option>
            <a-select-option value="summary">摘要</a-select-option>
            <a-select-option value="direction">收支方向</a-select-option>
            <a-select-option value="amount">金额范围</a-select-option>
          </a-select>
          <a-select v-model:value="cond.operator" placeholder="匹配方式" style="width: 100px" size="small">
            <a-select-option value="contains">包含</a-select-option>
            <a-select-option value="equals">等于</a-select-option>
            <a-select-option value="gt">大于</a-select-option>
            <a-select-option value="lt">小于</a-select-option>
          </a-select>
          <a-input v-model:value="cond.value" placeholder="值" style="flex: 1" size="small" />
          <a-button type="link" size="small" danger @click="removeCondition(idx)">
            <DeleteOutlined />
          </a-button>
        </div>
        <div v-if="formData.conditions.length === 0" class="empty-conditions">
          暂无匹配条件，请添加
        </div>

        <!-- 凭证配置 -->
        <div class="section-title" style="margin-top: 20px">凭证配置</div>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="借方科目" name="debitAccount">
              <a-input v-model:value="formData.debitAccount" placeholder="请输入借方科目" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="贷方科目" name="creditAccount">
              <a-input v-model:value="formData.creditAccount" placeholder="请输入贷方科目" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="摘要模板">
              <a-input v-model:value="formData.summaryTemplate" placeholder="支持变量如 {对方户名}、{金额}" />
              <div class="form-tip">支持变量：{对方户名}、{金额}、{交易日期}、{摘要}</div>
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
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getVoucherRuleList,
  createVoucherRule,
  updateVoucherRule,
  deleteVoucherRule,
  getVoucherRuleById,
  getAllEnabledBankChannels,
  type VoucherRuleDto,
} from '@/api/finance'
import { usePermission, FinancePermissions } from '@/utils/permission'

const { has } = usePermission()

// 表格列配置
const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', width: 160 },
  { title: '渠道', dataIndex: 'channelName', key: 'channelName', width: 130 },
  { title: '匹配条件', dataIndex: 'matchCondition', key: 'matchCondition', width: 220, ellipsis: true },
  { title: '借方科目', dataIndex: 'debitAccount', key: 'debitAccount', width: 120 },
  { title: '贷方科目', dataIndex: 'creditAccount', key: 'creditAccount', width: 120 },
  { title: '优先级', dataIndex: 'priority', key: 'priority', width: 100, align: 'center' as const, sorter: (a: any, b: any) => a.priority - b.priority, defaultSortOrder: 'ascend' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

// 表格数据
const loading = ref(false)
const tableData = ref<VoucherRuleDto[]>([])
const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  total: 0,
})

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

// 渠道选项
const channelOptions = ref<{ label: string; value: number }[]>([])

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentRuleId = ref<number | null>(null)

interface ConditionRow {
  field: string
  operator: string
  value: string
}

const formData = reactive({
  ruleName: '',
  channelId: undefined as number | undefined,
  priority: 10,
  conditions: [] as ConditionRow[],
  debitAccount: '',
  creditAccount: '',
  summaryTemplate: '',
})

const formRules: Record<string, Rule[]> = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  priority: [{ required: true, message: '请输入优先级', trigger: 'blur' }],
}

function addCondition() {
  formData.conditions.push({ field: '', operator: 'contains', value: '' })
}

function removeCondition(idx: number) {
  formData.conditions.splice(idx, 1)
}

function formatConditionBrief(conditionJson?: string): string {
  if (!conditionJson) return '-'
  try {
    const conditions = JSON.parse(conditionJson) as ConditionRow[]
    if (!Array.isArray(conditions) || conditions.length === 0) return '-'
    const fieldNames: Record<string, string> = {
      counterpartName: '对方户名',
      summary: '摘要',
      direction: '收支方向',
      amount: '金额',
    }
    const operatorNames: Record<string, string> = {
      contains: '包含',
      equals: '等于',
      gt: '大于',
      lt: '小于',
    }
    return conditions.map((c) =>
      `${fieldNames[c.field] || c.field} ${operatorNames[c.operator] || c.operator} "${c.value}"`
    ).join(' 且 ')
  } catch {
    return conditionJson.substring(0, 40)
  }
}

// 获取列表
async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    const res = await getVoucherRuleList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

async function fetchChannels() {
  try {
    const res = await getAllEnabledBankChannels() as any[]
    channelOptions.value = (res || []).map((ch: any) => ({ label: ch.name, value: ch.id }))
  } catch (error) {
    console.error('加载渠道列表失败:', error)
  }
}

function resetForm() {
  formData.ruleName = ''
  formData.channelId = undefined
  formData.priority = 10
  formData.conditions = []
  formData.debitAccount = ''
  formData.creditAccount = ''
  formData.summaryTemplate = ''
}

function handleAdd() {
  dialogType.value = 'add'
  currentRuleId.value = null
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: VoucherRuleDto) {
  dialogType.value = 'edit'
  currentRuleId.value = row.id
  resetForm()
  dialogVisible.value = true
  try {
    const detail = await getVoucherRuleById(row.id) as any
    if (detail) {
      formData.ruleName = detail.ruleName || ''
      formData.channelId = detail.channelId || undefined
      formData.priority = detail.priority ?? 10
      formData.debitAccount = detail.debitAccount || ''
      formData.creditAccount = detail.creditAccount || ''
      formData.summaryTemplate = detail.summaryTemplate || ''
      if (detail.matchCondition) {
        try {
          formData.conditions = JSON.parse(detail.matchCondition)
        } catch {
          formData.conditions = []
        }
      }
    }
  } catch (error) {
    console.error('获取规则详情失败:', error)
  }
}

async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }
  submitLoading.value = true
  try {
    const data: any = {
      ruleName: formData.ruleName,
      channelId: formData.channelId || undefined,
      priority: formData.priority,
      matchCondition: formData.conditions.length > 0 ? JSON.stringify(formData.conditions) : undefined,
      debitAccount: formData.debitAccount || undefined,
      creditAccount: formData.creditAccount || undefined,
      summaryTemplate: formData.summaryTemplate || undefined,
    }
    if (dialogType.value === 'add') {
      await createVoucherRule(data)
      message.success('新增成功')
    } else {
      await updateVoucherRule(currentRuleId.value!, { ...data, status: 1 })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

async function handleDelete(row: VoucherRuleDto) {
  try {
    await deleteVoucherRule(row.id)
    message.success('删除成功')
    fetchList()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

async function handleToggleStatus(row: VoucherRuleDto) {
  const newStatus = row.status === 1 ? 0 : 1
  const actionText = newStatus === 1 ? '启用' : '停用'
  try {
    await updateVoucherRule(row.id, {
      ruleName: row.ruleName,
      channelId: row.channelId,
      matchCondition: row.matchCondition,
      debitAccount: row.debitAccount,
      creditAccount: row.creditAccount,
      summaryTemplate: row.summaryTemplate,
      priority: row.priority,
      status: newStatus,
    })
    message.success(`${actionText}成功`)
    fetchList()
  } catch (error) {
    console.error(`${actionText}失败:`, error)
  }
}

async function handlePriorityChange(row: VoucherRuleDto, val: number) {
  try {
    await updateVoucherRule(row.id, {
      ruleName: row.ruleName,
      channelId: row.channelId,
      matchCondition: row.matchCondition,
      debitAccount: row.debitAccount,
      creditAccount: row.creditAccount,
      summaryTemplate: row.summaryTemplate,
      priority: val,
      status: row.status,
    })
    row.priority = val
    message.success('优先级已更新')
  } catch (error) {
    console.error('更新优先级失败:', error)
  }
}

onMounted(() => {
  fetchChannels()
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 1px solid $border-color-lighter;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.condition-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.empty-conditions {
  color: #999;
  font-size: 13px;
  text-align: center;
  padding: 12px;
  background: #fafafa;
  border-radius: 4px;
  margin-bottom: 12px;
}

.condition-brief {
  font-size: 13px;
  color: #666;
}

.form-tip {
  font-size: 12px;
  color: #999;
  margin-top: 4px;
}
</style>
