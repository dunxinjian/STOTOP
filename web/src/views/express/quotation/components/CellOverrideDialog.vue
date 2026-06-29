<template>
  <a-modal
    :open="visible"
    :title="dialogTitle"
    @cancel="handleCancel"
    @ok="handleOk"
    :width="420"
    destroy-on-close
  >
    <a-form layout="vertical" :colon="false">
      <a-form-item label="重量进位方式">
        <a-select
          v-model:value="localForm.roundingMethodOverride"
          :options="roundingMethodSelectOptions"
          placeholder="继承默认"
          allowClear
          style="width: 100%"
        />
      </a-form-item>
      <template v-if="localForm.roundingMethodOverride === 4 || localForm.roundingMethodOverride === 6 || localForm.roundingMethodOverride === 7">
        <div style="display: flex; gap: 8px">
          <a-form-item label="舍位" style="flex: 1">
            <a-input-number
              v-model:value="localForm.truncParamOverride"
              :precision="2"
              :min="0"
              :step="0.1"
              placeholder="舍位参数"
              style="width: 100%"
            />
          </a-form-item>
          <a-form-item label="进位" style="flex: 1">
            <a-input-number
              v-model:value="localForm.ceilParamOverride"
              :precision="2"
              :min="0"
              :step="0.1"
              placeholder="进位参数"
              style="width: 100%"
            />
          </a-form-item>
        </div>
      </template>
    </a-form>
    <template #footer>
      <div style="display: flex; justify-content: space-between">
        <a-button danger @click="handleReset">恢复默认</a-button>
        <div style="display: flex; gap: 8px">
          <a-button @click="handleCancel">取消</a-button>
          <a-button type="primary" @click="handleOk">确认</a-button>
        </div>
      </div>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { reactive, computed, watch } from 'vue'
import { roundingMethodOptions } from '../composables/useQuotationForm'

export interface CellOverrideData {
  provinceId: number
  segmentIndex: number
  provinceName: string
  segmentRange: string
  roundingMethodOverride: number | null
  truncParamOverride: number | null
  ceilParamOverride: number | null
}

const props = defineProps<{
  visible: boolean
  data: CellOverrideData | null
}>()

const emit = defineEmits<{
  'update:visible': [val: boolean]
  save: [data: {
    provinceId: number
    segmentIndex: number
    roundingMethodOverride: number | null
    truncParamOverride: number | null
    ceilParamOverride: number | null
  }]
}>()

const localForm = reactive({
  roundingMethodOverride: undefined as number | undefined,
  truncParamOverride: undefined as number | undefined,
  ceilParamOverride: undefined as number | undefined,
})

// 加"继承默认"选项的进位方式列表
const roundingMethodSelectOptions = computed(() => {
  return roundingMethodOptions.map(o => ({ ...o }))
})

const dialogTitle = computed(() => {
  if (!props.data) return '高级设置'
  return `高级设置 - ${props.data.provinceName} / ${props.data.segmentRange}`
})

watch(
  () => props.data,
  (val) => {
    if (val) {
      localForm.roundingMethodOverride = val.roundingMethodOverride ?? undefined
      localForm.truncParamOverride = val.truncParamOverride ?? undefined
      localForm.ceilParamOverride = val.ceilParamOverride ?? undefined
    }
  },
  { immediate: true },
)

function handleOk() {
  if (!props.data) return
  emit('save', {
    provinceId: props.data.provinceId,
    segmentIndex: props.data.segmentIndex,
    roundingMethodOverride: localForm.roundingMethodOverride ?? null,
    truncParamOverride: (localForm.roundingMethodOverride === 4 || localForm.roundingMethodOverride === 6 || localForm.roundingMethodOverride === 7)
      ? (localForm.truncParamOverride ?? null)
      : null,
    ceilParamOverride: (localForm.roundingMethodOverride === 4 || localForm.roundingMethodOverride === 6 || localForm.roundingMethodOverride === 7)
      ? (localForm.ceilParamOverride ?? null)
      : null,
  })
  emit('update:visible', false)
}

function handleCancel() {
  emit('update:visible', false)
}

function handleReset() {
  localForm.roundingMethodOverride = undefined
  localForm.truncParamOverride = undefined
  localForm.ceilParamOverride = undefined
}
</script>
