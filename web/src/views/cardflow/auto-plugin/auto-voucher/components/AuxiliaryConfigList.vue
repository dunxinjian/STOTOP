<!-- 辅助核算配置：动态增删列表，支持固定/动态来源切换和匹配方式选择 -->
<template>
  <div class="auxiliary-config-list">
    <div v-for="(item, idx) in list" :key="idx" class="aux-item">
      <!-- 辅助类型选择 -->
      <a-select
        v-model:value="item.name"
        :options="auxTypeOptions"
        placeholder="辅助类型"
        size="small"
        style="width: 120px;"
        show-search
        :filter-option="filterOption"
        @change="() => onAuxNameChange(item)"
      />

      <!-- 来源切换 -->
      <a-radio-group
        v-model:value="item.type"
        size="small"
        button-style="solid"
        @change="() => onAuxSourceChange(item)"
      >
        <a-radio-button value="fixed">固定值</a-radio-button>
        <a-radio-button value="field">动态字段</a-radio-button>
      </a-radio-group>

      <!-- 固定值：值选择器 -->
      <template v-if="item.type === 'fixed'">
        <a-select
          v-model:value="item.value"
          placeholder="选择辅助核算项目"
          size="small"
          style="flex: 1; min-width: 120px;"
          show-search
          :filter-option="(input: string, option: any) => option.label?.toLowerCase().includes(input.toLowerCase())"
          :options="getAuxItemOptions(item.name)"
          allow-clear
          @focus="onAuxFocus(item)"
          @change="syncToParent"
        />
      </template>

      <!-- 动态字段：字段选择 + 匹配方式 -->
      <template v-else>
        <a-select
          v-model:value="item.field"
          placeholder="选择字段"
          size="small"
          style="min-width: 120px;"
          show-search
          allow-clear
          @change="syncToParent"
        >
          <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
        </a-select>
        <a-select
          v-model:value="item.matchMode"
          placeholder="匹配方式"
          size="small"
          style="width: 150px;"
          allow-clear
          @change="syncToParent"
        >
          <a-select-option value="exact_code">按编码精确匹配</a-select-option>
          <a-select-option value="exact_name">按名称精确匹配</a-select-option>
          <a-select-option value="source_contains_name">源数据包含项目名</a-select-option>
          <a-select-option value="name_contains_source">项目名包含源数据</a-select-option>
        </a-select>
      </template>

      <!-- 删除按钮 -->
      <a-button type="text" danger size="small" @click="removeItem(idx)">
        <DeleteOutlined />
      </a-button>
    </div>

    <!-- 空状态 -->
    <a-empty
      v-if="list.length === 0"
      :image="simpleImage"
      description="暂无辅助核算项"
    />

    <!-- 添加按钮 -->
    <a-button
      type="dashed"
      block
      size="small"
      style="margin-top: 8px;"
      @click="addItem"
    >
      <PlusOutlined /> 添加核算项
    </a-button>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { Empty } from 'ant-design-vue'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'
import type { AuxiliaryConfig } from '@/stores/autoVoucherRule'

const simpleImage = Empty.PRESENTED_IMAGE_SIMPLE
const store = useAutoVoucherRuleStore()

const props = defineProps<{
  /** 辅助核算配置列表 */
  modelValue: AuxiliaryConfig[]
  /** 可用字段列表（暂存表列名） */
  fields: string[]
  /** 账套 ID，用于加载辅助核算项 */
  accountSetId?: number
}>()

const emit = defineEmits<{
  'update:modelValue': [value: AuxiliaryConfig[]]
}>()

// ==================== 内部列表（深拷贝，编辑后 emit） ====================
const list = ref<AuxiliaryConfig[]>(JSON.parse(JSON.stringify(props.modelValue || [])))

// 仅在父组件整体替换时同步，避免深度监听导致的循环
watch(() => props.modelValue, (val) => {
  list.value = JSON.parse(JSON.stringify(val || []))
}, { immediate: false })

function syncToParent() {
  emit('update:modelValue', JSON.parse(JSON.stringify(list.value)))
}

// ==================== 辅助类型选项 ====================
const auxTypeOptions = [
  { value: '客户', label: '客户' },
  { value: '供应商', label: '供应商' },
  { value: '部门', label: '部门' },
  { value: '项目', label: '项目' },
  { value: '员工', label: '员工' },
  { value: '经营单元', label: '经营单元' },
  { value: '快递品牌', label: '快递品牌' },
  { value: '网点', label: '网点' },
]

const auxTypeMap: Record<string, string> = {
  '客户': 'customer', '供应商': 'supplier', '员工': 'employee',
  '部门': 'department', '项目': 'project', '经营单元': 'business_unit', '快递品牌': 'express_brand', '网点': 'outlet',
}

// ==================== 操作方法 ====================
function addItem() {
  list.value.push({ name: '', type: 'fixed', value: '', field: '' })
  syncToParent()
}

function removeItem(idx: number) {
  list.value.splice(idx, 1)
  syncToParent()
}

function onAuxNameChange(item: AuxiliaryConfig) {
  item.value = ''
  item.field = ''
  if (item.type === 'fixed') {
    onAuxFocus(item)
  }
  syncToParent()
}

function onAuxSourceChange(item: AuxiliaryConfig) {
  if (item.type === 'fixed' && item.name) {
    onAuxFocus(item)
  }
  syncToParent()
}

function onAuxFocus(item: AuxiliaryConfig) {
  const trimmed = item.name?.trim()
  if (!trimmed) return
  const auxType = auxTypeMap[trimmed]
  if (auxType) store.loadAuxItems(trimmed, auxType)
}

function getAuxItemOptions(auxName: string): Array<{ value: string; label: string }> {
  const trimmed = auxName?.trim()
  if (!trimmed) return []
  return (store.auxItemsCache[trimmed] || []) as Array<{ value: string; label: string }>
}

function filterOption(input: string, option: any) {
  const label = option?.label || option?.children?.[0]?.children || ''
  return String(label).toLowerCase().includes(input.toLowerCase())
}
</script>

<style lang="scss" scoped>
.auxiliary-config-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.aux-item {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  padding: 8px;
  background: var(--bg-muted);
  border-radius: 4px;
  border: 1px solid var(--border);
}
</style>
