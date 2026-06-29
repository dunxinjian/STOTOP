<template>
  <div class="page-container">
    <!-- 面包屑导航 -->
    <a-breadcrumb style="margin-bottom: 12px">
      <a-breadcrumb-item>CRM</a-breadcrumb-item>
      <a-breadcrumb-item><router-link to="/crm/customers">客户管理</router-link></a-breadcrumb-item>
      <a-breadcrumb-item>客户详情</a-breadcrumb-item>
    </a-breadcrumb>

    <!-- 客户基本信息 -->
    <a-card :bordered="false" :loading="detailLoading" style="margin-bottom: 8px">
      <a-descriptions :column="4" bordered size="small" title="客户基本信息">
        <a-descriptions-item label="编号">{{ customer.code || '-' }}</a-descriptions-item>
        <a-descriptions-item label="简称">{{ customer.shortName }}</a-descriptions-item>
        <a-descriptions-item label="全称">{{ customer.fullName || '-' }}</a-descriptions-item>
        <a-descriptions-item label="联系人">{{ customer.contact || '-' }}</a-descriptions-item>
        <a-descriptions-item label="电话">{{ customer.phone || '-' }}</a-descriptions-item>
        <a-descriptions-item label="行业">{{ customer.industry || '-' }}</a-descriptions-item>
        <a-descriptions-item label="规模">{{ customer.scale || '-' }}</a-descriptions-item>
        <a-descriptions-item label="状态">
          <StatusTag :type="custStatusType(customer.status ?? 0)" dot>{{ statusTextMap[customer.status ?? 0] || '未知' }}</StatusTag>
        </a-descriptions-item>
        <a-descriptions-item label="BD负责人">{{ getEmployeeName(customer.bdEmployeeId) }}</a-descriptions-item>
        <a-descriptions-item label="运维负责人">{{ getEmployeeName(customer.maintenanceEmployeeId) }}</a-descriptions-item>
      </a-descriptions>
    </a-card>

    <!-- Tab 标签页 -->
    <a-card :bordered="false">
      <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
        <!-- 联系人 -->
        <a-tab-pane key="contacts" tab="联系人">
          <div style="margin-bottom: 12px">
            <a-button type="primary" size="small" @click="handleAddContact">
              <template #icon><PlusOutlined /></template>新增联系人
            </a-button>
          </div>
          <a-table
            :columns="contactColumns"
            :data-source="contacts"
            :loading="contactLoading"
            :pagination="false"
            row-key="id"
            :bordered="false"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'isPrimary'">
                <StatusTag v-if="record.isPrimary" type="warning">主联系人</StatusTag>
                <span v-else>-</span>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="handleEditContact(record as CustomerContactDto)">编辑</a-button>
                <a-popconfirm title="确定删除？" @confirm="handleDeleteContact(record as CustomerContactDto)">
                  <a-button type="link" size="small" danger>删除</a-button>
                </a-popconfirm>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 拜访历史 -->
        <a-tab-pane key="visits" tab="拜访历史">
          <a-table
            :columns="visitColumns"
            :data-source="visits"
            :loading="visitLoading"
            :pagination="visitPaginationConfig"
            row-key="id"
            :bordered="false"
            size="small"
            @change="handleVisitTableChange"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">
                {{ (visitPagination.pageIndex - 1) * visitPagination.pageSize + index + 1 }}
              </template>
              <template v-if="column.dataIndex === 'visitMethod'">
                <StatusTag>{{ methodTextMap[record.visitMethod] || '其他' }}</StatusTag>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 服务工单 -->
        <a-tab-pane key="orders" tab="服务工单">
          <a-table
            :columns="orderColumns"
            :data-source="orders"
            :loading="orderLoading"
            :pagination="false"
            row-key="id"
            :bordered="false"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'status'">
                <StatusTag :type="orderStatusTagType(record.status)" dot>{{ orderStatusText[record.status] || '未知' }}</StatusTag>
              </template>
              <template v-if="column.dataIndex === 'priority'">
                <StatusTag :type="priorityTagType(record.priority)">{{ priorityText[record.priority] || '-' }}</StatusTag>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 合同 -->
        <a-tab-pane key="contracts" tab="合同">
          <a-table
            :columns="contractColumns"
            :data-source="contractList"
            :loading="contractLoading"
            :pagination="false"
            row-key="id"
            :bordered="false"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'status'">
                <StatusTag :type="record.status === 1 ? 'success' : 'default'">{{ record.status === 1 ? '生效' : '草稿' }}</StatusTag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="handleViewContract(record)">
                  <LinkOutlined /> 查看详情
                </a-button>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无合同" />
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 快递账单 -->
        <a-tab-pane key="invoices" tab="快递账单">
          <a-table
            :columns="invoiceColumns"
            :data-source="invoiceList"
            :loading="invoiceLoading"
            :pagination="false"
            row-key="id"
            :bordered="false"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'totalCharge'">
                ¥{{ (record.totalCharge ?? 0).toFixed(2) }}
              </template>
              <template v-if="column.dataIndex === 'status'">
                <StatusTag :type="invoiceStatusTagType(record.status)" dot>{{ invoiceStatusText[record.status] || '未知' }}</StatusTag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <a-button type="link" size="small" @click="router.push({ name: 'ExpressInvoiceDetail', params: { id: record.id } })">
                  <LinkOutlined /> 查看详情
                </a-button>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无快递账单" />
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 预付款 -->
        <a-tab-pane key="prepayments" tab="预付款">
          <a-table
            :columns="prepaymentColumns"
            :data-source="prepayments"
            :loading="prepaymentLoading"
            :pagination="false"
            row-key="id"
            :bordered="false"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'status'">
                <StatusTag :type="record.status === 1 ? 'success' : record.status === 0 ? 'info' : 'default'">{{ record.status === 0 ? '待确认' : record.status === 1 ? '已确认' : '已取消' }}</StatusTag>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无预付款记录" />
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 运单编号发放 -->
        <a-tab-pane key="allocations" tab="运单编号发放">
          <a-table
            :columns="allocationColumns"
            :data-source="allocations"
            :loading="allocationLoading"
            :pagination="false"
            row-key="id"
            :bordered="false"
            size="small"
          >
            <template #emptyText>
              <EmptyState description="暂无发放记录" />
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 毛利曲线 -->
        <a-tab-pane key="profit" tab="毛利曲线">
          <div v-if="profitData.length === 0 && !profitLoading" style="text-align: center; padding: 40px">
            <EmptyState description="暂无毛利数据" />
          </div>
          <v-chart v-else :option="profitChartOption" style="height: 400px" autoresize />
        </a-tab-pane>

        <!-- 积分记录 -->
        <a-tab-pane key="points" tab="积分记录">
          <a-table
            :columns="pointColumns"
            :data-source="pointRecords"
            :loading="pointLoading"
            size="small"
            :pagination="{ pageSize: 10 }"
            row-key="id"
            :bordered="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'points'">
                <span :style="{ color: record.points > 0 ? 'var(--color-success)' : 'var(--color-danger)' }">
                  {{ record.points > 0 ? '+' : '' }}{{ record.points }}
                </span>
              </template>
            </template>
            <template #emptyText>
              <EmptyState description="暂无积分记录" />
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 财务往来 -->
        <a-tab-pane key="finance" tab="财务往来">
          <a-row :gutter="16" style="margin-bottom: 16px">
            <a-col :span="8">
              <a-statistic title="应收总额" :value="financeStats.receivable" :precision="2" prefix="¥" />
            </a-col>
            <a-col :span="8">
              <a-statistic title="已收总额" :value="financeStats.received" :precision="2" prefix="¥" :value-style="{ color: 'var(--color-success)' }" />
            </a-col>
            <a-col :span="8">
              <a-statistic title="预付余额" :value="financeStats.prepaidBalance" :precision="2" prefix="¥" :value-style="{ color: 'var(--color-info)' }" />
            </a-col>
          </a-row>
          <a-table
            :columns="financeColumns"
            :data-source="financeRecords"
            :loading="financeLoading"
            size="small"
            :pagination="{ pageSize: 10 }"
            row-key="refNo"
            :bordered="false"
          >
            <template #emptyText>
              <EmptyState description="暂无财务记录" />
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 时间线 -->
        <a-tab-pane key="timeline" tab="时间线">
          <a-spin :spinning="timelineLoading">
            <a-timeline v-if="timelineItems.length > 0" mode="left" style="padding: 16px 0">
              <a-timeline-item
                v-for="item in timelineItems"
                :key="item.id"
                :color="timelineDotColor(item.type)"
              >
                <template #label>
                  <span style="color: var(--text-3); font-size: 12px">{{ item.occurredTime }}</span>
                </template>
                <div>
                  <StatusTag :type="timelineTagType(item.type)">{{ timelineTypeText(item.type) }}</StatusTag>
                  <strong style="margin-left: 4px">{{ item.title }}</strong>
                </div>
                <div v-if="item.content" style="color: var(--text-2); margin-top: 4px; font-size: 13px">{{ item.content }}</div>
                <div v-if="item.creatorName" style="color: var(--text-3); margin-top: 2px; font-size: 12px">操作人: {{ item.creatorName }}</div>
              </a-timeline-item>
            </a-timeline>
            <EmptyState v-else description="暂无时间线数据" />
          </a-spin>
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 联系人弹窗 -->
    <a-modal
      v-model:open="contactDialogVisible"
      :title="contactDialogType === 'add' ? '新增联系人' : '编辑联系人'"
      width="500px"
      :destroy-on-close="true"
      @cancel="contactDialogVisible = false"
    >
      <a-form
        ref="contactFormRef"
        :model="contactForm"
        :rules="contactFormRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="姓名" name="name">
          <a-input v-model:value="contactForm.name" placeholder="请输入姓名" :maxlength="50" />
        </a-form-item>
        <a-form-item label="电话">
          <a-input v-model:value="contactForm.phone" placeholder="请输入电话" :maxlength="30" />
        </a-form-item>
        <a-form-item label="职务">
          <a-input v-model:value="contactForm.position" placeholder="请输入职务" :maxlength="50" />
        </a-form-item>
        <a-form-item label="角色标签">
          <a-input v-model:value="contactForm.roleTag" placeholder="如：决策者/影响者" :maxlength="50" />
        </a-form-item>
        <a-form-item label="主联系人">
          <a-switch v-model:checked="contactForm.isPrimary" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="contactDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="contactSubmitLoading" @click="handleContactSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { useRoute, useRouter } from 'vue-router'
