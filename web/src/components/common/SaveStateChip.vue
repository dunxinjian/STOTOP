<script setup lang="ts">
/**
 * 顶栏保存状态四态胶囊（设计 C5 状态机）：
 * 编辑中(dirty) → 保存中(saving) → 已保存 HH:mm(saved) ↘ 保存失败·重试(error)。
 * 状态语义对齐 useAutoSave 的 SaveState，不另造状态集。
 */
import { computed } from 'vue'
import { EditOutlined, LoadingOutlined, CheckCircleFilled, CloseCircleFilled } from '@ant-design/icons-vue'
import type { SaveState } from '@/composables/useAutoSave'

const props = defineProps<{
  state: SaveState
  savedAt?: Date | null
}>()

const emit = defineEmits<{ retry: [] }>()

const timeText = computed(() => {
  if (!props.savedAt) return ''
  const h = String(props.savedAt.getHours()).padStart(2, '0')
  const m = String(props.savedAt.getMinutes()).padStart(2, '0')
  return `${h}:${m}`
})

const text = computed(() => {
  switch (props.state) {
    case 'dirty': return '编辑中…'
    case 'saving': return '保存中'
    case 'error': return '保存失败'
    default: return timeText.value ? `已保存 ${timeText.value}` : '已保存'
  }
})
</script>

<template>
  <span class="cfd-save-chip" :class="`is-${state}`" role="status" aria-live="polite">
    <EditOutlined v-if="state === 'dirty'" />
    <LoadingOutlined v-else-if="state === 'saving'" spin />
    <CloseCircleFilled v-else-if="state === 'error'" />
    <CheckCircleFilled v-else />
    <span>{{ text }}</span>
    <a v-if="state === 'error'" class="cfd-save-chip__retry" @click="emit('retry')">重试</a>
  </span>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

// mock C5 .chip 规格：圆角 14px 胶囊 / 2x10 内距 / 12px 字号
.cfd-save-chip {
  display: inline-flex;
  gap: 5px;
  align-items: center;
  padding: 2px 10px;
  font-size: $font-size-sm;
  line-height: 1.6;
  border-radius: 14px;

  &.is-dirty {
    color: $text-secondary;
    background: $bg-page;
    border: 1px solid $border-color;
  }

  &.is-saving {
    color: $color-primary;
    background: $color-primary-light;
    border: 1px solid var(--color-primary-border);
  }

  &.is-saved {
    color: var(--color-success-text);
    background: var(--color-success-light);
    border: 1px solid var(--color-success-border);
  }

  &.is-error {
    color: var(--color-danger-text);
    background: var(--color-danger-light);
    border: 1px solid var(--color-danger-border);
  }

  &__retry {
    margin-left: 2px;
    color: var(--color-danger-text);
    text-decoration: underline;
    cursor: pointer;
  }
}
</style>
