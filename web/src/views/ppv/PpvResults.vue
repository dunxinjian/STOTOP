<template>
  <div class="ppv-results page-container">
    <a-page-header title="月度汇总" sub-title="PPV 月度产值汇总结果" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-month-picker v-model:value="searchPeriod" placeholder="选择期间" style="width: 160px" />
          <a-input v-model:value="searchEmployee" placeholder="员工姓名/工号" allow-clear style="width: 200px" />
          <a-select v-model:value="searchStatus" placeholder="状态筛选" allow-clear style="width: 140px" :options="statusOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button>导出</a-button>
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
            <a-tag v-if="record.f状态 === 1" color="green">正常</a-tag>
            <a-tag v-else-if="record.f状态 === 2" color="red">清零</a-tag>
            <a-tag v-else-if="record.f状态 === 3" color="orange">异常</a-tag>
          </template>
          <template v-else-if="column.key === 'f是否跨岗清零'">
            <a-tag v-if="record.f是否跨岗清零" color="red">已清零</a-tag>
            <span v-else>—</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small">查看明细</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Dayjs } from 'dayjs'
import type { PpvMonthlyResult } from '@/types/ppv'

const searchPeriod = ref<Dayjs | undefined>(undefined)
const searchEmployee = ref<string>('')
const searchStatus = ref<number | undefined>(undefined)

const statusOptions = [
  { label: '正常', value: 1 },
  { label: '清零', value: 2 },
  { label: '异常', value: 3 },
]

const columns = [
  { title: '员工', dataIndex: 'f员工ID', key: 'f员工ID', width: 140 },
  { title: '期间', dataIndex: 'f期间', key: 'f期间', width: 110 },
  { title: '岗位(快照)', dataIndex: 'f岗位ID快照', key: 'f岗位ID快照', width: 140 },
  { title: '部门(快照)', dataIndex: 'f部门ID快照', key: 'f部门ID快照', width: 140 },
  { title: '总产值', dataIndex: 'f总产值', key: 'f总产值', width: 110 },
  { title: '本岗产值', dataIndex: 'f本岗产值', key: 'f本岗产值', width: 110 },
  { title: '跨岗产值', dataIndex: 'f跨岗产值', key: 'f跨岗产值', width: 110 },
  { title: '综合质量', dataIndex: 'f综合质量等级', key: 'f综合质量等级', width: 100 },
  { title: '跨岗清零', dataIndex: 'f是否跨岗清零', key: 'f是否跨岗清零', width: 100 },
  { title: 'B分变化', dataIndex: 'fB分变化', key: 'fB分变化', width: 100 },
  { title: 'A分变化', dataIndex: 'fA分变化', key: 'fA分变化', width: 100 },
  { title: '状态', dataIndex: 'f状态', key: 'f状态', width: 90 },
  { title: '操作', key: 'action', width: 120, fixed: 'right' as const },
]

const dataSource = ref<PpvMonthlyResult[]>([])

// TODO: 调用 /api/ppv/results 查询月度汇总
</script>

<style scoped>
.ppv-results {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
