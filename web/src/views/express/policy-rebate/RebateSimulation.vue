<template>
  <div class="page-container">
    <PageHeader title="返利测算" />

    <a-row :gutter="16">
      <!-- 左侧配置区 -->
      <a-col :span="10">
        <a-card title="测算配置" :bordered="false">
          <a-form :label-col="{ style: { width: '100px' } }">
            <a-form-item label="返利方案ID" required>
              <a-input-number v-model:value="simForm.policyRebateId" style="width: 100%" placeholder="请输入返利方案ID" />
            </a-form-item>
            <a-form-item label="测算方式">
              <a-radio-group v-model:value="simForm.useHistory">
                <a-radio :value="true">基于历史数据</a-radio>
                <a-radio :value="false">基于假设数据</a-radio>
              </a-radio-group>
            </a-form-item>

            <!-- 历史数据模式 -->
            <template v-if="simForm.useHistory">
              <a-form-item label="时间范围" required>
                <a-range-picker v-model:value="simForm.dateRange" style="width: 100%" />
              </a-form-item>
            </template>

            <!-- 假设数据模式 -->
            <template v-else>
              <a-form-item label="日均单量">
                <a-input-number v-model:value="simForm.assumedDailyVolume" :min="0" style="width: 100%" placeholder="请输入日均单量" />
              </a-form-item>
              <a-form-item label="测算天数">
                <a-input-number v-model:value="simForm.assumedDays" :min="1" style="width: 100%" placeholder="请输入测算天数" />
              </a-form-item>
              <a-form-item label="假设均重(kg)">
                <a-input-number v-model:value="simForm.assumedAvgWeight" :min="0" :precision="2" style="width: 100%" placeholder="请输入假设均重" />
              </a-form-item>
            </template>

            <a-form-item :wrapper-col="{ offset: 4 }">
              <a-button type="primary" :loading="simLoading" @click="handleSimulate" block>
                <template #icon><CalculatorOutlined /></template>执行测算
              </a-button>
            </a-form-item>
          </a-form>
        </a-card>
      </a-col>

      <!-- 右侧结果区 -->
      <a-col :span="14">
        <a-card title="测算结果" :bordered="false">
          <a-empty v-if="!result" description="请配置参数后执行测算" />
          <template v-else>
            <a-row :gutter="16" style="margin-bottom: 16px">
              <a-col :span="8">
                <a-statistic title="总运单数" :value="result.totalWaybills" />
              </a-col>
              <a-col :span="8">
                <a-statistic title="总重量(kg)" :value="result.totalWeight" :precision="2" />
              </a-col>
              <a-col :span="8">
                <a-statistic title="均重(kg)" :value="result.avgWeight" :precision="2" />
              </a-col>
            </a-row>

            <a-row :gutter="16" style="margin-bottom: 16px">
              <a-col :span="8">
                <a-statistic title="基础返利" :value="result.baseRebateAmount" :precision="2" prefix="¥" :value-style="{ color: 'var(--color-info)' }" />
              </a-col>
              <a-col :span="8">
                <a-statistic title="奖励金额" :value="result.totalReward" :precision="2" prefix="¥" :value-style="{ color: 'var(--color-success)' }" />
              </a-col>
              <a-col :span="8">
                <a-statistic title="处罚金额" :value="result.totalPenalty" :precision="2" prefix="¥" :value-style="{ color: 'var(--color-danger)' }" />
              </a-col>
            </a-row>

            <a-row style="margin-bottom: 20px">
              <a-col :span="24" style="text-align: center">
                <a-statistic title="最终返利金额" :value="result.finalRebateAmount" :precision="2" prefix="¥"
                  :value-style="{ color: 'var(--color-info)', fontSize: '28px', fontWeight: 700 }" />
              </a-col>
            </a-row>

            <a-divider>奖罚调整明细</a-divider>
            <a-descriptions v-if="result.adjustments.length === 0" :column="1">
              <a-descriptions-item label="调整项">无</a-descriptions-item>
            </a-descriptions>
            <a-table v-else :columns="adjustColumns" :data-source="result.adjustments" :pagination="false" size="small" bordered row-key="ruleName">
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'ruleType'">
                  {{ ruleTypeText(record.ruleType) }}
                </template>
                <template v-if="column.dataIndex === 'adjustType'">
                  <a-tag :color="record.adjustType === 1 ? 'green' : 'red'">{{ record.adjustType === 1 ? '奖励' : '处罚' }}</a-tag>
                </template>
                <template v-if="column.dataIndex === 'adjustAmount'">
                  <span :style="{ color: record.adjustType === 1 ? 'var(--color-success)' : 'var(--color-danger)' }">
                    {{ record.adjustType === 1 ? '+' : '-' }}¥{{ (record.adjustAmount ?? 0).toFixed(2) }}
                  </span>
                </template>
              </template>
            </a-table>
          </template>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { message } from 'ant-design-vue'
import type { Dayjs } from 'dayjs'
import { CalculatorOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  simulateRebate,
  type SimulationResult,
} from '@/api/express'

const simForm = reactive({
  policyRebateId: undefined as number | undefined,
  useHistory: true,
  dateRange: undefined as [Dayjs, Dayjs] | undefined,
  assumedDailyVolume: undefined as number | undefined,
  assumedDays: undefined as number | undefined,
  assumedAvgWeight: undefined as number | undefined,
})

const simLoading = ref(false)
const result = ref<SimulationResult | null>(null)

function ruleTypeText(t: number) {
  return ['', '均重', '单量', '重量段占比', '目的地流向', '计泡'][t] ?? '未知'
}

const adjustColumns = [
  { title: '规则名称', dataIndex: 'ruleName', width: 140 },
  { title: '规则类型', dataIndex: 'ruleType', width: 100 },
  { title: '实际值', dataIndex: 'actualValue', width: 100, align: 'right' as const },
  { title: '调整类型', dataIndex: 'adjustType', width: 90, align: 'center' as const },
  { title: '调整金额', dataIndex: 'adjustAmount', width: 120, align: 'right' as const },
]

async function handleSimulate() {
  if (!simForm.policyRebateId) {
    message.warning('请输入返利方案ID')
    return
  }
  if (simForm.useHistory && !simForm.dateRange) {
    message.warning('请选择时间范围')
    return
  }

  simLoading.value = true
  try {
    result.value = await simulateRebate({
      policyRebateId: simForm.policyRebateId,
      useHistory: simForm.useHistory,
      periodStart: simForm.useHistory ? simForm.dateRange![0].format('YYYY-MM-DD') : undefined,
      periodEnd: simForm.useHistory ? simForm.dateRange![1].format('YYYY-MM-DD') : undefined,
      assumedDailyVolume: !simForm.useHistory ? simForm.assumedDailyVolume : undefined,
      assumedDays: !simForm.useHistory ? simForm.assumedDays : undefined,
      assumedAvgWeight: !simForm.useHistory ? simForm.assumedAvgWeight : undefined,
    })
    message.success('测算完成')
  } catch {
    message.error('测算失败')
  } finally {
    simLoading.value = false
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
