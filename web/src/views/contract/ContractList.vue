<template>
  <div class="page-container page-container--flush">
    <PageHeader title="合同管理" description="管理合同信息与审批流程">
      <template #left>
        <a-input v-model:value="searchForm.keyword" size="middle" placeholder="合同号/标题" style="width: 200px" allow-clear @keyup.enter="handleSearch" />
        <a-select v-model:value="searchForm.typeId" size="middle" placeholder="类型" style="width: 140px" allow-clear :options="typeOptions" @change="handleSearch" />
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
      </template>
      <template #right>
        <a-button v-if="has(ContractPermissions.ContractCreate)" type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建合同
        </a-button>
      </template>
    </PageHeader>

    <StatFilterTabs v-model:active="searchForm.status" :tabs="statusTabs" @change="handleSearch" />

    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1400 }"
      row-key="id"
      empty-text="暂无合同数据"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'title'">
          <a-tooltip :title="record.title">
            <a class="contract-title-link" @click="handleViewDetail(record as ContractListItemDto)">{{ record.title }}</a>
          </a-tooltip>
        </template>
        <template v-if="column.dataIndex === 'typeName'">
          <a-tag>{{ record.typeName || '-' }}</a-tag>
        </template>
        <template v-if="column.dataIndex === 'amount'">
          {{ record.amount != null ? formatAmount(record.amount) : '-' }}
        </template>
        <template v-if="column.dataIndex === 'contractNature'">
          {{ natureText(record.contractNature) }}
        </template>
        <template v-if="column.dataIndex === 'status'">
          <StatusTag :type="statusTagType(record.status)">{{ statusText(record.status) }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" size="small" @click="handleViewDetail(record as ContractListItemDto)">查看</a-button>
          <a-button
            v-if="has(ContractPermissions.ContractEdit) && record.status === 0"
            type="link" size="small" @click="handleEdit(record)"
          >编辑</a-button>
          <a-button
            v-if="has(ContractPermissions.ContractApprove) && record.status === 0"
            type="link" size="small" @click="handleSubmitApproval(record)"
          >发起审批</a-button>
          <a-popconfirm
            v-if="has(ContractPermissions.ContractDelete) && record.status === 0"
            title="确定删除该合同吗？" ok-text="确定" cancel-text="取消" @confirm="handleDelete(record as ContractListItemDto)"
          >
            <a-button type="link" size="small" danger>删除</a-button>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>

    <!-- 新建/编辑合同弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新建合同' : '编辑合同'"
      width="900px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px; max-height: 65vh; overflow-y: auto"
      >
        <!-- 基础信息 -->
        <div class="section-title">基础信息</div>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="合同号" name="contractNo">
              <a-input v-model:value="formData.contractNo" placeholder="留空自动生成或手动输入" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="标题" name="title">
              <a-input v-model:value="formData.title" placeholder="请输入合同标题" :maxlength="200" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="合同类型" name="typeId">
              <a-select
                v-model:value="formData.typeId"
                placeholder="请选择合同类型"
                :options="typeOptions"
                @change="handleTypeChange"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="模板">
              <a-select
                v-model:value="formData.templateId"
                placeholder="请选择模板"
                allow-clear
                :options="templateOptions"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="金额">
              <a-input-number
                v-model:value="formData.amount"
                placeholder="请输入金额"
                :min="0"
                :precision="2"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="合同性质">
              <a-radio-group v-model:value="formData.contractNature">
                <a-radio :value="0">新签</a-radio>
                <a-radio :value="1">续签</a-radio>
                <a-radio :value="2">变更</a-radio>
                <a-radio :value="3">补充</a-radio>
              </a-radio-group>
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20" v-if="formData.contractNature === 1 || formData.contractNature === 2">
          <a-col :span="12">
            <a-form-item label="关联合同">
              <a-select
                v-model:value="formData.relatedContractId"
                placeholder="请选择关联合同"
                allow-clear
                show-search
                :filter-option="filterContractOption"
                :options="relatedContractOptions"
              />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="开始日期">
              <a-date-picker v-model:value="formData.startDate" style="width: 100%" placeholder="请选择开始日期" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="结束日期">
              <a-date-picker v-model:value="formData.endDate" style="width: 100%" placeholder="请选择结束日期" />
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 合同方管理 -->
        <div class="section-title">
          合同方管理
          <a-button type="link" @click="handleAddParty"><PlusOutlined />添加合同方</a-button>
        </div>
        <a-table
          v-if="formData.parties.length > 0"
          :columns="partyColumns"
          :data-source="formData.parties"
          :pagination="false"
          :bordered="false"
          size="small"
          style="width: 100%; margin-bottom: 16px"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.dataIndex === 'partyRole'">
              <a-select v-model:value="record.partyRole" size="small" style="width: 100%">
                <a-select-option :value="0">甲方</a-select-option>
                <a-select-option :value="1">乙方</a-select-option>
                <a-select-option :value="2">丙方</a-select-option>
              </a-select>
            </template>
            <template v-if="column.dataIndex === 'relatedBusinessType'">
              <a-select v-model:value="record.relatedBusinessType" size="small" style="width: 100%" allow-clear placeholder="请选择">
                <a-select-option value="customer">客户</a-select-option>
                <a-select-option value="employee">员工</a-select-option>
                <a-select-option value="supplier">供应商</a-select-option>
              </a-select>
            </template>
            <template v-if="column.dataIndex === 'partyName'">
              <a-input v-model:value="record.partyName" size="small" placeholder="名称" />
            </template>
            <template v-if="column.dataIndex === 'contact'">
              <a-input v-model:value="record.contact" size="small" placeholder="联系人" />
            </template>
            <template v-if="column.dataIndex === 'phone'">
              <a-input v-model:value="record.phone" size="small" placeholder="电话" />
            </template>
            <template v-if="column.dataIndex === 'partyAction'">
              <a-button type="link" size="small" danger @click="formData.parties.splice(index, 1)">
                <DeleteOutlined />
              </a-button>
            </template>
          </template>
        </a-table>

        <!-- 合同条款 -->
        <div class="section-title">
          合同条款
          <a-button type="link" @click="handleAddClause"><PlusOutlined />添加条款</a-button>
        </div>
        <div v-for="(clause, idx) in formData.clauses" :key="idx" class="clause-item">
          <a-row :gutter="12" align="middle">
            <a-col :span="2">
              <a-input-number v-model:value="clause.clauseOrder" size="small" :min="1" placeholder="序号" style="width: 100%" />
            </a-col>
            <a-col :span="6">
              <a-input v-model:value="clause.clauseTitle" size="small" placeholder="条款标题" />
            </a-col>
            <a-col :span="12">
              <a-textarea v-model:value="clause.clauseContent" size="small" :rows="1" placeholder="条款内容" />
            </a-col>
            <a-col :span="2">
              <a-checkbox v-model:checked="clause.isKeyClause">关键</a-checkbox>
            </a-col>
            <a-col :span="2">
              <a-button type="link" size="small" danger @click="formData.clauses.splice(idx, 1)">
                <DeleteOutlined />
              </a-button>
            </a-col>
          </a-row>
        </div>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 合同详情抽屉 -->
    <a-drawer
      v-model:open="detailVisible"
      title="合同详情"
      width="900px"
      :destroy-on-close="true"
    >
      <template v-if="detailData">
        <a-descriptions bordered :column="2" size="small" style="margin-bottom: 16px">
          <a-descriptions-item label="合同号">{{ detailData.contractNo }}</a-descriptions-item>
          <a-descriptions-item label="标题">{{ detailData.title }}</a-descriptions-item>
          <a-descriptions-item label="合同类型">{{ detailData.typeName || '-' }}</a-descriptions-item>
          <a-descriptions-item label="金额">{{ detailData.amount != null ? formatAmount(detailData.amount) : '-' }}</a-descriptions-item>
          <a-descriptions-item label="开始日期">{{ detailData.startDate || '-' }}</a-descriptions-item>
          <a-descriptions-item label="结束日期">{{ detailData.endDate || '-' }}</a-descriptions-item>
          <a-descriptions-item label="合同性质">{{ natureText(detailData.contractNature) }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <StatusTag :type="statusTagType(detailData.status)">{{ statusText(detailData.status) }}</StatusTag>
          </a-descriptions-item>
          <a-descriptions-item label="关联合同" v-if="detailData.relatedContractNo">
            {{ detailData.relatedContractNo }}
          </a-descriptions-item>
          <a-descriptions-item label="创建人">{{ detailData.creatorName || '-' }}</a-descriptions-item>
        </a-descriptions>

        <ContractStatusFlow
          :status="detailData.status"
          :createdAt="detailData.createdTime"
          :effectiveAt="detailData.startDate"
          :expiredAt="detailData.endDate"
        />

        <a-tabs>
          <a-tab-pane key="parties" tab="合同方列表">
            <a-table
              :columns="detailPartyColumns"
              :data-source="detailData.parties || []"
              :pagination="false"
              :bordered="false"
              size="small"
              row-key="id"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'partyRole'">
                  {{ ['甲方', '乙方', '丙方'][record.partyRole] || '-' }}
                </template>
                <template v-if="column.dataIndex === 'partyName'">
                  <a
                    v-if="record.relatedBusinessType === 'customer' && record.partyName"
                    class="cross-module-link"
                    @click="router.push({ path: '/crm/customers', query: { keyword: record.partyName } })"
                  >{{ record.partyName }}</a>
                  <span v-else>{{ record.partyName || '-' }}</span>
                </template>
              </template>
              <template #emptyText><EmptyState description="暂无合同方" /></template>
            </a-table>
          </a-tab-pane>
          <a-tab-pane key="clauses" tab="条款列表">
            <a-table
              :columns="detailClauseColumns"
              :data-source="detailData.clauses || []"
              :pagination="false"
              :bordered="false"
              size="small"
              row-key="id"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'isKeyClause'">
                  <StatusTag v-if="record.isKeyClause" type="danger">关键条款</StatusTag>
                  <span v-else>-</span>
                </template>
              </template>
              <template #emptyText><EmptyState description="暂无条款" /></template>
            </a-table>
          </a-tab-pane>
          <a-tab-pane key="esign" tab="签署记录">
            <a-table
              :columns="detailESignColumns"
              :data-source="detailData.eSignRecords || []"
              :pagination="false"
              :bordered="false"
              size="small"
              row-key="id"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'signStatus'">
                  <StatusTag :type="(['info', 'success', 'danger'] as const)[record.signStatus] || 'default'">
                    {{ ['待签', '已签', '已拒签'][record.signStatus] }}
                  </StatusTag>
                </template>
              </template>
              <template #emptyText><EmptyState description="暂无签署记录" /></template>
            </a-table>
          </a-tab-pane>
          <a-tab-pane key="reminders" tab="提醒列表">
            <a-table
              :columns="detailReminderColumns"
              :data-source="detailData.reminders || []"
              :pagination="false"
              :bordered="false"
              size="small"
              row-key="id"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'isHandled'">
                  <StatusTag :type="record.isHandled ? 'success' : 'warning'">
                    {{ record.isHandled ? '已处理' : '未处理' }}
                  </StatusTag>
                </template>
              </template>
              <template #emptyText><EmptyState description="暂无提醒" /></template>
            </a-table>
          </a-tab-pane>
          <a-tab-pane key="chain" tab="续签链">
            <div v-if="detailData.relatedContractNo" style="padding: 16px">
              <a-timeline>
                <a-timeline-item color="var(--color-info)">
                  原合同：{{ detailData.relatedContractNo }}
                </a-timeline-item>
                <a-timeline-item color="var(--color-success)">
                  当前合同：{{ detailData.contractNo }}
                </a-timeline-item>
              </a-timeline>
            </div>
            <EmptyState v-else description="无关联续签合同" />
          </a-tab-pane>
        </a-tabs>

        <div style="margin-top: 16px; text-align: right">
          <a-space>
            <a-button
              v-if="has(ContractPermissions.ContractEdit) && detailData.status === 0"
              type="primary"
              @click="handleEdit(detailData); detailVisible = false"
            >编辑</a-button>
            <a-button
              v-if="has(ContractPermissions.ContractApprove) && detailData.status === 0"
              @click="handleSubmitApproval(detailData)"
            >发起审批</a-button>
            <a-button
              v-if="has(ContractPermissions.ESignManage) && detailData.status === 2"
              @click="handleInitiateSign(detailData)"
            >发起签署</a-button>
          </a-space>
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import type { Dayjs } from 'dayjs'
import { PlusOutlined, DeleteOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import DataTable from '@/components/DataTable.vue'
import StatFilterTabs from '@/components/StatFilterTabs.vue'
import StatusTag from '@/components/StatusTag.vue'
import ContractStatusFlow from './components/ContractStatusFlow.vue'
import { usePermission, ContractPermissions } from '@/utils/permission'
import {
  getContractList,
  getContractById,
  createContract,
  updateContract,
  deleteContract,
  updateContractStatus,
  getAllEnabledContractTypes,
  getContractTemplateList,
  getContractStatistics,
  type ContractListItemDto,
  type ContractDto,
  type ContractStatisticsDto,
  type CreateContractPartyRequest,
  type CreateContractClauseRequest,
} from '@/api/contract'

const { has } = usePermission()
const router = useRouter()

// 枚举映射
function statusText(s: number) { return ['草稿', '审批中', '待签署', '已生效', '已到期', '已终止'][s] || '未知' }
function statusTagType(s: number): 'success' | 'warning' | 'danger' | 'info' | 'default' {
  return (['default', 'info', 'warning', 'success', 'danger', 'default'] as const)[s] || 'default'
}
function natureText(n: number) { return ['新签', '续签', '变更', '补充'][n] || '-' }
function formatAmount(v: number) { return `¥${v.toLocaleString('zh-CN', { minimumFractionDigits: 2 })}` }

// 表格列
const tableColumns = [
  { title: '合同号', dataIndex: 'contractNo', key: 'contractNo', width: 150 },
  { title: '标题', dataIndex: 'title', key: 'title', width: 200, ellipsis: true },
  { title: '类型', dataIndex: 'typeName', key: 'typeName', width: 100 },
  { title: '金额', dataIndex: 'amount', key: 'amount', width: 120, align: 'right' as const },
  { title: '开始日期', dataIndex: 'startDate', key: 'startDate', width: 110 },
  { title: '结束日期', dataIndex: 'endDate', key: 'endDate', width: 110 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '合同性质', dataIndex: 'contractNature', key: 'contractNature', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 240, align: 'center' as const, fixed: 'right' as const },
]

const partyColumns = [
  { title: '角色', dataIndex: 'partyRole', key: 'partyRole', width: 80 },
  { title: '关联类型', dataIndex: 'relatedBusinessType', key: 'relatedBusinessType', width: 110 },
  { title: '名称', dataIndex: 'partyName', key: 'partyName', width: 150 },
  { title: '联系人', dataIndex: 'contact', key: 'contact', width: 100 },
  { title: '电话', dataIndex: 'phone', key: 'phone', width: 120 },
  { title: '操作', dataIndex: 'partyAction', key: 'partyAction', width: 60, align: 'center' as const },
]

// 详情页表格列
const detailPartyColumns = [
  { title: '角色', dataIndex: 'partyRole', key: 'partyRole', width: 80 },
  { title: '名称', dataIndex: 'partyName', key: 'partyName', width: 200 },
  { title: '联系人', dataIndex: 'contact', key: 'contact', width: 120 },
  { title: '电话', dataIndex: 'phone', key: 'phone', width: 140 },
  { title: '地址', dataIndex: 'address', key: 'address', ellipsis: true },
]

const detailClauseColumns = [
  { title: '序号', dataIndex: 'clauseOrder', key: 'clauseOrder', width: 60, align: 'center' as const },
  { title: '标题', dataIndex: 'clauseTitle', key: 'clauseTitle', width: 200 },
  { title: '内容', dataIndex: 'clauseContent', key: 'clauseContent', ellipsis: true },
  { title: '关键条款', dataIndex: 'isKeyClause', key: 'isKeyClause', width: 100, align: 'center' as const },
]

const detailESignColumns = [
  { title: '签署人', dataIndex: 'signer', key: 'signer', width: 120 },
  { title: '签署角色', dataIndex: 'signerRole', key: 'signerRole', width: 100 },
  { title: '签署状态', dataIndex: 'signStatus', key: 'signStatus', width: 100, align: 'center' as const },
  { title: '签署时间', dataIndex: 'signedTime', key: 'signedTime', width: 180 },
]

const detailReminderColumns = [
  { title: '提醒日期', dataIndex: 'reminderDate', key: 'reminderDate', width: 120 },
  { title: '备注', dataIndex: 'remark', key: 'remark', ellipsis: true },
  { title: '状态', dataIndex: 'isHandled', key: 'isHandled', width: 100, align: 'center' as const },
]

// 搜索
const searchForm = reactive({
  keyword: '',
  typeId: undefined as number | undefined,
  status: '' as '' | number,
})

// 表格数据
const loading = ref(false)
const tableData = ref<ContractListItemDto[]>([])
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })

const statistics = ref<ContractStatisticsDto>({ totalCount: 0, byStatus: [] })

function getStatusCount(s: number): number {
  return statistics.value.byStatus.find(g => g.status === s)?.count ?? 0
}

const statusTabs = computed(() => [
  { key: '', label: '全部', count: statistics.value.totalCount },
  { key: 0, label: '草稿',   count: getStatusCount(0), color: 'var(--text-3)' },
  { key: 1, label: '审批中', count: getStatusCount(1), color: 'var(--color-info)' },
  { key: 2, label: '待签署', count: getStatusCount(2), color: 'var(--color-warning)' },
  { key: 3, label: '已生效', count: getStatusCount(3), color: 'var(--color-success)' },
  { key: 4, label: '已到期', count: getStatusCount(4), color: 'var(--color-danger)' },
  { key: 5, label: '已终止', count: getStatusCount(5), color: 'var(--text-3)' },
])

// 下拉选项
const typeOptions = ref<{ label: string; value: number }[]>([])
const templateOptions = ref<{ label: string; value: number }[]>([])
const relatedContractOptions = ref<{ label: string; value: number }[]>([])

// 弹窗
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

interface PartyRow extends CreateContractPartyRequest {}
interface ClauseRow extends CreateContractClauseRequest {}

const formData = reactive({
  contractNo: '',
  title: '',
  typeId: undefined as number | undefined,
  templateId: undefined as number | undefined,
  amount: undefined as number | undefined,
  contractNature: 0,
  relatedContractId: undefined as number | undefined,
  startDate: undefined as Dayjs | undefined,
  endDate: undefined as Dayjs | undefined,
  parties: [] as PartyRow[],
  clauses: [] as ClauseRow[],
})

const formRules: Record<string, Rule[]> = {
  title: [{ required: true, message: '请输入合同标题', trigger: 'blur' }],
  typeId: [{ required: true, message: '请选择合同类型', trigger: 'change' }],
}

// 详情抽屉
const detailVisible = ref(false)
const detailData = ref<ContractDto | null>(null)

// 数据获取
async function fetchTypeOptions() {
  try {
    const res = await getAllEnabledContractTypes() as any
    const list = res?.items || res || []
    typeOptions.value = list.map((t: any) => ({ label: t.name, value: t.id }))
  } catch (e) { console.error(e) }
}

async function fetchTemplateOptions(typeId?: number) {
  try {
    const res = await getContractTemplateList({ typeId, status: 1, pageSize: 999 }) as any
    const list = res?.items || res || []
    templateOptions.value = list.map((t: any) => ({ label: t.templateName, value: t.id }))
  } catch (e) { console.error(e) }
}

async function fetchRelatedContracts() {
  try {
    const res = await getContractList({ pageSize: 999, status: 3 }) as any
    const list = res?.items || res || []
    relatedContractOptions.value = list.map((c: any) => ({ label: `${c.contractNo} - ${c.title}`, value: c.id }))
  } catch (e) { console.error(e) }
}

function filterContractOption(input: string, option: any) {
  return option.label.toLowerCase().includes(input.toLowerCase())
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.typeId !== undefined) params.typeId = searchForm.typeId
    if (searchForm.status !== '' && searchForm.status !== undefined) params.status = searchForm.status
    const res = await getContractList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.value.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

async function fetchStatistics() {
  try {
    const res = await getContractStatistics() as any
    if (res) statistics.value = res
  } catch (e) { console.error('获取合同统计失败:', e) }
}

function handleSearch() { pagination.value.pageIndex = 1; fetchList() }

function handleReset() {
  searchForm.keyword = ''
  searchForm.typeId = undefined
  searchForm.status = ''
  pagination.value.pageIndex = 1
  fetchList()
}

function handleTypeChange() {
  formData.templateId = undefined
  if (formData.typeId) fetchTemplateOptions(formData.typeId)
}

function resetForm() {
  formData.contractNo = ''
  formData.title = ''
  formData.typeId = undefined
  formData.templateId = undefined
  formData.amount = undefined
  formData.contractNature = 0
  formData.relatedContractId = undefined
  formData.startDate = undefined
  formData.endDate = undefined
  formData.parties = []
  formData.clauses = []
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  fetchTemplateOptions()
  fetchRelatedContracts()
  dialogVisible.value = true
}

async function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentId.value = row.id
  resetForm()
  fetchTemplateOptions()
  fetchRelatedContracts()
  dialogVisible.value = true
  try {
    const detail = await getContractById(row.id) as any
    if (detail) {
      formData.contractNo = detail.contractNo
      formData.title = detail.title
      formData.typeId = detail.typeId
      formData.templateId = detail.templateId
      formData.amount = detail.amount
      formData.contractNature = detail.contractNature || 0
      formData.relatedContractId = detail.relatedContractId
      formData.parties = (detail.parties || []).map((p: any) => ({
        partyRole: p.partyRole,
        relatedBusinessType: p.relatedBusinessType,
        relatedBusinessId: p.relatedBusinessId,
        partyName: p.partyName,
        contact: p.contact || '',
        phone: p.phone || '',
        address: p.address || '',
      }))
      formData.clauses = (detail.clauses || []).map((c: any) => ({
        clauseOrder: c.clauseOrder,
        clauseTitle: c.clauseTitle,
        clauseContent: c.clauseContent || '',
        isKeyClause: c.isKeyClause,
      }))
    }
  } catch (e) { console.error('获取合同详情失败:', e) }
}

