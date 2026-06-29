<template>
  <div class="visibility-config">
    <a-radio-group v-model:value="innerValue" @change="handleChange">
      <a-radio-button :value="0">公开</a-radio-button>
      <a-radio-button :value="1">项目内</a-radio-button>
      <a-radio-button :value="2">仅指定人员</a-radio-button>
    </a-radio-group>

    <!-- 指定人员模式 -->
    <div v-if="innerValue === 2" class="visibility-config__members">
      <a-select
        v-model:value="selectedUserIds"
        mode="multiple"
        placeholder="搜索并添加人员"
        show-search
        :filter-option="false"
        @search="handleSearch"
        @change="handleMembersChange"
        style="width: 100%; margin-top: 8px"
      >
        <a-select-option
          v-for="user in userOptions"
          :key="user.id"
          :value="user.id"
        >
          {{ user.name }}
        </a-select-option>
      </a-select>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import { get } from '@/api/request'

interface UserOption {
  id: number
  name: string
}

const props = defineProps<{
  modelValue: number
  taskId?: number
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: number): void
  (e: 'membersChange', userIds: number[]): void
}>()

const innerValue = ref(props.modelValue)
const selectedUserIds = ref<number[]>([])
const userOptions = ref<UserOption[]>([])

watch(
  () => props.modelValue,
  (val) => {
    innerValue.value = val
  },
)

function handleChange() {
  emit('update:modelValue', innerValue.value)
}

let searchTimer: ReturnType<typeof setTimeout> | null = null

function handleSearch(keyword: string) {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!keyword) return
    try {
      const res = await get<UserOption[]>('/system/users/search', { keyword })
      userOptions.value = res
    } catch {
      // ignore
    }
  }, 300)
}

function handleMembersChange(value: SelectValue) {
  emit('membersChange', (value as number[]) ?? [])
}
</script>

<style scoped lang="scss">
.visibility-config {
  &__members {
    margin-top: 4px;
  }
}
</style>
