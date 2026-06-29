<!-- 全局配置抽屉：匹配字段、凭证生成、数据控制等全局设置 -->
<template>
  <a-drawer
    :open="open"
    title="规则全局设置"
    :width="560"
    placement="right"
    destroy-on-close
    @close="emit('update:open', false)"
  >
    <a-form layout="vertical" size="small" :model="formState">
      <!-- 匹配字段配置 -->
      <a-divider orientation="left">匹配字段配置</a-divider>
      <a-row :gutter="12">
        <a-col :span="8">
          <a-form-item label="Layer1 字段">
            <a-select
              v-model:value="formState.layer1Field"
              placeholder="精确编码字段"
              allow-clear
              show-search
              @change="syncToStore"
            >
              <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="Layer2 字段">
            <a-select
              v-model:value="formState.layer2Field"
              placeholder="分类字段"
              allow-clear
              show-search
              @change="syncToStore"
            >
              <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="Layer3 字段">
            <a-select
              v-model:value="formState.layer3Field"
              placeholder="摘要关键词字段"
              allow-clear
              show-search
              @change="syncToStore"
            >
              <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>

      <!-- 凭证生成配置 -->
      <a-divider orientation="left">凭证生成配置</a-divider>
      <a-row :gutter="12">
        <a-col :span="8">
          <a-form-item label="分组字段 (GroupBy)">
            <a-select
              v-model:value="formState.groupBy"
              placeholder="选择分组字段"
              allow-clear
              show-search
              @change="syncToStore"
            >
              <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="日期字段">
            <a-select
              v-model:value="formState.dateField"
              placeholder="选择日期字段"
              allow-clear
              show-search
              @change="syncToStore"
            >
              <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="凭证字">
            <a-input
              v-model:value="formState.voucherWord"
              placeholder="如：记、收、付、转"
              @change="syncToStore"
            />
          </a-form-item>
        </a-col>
      </a-row>
      <a-row :gutter="12">
        <a-col :span="8">
          <a-form-item label="账套 (AccountSetId)">
            <a-select
              v-model:value="formState.accountSetId"
              placeholder="选择账套"
              show-search
              @change="onAccountSetChange"
            >
              <a-select-option v-for="s in accountSets" :key="s.id" :value="s.id">
                {{ s.fName }}
              </a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>

      <!-- 数据控制 -->
      <a-divider orientation="left">数据控制</a-divider>
      <a-row :gutter="12">
        <a-col :span="12">
          <a-form-item label="去重键 (KeyFields)">
            <a-select
              v-model:value="formState.keyFields"
              mode="tags"
              placeholder="选择或输入去重键字段"
              :options="fieldOptions"
              :filter-option="filterOption"
              allow-clear
              @change="syncToStore"
            />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="未匹配行处理">
            <a-select
              v-model:value="formState.unmatchedAction"
              placeholder="选择处理策略"
              @change="syncToStore"
            >
              <a-select-option value="skip">跳过 (skip)</a-select-option>
              <a-select-option value="error">报错 (error)</a-select-option>
              <a-select-option value="default">使用默认科目 (default)</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>
      <a-row :gutter="12">
        <a-col :span="12">
          <a-form-item label="日期回退策略">
            <a-select
              v-model:value="formState.dateFallbackStrategy"
              placeholder="选择策略"
              allow-clear
              @change="syncToStore"
            >
              <a-select-option value="none">不回退</a-select-option>
              <a-select-option value="previousDay">回退到前一工作日</a-select-option>
              <a-select-option value="monthEnd">回退到月末</a-select-option>
            </a-select>
          </a-form-item>
        </a-col>
      </a-row>

      <!-- 筛选条件（动态增删表格） -->
      <a-divider orientation="left">筛选条件</a-divider>
      <div class="filter-conditions">
        <div v-for="(cond, idx) in formState.filterConditions" :key="idx" class="filter-row">
          <a-select
            v-model:value="cond.field"
            placeholder="字段"
            size="small"
            style="width: 140px;"
            show-search
            allow-clear
            @change="syncToStore"
          >
            <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
          </a-select>
          <a-select
            v-model:value="cond.operator"
            placeholder="操作"
            size="small"
            style="width: 100px;"
            @change="syncToStore"
          >
            <a-select-option value="eq">等于</a-select-option>
            <a-select-option value="neq">不等于</a-select-option>
            <a-select-option value="contains">包含</a-select-option>
            <a-select-option value="notContains">不包含</a-select-option>
            <a-select-option value="gt">大于</a-select-option>
            <a-select-option value="lt">小于</a-select-option>
          </a-select>
          <a-input
            v-model:value="cond.value"
            placeholder="值"
            size="small"
            style="flex: 1; min-width: 80px;"
            @change="syncToStore"
          />
          <a-button type="text" danger size="small" @click="removeFilterCondition(idx)">
            <DeleteOutlined />
          </a-button>
        </div>
        <a-button type="dashed" block size="small" @click="addFilterCondition">
          <PlusOutlined /> 添加筛选条件
        </a-button>
      </div>
    </a-form>

    <template #footer>
      <div style="display: flex; gap: 8px; justify-content: flex-end;">
        <a-button @click="emit('update:open', false)">关闭</a-button>
      </div>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { reactive, computed, watch } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'

