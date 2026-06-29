<template>
  <div class="ppv-my-progress">
    <a-page-header title="我的产值" sub-title="员工自助录入与查询" />

    <!-- 当前期间汇总 -->
    <a-card title="本月汇总" style="margin-bottom: 16px">
      <a-row :gutter="16">
        <a-col :span="6">
          <a-statistic title="期间" value="--" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="总产值(元)" :value="0" :precision="2" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="本岗产值(元)" :value="0" :precision="2" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="跨岗产值(元)" :value="0" :precision="2" />
        </a-col>
      </a-row>
    </a-card>

    <!-- 录入产值 -->
    <a-card title="录入产值" style="margin-bottom: 16px">
      <a-form layout="inline" :model="formState">
        <a-form-item label="产值项">
          <a-select v-model:value="formState.templateId" placeholder="选择产值项" style="width: 200px" :options="templateOptions" />
        </a-form-item>
        <a-form-item label="数量">
          <a-input-number v-model:value="formState.quantity" :min="0" :precision="2" style="width: 140px" />
        </a-form-item>
        <a-form-item label="质量等级">
          <a-select v-model:value="formState.qualityLevel" placeholder="质量等级" style="width: 120px" :options="qualityOptions" />
        </a-form-item>
        <a-form-item label="跨岗">
          <a-switch v-model:checked="formState.isCrossPosition" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary">提交</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 本人记录列表 -->
    <a-card title="我的记录">
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
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import type { PpvRecord, CreatePpvRecordRequest } from '@/types/ppv'

const formState = reactive<Partial<CreatePpvRecordRequest> & { templateId?: number; quantity?: number; qualityLevel?: number; isCrossPosition?: boolean }>({
  templateId: undefined,
  quantity: 0,
  qualityLevel: 1,
  isCrossPosition: false,
})

const templateOptions = ref<{ label: string; value: number }[]>([])

const qualityOptions = [
  { label: 'A', value: 1 },
  { label: 'B', value: 2 },
  { label: 'C', value: 3 },
  { label: 'D', value: 4 },
]

const columns = [
  { title: '期间', dataIndex: 'f期间', key: 'f期间', width: 100 },
  { title: '产值项', dataIndex: 'f产值项编码', key: 'f产值项编码', width: 140 },
  { title: '数量', dataIndex: 'f数量', key: 'f数量', width: 90 },
  { title: '产值金额', dataIndex: 'f产值金额', key: 'f产值金额', width: 110 },
  { title: '质量等级', dataIndex: 'f质量等级', key: 'f质量等级', width: 90 },
  { title: '审核状态', dataIndex: 'f审核状态', key: 'f审核状态', width: 110 },
  { title: '创建时间', dataIndex: 'f创建时间', key: 'f创建时间', width: 160 },
]

const dataSource = ref<PpvRecord[]>([])

// TODO: 调用 /api/ppv/my/summary 获取本月汇总
// TODO: 调用 /api/ppv/templates?mine=true 获取可录入产值项
// TODO: 调用 /api/ppv/my/records 获取本人记录
// TODO: 调用 POST /api/ppv/records 提交录入
</script>

<style scoped>
.ppv-my-progress {
  padding: 16px;
}
</style>
