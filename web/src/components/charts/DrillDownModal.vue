<template>
  <a-modal
    :open="visible"
    :title="title"
    :width="900"
    :centered="true"
    :destroyOnClose="true"
    @cancel="$emit('update:visible', false)"
    :footer="null"
  >
    <a-table
      :columns="drillColumns"
      :dataSource="data"
      rowKey="voucherId"
      :loading="loading"
      :pagination="{ pageSize: 15, showSizeChanger: true, showTotal: (total: number) => `共 ${total} 条` }"
      bordered
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'voucherNo'">
          <a class="voucher-link" @click="goToVoucher(record)">{{ record.voucherNo }}</a>
        </template>
        <template v-if="column.dataIndex === 'voucherDate'">
          {{ formatDate(record.voucherDate) }}
        </template>
        <template v-if="column.dataIndex === 'debitAmount'">
          <span v-if="record.debitAmount">{{ formatMoney(record.debitAmount) }}</span>
          <span v-else>-</span>
        </template>
        <template v-if="column.dataIndex === 'creditAmount'">
          <span v-if="record.creditAmount">{{ formatMoney(record.creditAmount) }}</span>
          <span v-else>-</span>
        </template>
      </template>
    </a-table>
  </a-modal>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

defineProps<{
  visible: boolean
  title: string
  data: any[]
  loading?: boolean
}>()

defineEmits(['update:visible'])

const router = useRouter()

const drillColumns = [
  { title: '凭证号', dataIndex: 'voucherNo', key: 'voucherNo', width: 100 },
  { title: '日期', dataIndex: 'voucherDate', key: 'voucherDate', width: 110 },
  { title: '科目编码', dataIndex: 'accountCode', key: 'accountCode', width: 100 },
  { title: '科目名称', dataIndex: 'accountName', key: 'accountName', width: 140 },
  { title: '摘要', dataIndex: 'summary', key: 'summary', ellipsis: true },
  { title: '借方金额', dataIndex: 'debitAmount', key: 'debitAmount', width: 120, align: 'right' as const },
  { title: '贷方金额', dataIndex: 'creditAmount', key: 'creditAmount', width: 120, align: 'right' as const },
]

function formatDate(val: string) {
  if (!val) return ''
  return val.substring(0, 10)
}

function formatMoney(val: number) {
  if (!val) return '-'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function goToVoucher(record: any) {
  if (record.voucherId) {
    router.push({ name: 'VoucherEdit', params: { id: record.voucherId } })
  }
}
</script>

<style scoped>
.voucher-link {
  color: var(--text-1);
  cursor: pointer;
}
.voucher-link:hover {
  color: var(--color-primary);
  text-decoration: underline;
}
</style>
