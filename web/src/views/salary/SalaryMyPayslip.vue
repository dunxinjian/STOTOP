<template>
  <div class="salary-my-payslip">
    <a-page-header title="我的工资条" sub-title="员工自助查看工资明细" />

    <a-card :bordered="false" style="margin-bottom: 16px">
      <a-space>
        <a-month-picker v-model:value="period" placeholder="选择期间" style="width: 180px" />
        <a-button type="primary">查询</a-button>
      </a-space>
    </a-card>

    <a-row :gutter="16">
      <a-col :span="6">
        <a-card title="应发合计">
          <a-statistic title="金额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="实发合计">
          <a-statistic title="金额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="个税">
          <a-statistic title="金额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="B分兑换">
          <a-statistic title="金额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="16" style="margin-top: 16px">
      <a-col :span="8">
        <a-card title="基本/绩效">
          <a-list :data-source="basicItems" size="small">
            <template #renderItem="{ item }">
              <a-list-item>
                <span>{{ item.name }}</span>
                <span>{{ item.amount }}</span>
              </a-list-item>
            </template>
            <template #header><a-empty v-if="!basicItems.length" description="暂无数据" /></template>
          </a-list>
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card title="加项">
          <a-list :data-source="addItems" size="small">
            <template #renderItem="{ item }">
              <a-list-item>
                <span>{{ item.name }}</span>
                <span style="color: var(--color-success)">+{{ item.amount }}</span>
              </a-list-item>
            </template>
            <template #header><a-empty v-if="!addItems.length" description="暂无加项" /></template>
          </a-list>
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card title="扣项">
          <a-list :data-source="deductItems" size="small">
            <template #renderItem="{ item }">
              <a-list-item>
                <span>{{ item.name }}</span>
                <span style="color: var(--color-danger)">-{{ item.amount }}</span>
              </a-list-item>
            </template>
            <template #header><a-empty v-if="!deductItems.length" description="暂无扣项" /></template>
          </a-list>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Dayjs } from 'dayjs'
import type { SalaryPayrollDetail } from '@/types/salary'

const period = ref<Dayjs | undefined>(undefined)

interface PayslipItem {
  name: string
  amount: number
}

const basicItems = ref<PayslipItem[]>([])
const addItems = ref<PayslipItem[]>([])
const deductItems = ref<PayslipItem[]>([])

// TODO: 调用 /api/salary/my-payslip?period=YYYY-MM 加载工资条
// TODO: 根据 SalaryPayrollDetail.f项目类型 进行分组（1=加项 2=扣项 3=基本 4=绩效 等）
// 占位：使用 SalaryPayrollDetail 类型避免未引用警告
const _typeRef: SalaryPayrollDetail | null = null
void _typeRef
</script>

<style scoped>
.salary-my-payslip {
  padding: 16px;
}
</style>
