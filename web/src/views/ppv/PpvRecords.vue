<template>
  <div class="ppv-records page-container">
    <a-page-header title="产值记录" sub-title="PPV 产值记录与审核" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-month-picker v-model:value="searchPeriod" placeholder="选择期间" style="width: 160px" />
          <a-input v-model:value="searchEmployee" placeholder="员工姓名/工号" allow-clear style="width: 200px" />
          <a-select v-model:value="searchStatus" placeholder="审核状态" allow-clear style="width: 140px" :options="statusOptions" />
          <a-select v-model:value="searchQuality" placeholder="质量等级" allow-clear style="width: 140px" :options="qualityOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button type="primary" ghost>批量通过</a-button>
          <a-button danger ghost>批量驳回</a-button>
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
          <template v-if="column.key === 'f审核状态'">
            <a-tag v-if="record.f审核状态 === 0" color="orange">待审核</a-tag>
            <a-tag v-else-if="record.f审核状态 === 1" color="green">已通过</a-tag>
            <a-tag v-else-if="record.f审核状态 === 2" color="red">已驳回</a-tag>
          </template>
          <template v-else-if="column.key === 'f是否跨岗'">
            <a-tag v-if="record.f是否跨岗" color="blue">跨岗</a-tag>
            <span v-else>—</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small">审核通过</a-button>
            <a-button type="link" size="small" danger>驳回</a-button>
            <a-button type="link" size="small">详情</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Dayjs } from 'dayjs'
import type { PpvRecord } from '@/types/ppv'

const searchPeriod = ref<Dayjs | undefined>(undefined)
const searchEmployee = ref<string>('')
const searchStatus = ref<number | undefined>(undefined)
const searchQuality = ref<number | undefined>(undefined)

const statusOptions = [
  { label: '待审核', value: 0 },
  { label: '已通过', value: 1 },
  { label: '已驳回', value: 2 },
]

const qualityOptions = [
  { label: 'A', value: 1 },
  { label: 'B', value: 2 },
  { label: 'C', value: 3 },
  { label: 'D', value: 4 },
]

const columns = [
  { title: '员工', dataIndex: 'f员工ID', key: 'f员工ID', width: 140 },
  { title: '期间', dataIndex: 'f期间', key: 'f期间', width: 110 },
  { title: '产值项编码', dataIndex: 'f产值项编码', key: 'f产值项编码', width: 140 },
  { title: '数量', dataIndex: 'f数量', key: 'f数量', width: 90 },
  { title: '产值金额', dataIndex: 'f产值金额', key: 'f产值金额', width: 110 },
  { title: '质量等级', dataIndex: 'f质量等级', key: 'f质量等级', width: 90 },
  { title: '跨岗', dataIndex: 'f是否跨岗', key: 'f是否跨岗', width: 80 },
  { title: '审核状态', dataIndex: 'f审核状态', key: 'f审核状态', width: 110 },
  { title: '创建时间', dataIndex: 'f创建时间', key: 'f创建时间', width: 160 },
  { title: '操作', key: 'action', width: 240, fixed: 'right' as const },
]

const dataSource = ref<PpvRecord[]>([])

// TODO: 调用 /api/ppv/records 查询记录
// TODO: 调用 /api/ppv/records/{id}/review 审核
</script>

<style scoped>
.ppv-records {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
