<template>
  <div class="page-container">
    <PageHeader title="返利政策">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增方案
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="searchForm.brandCode" size="small" placeholder="品牌编码" allow-clear style="width: 120px" />
          <a-select v-model:value="searchForm.rebateMode" size="small" placeholder="返利模式" allow-clear style="width: 120px"
            :options="rebateModeOptions" />
          <a-select v-model:value="searchForm.status" size="small" placeholder="状态" allow-clear style="width: 120px"
            :options="statusOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ (pagination.pageIndex - 1) * pagination.pageSize + index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'rebateMode'">
            <a-tag :color="record.rebateMode === 1 ? 'blue' : 'purple'">
              {{ record.rebateMode === 1 ? '通票' : '阶梯' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'settlementCycle'">
            {{ settlementCycleText(record.settlementCycle) }}
          </template>
          <template v-if="column.dataIndex === 'flatRebateAmount'">
            {{ record.rebateMode === 1 ? `¥${(record.flatRebateAmount ?? 0).toFixed(2)}` : '-' }}
          </template>
          <template v-if="column.dataIndex === 'effectiveDate'">
            {{ record.effectiveDate?.slice(0, 10) }}
          </template>
          <template v-if="column.dataIndex === 'expiryDate'">
            {{ record.expiryDate?.slice(0, 10) ?? '长期' }}
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'green' : record.status === 0 ? 'default' : 'red'">
              {{ statusText(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as PolicyRebateListItemDto)">编辑</a-button>
            <a-popconfirm
              :title="record.status === 1 ? '确定停用此方案？' : '确定启用此方案？'"
              @confirm="handleToggleStatus(record as PolicyRebateListItemDto)"
            >
              <a-button type="link" size="small">{{ record.status === 1 ? '停用' : '启用' }}</a-button>
            </a-popconfirm>
            <a-popconfirm title="确定删除此方案？" @confirm="handleDelete(record as PolicyRebateListItemDto)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="isEdit ? '编辑返利方案' : '新增返利方案'"
      width="800px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form ref="formRef" :model="form" :rules="formRules" :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px; max-height: 60vh; overflow-y: auto">
        <a-divider orientation="left">基本信息</a-divider>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="方案名称" name="policyName">
              <a-input v-model:value="form.policyName" placeholder="请输入方案名称" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="品牌编码" name="brandCode">
              <a-input v-model:value="form.brandCode" style="width: 100%" placeholder="请输入品牌编码" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="返利模式" name="rebateMode">
              <a-radio-group v-model:value="form.rebateMode">
                <a-radio :value="1">通票</a-radio>
                <a-radio :value="2">阶梯</a-radio>
              </a-radio-group>
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="结算周期" name="settlementCycle">
              <a-select v-model:value="form.settlementCycle" placeholder="请选择" :options="settlementCycleOptions" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="生效日期" name="effectiveDate">
              <a-date-picker v-model:value="form.effectiveDate" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="到期日期">
              <a-date-picker v-model:value="form.expiryDate" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="备注">
          <a-textarea v-model:value="form.remark" :rows="2" placeholder="请输入备注" :maxlength="200" show-count />
        </a-form-item>

        <!-- 通票模式 -->
        <template v-if="form.rebateMode === 1">
          <a-divider orientation="left">通票返利</a-divider>
          <a-form-item label="每票返利" name="flatRebateAmount">
            <a-input-number v-model:value="form.flatRebateAmount" :min="0" :precision="2" style="width: 200px" placeholder="请输入金额" prefix="¥" />
          </a-form-item>
        </template>

        <!-- 阶梯模式 -->
        <template v-if="form.rebateMode === 2">
          <a-divider orientation="left">阶梯档位</a-divider>
          <a-table :columns="tierColumns" :data-source="form.tiers" :pagination="false" size="small" bordered row-key="sortOrder">
            <template #bodyCell="{ column, record: tier, index: tIdx }">
              <template v-if="column.dataIndex === 'dailyVolumeFrom'">
                <a-input-number v-model:value="tier.dailyVolumeFrom" :min="0" style="width: 100%" />
              </template>
              <template v-if="column.dataIndex === 'dailyVolumeTo'">
                <a-input-number v-model:value="tier.dailyVolumeTo" :min="0" style="width: 100%" />
              </template>
              <template v-if="column.dataIndex === 'rebatePerTicket'">
                <a-input-number v-model:value="tier.rebatePerTicket" :min="0" :precision="2" style="width: 100%" />
              </template>
              <template v-if="column.dataIndex === 'tierAction'">
                <a-button type="link" size="small" danger @click="form.tiers.splice(tIdx, 1)">删除</a-button>
              </template>
            </template>
          </a-table>
          <a-button type="dashed" block style="margin-top: 8px" @click="addTier">
            <template #icon><PlusOutlined /></template>添加档位
          </a-button>
        </template>

        <!-- 奖罚规则 -->
        <a-divider orientation="left">奖罚规则</a-divider>
        <div v-for="(rule, rIdx) in form.rules" :key="rIdx" style="margin-bottom: 16px; border: 1px solid #f0f0f0; padding: 12px; border-radius: 4px;">
          <a-row :gutter="12" align="middle">
            <a-col :span="6">
              <a-form-item label="规则名称" :label-col="{ style: { width: '70px' } }" style="margin-bottom: 8px">
                <a-input v-model:value="rule.ruleName" placeholder="规则名称" />
              </a-form-item>
            </a-col>
            <a-col :span="6">
              <a-form-item label="规则类型" :label-col="{ style: { width: '70px' } }" style="margin-bottom: 8px">
                <a-select v-model:value="rule.ruleType" placeholder="请选择" :options="ruleTypeOptions" />
              </a-form-item>
            </a-col>
            <a-col :span="4">
              <a-form-item label="启用" :label-col="{ style: { width: '40px' } }" style="margin-bottom: 8px">
                <a-switch v-model:checked="rule.enabled" />
              </a-form-item>
            </a-col>
            <a-col :span="4">
              <a-form-item label="排序" :label-col="{ style: { width: '40px' } }" style="margin-bottom: 8px">
                <a-input-number v-model:value="rule.sortOrder" :min="0" style="width: 80px" />
              </a-form-item>
            </a-col>
            <a-col :span="4" style="text-align: right">
              <a-button type="link" danger size="small" @click="form.rules.splice(rIdx, 1)">删除规则</a-button>
            </a-col>
          </a-row>
          <!-- 规则条件 -->
          <a-table :columns="ruleItemColumns" :data-source="rule.items" :pagination="false" size="small" bordered row-key="sortOrder" style="margin-top: 4px">
            <template #bodyCell="{ column, record: item, index: iIdx }">
              <template v-if="column.dataIndex === 'thresholdLower'">
                <a-input-number v-model:value="item.thresholdLower" style="width: 100%" size="small" />
              </template>
              <template v-if="column.dataIndex === 'thresholdUpper'">
                <a-input-number v-model:value="item.thresholdUpper" style="width: 100%" size="small" />
              </template>
              <template v-if="column.dataIndex === 'adjustType'">
                <a-select v-model:value="item.adjustType" size="small" style="width: 100%"
                  :options="[{ label: '奖励', value: 1 }, { label: '处罚', value: 2 }]" />
              </template>
              <template v-if="column.dataIndex === 'adjustCalcMethod'">
                <a-select v-model:value="item.adjustCalcMethod" size="small" style="width: 100%"
                  :options="[{ label: '固定金额', value: 1 }, { label: '按比例', value: 2 }]" />
              </template>
              <template v-if="column.dataIndex === 'adjustAmount'">
                <a-input-number v-model:value="item.adjustAmount" :precision="2" style="width: 100%" size="small" />
              </template>
              <template v-if="column.dataIndex === 'itemAction'">
                <a-button type="link" size="small" danger @click="rule.items.splice(iIdx, 1)">删除</a-button>
              </template>
            </template>
          </a-table>
          <a-button type="dashed" size="small" block style="margin-top: 4px" @click="addRuleItem(rule)">
            <template #icon><PlusOutlined /></template>添加条件
          </a-button>
        </div>
        <a-button type="dashed" block @click="addRule">
          <template #icon><PlusOutlined /></template>添加规则
        </a-button>
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
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getPolicyRebateList,
  getPolicyRebateDetail,
  createPolicyRebate,
  updatePolicyRebate,
  deletePolicyRebate,
  enablePolicyRebate,
  disablePolicyRebate,
  type PolicyRebateListItemDto,
} from '@/api/express'

// 搜索
const searchForm = reactive({
  brandCode: undefined as string | undefined,
  rebateMode: undefined as number | undefined,
  status: undefined as number | undefined,
})
const rebateModeOptions = [
  { label: '通票', value: 1 },
  { label: '阶梯', value: 2 },
]
const statusOptions = [
  { label: '草稿', value: 0 },
  { label: '启用', value: 1 },
  { label: '停用', value: 2 },
]
const settlementCycleOptions = [
  { label: '日结', value: 1 },
  { label: '周结', value: 2 },
  { label: '月结', value: 3 },
]
const ruleTypeOptions = [
  { label: '均重', value: 1 },
  { label: '单量', value: 2 },
  { label: '重量段占比', value: 3 },
  { label: '目的地流向', value: 4 },
  { label: '计泡', value: 5 },
]

function statusText(s: number) { return ['草稿', '启用', '停用'][s] ?? '未知' }
function settlementCycleText(s: number) { return ['', '日结', '周结', '月结'][s] ?? '未知' }

// 表格
const loading = ref(false)
const tableData = ref<PolicyRebateListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const tableColumns = [
  { title: '序号', dataIndex: 'index', width: 60, align: 'center' as const },
  { title: '方案名称', dataIndex: 'policyName', width: 160, ellipsis: true },
  { title: '品牌编码', dataIndex: 'brandCode', width: 100, align: 'center' as const },
  { title: '返利模式', dataIndex: 'rebateMode', width: 90, align: 'center' as const },
  { title: '结算周期', dataIndex: 'settlementCycle', width: 90, align: 'center' as const },
  { title: '通票金额', dataIndex: 'flatRebateAmount', width: 100, align: 'right' as const },
  { title: '生效日期', dataIndex: 'effectiveDate', width: 110 },
  { title: '到期日期', dataIndex: 'expiryDate', width: 110 },
  { title: '状态', dataIndex: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

const tierColumns = [
  { title: '日单量起始', dataIndex: 'dailyVolumeFrom', width: 120 },
  { title: '日单量结束', dataIndex: 'dailyVolumeTo', width: 120 },
  { title: '每票返利(¥)', dataIndex: 'rebatePerTicket', width: 120 },
  { title: '操作', dataIndex: 'tierAction', width: 80, align: 'center' as const },
]

const ruleItemColumns = [
  { title: '下限', dataIndex: 'thresholdLower', width: 100 },
  { title: '上限', dataIndex: 'thresholdUpper', width: 100 },
  { title: '调整类型', dataIndex: 'adjustType', width: 100 },
  { title: '计算方式', dataIndex: 'adjustCalcMethod', width: 110 },
  { title: '调整金额', dataIndex: 'adjustAmount', width: 100 },
  { title: '操作', dataIndex: 'itemAction', width: 70, align: 'center' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const res = await getPolicyRebateList({
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      ...searchForm,
    })
    tableData.value = res.items
    pagination.total = res.total
  } catch {
    message.error('获取返利政策列表失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function handleSearch() {
  pagination.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.brandCode = undefined
  searchForm.rebateMode = undefined
  searchForm.status = undefined
  handleSearch()
}

// 启停
async function handleToggleStatus(row: PolicyRebateListItemDto) {
  try {
    if (row.status === 1) {
      await disablePolicyRebate(row.id)
      message.success('已停用')
    } else {
      await enablePolicyRebate(row.id)
      message.success('已启用')
    }
    fetchList()
  } catch { /* handled */ }
}

// 删除
async function handleDelete(row: PolicyRebateListItemDto) {
  try {
    await deletePolicyRebate(row.id)
    message.success('删除成功')
    fetchList()
  } catch { /* handled */ }
}

// 新增/编辑弹窗
interface TierForm { dailyVolumeFrom: number; dailyVolumeTo?: number; rebatePerTicket: number; sortOrder: number }
interface RuleItemForm { thresholdLower?: number; thresholdUpper?: number; weightFrom?: number; weightTo?: number; provinceId?: number; adjustType?: number; adjustCalcMethod?: number; adjustAmount?: number; adjustRate?: number; sortOrder: number }
interface RuleForm { ruleType: number; ruleName: string; enabled: boolean; sortOrder: number; items: RuleItemForm[] }

const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref(0)
const formRef = ref<FormInstance>()
const submitLoading = ref(false)

const form = reactive({
  policyName: '',
  brandCode: undefined as string | undefined,
  rebateMode: 1,
  flatRebateAmount: undefined as number | undefined,
  settlementCycle: undefined as number | undefined,
  effectiveDate: undefined as Dayjs | undefined,
  expiryDate: undefined as Dayjs | undefined,
  remark: '',
  tiers: [] as TierForm[],
  rules: [] as RuleForm[],
})

const formRules: Record<string, Rule[]> = {
  policyName: [{ required: true, message: '请输入方案名称', trigger: 'blur' }],
  brandCode: [{ required: true, message: '请输入品牌编码', trigger: 'blur' }],
  rebateMode: [{ required: true, message: '请选择返利模式', trigger: 'change' }],
  settlementCycle: [{ required: true, message: '请选择结算周期', trigger: 'change' }],
  effectiveDate: [{ required: true, message: '请选择生效日期', trigger: 'change' }],
}

function resetForm() {
  form.policyName = ''
  form.brandCode = undefined
  form.rebateMode = 1
  form.flatRebateAmount = undefined
  form.settlementCycle = undefined
  form.effectiveDate = undefined
  form.expiryDate = undefined
  form.remark = ''
  form.tiers = []
  form.rules = []
}

function handleAdd() {
  isEdit.value = false
  editId.value = 0
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: PolicyRebateListItemDto) {
  isEdit.value = true
  editId.value = row.id
  resetForm()
  try {
    const detail = await getPolicyRebateDetail(row.id)
    form.policyName = detail.policyName
    form.brandCode = detail.brandCode
    form.rebateMode = detail.rebateMode
    form.flatRebateAmount = detail.flatRebateAmount
    form.settlementCycle = detail.settlementCycle
    form.effectiveDate = detail.effectiveDate ? dayjs(detail.effectiveDate) : undefined
    form.expiryDate = detail.expiryDate ? dayjs(detail.expiryDate) : undefined
    form.remark = detail.remark ?? ''
    form.tiers = detail.tiers.map(t => ({ dailyVolumeFrom: t.dailyVolumeFrom, dailyVolumeTo: t.dailyVolumeTo, rebatePerTicket: t.rebatePerTicket, sortOrder: t.sortOrder }))
    form.rules = detail.rules.map(r => ({
      ruleType: r.ruleType, ruleName: r.ruleName, enabled: r.enabled, sortOrder: r.sortOrder,
      items: r.items.map(i => ({ thresholdLower: i.thresholdLower, thresholdUpper: i.thresholdUpper, weightFrom: i.weightFrom, weightTo: i.weightTo, provinceId: i.provinceId, adjustType: i.adjustType, adjustCalcMethod: i.adjustCalcMethod, adjustAmount: i.adjustAmount, adjustRate: i.adjustRate, sortOrder: i.sortOrder })),
    }))
    dialogVisible.value = true
  } catch {
    message.error('获取方案详情失败')
  }
}

function addTier() {
  form.tiers.push({ dailyVolumeFrom: 0, dailyVolumeTo: undefined, rebatePerTicket: 0, sortOrder: form.tiers.length + 1 })
}

function addRule() {
  form.rules.push({ ruleType: 1, ruleName: '', enabled: true, sortOrder: form.rules.length + 1, items: [] })
}

function addRuleItem(rule: RuleForm) {
  rule.items.push({ adjustType: 1, adjustCalcMethod: 1, sortOrder: rule.items.length + 1 })
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  const payload = {
    policyName: form.policyName,
    brandCode: form.brandCode!,
    rebateMode: form.rebateMode,
    flatRebateAmount: form.rebateMode === 1 ? form.flatRebateAmount : undefined,
    settlementCycle: form.settlementCycle!,
    effectiveDate: form.effectiveDate!.format('YYYY-MM-DD'),
    expiryDate: form.expiryDate?.format('YYYY-MM-DD'),
    remark: form.remark || undefined,
    tiers: form.rebateMode === 2 ? form.tiers : [],
    rules: form.rules.map(r => ({
      ...r,
      items: r.items.map(i => ({ ...i })),
    })),
  }

  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updatePolicyRebate(editId.value, payload)
      message.success('更新成功')
    } else {
      await createPolicyRebate(payload)
      message.success('创建成功')
    }
    dialogVisible.value = false
    fetchList()
  } catch { /* handled */ } finally {
    submitLoading.value = false
  }
}

onMounted(() => fetchList())
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