function handleAddParty() {
  formData.parties.push({
    partyRole: 0,
    relatedBusinessType: undefined,
    relatedBusinessId: undefined,
    partyName: '',
    contact: '',
    phone: '',
    address: '',
  })
}

function handleAddClause() {
  formData.clauses.push({
    clauseOrder: formData.clauses.length + 1,
    clauseTitle: '',
    clauseContent: '',
    isKeyClause: false,
  })
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    if (dialogType.value === 'add') {
      await createContract({
        contractNo: formData.contractNo,
        title: formData.title,
        typeId: formData.typeId!,
        templateId: formData.templateId,
        amount: formData.amount,
        startDate: formData.startDate?.format('YYYY-MM-DD'),
        endDate: formData.endDate?.format('YYYY-MM-DD'),
        relatedContractId: formData.relatedContractId,
        contractNature: formData.contractNature,
        parties: formData.parties.length > 0 ? formData.parties : undefined,
        clauses: formData.clauses.length > 0 ? formData.clauses : undefined,
      })
      message.success('新建成功')
    } else {
      await updateContract(currentId.value!, {
        title: formData.title,
        typeId: formData.typeId!,
        amount: formData.amount,
        startDate: formData.startDate?.format('YYYY-MM-DD'),
        endDate: formData.endDate?.format('YYYY-MM-DD'),
        parties: formData.parties.length > 0 ? formData.parties : undefined,
        clauses: formData.clauses.length > 0 ? formData.clauses : undefined,
      })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
    fetchStatistics()
  } finally { submitLoading.value = false }
}

async function handleDelete(row: ContractListItemDto) {
  try {
    await deleteContract(row.id)
    message.success('删除成功')
    fetchList()
    fetchStatistics()
  } catch (e) { console.error('删除失败:', e) }
}

async function handleSubmitApproval(row: any) {
  try {
    await updateContractStatus(row.id, 1)
    message.success('已发起审批')
    fetchList()
    fetchStatistics()
  } catch (e) { console.error('发起审批失败:', e) }
}

async function handleViewDetail(row: ContractListItemDto) {
  detailVisible.value = true
  detailData.value = null
  try {
    const detail = await getContractById(row.id) as any
    detailData.value = detail
  } catch (e) { console.error('获取详情失败:', e) }
}

function handleInitiateSign(row: any) {
  message.info('签署功能即将上线')
}

onMounted(() => {
  fetchTypeOptions()
  fetchList()
  fetchStatistics()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-1);
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border-faint);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.cross-module-link {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}

.contract-title-link {
  cursor: pointer;
}

.clause-item {
  margin-bottom: 8px;
  padding: 8px;
  background: var(--bg-page);
  border-radius: $border-radius-sm;
}
</style>
