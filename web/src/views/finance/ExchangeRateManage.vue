<template>
  <div class="exchange-rate-page page-container">
    <PageHeader title="汇率管理">
      <template #actions>
        <a-button type="primary" @click="openAddDialog"><PlusOutlined />新增汇率</a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select
            v-model:value="currentAccountSetId"
            placeholder="请选择账套"
            size="small"
            style="width: 200px"
            @change="loadRates"
            :options="accountSets.map((a: any) => ({ label: a.fName, value: a.id }))"
          />
          <a-select
            v-model:value="filterCurrency"
            placeholder="全部币种"
            allowClear
            size="small"
            style="width: 150px"
            @change="loadRates"
            :options="currencyOptions.map(c => ({ label: `${c.code} ${c.name}`, value: c.code }))"
          />
        </div>
      </template>
    </PageHeader>

    <!-- 表格 -->
    <a-card :bordered="false" class="table-card">
      <a-table
        :columns="columns"
        :dataSource="rateList"
        :loading="loading"
        bordered
        rowKey="id"
        :pagination="false"
      >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'currencyCode'">
          <a-tag color="warning">{{ record.currencyCode }}</a-tag>
        </template>
        <template v-if="column.dataIndex === 'rate'">
          <span class="rate-value">{{ record.rate.toFixed(6) }}</span>
        </template>
        <template v-if="column.dataIndex === 'effectiveDate'">
          {{ formatDate(record.effectiveDate) }}
        </template>
        <template v-if="column.dataIndex === 'createTime'">
          {{ formatDateTime(record.createTime) }}
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button type="link" size="small" @click="openEditDialog(record)">编辑</a-button>
          <a-button type="link" size="small" danger @click="handleDelete(record)">删除</a-button>
        </template>
      </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="editingId ? '编辑汇率' : '新增汇率'"
      :width="480"
      :destroyOnClose="true"
    >
      <a-form
        ref="formRef"
        :model="form"
        :rules="rules"
        :labelCol="{ span: 6 }"
        :wrapperCol="{ span: 16 }"
      >
        <a-form-item label="币种" name="currencyCode">
          <a-select
            v-model:value="form.currencyCode"
            placeholder="请选择币种"
            style="width: 100%"
            @change="onCurrencyChange"
            :options="presetCurrencies.map(c => ({ label: `${c.code} - ${c.name}`, value: c.code }))"
          />
        </a-form-item>
        <a-form-item label="币种名称" name="currencyName">
          <a-input v-model:value="form.currencyName" placeholder="币种中文名称" />
        </a-form-item>
        <a-form-item label="汇率" name="rate">
          <a-input-number
            v-model:value="form.rate"
            :precision="6"
            :min="0.000001"
            :step="0.01"
            style="width: 100%"
            placeholder="1外币=?人民币"
          />
        </a-form-item>
        <a-form-item label="生效日期" name="effectiveDate">
          <a-date-picker
            v-model:value="form.effectiveDate"
            valueFormat="YYYY-MM-DD"
            placeholder="请选择生效日期"
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import type { FormInstance } from 'ant-design-vue'
import {
  getExchangeRates,
  getExchangeCurrencies,
  saveExchangeRate,
  deleteExchangeRate,
  getAccountSets
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

// 预设常用币种
const presetCurrencies = [
  { code: 'USD', name: '美元' },
  { code: 'EUR', name: '欧元' },
  { code: 'JPY', name: '日元' },
  { code: 'HKD', name: '港币' },
  { code: 'GBP', name: '英镑' },
  { code: 'AUD', name: '澳元' },
  { code: 'CAD', name: '加元' },
  { code: 'CHF', name: '瑞士法郎' },
  { code: 'SGD', name: '新加坡元' },
  { code: 'KRW', name: '韩元' },
  { code: 'TWD', name: '新台币' },
  { code: 'MOP', name: '澳门元' },
]

const presetMap: Record<string, string> = Object.fromEntries(
  presetCurrencies.map(c => [c.code, c.name])
)

// 账套列表
const accountSets = ref<any[]>([])
const currentAccountSetId = ref<number>(accountSetStore.currentAccountSetId || 0)

// 筛选
const filterCurrency = ref<string | undefined>(undefined)

// 已录入的币种列表（用于筛选下拉）
const currencyOptions = ref<{ code: string; name: string }[]>([])

// 汇率列表
const rateList = ref<any[]>([])
const loading = ref(false)

// 弹窗
const dialogVisible = ref(false)
const saving = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const columns = [
  { title: '币种代码', dataIndex: 'currencyCode', key: 'currencyCode', width: 110, align: 'center' as const },
  { title: '币种名称', dataIndex: 'currencyName', key: 'currencyName', width: 120, align: 'center' as const },
  { title: '汇率（1外币=?人民币）', dataIndex: 'rate', key: 'rate', align: 'center' as const },
  { title: '生效日期', dataIndex: 'effectiveDate', key: 'effectiveDate', width: 140, align: 'center' as const },
  { title: '录入时间', dataIndex: 'createTime', key: 'createTime', width: 160, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

const form = reactive({
  currencyCode: '' as string | undefined,
  currencyName: '',
  rate: 1,
  effectiveDate: new Date().toISOString().split('T')[0]
})

const rules: Record<string, any[]> = {
  currencyCode: [{ required: true, message: '请选择币种', trigger: 'change' }],
  currencyName: [{ required: true, message: '请输入币种名称', trigger: 'blur' }],
  rate: [{ required: true, message: '请输入汇率', trigger: 'blur' }],
  effectiveDate: [{ required: true, message: '请选择生效日期', trigger: 'change' }]
}

onMounted(async () => {
  await loadAccountSets()
  if (currentAccountSetId.value) {
    await loadRates()
  }
})

async function loadAccountSets() {
  try {
    accountSets.value = await getAccountSets()
    if (!currentAccountSetId.value && accountSets.value.length > 0) {
      const def = accountSets.value.find((a: any) => a.fIsDefault) || accountSets.value[0]
      currentAccountSetId.value = def.id
    }
  } catch (e) {
    console.error(e)
  }
}

async function loadRates() {
  if (!currentAccountSetId.value) return
  loading.value = true
  try {
    rateList.value = await getExchangeRates(currentAccountSetId.value, filterCurrency.value || undefined)
    // 加载币种选项
    const currencies = await getExchangeCurrencies(currentAccountSetId.value)
    currencyOptions.value = currencies
  } catch (e) {
    message.error('加载汇率数据失败')
  } finally {
    loading.value = false
  }
}

function openAddDialog() {
  if (!currentAccountSetId.value) {
    message.warning('请先选择账套')
    return
  }
  editingId.value = null
  Object.assign(form, {
    currencyCode: undefined,
    currencyName: '',
    rate: 1,
    effectiveDate: new Date().toISOString().split('T')[0]
  })
  dialogVisible.value = true
}

function openEditDialog(row: any) {
  editingId.value = row.id
  Object.assign(form, {
    currencyCode: row.currencyCode,
    currencyName: row.currencyName,
    rate: row.rate,
    effectiveDate: row.effectiveDate?.substring(0, 10)
  })
  dialogVisible.value = true
}

function onCurrencyChange(code: any) {
  if (presetMap[code]) {
    form.currencyName = presetMap[code]
  }
}

async function handleSave() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  saving.value = true
  try {
    await saveExchangeRate({
      id: editingId.value,
      accountSetId: currentAccountSetId.value,
      currencyCode: form.currencyCode!,
      currencyName: form.currencyName,
      rate: form.rate,
      effectiveDate: form.effectiveDate || ''
    })
    message.success('保存成功')
    dialogVisible.value = false
    await loadRates()
  } catch (e: any) {
    message.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定删除 ${row.currencyCode}（${row.currencyName}）${formatDate(row.effectiveDate)} 的汇率记录？`,
    onOk: async () => {
      try {
        await deleteExchangeRate(row.id)
        message.success('删除成功')
        await loadRates()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    }
  })
}

function formatDate(dateStr: string) {
  if (!dateStr) return ''
  return dateStr.substring(0, 10)
}

function formatDateTime(dateStr: string) {
  if (!dateStr) return ''
  return dateStr.replace('T', ' ').substring(0, 19)
}
</script>

<style scoped lang="scss">
.exchange-rate-page {
  // 覆盖 page-container 的 padding，保持与其他页面一致
  padding: 0;
}

/* 表格卡片充满剩余空间 */
.table-card {
  flex: 1;
  overflow: hidden;
  min-height: 0;
  display: flex;
  flex-direction: column;

  :deep(.ant-card-body) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: 12px;
  }

  :deep(.ant-table-wrapper) {
    flex: 1;
    overflow: hidden;
    min-height: 0;
  }

  :deep(.ant-table) {
    height: 100%;
  }

  :deep(.ant-table-container) {
    height: 100%;
    display: flex;
    flex-direction: column;
  }

  :deep(.ant-table-body) {
    flex: 1;
    overflow-y: auto !important;
  }
}

.rate-value {
  font-family: 'Courier New', monospace;
  font-size: 14px;
  color: var(--color-warning);
  font-weight: 600;
}
</style>
