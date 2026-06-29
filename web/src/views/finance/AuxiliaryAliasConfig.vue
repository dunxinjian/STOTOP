<template>
  <div class="page-container">
    <PageHeader title="辅助别名配置">
      <template #actions>
        <a-select
          v-model:value="filterAuxType"
          placeholder="全部辅助类型"
          allowClear
          style="width: 160px; margin-right: 12px;"
          :options="auxTypeOptions"
          @change="loadData"
        />
        <a-button type="primary" @click="handleAdd">
          <PlusOutlined />新增别名
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        rowKey="id"
        :loading="loading"
        bordered
        size="small"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'auxType'">
            {{ auxTypeLabel(record.auxType) }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as AuxiliaryAliasDto)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record as AuxiliaryAliasDto)">删除</a-button>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      :open="dialogVisible"
      :title="isEdit ? '编辑别名' : '新增别名'"
      width="500px"
      :destroyOnClose="true"
      @cancel="dialogVisible = false"
      @ok="handleSubmit"
      :confirmLoading="submitLoading"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ span: 5 }">
        <a-form-item label="辅助类型" name="auxType">
          <a-select
            v-model:value="formData.auxType"
            placeholder="请选择辅助类型"
            :options="auxTypeOptions"
            style="width: 100%"
            :disabled="isEdit"
            @change="handleAuxTypeChange"
          />
        </a-form-item>
        <a-form-item label="辅助项" name="auxiliaryItemId">
          <a-select
            v-model:value="formData.auxiliaryItemId"
            placeholder="请选择辅助项"
            style="width: 100%"
            :loading="itemsLoading"
            showSearch
            :filterOption="filterOption"
            :options="auxiliaryItems.map(item => ({ label: `${item.code} - ${item.name}`, value: item.id }))"
          />
        </a-form-item>
        <a-form-item label="别名" name="alias">
          <a-input v-model:value="formData.alias" placeholder="请输入别名" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getAuxiliaryAliases,
  createAuxiliaryAlias,
  updateAuxiliaryAlias,
  deleteAuxiliaryAlias,
  getAuxiliaryItemsByAccountSet,
} from '@/api/finance'
import type { AuxiliaryAliasDto } from '@/api/finance'
import type { SelectValue } from 'ant-design-vue/es/select'
import { useAccountSetStore } from '@/stores/accountSet'

const accountSetStore = useAccountSetStore()

const auxTypeOptions = [
  { value: 'customer', label: '客户' },
  { value: 'supplier', label: '供应商' },
  { value: 'department', label: '部门' },
  { value: 'project', label: '项目' },
  { value: 'employee', label: '员工' },
  { value: 'business_unit', label: '经营单元' },
  { value: 'express_brand', label: '快递品牌' },
]

const auxTypeLabel = (val: string) => {
  return auxTypeOptions.find(o => o.value === val)?.label || val
}

const filterOption = (input: string, option: any) => {
  return option.label.toLowerCase().includes(input.toLowerCase())
}

const loading = ref(false)
const tableData = ref<AuxiliaryAliasDto[]>([])
const filterAuxType = ref<string | undefined>(undefined)

const columns = [
  { title: '辅助类型', dataIndex: 'auxType', width: 120 },
  { title: '辅助项编码', dataIndex: 'auxiliaryItemCode', width: 140 },
  { title: '辅助项名称', dataIndex: 'auxiliaryItemName', width: 180 },
  { title: '别名', dataIndex: 'alias', width: 200 },
  { title: '操作', dataIndex: 'action', width: 140 },
]

const dialogVisible = ref(false)
const isEdit = ref(false)
const submitLoading = ref(false)
const formRef = ref()
const itemsLoading = ref(false)
const auxiliaryItems = ref<Array<{ id: number; code: string; name: string }>>([])

const formData = reactive({
  id: '',
  auxType: undefined as string | undefined,
  auxiliaryItemId: undefined as number | undefined,
  alias: '',
})

const formRules = {
  auxType: [{ required: true, message: '请选择辅助类型' }],
  auxiliaryItemId: [{ required: true, message: '请选择辅助项' }],
  alias: [{ required: true, message: '请输入别名' }],
}

async function loadData() {
  loading.value = true
  try {
    const params = filterAuxType.value ? { auxType: filterAuxType.value } : undefined
    tableData.value = await getAuxiliaryAliases(params)
  } catch (e: any) {
    message.error(e.message || '加载失败')
  } finally {
    loading.value = false
  }
}

function handleAdd() {
  isEdit.value = false
  formData.id = ''
  formData.auxType = undefined
  formData.auxiliaryItemId = undefined
  formData.alias = ''
  auxiliaryItems.value = []
  dialogVisible.value = true
}

function handleEdit(record: AuxiliaryAliasDto) {
  isEdit.value = true
  formData.id = record.id
  formData.auxType = record.auxType
  formData.auxiliaryItemId = record.auxiliaryItemId
  formData.alias = record.alias
  loadAuxiliaryItems(record.auxType)
  dialogVisible.value = true
}

function handleDelete(record: AuxiliaryAliasDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除别名"${record.alias}"吗？`,
    okType: 'danger',
    async onOk() {
      await deleteAuxiliaryAlias(record.id)
      message.success('删除成功')
      loadData()
    },
  })
}

async function handleAuxTypeChange(value: SelectValue) {
  const val = value as string | undefined
  formData.auxiliaryItemId = undefined
  if (val) {
    await loadAuxiliaryItems(val)
  } else {
    auxiliaryItems.value = []
  }
}

async function loadAuxiliaryItems(auxType: string) {
  itemsLoading.value = true
  try {
    const accountSetId = accountSetStore.currentAccountSetId || 0
    const res = await getAuxiliaryItemsByAccountSet({ accountSetId, auxType })
    auxiliaryItems.value = (res as any) || []
  } catch (e: any) {
    message.error('加载辅助项失败')
    auxiliaryItems.value = []
  } finally {
    itemsLoading.value = false
  }
}

async function handleSubmit() {
  try {
    await formRef.value.validateFields()
  } catch {
    return
  }

  submitLoading.value = true
  try {
    const data: Partial<AuxiliaryAliasDto> = {
      auxType: formData.auxType,
      auxiliaryItemId: formData.auxiliaryItemId,
      alias: formData.alias,
    }
    if (isEdit.value) {
      await updateAuxiliaryAlias(formData.id, data)
      message.success('更新成功')
    } else {
      await createAuxiliaryAlias(data)
      message.success('创建成功')
    }
    dialogVisible.value = false
    loadData()
  } catch (e: any) {
    message.error(e.message || '操作失败')
  } finally {
    submitLoading.value = false
  }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.page-container {
  padding: 0;
}
</style>
