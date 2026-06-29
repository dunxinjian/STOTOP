<template>
  <a-modal
    :open="dialogVisible"
    :title="dialogTitle"
    :width="680"
    :destroy-on-close="true"
    :centered="true"
    class="auxiliary-picker-dialog"
    @cancel="handleClose"
  >
    <!-- 顶部工具栏 -->
    <div class="picker-toolbar">
      <a-input
        v-model:value="searchKeyword"
        placeholder="搜索编码/名称..."
        allow-clear
        style="width: 220px"
        @input="handleSearch"
      >
        <template #prefix>
          <SearchOutlined />
        </template>
      </a-input>
      <a-button v-if="!isSupplierType" type="primary" ghost @click="showAddForm = true">
        <template #icon><PlusOutlined /></template>快速新增
      </a-button>
    </div>

    <!-- 辅助项列表 -->
    <a-spin :spinning="loading">
      <div class="picker-list">
        <a-table
          :columns="tableColumns"
          :data-source="filteredItems"
          :scroll="{ y: 320 }"
          :pagination="false"
          row-key="id"
          :row-class-name="getRowClass"
          :custom-row="customRow"
          size="small"
        />
      </div>
    </a-spin>

    <!-- 快速新增表单（内嵌） -->
    <div v-if="showAddForm" class="quick-add-form">
      <div class="quick-add-title">
        <span>快速新增{{ typeLabel }}</span>
        <CloseOutlined class="close-icon" @click="showAddForm = false" />
      </div>
      <a-form ref="addFormRef" :model="addForm" :rules="addRules" layout="horizontal" :label-col="{ style: { width: '72px' } }" size="small">
        <a-row :gutter="12">
          <a-col :span="12">
            <a-form-item label="编码" name="code">
              <a-input v-model:value="addForm.code" placeholder="请输入编码" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="名称" name="name">
              <a-input v-model:value="addForm.name" placeholder="请输入名称" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="简称">
              <a-input v-model:value="addForm.shortName" placeholder="可选" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="联系人">
              <a-input v-model:value="addForm.contact" placeholder="可选" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="电话">
              <a-input v-model:value="addForm.phone" placeholder="可选" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item>
              <a-button type="primary" size="small" @click="handleQuickAdd" :loading="addLoading">
                保存并选择
              </a-button>
              <a-button size="small" style="margin-left: 8px" @click="showAddForm = false">取消</a-button>
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
    </div>

    <template #footer>
      <div class="picker-footer">
        <span class="selected-info" v-if="currentSelected">
          已选：<strong>{{ currentSelected.code }} {{ currentSelected.name }}</strong>
        </span>
        <div class="footer-btns">
          <a-button @click="handleClose">取 消</a-button>
          <a-button type="primary" @click="handleConfirm" :disabled="!currentSelected">确 定</a-button>
        </div>
      </div>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import { SearchOutlined, PlusOutlined, CloseOutlined } from '@ant-design/icons-vue'
import { getAuxiliaryItemsByAccountSet, createAuxiliaryItem } from '@/api/finance'
import { getAllEnabledSuppliers } from '@/api/supplier'
import { AUX_TYPE_LABELS } from '@/constants/auxTypes'

interface AuxItem {
  id: number
  code: string
  name: string
  shortName?: string
  contact?: string
  phone?: string
  auxType?: string
  accountSetId?: number
}

const props = defineProps<{
  visible: boolean
  auxType: string
  accountSetId: number
  modelValue?: AuxItem | null
}>()

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'update:modelValue', val: AuxItem | null): void
  (e: 'select', val: AuxItem): void
}>()

// 弹窗显隐
const dialogVisible = computed({
  get: () => props.visible,
  set: (val) => emit('update:visible', val)
})

// 辅助类型标签映射统一收敛到 @/constants/auxTypes（单一真源）
const typeLabel = computed(() => AUX_TYPE_LABELS[props.auxType] || '辅助项')
const dialogTitle = computed(() => `选择${typeLabel.value}`)

// 供应商类型：数据从供应商管理模块获取，不允许快速新增
const isSupplierType = computed(() => props.auxType === 'supplier')

// 列表数据
const loading = ref(false)
const allItems = ref<AuxItem[]>([])
const searchKeyword = ref('')
const currentSelected = ref<AuxItem | null>(props.modelValue || null)

// 表格列定义
const tableColumns = [
  { title: '编码', dataIndex: 'code', key: 'code', width: 120 },
  { title: '名称', dataIndex: 'name', key: 'name', ellipsis: true },
  { title: '简称', dataIndex: 'shortName', key: 'shortName', width: 120 },
  { title: '联系人', dataIndex: 'contact', key: 'contact', width: 100 },
  { title: '电话', dataIndex: 'phone', key: 'phone', width: 130 },
]

// 搜索过滤
const filteredItems = computed(() => {
  if (!searchKeyword.value) return allItems.value
  const kw = searchKeyword.value.toLowerCase()
  return allItems.value.filter(item =>
    item.code.toLowerCase().includes(kw) ||
    item.name.toLowerCase().includes(kw) ||
    (item.shortName && item.shortName.toLowerCase().includes(kw))
  )
})

