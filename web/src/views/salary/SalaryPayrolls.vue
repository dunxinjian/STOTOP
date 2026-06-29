<template>
  <div class="salary-payrolls page-container">
    <a-page-header title="月度工资单" sub-title="月度工资单审核与发放" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-month-picker v-model:value="searchPeriod" placeholder="期间" style="width: 160px" />
          <a-input v-model:value="searchEmployee" placeholder="员工姓名" allow-clear style="width: 200px" />
          <a-select v-model:value="searchStatus" placeholder="状态" allow-clear style="width: 140px" :options="statusOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button type="primary">
            <template #icon><PlusOutlined /></template>
            生成工资单
          </a-button>
          <a-button>批量审核</a-button>
          <a-button>批量发放</a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataSource"
        :pagination="false"
        row-key="fid"
        bordered
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'f状态'">
            <a-tag :color="statusColor(record.f状态)">{{ statusText(record.f状态) }}</a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small">明细</a-button>
            <a-button type="link" size="small">审核</a-button>
            <a-button type="link" size="small">发放</a-button>
            <a-button type="link" size="small" danger>驳回</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { Dayjs } from 'dayjs'
import type { SalaryPayroll } from '@/types/salary'

const searchPeriod = ref<Dayjs | undefined>(undefined)
const searchEmployee = ref<string>('')
const searchStatus = ref<number | undefined>(undefined)

const statusOptions = [
  { label: '草稿', value: 0 },
  { label: '待审', value: 1 },
  { label: '已审', value: 2 },
  { label: '已发放', value: 3 },
  { label: '已驳回', value: 4 },
]

function statusText(s: number): string {
  return statusOptions.find((o) => o.value === s)?.label ?? '-'
}

function statusColor(s: number): string {
  switch (s) {
    case 0: return 'default'
    case 1: return 'orange'
    case 2: return 'blue'
    case 3: return 'green'
    case 4: return 'red'
    default: return 'default'
  }
}

const columns = [
  { title: '期间', dataIndex: 'f期间', key: 'f期间', width: 110 },
  { title: '员工', dataIndex: 'f员工姓名', key: 'f员工姓名', width: 120 },
  { title: '基本工资', dataIndex: 'f基本工资', key: 'f基本工资', width: 110 },
  { title: 'KSF', dataIndex: 'fKSF金额', key: 'fKSF金额', width: 100 },
  { title: 'PPV', dataIndex: 'fPPV金额', key: 'fPPV金额', width: 100 },
  { title: '加项', dataIndex: 'f加项合计', key: 'f加项合计', width: 100 },
  { title: '扣项', dataIndex: 'f扣项合计', key: 'f扣项合计', width: 100 },
  { title: '应发', dataIndex: 'f应发合计', key: 'f应发合计', width: 110 },
  { title: '实发', dataIndex: 'f实发合计', key: 'f实发合计', width: 110 },
  { title: '状态', key: 'f状态', dataIndex: 'f状态', width: 100 },
  { title: '操作', key: 'action', width: 260, fixed: 'right' as const },
]

const dataSource = ref<SalaryPayroll[]>([])

// TODO: 调用 /api/salary/payrolls 加载工资单列表
// TODO: 调用 /api/salary/payrolls/generate 生成工资单
// TODO: 调用 /api/salary/payrolls/{id}/audit 审核
// TODO: 调用 /api/salary/payrolls/{id}/pay 发放
</script>

<style scoped>
.salary-payrolls {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