import { PlusOutlined, LinkOutlined } from '@ant-design/icons-vue'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import VChart from 'vue-echarts'
import EmptyState from '@/components/EmptyState.vue'
import StatusTag from '@/components/StatusTag.vue'
import {
  getCustomerById,
  getCustomerTimeline,
  getVisitRecordList,
  getServiceOrderList,
  getPrepaymentList,
  getCustomerAllocations,
  getProfitList,
  type CustomerDto,
  type CustomerContactDto,
  type CustomerTimelineItemDto,
  type VisitRecordListItemDto,
  type ServiceOrderListItemDto,
  type PrepaymentDto,
  type WaybillAllocationDto,
  type CustomerProfitDto,
  type CreateContactRequest,
} from '@/api/crm'
import { getContractList } from '@/api/contract'
import { getEmployeeList, type EmployeeDto } from '@/api/hr'
import { post, put, del, get as httpGet } from '@/api/request'

use([CanvasRenderer, LineChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const route = useRoute()
const router = useRouter()
const customerId = computed(() => Number(route.params.id))

// 状态映射
const statusTextMap: Record<number, string> = { 0: '潜在', 1: '活跃', 2: '流失' }
const methodTextMap: Record<number, string> = { 1: '上门', 2: '电话', 3: '线上', 4: '其他' }
const orderStatusText: Record<number, string> = { 0: '待接单', 1: '处理中', 2: '待确认', 3: '已完成', 4: '已关闭' }
const priorityText: Record<number, string> = { 1: '紧急', 2: '高', 3: '中', 4: '低' }

// a-tag 预设色名 → StatusTag 语义类型（有序走语义、纯分类走中性）
type STagType = 'success' | 'warning' | 'danger' | 'info' | 'default'
const custStatusType = (s: number): STagType => (['info', 'success', 'danger'] as const)[s] || 'default'
const orderStatusTagType = (s: number): STagType => (['default', 'info', 'warning', 'success', 'default'] as const)[s] || 'default'
const priorityTagType = (p: number): STagType => (({ 1: 'danger', 2: 'warning', 3: 'info', 4: 'default' } as Record<number, STagType>)[p]) || 'default'
const invoiceStatusTagType = (s: number): STagType => (['default', 'info', 'warning', 'success'] as const)[s] || 'default'
const timelineTagType = (t: string): STagType => (({ visit: 'info', order: 'warning', contract: 'success', prepayment: 'warning', feedback: 'danger' } as Record<string, STagType>)[t]) || 'default'
const timelineDotColor = (t: string): string => (({ visit: 'var(--color-info)', order: 'var(--color-warning)', contract: 'var(--color-success)', prepayment: 'var(--color-warning)', feedback: 'var(--color-danger)' } as Record<string, string>)[t]) || 'var(--text-3)'

// 员工映射
const employeeMap = ref<Record<number, string>>({})
function getEmployeeName(id?: number) {
  return id ? (employeeMap.value[id] || '-') : '-'
}

// 客户详情
const detailLoading = ref(false)
const customer = reactive<Partial<CustomerDto>>({ shortName: '', status: 0, contacts: [] })
const activeTab = ref('contacts')

// 联系人
const contacts = ref<CustomerContactDto[]>([])
const contactLoading = ref(false)
const contactColumns = [
  { title: '姓名', dataIndex: 'name', key: 'name', width: 120 },
  { title: '电话', dataIndex: 'phone', key: 'phone', width: 140 },
  { title: '职务', dataIndex: 'position', key: 'position', width: 120 },
  { title: '角色标签', dataIndex: 'roleTag', key: 'roleTag', width: 120 },
  { title: '主联系人', dataIndex: 'isPrimary', key: 'isPrimary', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const },
]

// 拜访
const visits = ref<VisitRecordListItemDto[]>([])
const visitLoading = ref(false)
const visitPagination = reactive({ pageIndex: 1, pageSize: 10, total: 0 })
const visitPaginationConfig = computed(() => ({
  current: visitPagination.pageIndex,
  pageSize: visitPagination.pageSize,
  total: visitPagination.total,
  showTotal: (t: number) => `共 ${t} 条`,
}))
const visitColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '拜访人', dataIndex: 'creatorName', key: 'creatorName', width: 100 },
  { title: '拜访日期', dataIndex: 'visitDate', key: 'visitDate', width: 120 },
  { title: '方式', dataIndex: 'visitMethod', key: 'visitMethod', width: 80, align: 'center' as const },
  { title: '内容', dataIndex: 'content', key: 'content', ellipsis: true },
  { title: '下次跟进', dataIndex: 'nextFollowUpDate', key: 'nextFollowUpDate', width: 120 },
]

