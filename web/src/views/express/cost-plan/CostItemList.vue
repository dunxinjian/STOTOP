<template>
  <div class="page-container">
    <PageHeader title="成本项目管理">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="false"
        row-key="id"
        bordered
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'isRebate'">
            <a-tag v-if="record.isRebate" color="green">返利抵减</a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as CostItemDto)">编辑</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="modalVisible"
      :title="isEdit ? '编辑成本项目' : '新增成本项目'"
      :confirm-loading="submitLoading"
      @ok="handleSubmit"
      @cancel="handleCancel"
    >
      <a-form ref="formRef" :model="formState" :rules="formRules" layout="vertical">
        <a-form-item label="编码" name="code">
          <a-input v-model:value="formState.code" :disabled="isEdit" placeholder="请输入编码" />
        </a-form-item>
        <a-form-item label="名称" name="name">
          <a-input v-model:value="formState.name" placeholder="请输入名称" />
        </a-form-item>
        <a-form-item label="是否返利" name="isRebate">
          <a-switch v-model:checked="formState.isRebate" />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formState.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getCostItems,
  createCostItem,
  updateCostItem,
  type CostItemDto,
} from '@/api/express'
import type { FormInstance } from 'ant-design-vue'

const loading = ref(false)
const tableData = ref<CostItemDto[]>([])

const tableColumns = [
  { title: '编码', dataIndex: 'code', width: 150 },
  { title: '名称', dataIndex: 'name', width: 200 },
  { title: '是否返利', dataIndex: 'isRebate', width: 120, align: 'center' as const },
  { title: '排序', dataIndex: 'sortOrder', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 100, align: 'center' as const },
]

async function fetchList() {
  loading.value = true
  try {
    tableData.value = await getCostItems()
  } catch {
    message.error('获取成本项目列表失败')
  } finally {
    loading.value = false
  }
}

// 弹窗相关
const modalVisible = ref(false)
const submitLoading = ref(false)
const isEdit = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive({
  code: '',
  name: '',
  isRebate: false,
  sortOrder: 0,
})

const formRules = {
  code: [{ required: true, message: '请输入编码' }],
  name: [{ required: true, message: '请输入名称' }],
}

function handleAdd() {
  isEdit.value = false
  editingId.value = null
  Object.assign(formState, { code: '', name: '', isRebate: false, sortOrder: 0 })
  modalVisible.value = true
}

function handleEdit(record: CostItemDto) {
  isEdit.value = true
  editingId.value = record.id
  Object.assign(formState, {
    code: record.code,
    name: record.name,
    isRebate: record.isRebate,
    sortOrder: record.sortOrder,
  })
  modalVisible.value = true
}

async function handleSubmit() {
  try {
    await formRef.value?.validateFields()
  } catch {
    return
  }
  submitLoading.value = true
  try {
    if (isEdit.value && editingId.value !== null) {
      await updateCostItem(editingId.value, {
        name: formState.name,
        isRebate: formState.isRebate,
        sortOrder: formState.sortOrder,
      })
      message.success('更新成功')
    } else {
      await createCostItem({
        code: formState.code,
        name: formState.name,
        isRebate: formState.isRebate,
        sortOrder: formState.sortOrder,
      })
      message.success('新增成功')
    }
    modalVisible.value = false
    fetchList()
  } catch {
    message.error(isEdit.value ? '更新失败' : '新增失败')
  } finally {
    submitLoading.value = false
  }
}

function handleCancel() {
  formRef.value?.resetFields()
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
