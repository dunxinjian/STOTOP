<template>
  <div class="dispatch-rules">
    <PageHeader title="派发规则管理" description="配置数据派发规则，定义导入数据的自动处理和分发策略" />

    <!-- 工具栏 -->
    <div class="toolbar">
      <a-input-search
        v-model:value="searchText"
        placeholder="搜索规则名称"
        style="width: 220px;"
        allowClear
        @search="fetchList"
      />
      <a-radio-group v-model:value="statusFilter" button-style="solid" size="small" @change="fetchList">
        <a-radio-button value="">全部</a-radio-button>
        <a-radio-button value="1">启用</a-radio-button>
        <a-radio-button value="0">禁用</a-radio-button>
      </a-radio-group>
      <a-select
        v-model:value="handlerTypeFilter"
        placeholder="处理器类型"
        allowClear
        style="width: 160px;"
        @change="fetchList"
      >
        <a-select-option value="AutoVoucher">自动凭证</a-select-option>
        <a-select-option value="WorkTask">工作任务</a-select-option>
        <a-select-option value="AlertNotify">告警通知</a-select-option>
        <a-select-option value="InfoRecord">信息记录</a-select-option>
      </a-select>
      <a-button v-if="has(CardFlowPermissions.DispatchRuleManage)" type="primary" style="margin-left: auto;" @click="openDrawer()"><PlusOutlined /> 新建规则</a-button>
    </div>

    <!-- 列表表格 -->
    <a-card class="table-card">
      <a-table
        :columns="tableColumns"
        :dataSource="tableData"
        :loading="loading"
        rowKey="id"
        bordered
        :pagination="pagination"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'triggerEvent'">
            <a-tag :color="record.triggerEvent === 'PipelineCompleted' ? 'blue' : 'orange'">
              {{ record.triggerEvent === 'PipelineCompleted' ? '管道完成' : '手动触发' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'targetTables'">
            <template v-if="record.targetTables && record.targetTables.length > 0">
              <a-tag v-for="t in parseArray(record.targetTables)" :key="t" style="margin-bottom: 2px;">{{ t }}</a-tag>
            </template>
            <span v-else style="color: #c0c4cc;">全部</span>
          </template>
          <template v-else-if="column.dataIndex === 'ruleType'">
            <a-tag :color="ruleTypeColor(record.ruleType)">{{ ruleTypeLabel(record.ruleType) }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'handlerType'">
            <a-tag :color="handlerTypeColor(record.handlerType)">{{ handlerTypeLabel(record.handlerType) }}</a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-switch
              :checked="record.status === 1"
              checkedChildren="启用"
              unCheckedChildren="禁用"
              :disabled="!has(CardFlowPermissions.DispatchRuleManage)"
              @change="(val: string | number | boolean) => handleToggle(record, val as boolean)"
            />
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button v-if="has(CardFlowPermissions.DispatchRuleManage)" type="link" size="small" @click="openDrawer(record)">编辑</a-button>
            <a-button v-if="has(CardFlowPermissions.DispatchRuleManage)" type="link" size="small" danger @click="handleDelete(record)">删除</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新建/编辑抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="isEdit ? '编辑派发规则' : '新建派发规则'"
      width="720"
      :destroyOnClose="true"
      class="rule-drawer"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :labelCol="{ style: { width: '100px' } }"
        layout="horizontal"
      >
        <!-- 区域1: 触发条件 -->
        <div class="section-title">触发条件</div>
        <a-form-item label="规则名称" name="ruleName">
          <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" />
        </a-form-item>
        <a-form-item label="触发事件" name="triggerEvent">
          <a-select v-model:value="formData.triggerEvent" placeholder="请选择触发事件">
            <a-select-option value="PipelineCompleted">管道完成 (PipelineCompleted)</a-select-option>
            <a-select-option value="ManualTrigger">手动触发 (ManualTrigger)</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="适用暂存表" name="targetTables">
          <a-select
            v-model:value="formData.targetTables"
            mode="multiple"
            placeholder="请选择适用暂存表（留空表示全部）"
            :loading="stagingTablesLoading"
            showSearch
            allowClear
          >
            <a-select-option v-for="t in stagingTables" :key="t.tableName" :value="t.tableName">{{ t.tableName }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="规则类型" name="ruleType">
          <a-select v-model:value="formData.ruleType" placeholder="请选择规则类型">
            <a-select-option value="AlwaysMatch">始终匹配 (AlwaysMatch)</a-select-option>
            <a-select-option value="RowCondition">行级条件 (RowCondition)</a-select-option>
            <a-select-option value="BatchAggregate">批次聚合 (BatchAggregate)</a-select-option>
            <a-select-option value="HistoryCompare">历史对比 (HistoryCompare)</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="严重级别" name="severity">
          <a-select v-model:value="formData.severity" placeholder="请选择严重级别">
            <a-select-option value="Info">Info</a-select-option>
            <a-select-option value="Warning">Warning</a-select-option>
            <a-select-option value="Error">Error</a-select-option>
            <a-select-option value="Critical">Critical</a-select-option>
          </a-select>
        </a-form-item>

        <!-- 条件配置（行级条件） -->
        <template v-if="formData.ruleType === 'RowCondition'">
          <div class="section-title">条件配置</div>
          <a-form-item label="逻辑关系">
            <a-radio-group v-model:value="formData.conditionLogic" button-style="solid" size="small">
              <a-radio-button value="AND">AND（全部满足）</a-radio-button>
              <a-radio-button value="OR">OR（任一满足）</a-radio-button>
            </a-radio-group>
          </a-form-item>

          <div v-for="(cond, idx) in conditions" :key="idx" class="condition-row">
            <a-input v-model:value="cond.field" placeholder="字段名" style="flex: 1;" />
            <a-select v-model:value="cond.operator" placeholder="操作符" style="width: 130px;">
              <a-select-option v-for="op in operators" :key="op.value" :value="op.value">{{ op.label }}</a-select-option>
            </a-select>
            <template v-if="cond.operator === 'isNull' || cond.operator === 'isNotNull'">
              <!-- 无需值 -->
            </template>
            <template v-else-if="cond.operator === 'between'">
              <a-input v-model:value="cond.value" placeholder="最小值" style="flex: 0.5;" />
              <span style="color: #999;">~</span>
              <a-input v-model:value="cond.value2" placeholder="最大值" style="flex: 0.5;" />
            </template>
            <template v-else>
              <a-input v-model:value="cond.value" placeholder="值" style="flex: 1;" />
            </template>
            <a-button danger shape="circle" size="small" @click="conditions.splice(idx, 1)"><DeleteOutlined /></a-button>
          </div>

          <a-button size="small" @click="addCondition" style="margin-top: 8px;"><PlusOutlined /> 添加条件</a-button>
        </template>

        <!-- 条件配置（批次聚合） -->
        <template v-if="formData.ruleType === 'BatchAggregate'">
          <div class="section-title">批次聚合配置</div>
          <a-form-item label="聚合函数">
            <a-select v-model:value="batchAgg.aggFunction" placeholder="请选择聚合函数" style="width: 200px;">
              <a-select-option v-for="fn in aggFunctions" :key="fn.value" :value="fn.value">{{ fn.label }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="聚合字段">
            <a-input v-model:value="batchAgg.aggField" placeholder="例如：F发生额收入" style="width: 300px;" />
          </a-form-item>
          <a-form-item label="比较方式">
            <a-radio-group v-model:value="batchAgg.compareMode" button-style="solid" size="small">
              <a-radio-button value="aggregate">与另一聚合值比较</a-radio-button>
              <a-radio-button value="fixed">与固定值比较</a-radio-button>
            </a-radio-group>
          </a-form-item>
          <template v-if="batchAgg.compareMode === 'aggregate'">
            <a-form-item label="目标函数">
              <a-select v-model:value="batchAgg.targetFunction" placeholder="请选择目标聚合函数" style="width: 200px;">
                <a-select-option v-for="fn in aggFunctions" :key="fn.value" :value="fn.value">{{ fn.label }}</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item label="目标字段">
              <a-input v-model:value="batchAgg.targetField" placeholder="例如：F发生额支出" style="width: 300px;" />
            </a-form-item>
          </template>
          <template v-if="batchAgg.compareMode === 'fixed'">
            <a-form-item label="固定值">
              <a-input-number v-model:value="batchAgg.fixedValue" placeholder="请输入固定值" style="width: 200px;" />
            </a-form-item>
          </template>
          <a-form-item label="比较操作符">
            <a-select v-model:value="batchAgg.operator" placeholder="请选择操作符" style="width: 120px;">
              <a-select-option v-for="op in compareOperators" :key="op.value" :value="op.value">{{ op.label }}</a-select-option>
            </a-select>
          </a-form-item>
        </template>

        <!-- 条件配置（历史对比） -->
        <template v-if="formData.ruleType === 'HistoryCompare'">
          <div class="section-title">历史对比配置</div>
          <div class="section-subtitle">当前批次</div>
          <a-form-item label="聚合函数">
            <a-select v-model:value="historyCompare.currentFunction" placeholder="请选择" style="width: 200px;">
              <a-select-option v-for="fn in aggFunctions" :key="fn.value" :value="fn.value">{{ fn.label }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="聚合字段">
            <a-input v-model:value="historyCompare.currentField" placeholder="例如：F发生额支出" style="width: 300px;" />
          </a-form-item>
          <div class="section-subtitle">历史数据</div>
          <a-form-item label="时间范围">
            <a-select v-model:value="historyCompare.scope" placeholder="请选择时间范围" style="width: 200px;">
              <a-select-option value="LAST_7_DAYS">最近7天</a-select-option>
              <a-select-option value="LAST_30_DAYS">最近30天</a-select-option>
              <a-select-option value="LAST_90_DAYS">最近90天</a-select-option>
              <a-select-option value="ALL">全部</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="聚合函数">
            <a-select v-model:value="historyCompare.historyFunction" placeholder="请选择" style="width: 200px;">
              <a-select-option v-for="fn in aggFunctions" :key="fn.value" :value="fn.value">{{ fn.label }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="聚合字段">
            <a-input v-model:value="historyCompare.historyField" placeholder="例如：F发生额支出" style="width: 300px;" />
          </a-form-item>
          <div class="section-subtitle">偏差阈值</div>
          <a-form-item label="操作符">
            <a-select v-model:value="historyCompare.deviationOperator" placeholder="请选择" style="width: 120px;">
              <a-select-option v-for="op in compareOperators" :key="op.value" :value="op.value">{{ op.label }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="百分比">
            <a-input-number v-model:value="historyCompare.deviationPercent" :min="0" :max="10000" style="width: 160px;">
              <template #addonAfter>%</template>
            </a-input-number>
          </a-form-item>
        </template>

        <!-- 区域2: 目标插件 -->
        <div class="section-title">目标插件</div>
        <a-form-item label="处理器类型" name="handlerType">
          <a-select v-model:value="formData.handlerType" placeholder="请选择处理器类型" @change="onHandlerTypeChange">
            <a-select-option value="AutoVoucher">AutoVoucher（自动凭证）</a-select-option>
            <a-select-option value="WorkTask">WorkTask（工作任务）</a-select-option>
            <a-select-option value="AlertNotify">AlertNotify（告警通知）</a-select-option>
            <a-select-option value="InfoRecord">InfoRecord（信息记录）</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="异步执行">
          <a-switch v-model:checked="formData.isAsync" checkedChildren="是" unCheckedChildren="否" />
        </a-form-item>
        <a-form-item label="优先级" name="priority">
          <a-input-number v-model:value="formData.priority" :min="0" :max="9999" style="width: 180px;" />
        </a-form-item>
        <a-form-item label="说明" name="description">
          <a-textarea v-model:value="formData.description" :rows="2" placeholder="请输入说明" />
        </a-form-item>

        <!-- 区域3: 插件配置 -->
        <!-- AutoVoucher 结构化配置 -->
        <template v-if="formData.handlerType === 'AutoVoucher'">
          <div class="section-title">插件配置 - 自动凭证</div>
          <a-form-item label="目的账套">
            <a-select v-model:value="handlerConfig.accountSetId" placeholder="请选择账套" :loading="accountSetsLoading" allowClear style="width: 280px;">
              <a-select-option v-for="item in accountSets" :key="item.id" :value="item.id">{{ item.fName }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="日期字段">
            <a-input v-model:value="handlerConfig.dateField" style="width: 280px;" placeholder="用于确定会计期间的暂存表列名" />
            <div class="config-hint">用于确定会计期间，默认 F业务日期</div>
          </a-form-item>
          <a-alert
            type="info"
            showIcon
            message="凭证分录规则请在保存后通过详情页管理"
            style="margin-top: 8px;"
          />
        </template>

        <!-- AlertNotify -->
        <template v-else-if="formData.handlerType === 'AlertNotify'">
          <div class="section-title">插件配置 - 告警通知</div>
          <a-form-item label="通知通道">
            <a-checkbox-group v-model:value="handlerConfig.channels">
              <a-checkbox value="system">系统通知</a-checkbox>
              <a-checkbox value="dingtalk">钉钉</a-checkbox>
            </a-checkbox-group>
          </a-form-item>
          <a-form-item label="接收人/组">
            <a-input v-model:value="handlerConfig.receivers" placeholder="多个接收人用逗号分隔" />
          </a-form-item>
          <a-form-item label="消息模板">
            <a-textarea v-model:value="handlerConfig.messageTemplate" :rows="3"
              placeholder="可用变量：{BatchId}、{TargetTable}、{AffectedRowCount}、{Severity}" />
            <div class="config-hint">可用变量：&#x7b;BatchId&#x7d;、&#x7b;TargetTable&#x7d;、&#x7b;AffectedRowCount&#x7d;、&#x7b;Severity&#x7d;</div>
          </a-form-item>
        </template>

        <!-- InfoRecord -->
        <template v-else-if="formData.handlerType === 'InfoRecord'">
          <div class="section-title">插件配置 - 信息记录</div>
          <a-form-item label="日志级别">
            <a-select v-model:value="handlerConfig.logLevel" placeholder="请选择日志级别">
              <a-select-option value="Information">Information</a-select-option>
              <a-select-option value="Warning">Warning</a-select-option>
              <a-select-option value="Error">Error</a-select-option>
            </a-select>
          </a-form-item>
        </template>

        <!-- WorkTask -->
        <template v-else-if="formData.handlerType === 'WorkTask'">
          <div class="section-title">插件配置 - 创建工作任务</div>
          <a-form-item label="任务标题模板">
            <a-textarea v-model:value="handlerConfig.titleTemplate" :rows="2"
              placeholder="可用变量：{BatchId}、{TargetTable}、{AffectedRowCount}、{Severity}" />
          </a-form-item>
          <a-form-item label="任务描述模板">
            <a-textarea v-model:value="handlerConfig.descriptionTemplate" :rows="3"
              placeholder="可用变量：{BatchId}、{TargetTable}、{AffectedRowCount}、{Severity}" />
          </a-form-item>
          <a-form-item label="优先级">
            <a-select v-model:value="handlerConfig.taskPriority" style="width: 180px;">
              <a-select-option :value="1">1 - 最高</a-select-option>
              <a-select-option :value="2">2 - 高</a-select-option>
              <a-select-option :value="3">3 - 中</a-select-option>
              <a-select-option :value="4">4 - 低</a-select-option>
              <a-select-option :value="5">5 - 最低</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="处理期限天数">
            <a-input-number v-model:value="handlerConfig.daysUntilDue" :min="1" :max="365" style="width: 180px;" />
          </a-form-item>
        </template>


        <!-- 其他/未知类型 - JSON textarea -->
        <template v-else-if="formData.handlerType">
          <div class="section-title">插件配置</div>
          <a-form-item label="JSON 配置">
            <div class="config-hint" style="margin-top: 0; margin-bottom: 6px;">请输入 JSON 格式的处理器配置</div>
            <a-textarea v-model:value="handlerConfigJson" :rows="5" placeholder='请输入 JSON 格式配置，如 {"key": "value"}' />
          </a-form-item>
        </template>
      </a-form>

      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px;">
          <a-button @click="drawerVisible = false">取消</a-button>
          <a-button type="primary" @click="handleSubmit" :loading="submitting">{{ isEdit ? '保存' : '创建' }}</a-button>
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance, TablePaginationConfig } from 'ant-design-vue'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import {
  getDispatchRules,
  getDispatchRule,
  createDispatchRule,
  updateDispatchRule,
  deleteDispatchRule,
} from '@/api/dispatchRule'
import { getStagingTables } from '@/api/cardflow'
import type { StagingTableInfo } from '@/api/cardflow'
import { getAccountSets } from '@/api/finance'
import type { AccountSetDto } from '@/api/finance'
import { usePermission, CardFlowPermissions } from '@/utils/permission'

const { has } = usePermission()

// ==================== 列表 ====================
const loading = ref(false)
const tableData = ref<any[]>([])
const searchText = ref('')
const statusFilter = ref('')
const handlerTypeFilter = ref<string | undefined>(undefined)
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const tableColumns = [
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', ellipsis: true },
  { title: '触发事件', dataIndex: 'triggerEvent', key: 'triggerEvent', width: 110, align: 'center' as const },
  { title: '适用暂存表', dataIndex: 'targetTables', key: 'targetTables', width: 200 },
  { title: '规则类型', dataIndex: 'ruleType', key: 'ruleType', width: 110, align: 'center' as const },
  { title: '目标插件', dataIndex: 'handlerType', key: 'handlerType', width: 110, align: 'center' as const },
  { title: '优先级', dataIndex: 'priority', key: 'priority', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (statusFilter.value !== '') params.status = statusFilter.value === '1' ? 1 : 0
    if (handlerTypeFilter.value) params.handlerType = handlerTypeFilter.value
    if (searchText.value) params.search = searchText.value
    const res: any = await getDispatchRules(params)
    const data = res.data ?? res
    if (Array.isArray(data)) {
      tableData.value = data
      pagination.total = data.length
    } else {
      tableData.value = data.items ?? data.list ?? []
      pagination.total = data.total ?? data.totalCount ?? 0
    }
  } catch {
    message.error('获取派发规则列表失败')
  } finally {
    loading.value = false
  }
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1
  pagination.pageSize = pag.pageSize ?? 20
  fetchList()
}

// ==================== 抽屉 ====================
const drawerVisible = ref(false)
const isEdit = ref(false)
const editingId = ref<number | null>(null)
const submitting = ref(false)
const formRef = ref<FormInstance>()

interface ConditionItem {
  field: string
  operator: string
  value: string
  value2: string
}

const conditions = ref<ConditionItem[]>([])

// ==================== 选项常量 ====================
const aggFunctions = [
  { value: 'SUM', label: 'SUM (求和)' },
  { value: 'COUNT', label: 'COUNT (计数)' },
  { value: 'AVG', label: 'AVG (平均值)' },
  { value: 'MAX', label: 'MAX (最大值)' },
  { value: 'MIN', label: 'MIN (最小值)' },
  { value: 'COUNT_DISTINCT', label: 'COUNT_DISTINCT (去重计数)' },
]

const compareOperators = [
  { value: '=', label: '=' }, { value: '!=', label: '!=' },
  { value: '>', label: '>' }, { value: '<', label: '<' },
  { value: '>=', label: '>=' }, { value: '<=', label: '<=' },
]

const operators = [
  { value: '=', label: '=' }, { value: '!=', label: '!=' },
  { value: '>', label: '>' }, { value: '<', label: '<' },
  { value: '>=', label: '>=' }, { value: '<=', label: '<=' },
  { value: 'contains', label: '包含 (contains)' },
  { value: 'startsWith', label: '开头 (startsWith)' },
  { value: 'endsWith', label: '结尾 (endsWith)' },
  { value: 'isNull', label: '为空 (isNull)' },
  { value: 'isNotNull', label: '不为空 (isNotNull)' },
  { value: 'in', label: '在集合中 (in)' },
  { value: 'between', label: '区间 (between)' },
]

// ==================== BatchAggregate 表单数据 ====================
const batchAgg = reactive({
  aggFunction: 'SUM' as string,
  aggField: '',
  compareMode: 'aggregate' as 'aggregate' | 'fixed',
  targetFunction: 'SUM' as string,
  targetField: '',
  fixedValue: undefined as number | undefined,
  operator: '!=' as string,
})

// ==================== HistoryCompare 表单数据 ====================
const historyCompare = reactive({
  currentFunction: 'AVG' as string,
  currentField: '',
  scope: 'LAST_30_DAYS' as string,
  historyFunction: 'AVG' as string,
  historyField: '',
  deviationOperator: '>' as string,
  deviationPercent: 50 as number,
})

// ==================== 处理器配置 ====================
const handlerConfig = reactive<Record<string, any>>({
  // AutoVoucher
  accountSetId: null as number | null,
  dateField: 'F业务日期',
  // AlertNotify
  channels: [] as string[],
  receivers: '',
  messageTemplate: '',
  // InfoRecord
  logLevel: 'Information',
  // WorkTask
  titleTemplate: '',
  descriptionTemplate: '',
  taskPriority: 3,
  daysUntilDue: 3,
})

const handlerConfigJson = ref('{}')

// ==================== 表单 ====================
const defaultForm = () => ({
  ruleName: '',
  triggerEvent: 'PipelineCompleted' as string,
  targetTables: [] as string[],
  ruleType: 'AlwaysMatch' as string,
  severity: 'Info' as string,
  conditionLogic: 'AND' as string,
  handlerType: '' as string,
  isAsync: true,
  priority: 100,
  description: '',
})

const formData = reactive(defaultForm())

const formRules = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' as const }],
  triggerEvent: [{ required: true, message: '请选择触发事件', trigger: 'change' as const }],
  ruleType: [{ required: true, message: '请选择规则类型', trigger: 'change' as const }],
  handlerType: [{ required: true, message: '请选择处理器类型', trigger: 'change' as const }],
}

function addCondition() {
  conditions.value.push({ field: '', operator: '=', value: '', value2: '' })
}

function onHandlerTypeChange() {
  handlerConfig.accountSetId = null
  handlerConfig.dateField = 'F业务日期'
  handlerConfig.channels = []
  handlerConfig.receivers = ''
  handlerConfig.messageTemplate = ''
  handlerConfig.logLevel = 'Information'
  handlerConfig.titleTemplate = ''
  handlerConfig.descriptionTemplate = ''
  handlerConfig.taskPriority = 3
  handlerConfig.daysUntilDue = 3
  handlerConfig.processCode = ''
  handlerConfig.bizDocType = 'ImportException'
  handlerConfig.oaTitleTemplate = ''
  handlerConfigJson.value = '{}'
  if (formData.handlerType === 'AutoVoucher') {
    fetchAccountSetsData()
  }
}

function buildHandlerConfigPayload(): string {
  if (formData.handlerType === 'AutoVoucher') {
    return JSON.stringify({
      mode: 'rulesBased',
      accountSetId: handlerConfig.accountSetId,
      dateField: handlerConfig.dateField || 'F业务日期',
    })
  } else if (formData.handlerType === 'AlertNotify') {
    return JSON.stringify({
      Channels: handlerConfig.channels,
      Receivers: handlerConfig.receivers,
      MessageTemplate: handlerConfig.messageTemplate,
    })
  } else if (formData.handlerType === 'InfoRecord') {
    return JSON.stringify({
      LogLevel: handlerConfig.logLevel,
    })
  } else if (formData.handlerType === 'WorkTask') {
    return JSON.stringify({
      titleTemplate: handlerConfig.titleTemplate,
      descriptionTemplate: handlerConfig.descriptionTemplate,
      priority: handlerConfig.taskPriority,
      daysUntilDue: handlerConfig.daysUntilDue,
    })
  } else {
    return handlerConfigJson.value || '{}'
  }
}

function parseHandlerConfig(type: string, configStr: string) {
  try {
    const cfg = configStr ? (typeof configStr === 'string' ? JSON.parse(configStr) : configStr) : {}
    if (type === 'AutoVoucher') {
      handlerConfig.accountSetId = cfg.accountSetId ?? cfg.AccountSetId ?? null
      handlerConfig.dateField = cfg.dateField ?? cfg.DateField ?? 'F业务日期'
    } else if (type === 'AlertNotify') {
      handlerConfig.channels = cfg.Channels ?? cfg.channels ?? []
      handlerConfig.receivers = cfg.Receivers ?? cfg.receivers ?? ''
      handlerConfig.messageTemplate = cfg.MessageTemplate ?? cfg.messageTemplate ?? ''
    } else if (type === 'InfoRecord') {
      handlerConfig.logLevel = cfg.LogLevel ?? cfg.logLevel ?? 'Information'
    } else if (type === 'WorkTask') {
      handlerConfig.titleTemplate = cfg.titleTemplate ?? ''
      handlerConfig.descriptionTemplate = cfg.descriptionTemplate ?? ''
      handlerConfig.taskPriority = cfg.priority ?? 3
      handlerConfig.daysUntilDue = cfg.daysUntilDue ?? 3
    } else {
      handlerConfigJson.value = typeof configStr === 'string' ? configStr : JSON.stringify(configStr) || '{}'
    }
  } catch {
    handlerConfigJson.value = configStr || '{}'
  }
}

// ==================== 打开抽屉 ====================
async function openDrawer(row?: any) {
  if (row) {
    isEdit.value = true
    editingId.value = row.id
    let detail = row
    try {
      const res: any = await getDispatchRule(row.id)
      detail = res.data ?? res ?? row
    } catch { /* fallback */ }
    Object.assign(formData, {
      ruleName: detail.ruleName ?? '',
      triggerEvent: detail.triggerEvent ?? 'PipelineCompleted',
      targetTables: parseArray(detail.targetTables),
      ruleType: detail.ruleType ?? 'AlwaysMatch',
      severity: detail.severity ?? 'Info',
      conditionLogic: detail.conditionLogic ?? 'AND',
      handlerType: detail.handlerType ?? '',
      isAsync: detail.isAsync ?? true,
      priority: detail.priority ?? 100,
      description: detail.description ?? '',
    })
    // 反序列化条件
    const conditionRaw = detail.condition
    if (conditionRaw) {
      try {
        const parsed = typeof conditionRaw === 'string' ? JSON.parse(conditionRaw) : conditionRaw
        if (detail.ruleType === 'RowCondition') {
          conditions.value = (parsed.conditions ?? parsed ?? []).map((c: any) => ({
            field: c.field ?? '', operator: c.operator ?? '=', value: c.value ?? '', value2: c.value2 ?? '',
          }))
        } else if (detail.ruleType === 'BatchAggregate') {
          batchAgg.aggFunction = parsed.aggregate?.function ?? 'SUM'
          batchAgg.aggField = parsed.aggregate?.field ?? ''
          if (parsed.compare?.target) {
            batchAgg.compareMode = 'aggregate'
            batchAgg.targetFunction = parsed.compare.target.function ?? 'SUM'
            batchAgg.targetField = parsed.compare.target.field ?? ''
          } else {
            batchAgg.compareMode = 'fixed'
            batchAgg.fixedValue = parsed.compare?.value ?? undefined
          }
          batchAgg.operator = parsed.compare?.operator ?? '!='
        } else if (detail.ruleType === 'HistoryCompare') {
          historyCompare.currentFunction = parsed.currentBatch?.function ?? 'AVG'
          historyCompare.currentField = parsed.currentBatch?.field ?? ''
          historyCompare.scope = parsed.history?.scope ?? 'LAST_30_DAYS'
          historyCompare.historyFunction = parsed.history?.function ?? 'AVG'
          historyCompare.historyField = parsed.history?.field ?? ''
          historyCompare.deviationOperator = parsed.deviation?.operator ?? '>'
          historyCompare.deviationPercent = parsed.deviation?.percent ?? 50
        }
      } catch { conditions.value = [] }
    } else {
      conditions.value = []
    }
    // 反序列化处理器配置
    parseHandlerConfig(detail.handlerType, detail.handlerConfig ?? detail.handlerConfiguration ?? '')
    // AutoVoucher 需要加载账套列表
    if (detail.handlerType === 'AutoVoucher') {
      fetchAccountSetsData()
    }
  } else {
    isEdit.value = false
    editingId.value = null
    Object.assign(formData, defaultForm())
    conditions.value = []
    onHandlerTypeChange()
  }
  drawerVisible.value = true
}

// ==================== 提交 ====================
async function handleSubmit() {
  if (!formRef.value) return
  await formRef.value.validate()
  submitting.value = true
  try {
    const payload: any = {
      ...formData,
      targetTables: formData.targetTables,
      handlerConfig: buildHandlerConfigPayload(),
    }
    // 序列化条件
    if (formData.ruleType === 'RowCondition' && conditions.value.length > 0) {
      payload.condition = JSON.stringify({
        logic: formData.conditionLogic,
        conditions: conditions.value.filter(c => c.field),
      })
    }
    if (formData.ruleType === 'BatchAggregate') {
      const condJson: any = {
        aggregate: { function: batchAgg.aggFunction, field: batchAgg.aggField },
        compare: { operator: batchAgg.operator } as any,
      }
      if (batchAgg.compareMode === 'aggregate') {
        condJson.compare.target = { function: batchAgg.targetFunction, field: batchAgg.targetField }
      } else {
        condJson.compare.value = batchAgg.fixedValue
      }
      payload.condition = JSON.stringify(condJson)
    }
    if (formData.ruleType === 'HistoryCompare') {
      payload.condition = JSON.stringify({
        currentBatch: { function: historyCompare.currentFunction, field: historyCompare.currentField },
        history: {
          scope: historyCompare.scope,
          function: historyCompare.historyFunction,
          field: historyCompare.historyField,
        },
        deviation: { operator: historyCompare.deviationOperator, percent: historyCompare.deviationPercent },
      })
    }
    if (isEdit.value && editingId.value != null) {
      await updateDispatchRule(editingId.value, payload)
      message.success('更新成功')
    } else {
      await createDispatchRule(payload)
      message.success('创建成功')
    }
    drawerVisible.value = false
    fetchList()
  } catch {
    message.error(isEdit.value ? '更新失败' : '创建失败')
  } finally {
    submitting.value = false
  }
}

// ==================== 删除 ====================
function handleDelete(row: any) {
  Modal.confirm({
    title: '删除确认',
    content: `确认删除规则「${row.ruleName}」？此操作不可恢复。`,
    okText: '删除',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      await deleteDispatchRule(row.id)
      message.success('删除成功')
      fetchList()
    },
  })
}

// ==================== 启用/禁用 ====================
async function handleToggle(row: any, val: boolean) {
  try {
    await updateDispatchRule(row.id, { status: val ? 1 : 0 })
    row.status = val ? 1 : 0
    message.success(val ? '已启用' : '已禁用')
  } catch {
    message.error('操作失败')
  }
}

// ==================== 账套 ====================
const accountSets = ref<AccountSetDto[]>([])
const accountSetsLoading = ref(false)

async function fetchAccountSetsData() {
  accountSetsLoading.value = true
  try {
    const res: any = await getAccountSets()
    const list = res.data ?? res ?? []
    accountSets.value = list
  } catch { /* silent */ } finally {
    accountSetsLoading.value = false
  }
}

// ==================== 暂存表 ====================
const stagingTables = ref<StagingTableInfo[]>([])
const stagingTablesLoading = ref(false)

async function fetchStagingTables() {
  stagingTablesLoading.value = true
  try {
    const res: any = await getStagingTables()
    stagingTables.value = res.data ?? res ?? []
  } catch { /* silent */ } finally {
    stagingTablesLoading.value = false
  }
}

// ==================== 辅助函数 ====================
function parseArray(val: unknown): string[] {
  if (!val) return []
  if (Array.isArray(val)) return val as string[]
  try { return JSON.parse(val as string) } catch { return [] }
}

function ruleTypeLabel(type: string) {
  const map: Record<string, string> = {
    AlwaysMatch: '始终匹配', RowCondition: '行级条件',
    BatchAggregate: '批次聚合', HistoryCompare: '历史对比',
  }
  return map[type] ?? type
}

function ruleTypeColor(type: string) {
  const map: Record<string, string> = {
    AlwaysMatch: 'green', RowCondition: 'blue',
    BatchAggregate: 'purple', HistoryCompare: 'orange',
  }
  return map[type] ?? 'default'
}

function handlerTypeLabel(type: string) {
  const map: Record<string, string> = {
    AutoVoucher: '自动凭证',
    WorkTask: '工作任务', AlertNotify: '告警通知', InfoRecord: '信息记录',
  }
  return map[type] ?? type
}

function handlerTypeColor(type: string) {
  const map: Record<string, string> = {
    AutoVoucher: 'green',
    WorkTask: 'orange', AlertNotify: 'red', InfoRecord: 'default',
  }
  return map[type] ?? 'default'
}

// ==================== 初始化 ====================
onMounted(() => {
  fetchList()
  fetchStagingTables()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.dispatch-rules {
  padding: $page-padding;
}

.toolbar {
  display: flex;
  justify-content: flex-start;
  align-items: center;
  margin-bottom: $spacing-md;
  gap: $spacing-sm;
}

.table-card :deep(.ant-card-body) {
  padding: 0;
}

.section-title {
  font-weight: 600;
  font-size: $font-size-lg;
  color: $text-primary;
  margin: 16px 0 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid $border-color-lighter;
}

.section-subtitle {
  font-weight: 500;
  font-size: $font-size-base;
  color: $text-secondary;
  margin: 12px 0 8px;
  padding-left: 4px;
}

.condition-row {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
  align-items: center;
}

.config-hint {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}
</style>