// 服务工单
const orders = ref<ServiceOrderListItemDto[]>([])
const orderLoading = ref(false)
const orderColumns = [
  { title: '工单号', dataIndex: 'orderNo', key: 'orderNo', width: 140 },
  { title: '标题', dataIndex: 'title', key: 'title', ellipsis: true },
  { title: '类别', dataIndex: 'category', key: 'category', width: 80 },
  { title: '优先级', dataIndex: 'priority', key: 'priority', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 160 },
]

// 合同
const contractList = ref<any[]>([])
const contractLoading = ref(false)
const contractColumns = [
  { title: '合同编号', dataIndex: 'contractNo', key: 'contractNo', width: 160 },
  { title: '合同标题', dataIndex: 'title', key: 'title', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 120, align: 'center' as const },
]

// 预付款
const prepayments = ref<PrepaymentDto[]>([])
const prepaymentLoading = ref(false)

// 快递账单
const invoiceList = ref<any[]>([])
const invoiceLoading = ref(false)
const invoiceStatusText: Record<number, string> = { 0: '草稿', 1: '已确认', 2: '已发送', 3: '已收款' }
const invoiceColumns = [
  { title: '账单号', dataIndex: 'invoiceNo', key: 'invoiceNo', width: 150 },
  { title: '应收金额', dataIndex: 'totalCharge', key: 'totalCharge', width: 120, align: 'right' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 120, align: 'center' as const },
]
const prepaymentColumns = [
  { title: '预付金额', dataIndex: 'prepayAmount', key: 'prepayAmount', width: 120 },
  { title: '已收金额', dataIndex: 'receivedAmount', key: 'receivedAmount', width: 120 },
  { title: '预期运单数', dataIndex: 'expectedWaybillCount', key: 'expectedWaybillCount', width: 120 },
  { title: '已分配运单数', dataIndex: 'allocatedWaybillCount', key: 'allocatedWaybillCount', width: 120 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 160 },
]