const store = useAutoVoucherRuleStore()

const props = defineProps<{
  open: boolean
  /** 可用字段列表 */
  fields: string[]
  /** 账套列表 */
  accountSets: any[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

// ==================== 表单状态 ====================
interface FilterCondition {
  field: string
  operator: string
  value: string
}

const formState = reactive({
  layer1Field: '' as string,
  layer2Field: '' as string,
  layer3Field: '' as string,
  groupBy: '' as string,
  dateField: '' as string,
  voucherWord: '记' as string,
  accountSetId: undefined as number | undefined,
  keyFields: [] as string[],
  unmatchedAction: 'skip' as string,
  dateFallbackStrategy: '' as string,
  filterConditions: [] as FilterCondition[],
})

// 从 store 同步到本地表单
watch(() => props.open, (val) => {
  if (val) {
    formState.groupBy = store.formData.groupBy || ''
    formState.dateField = store.formData.dateField || ''
    formState.voucherWord = store.formData.voucherWord || '记'
    formState.accountSetId = store.formData.accountSetId ?? undefined
    formState.keyFields = [...(store.formData.keyFields || [])]
    formState.unmatchedAction = store.formData.unmatchedAction || 'skip'
    formState.layer1Field = store.formData.matchingLayers?.exactMatchField || ''
    formState.layer2Field = store.formData.matchingLayers?.categoryField || ''
    formState.layer3Field = store.formData.matchingLayers?.summaryField || ''
    formState.filterConditions = []
  }
})

const fieldOptions = computed(() =>
  props.fields.map(f => ({ label: f, value: f }))
)

// ==================== 同步到 Store ====================
function syncToStore() {
  store.formData.groupBy = formState.groupBy
  store.formData.dateField = formState.dateField
  store.formData.voucherWord = formState.voucherWord
  store.formData.accountSetId = formState.accountSetId ?? undefined
  store.formData.keyFields = [...formState.keyFields]
  store.formData.unmatchedAction = formState.unmatchedAction
  store.formData.matchingLayers = {
    exactMatchField: formState.layer1Field || '',
    categoryField: formState.layer2Field || '',
    summaryField: formState.layer3Field || '',
  }
  store.markDirty()
}

async function onAccountSetChange(value: SelectValue) {
  const val = value as number | undefined
  if (val) {
    await store.loadAccountTree(val)
  }
  syncToStore()
}

// ==================== 筛选条件操作 ====================
function addFilterCondition() {
  formState.filterConditions.push({ field: '', operator: 'eq', value: '' })
}

function removeFilterCondition(idx: number) {
  formState.filterConditions.splice(idx, 1)
  syncToStore()
}

function filterOption(input: string, option: any) {
  const label = option?.label || ''
  return String(label).toLowerCase().includes(input.toLowerCase())
}
</script>

<style lang="scss" scoped>
.filter-conditions {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

:deep(.ant-form-item) {
  margin-bottom: 10px;
}
</style>
