<template>
  <div class="commission-config-panel">
    <div class="panel-toolbar">
      <span class="panel-title">佣金配置</span>
      <a-button v-if="!readonly" type="primary" size="small" @click="handleAdd">
        <template #icon><PlusOutlined /></template>
        新增配置
      </a-button>
    </div>

    <a-table
      :columns="columns"
      :data-source="commissions"
      :loading="loading"
      :pagination="false"
      row-key="fId"
      bordered
      size="small"
    >
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.dataIndex === 'fEnabled'">
          <a-switch
            :checked="record.fEnabled"
            :disabled="readonly"
            @change="(checked: string | number | boolean) => handleFieldChange(index, 'fEnabled', checked)"
            size="small"
          />
        </template>

        <template v-if="column.dataIndex === 'fCalcMethod'">
          <a-select
            :value="record.fCalcMethod"
            :disabled="readonly"
            @change="(val: SelectValue) => handleFieldChange(index, 'fCalcMethod', val)"
            style="width: 100%"
            size="small"
            :options="calcMethodOptions"
          />
        </template>

        <template v-if="column.dataIndex === 'fValue'">
          <a-input-number
            v-if="record.fCalcMethod === 'percent'"
            :value="record.fRate"
            :disabled="readonly"
            @change="(val: ValueType) => handleFieldChange(index, 'fRate', val)"
            :min="0" :max="100" :precision="2"
            size="small" style="width: 100%"
            placeholder="费率(%)"
          />
          <a-input-number
            v-else-if="record.fCalcMethod === 'fixed'"
            :value="record.fFixedAmount"
            :disabled="readonly"
            @change="(val: ValueType) => handleFieldChange(index, 'fFixedAmount', val)"
            :min="0" :precision="2"
            size="small" style="width: 100%"
            placeholder="固定金额"
          />
          <a-input-number
            v-else
            :value="record.fWeightAmount"
            :disabled="readonly"
            @change="(val: ValueType) => handleFieldChange(index, 'fWeightAmount', val)"
            :min="0" :precision="4"
            size="small" style="width: 100%"
            placeholder="单价(元/kg)"
          />
        </template>

        <template v-if="column.dataIndex === 'fTargetClientType'">
          <a-select
            :value="record.fTargetClientType"
            :disabled="readonly"
            @change="(val: SelectValue) => handleFieldChange(index, 'fTargetClientType', val)"
            style="width: 100%"
            size="small"
            :options="targetTypeOptions"
            allowClear
            placeholder="选择类型"
          />
        </template>

        <template v-if="column.dataIndex === 'fTargetClientId'">
          <!-- 业务对象编号是字符串（如 KH00000001/DL-0001），后端字段为 string，数字输入框无法表达且会 400 -->
          <a-input
            :value="record.fTargetClientId"
            :disabled="readonly"
            @update:value="(val: string) => handleFieldChange(index, 'fTargetClientId', val)"
            size="small" style="width: 100%"
            placeholder="目标对象编号"
            allow-clear
          />
        </template>

        <template v-if="column.dataIndex === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleSaveRow(record as QuotationCommissionDto & { _saving?: boolean; _isNew?: boolean })" :loading="record._saving">
              保存
            </a-button>
            <a-popconfirm title="确定删除此配置？" @confirm="handleDelete(record as QuotationCommissionDto)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import type { ValueType } from 'ant-design-vue/es/input-number/src/utils/MiniDecimal'
import { PlusOutlined } from '@ant-design/icons-vue'
import {
  getQuotationCommissions,
  saveQuotationCommission,
  deleteQuotationCommission,
  type QuotationCommissionDto,
} from '@/api/express'

const props = withDefaults(defineProps<{
  quotationId: number
  readonly?: boolean
}>(), {
  readonly: false,
})

const loading = ref(false)
const commissions = ref<(QuotationCommissionDto & { _saving?: boolean; _isNew?: boolean })[]>([])

const calcMethodOptions = [
  { value: 'percent', label: '百分比' },
  { value: 'fixed', label: '固定金额' },
  { value: 'weight', label: '按重量' },
]

const targetTypeOptions = [
  { value: 'KH', label: '客户' },
  { value: 'DL', label: '代理' },
  { value: 'WD', label: '网点' },
  { value: 'YW', label: '业务员' },
  { value: 'CB', label: '承包区' },
  { value: 'YZ', label: '驿站' },
]

const columns = [
  { title: '启用', dataIndex: 'fEnabled', width: 70, align: 'center' as const },
  { title: '计算方式', dataIndex: 'fCalcMethod', width: 120 },
  { title: '费率/金额', dataIndex: 'fValue', width: 130 },
  { title: '目标客户类型', dataIndex: 'fTargetClientType', width: 130 },
  { title: '目标客户ID', dataIndex: 'fTargetClientId', width: 120 },
  ...(!props.readonly ? [{ title: '操作', dataIndex: 'action', width: 130, align: 'center' as const }] : []),
]

function handleFieldChange(index: number, field: string, value: any) {
  ;(commissions.value[index] as any)[field] = value
}

// 后端 fRate 存小数（0.05 = 5%），计费引擎按 chargeAmount × fRate 直乘；
// UI 按百分比展示编辑，读写时换算（与商务条款 prepayRatio/insuranceRate 同一约定）
function rateToPercent(rate: number | null | undefined): number {
  return rate != null ? Number((rate * 100).toFixed(6)) : 0
}

function percentToRate(percent: number | null | undefined): number {
  return percent != null ? Number((percent / 100).toFixed(8)) : 0
}

function handleAdd() {
  commissions.value.push({
    fId: 0,
    fQuotationId: props.quotationId,
    fEnabled: true,
    fCalcMethod: 'percent',
    fRate: 0,
    fFixedAmount: 0,
    fWeightAmount: 0,
    fTargetClientType: '',
    fTargetClientId: undefined,
    _isNew: true,
  })
}

async function handleSaveRow(record: QuotationCommissionDto & { _saving?: boolean; _isNew?: boolean }) {
  record._saving = true
  try {
    await saveQuotationCommission(props.quotationId, {
      fId: record._isNew ? undefined : record.fId,
      fEnabled: record.fEnabled,
      fCalcMethod: record.fCalcMethod,
      fRate: percentToRate(record.fRate),
      fFixedAmount: record.fFixedAmount,
      fWeightAmount: record.fWeightAmount,
      fTargetClientType: record.fTargetClientType,
      fTargetClientId: record.fTargetClientId || undefined,
    })
    message.success('保存成功')
    await loadData()
  } catch {
    message.error('保存失败')
  } finally {
    record._saving = false
  }
}

async function handleDelete(record: QuotationCommissionDto) {
  if (!record.fId) {
    commissions.value = commissions.value.filter(c => c !== record)
    return
  }
  try {
    await deleteQuotationCommission(record.fId)
    message.success('删除成功')
    await loadData()
  } catch {
    message.error('删除失败')
  }
}

async function loadData() {
  loading.value = true
  try {
    const list = await getQuotationCommissions(props.quotationId)
    // 小数费率 → 百分比展示
    commissions.value = list.map(c => ({ ...c, fRate: rateToPercent(c.fRate) }))
  } catch {
    message.error('加载佣金配置失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  if (props.quotationId) {
    loadData()
  }
})
</script>

<style scoped lang="scss">
.commission-config-panel {
  padding: 4px 0;
}

.panel-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.panel-title {
  font-weight: 500;
  font-size: 14px;
}
</style>