// 运单编号发放
const allocations = ref<WaybillAllocationDto[]>([])
const allocationLoading = ref(false)
const allocationColumns = [
  { title: '起始号', dataIndex: 'startNo', key: 'startNo', width: 160 },
  { title: '终止号', dataIndex: 'endNo', key: 'endNo', width: 160 },
  { title: '数量', dataIndex: 'allocatedCount', key: 'allocatedCount', width: 100 },
  { title: '分配日期', dataIndex: 'allocationDate', key: 'allocationDate', width: 140 },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 160 },
]

// 积分记录
const pointRecords = ref<any[]>([])
const pointLoading = ref(false)
const pointColumns = [
  { title: '日期', dataIndex: 'createdAt', width: 120 },
  { title: '类型', dataIndex: 'type', width: 100 },
  { title: '积分', dataIndex: 'points', width: 80 },
  { title: '余额', dataIndex: 'balance', width: 80 },
  { title: '说明', dataIndex: 'remark' },
]

// 财务往来
const financeStats = ref({ receivable: 0, received: 0, prepaidBalance: 0 })
const financeRecords = ref<any[]>([])
const financeLoading = ref(false)
const financeColumns = [
  { title: '日期', dataIndex: 'date', width: 120 },
  { title: '类型', dataIndex: 'type', width: 100 },
  { title: '金额', dataIndex: 'amount', width: 120, align: 'right' as const },
  { title: '状态', dataIndex: 'status', width: 80 },
  { title: '关联单据', dataIndex: 'refNo' },
]