// 快速新增表单
const showAddForm = ref(false)
const addLoading = ref(false)
const addFormRef = ref()
const addForm = ref({
  code: '',
  name: '',
  shortName: '',
  contact: '',
  phone: ''
})

const addRules: Record<string, any[]> = {
  code: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }]
}

// 自定义行事件
function customRow(record: AuxItem) {
  return {
    onClick: () => handleRowSelect(record),
    onDblclick: () => handleRowDblClick(record),
    style: { cursor: 'pointer' },
  }
}

// 加载列表
async function loadItems() {
  if (!props.auxType) return
  loading.value = true
  try {
    // 供应商类型：数据从供应商管理模块获取
    if (isSupplierType.value) {
      const suppliers = await getAllEnabledSuppliers()
      allItems.value = (suppliers || []).map((s: any) => ({
        id: s.id,
        code: s.code,
        name: s.name,
        shortName: s.shortName,
        contact: s.contact,
        phone: s.phone,
        auxType: 'supplier',
        accountSetId: undefined
      }))
    } else {
      if (!props.accountSetId) return
      const res = await getAuxiliaryItemsByAccountSet({
        accountSetId: props.accountSetId,
        auxType: props.auxType
      }) as any[]
      allItems.value = (res || []).map((item: any) => ({
        id: item.id,
        code: item.code,
        name: item.name,
        shortName: item.shortName,
        contact: item.contact,
        phone: item.phone,
        auxType: item.auxType,
        accountSetId: item.accountSetId
      }))
    }
  } catch (e) {
    console.error('加载辅助核算项失败', e)
  } finally {
    loading.value = false
  }
}

// 搜索
function handleSearch() {
  // 由 computed 自动过滤
}

// 行选择
function handleRowSelect(row: AuxItem | null) {
  currentSelected.value = row
}

// 双击直接确认
function handleRowDblClick(row: AuxItem) {
  currentSelected.value = row
  handleConfirm()
}

// 获取行样式
function getRowClass(record: AuxItem) {
  if (currentSelected.value && currentSelected.value.id === record.id) {
    return 'selected-row'
  }
  return ''
}

// 确认选择
function handleConfirm() {
  if (!currentSelected.value) return
  emit('update:modelValue', currentSelected.value)
  emit('select', currentSelected.value)
  dialogVisible.value = false
}

// 关闭
function handleClose() {
  dialogVisible.value = false
  showAddForm.value = false
  searchKeyword.value = ''
}

// 快速新增
async function handleQuickAdd() {
  const valid = await addFormRef.value?.validate().catch(() => false)
  if (!valid) return

  addLoading.value = true
  try {
    const res = await createAuxiliaryItem({
      accountSetId: props.accountSetId,
      auxType: props.auxType,
      code: addForm.value.code,
      name: addForm.value.name,
      shortName: addForm.value.shortName || undefined,
      contact: addForm.value.contact || undefined,
      phone: addForm.value.phone || undefined
    }) as any

    message.success('新增成功')
    showAddForm.value = false

    // 重置表单
    addForm.value = { code: '', name: '', shortName: '', contact: '', phone: '' }
    addFormRef.value?.resetFields()

    // 刷新列表并自动选中新增项
    await loadItems()
    const newItem = allItems.value.find(i => i.code === (res?.code || addForm.value.code))
    if (newItem) {
      currentSelected.value = newItem
    }
  } catch (e: any) {
    message.error(e?.message || '新增失败')
  } finally {
    addLoading.value = false
  }
}

// 监听 visible 打开时加载数据
watch(() => props.visible, (val) => {
  if (val) {
    currentSelected.value = props.modelValue || null
    searchKeyword.value = ''
    showAddForm.value = false
    loadItems()
  }
})

// 监听 modelValue 外部更新
watch(() => props.modelValue, (val) => {
  currentSelected.value = val || null
})
</script>

<style scoped lang="scss">
.picker-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.picker-list {
  border: 1px solid #e4e7ed;
  border-radius: 4px;

  :deep(.ant-table-row) {
    cursor: pointer;
  }

  :deep(.selected-row) {
    td {
      background-color: var(--bg-muted) !important;
    }
  }

  :deep(.ant-table-row:hover > td) {
    background-color: #f5f7fa;
  }
}

.quick-add-form {
  margin-top: 16px;
  padding: 14px 16px;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  background: #fafbfc;

  .quick-add-title {
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 14px;
    font-weight: 500;
    color: #333;
    margin-bottom: 12px;

    .close-icon {
      cursor: pointer;
      color: #909399;
      &:hover { color: var(--color-primary); }
    }
  }

  :deep(.ant-form-item) {
    margin-bottom: 10px;
  }
}

.picker-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;

  .selected-info {
    font-size: 13px;
    color: #606266;

    strong {
      color: var(--color-info);
    }
  }

  .footer-btns {
    display: flex;
    gap: 8px;
  }
}
</style>