// 毛利
const profitData = ref<CustomerProfitDto[]>([])
const profitLoading = ref(false)
const profitChartOption = computed(() => {
  const sorted = [...profitData.value].sort((a, b) => a.period.localeCompare(b.period))
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['收入', '成本', '毛利'] },
    grid: { left: 60, right: 30, top: 40, bottom: 30 },
    xAxis: { type: 'category', data: sorted.map((d) => d.period) },
    yAxis: { type: 'value' },
    series: [
      { name: '收入', type: 'line', data: sorted.map((d) => d.revenue), smooth: true },
      { name: '成本', type: 'line', data: sorted.map((d) => d.cost), smooth: true },
      { name: '毛利', type: 'line', data: sorted.map((d) => d.profit), smooth: true, lineStyle: { width: 3 } },
    ],
  }
})

// 时间线
const timelineItems = ref<CustomerTimelineItemDto[]>([])
const timelineLoading = ref(false)

function timelineTypeText(type: string) {
  const map: Record<string, string> = { visit: '拜访', order: '工单', contract: '合同', prepayment: '预付款', feedback: '反馈' }
  return map[type] || type
}

// 联系人弹窗
const contactDialogVisible = ref(false)
const contactDialogType = ref<'add' | 'edit'>('add')
const contactFormRef = ref<FormInstance>()
const contactSubmitLoading = ref(false)
const currentContactId = ref<number | null>(null)
const contactForm = reactive({
  name: '',
  phone: '',
  position: '',
  roleTag: '',
  isPrimary: false,
})
const contactFormRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
}

// ==================== 数据加载 ====================

async function fetchDetail() {
  detailLoading.value = true
  try {
    const res = await getCustomerById(customerId.value) as any
    if (res) {
      Object.assign(customer, res)
      contacts.value = res.contacts || []
    }
  } finally {
    detailLoading.value = false
  }
}

async function fetchVisits() {
  visitLoading.value = true
  try {
    const res = await getVisitRecordList({
      customerId: customerId.value,
      pageIndex: visitPagination.pageIndex,
      pageSize: visitPagination.pageSize,
    }) as any
    if (res) {
      visits.value = res?.items || res || []
      visitPagination.total = res?.total || visits.value.length
    }
  } finally {
    visitLoading.value = false
  }
}

function handleVisitTableChange(pag: any) {
  visitPagination.pageIndex = pag.current
  visitPagination.pageSize = pag.pageSize
  fetchVisits()
}

async function fetchOrders() {
  orderLoading.value = true
  try {
    const res = await getServiceOrderList({ customerId: customerId.value, pageSize: 100 }) as any
    orders.value = res?.items || res || []
  } finally {
    orderLoading.value = false
  }
}

async function fetchContracts() {
  contractLoading.value = true
  try {
    const res = await getContractList({ keyword: customer.shortName, pageSize: 100 }) as any
    contractList.value = res?.items || res || []
  } finally {
    contractLoading.value = false
  }
}

function handleViewContract(record: any) {
  // 合同详情显示在列表页的弹窗中，跳转到合同列表并搜索
  router.push({ path: '/contract/list', query: { keyword: record.contractNo } })
}

async function fetchInvoices() {
  invoiceLoading.value = true
  try {
    const res = await httpGet('/express/invoices', { clientName: customer.shortName, pageSize: 100 }) as any
    invoiceList.value = res?.items || res || []
  } catch {
    // API可能不存在，静默处理
  } finally {
    invoiceLoading.value = false
  }
}

async function fetchPrepayments() {
  prepaymentLoading.value = true
  try {
    const res = await getPrepaymentList({ customerId: customerId.value, pageSize: 100 }) as any
    prepayments.value = res?.items || res || []
  } finally {
    prepaymentLoading.value = false
  }
}

async function fetchAllocations() {
  allocationLoading.value = true
  try {
    const res = await getCustomerAllocations(customerId.value) as any
    allocations.value = res || []
  } finally {
    allocationLoading.value = false
  }
}

async function fetchProfits() {
  profitLoading.value = true
  try {
    const res = await getProfitList({ customerId: customerId.value, pageSize: 100 }) as any
    profitData.value = res?.items || res || []
  } finally {
    profitLoading.value = false
  }
}

async function fetchPointRecords() {
  pointLoading.value = true
  try {
    const res = await httpGet(`/points/records`, { keyword: customer.shortName }) as any
    pointRecords.value = res?.items || []
  } catch {
    pointRecords.value = []
  } finally {
    pointLoading.value = false
  }
}

async function fetchFinanceData() {
  financeLoading.value = true
  try {
    const invoiceRes = await httpGet('/express/invoices', { clientName: customer.shortName, pageSize: 100 }) as any
    if (invoiceRes?.items) {
      const items = invoiceRes.items
      financeStats.value.receivable = items.reduce((sum: number, i: any) => sum + (i.totalAmount || 0), 0)
      financeStats.value.received = items.filter((i: any) => i.status >= 3).reduce((sum: number, i: any) => sum + (i.totalAmount || 0), 0)
      financeRecords.value = items.map((i: any) => ({
        date: i.createdAt?.slice(0, 10),
        type: '快递账单',
        amount: i.totalAmount,
        status: ['草稿', '已确认', '已发送', '已收款'][i.status] || '-',
        refNo: i.invoiceNo,
      }))
    }
    const prepayRes = await httpGet(`/crm/customers/${customerId.value}/prepayments`) as any
    if (prepayRes?.items) {
      financeStats.value.prepaidBalance = prepayRes.items.reduce((sum: number, p: any) => sum + (p.balance || 0), 0)
    }
  } catch {
    // 静默失败
  } finally {
    financeLoading.value = false
  }
}

async function fetchTimeline() {
  timelineLoading.value = true
  try {
    const res = await getCustomerTimeline(customerId.value, 50) as any
    timelineItems.value = res || []
  } finally {
    timelineLoading.value = false
  }
}

async function fetchEmployees() {
  try {
    const res = await getEmployeeList({ pageSize: 9999 }) as any
    const items: EmployeeDto[] = res?.items || res || []
    employeeMap.value = Object.fromEntries(items.map((e) => [e.id, e.name]))
  } catch { /* ignore */ }
}

function handleTabChange(activeKey: string | number) {
  const key = String(activeKey)
  if (key === 'visits' && visits.value.length === 0) fetchVisits()
  if (key === 'orders' && orders.value.length === 0) fetchOrders()
  if (key === 'contracts' && contractList.value.length === 0) fetchContracts()
  if (key === 'invoices' && invoiceList.value.length === 0) fetchInvoices()
  if (key === 'prepayments' && prepayments.value.length === 0) fetchPrepayments()
  if (key === 'allocations' && allocations.value.length === 0) fetchAllocations()
  if (key === 'points' && pointRecords.value.length === 0) fetchPointRecords()
  if (key === 'finance' && financeRecords.value.length === 0) fetchFinanceData()
  if (key === 'profit' && profitData.value.length === 0) fetchProfits()
  if (key === 'timeline' && timelineItems.value.length === 0) fetchTimeline()
}

// ==================== 联系人操作 ====================

function handleAddContact() {
  contactDialogType.value = 'add'
  currentContactId.value = null
  contactForm.name = ''
  contactForm.phone = ''
  contactForm.position = ''
  contactForm.roleTag = ''
  contactForm.isPrimary = false
  contactDialogVisible.value = true
}

function handleEditContact(record: CustomerContactDto) {
  contactDialogType.value = 'edit'
  currentContactId.value = record.id
  contactForm.name = record.name
  contactForm.phone = record.phone || ''
  contactForm.position = record.position || ''
  contactForm.roleTag = record.roleTag || ''
  contactForm.isPrimary = record.isPrimary
  contactDialogVisible.value = true
}

async function handleContactSubmit() {
  if (!contactFormRef.value) return
  try { await contactFormRef.value.validate() } catch { return }

  contactSubmitLoading.value = true
  try {
    const data: CreateContactRequest = {
      name: contactForm.name,
      phone: contactForm.phone || undefined,
      position: contactForm.position || undefined,
      roleTag: contactForm.roleTag || undefined,
      isPrimary: contactForm.isPrimary,
    }
    if (contactDialogType.value === 'add') {
      await post(`/crm/customers/${customerId.value}/contacts`, data)
      message.success('新增成功')
    } else {
      await put(`/crm/customers/${customerId.value}/contacts/${currentContactId.value}`, data)
      message.success('更新成功')
    }
    contactDialogVisible.value = false
    fetchDetail()
  } catch (error) {
    console.error('操作联系人失败:', error)
  } finally {
    contactSubmitLoading.value = false
  }
}

async function handleDeleteContact(record: CustomerContactDto) {
  try {
    await del(`/crm/customers/${customerId.value}/contacts/${record.id}`)
    message.success('删除成功')
    fetchDetail()
  } catch (error) {
    console.error('删除联系人失败:', error)
  }
}

onMounted(() => {
  fetchEmployees()
  fetchDetail()
})
</script>

<style scoped lang="scss">
</style>
