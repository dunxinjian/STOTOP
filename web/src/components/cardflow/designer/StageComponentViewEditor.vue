<script setup lang="ts">
import type { CardComponentAccessRule, CardComponentDefinition } from '@/types/cardflow'

const props = defineProps<{
  components: CardComponentDefinition[]
  modelValue?: Record<string, CardComponentAccessRule>
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: Record<string, CardComponentAccessRule>): void
}>()

const ACCESS_OPTIONS = [
  { value: 'readonly', label: '可见 / 只读' },
  { value: 'hidden', label: '隐藏' },
  { value: 'editable', label: '可编辑' },
  { value: 'required', label: '必填' },
  { value: 'masked', label: '脱敏' },
]

function currentAccess(componentId: string) {
  return props.modelValue?.[componentId]?.access || 'readonly'
}

function setAccess(componentId: string, access: string) {
  const next = { ...(props.modelValue || {}) }
  next[componentId] = {
    ...(next[componentId] || {}),
    access,
    required: access === 'required' ? true : next[componentId]?.required,
  }
  emit('update:modelValue', next)
}

function setRequired(componentId: string, required: boolean) {
  const next = { ...(props.modelValue || {}) }
  next[componentId] = {
    ...(next[componentId] || { access: currentAccess(componentId) }),
    required,
  }
  emit('update:modelValue', next)
}
</script>

<template>
  <section class="cf-stage-component-view">
    <header>
      <strong>节点组件视图权限</strong>
      <span>按节点职责设置组件可见性、编辑性和脱敏</span>
    </header>

    <div v-if="components.length === 0" class="cf-stage-component-view__empty">
      尚未配置卡片组件
    </div>

    <div v-else class="cf-stage-component-view__list">
      <article v-for="component in components" :key="component.id">
        <div class="cf-stage-component-view__meta">
          <strong>{{ component.title || component.id }}</strong>
          <span>{{ component.type }} / {{ component.binding?.source }}</span>
        </div>
        <a-select
          :value="currentAccess(component.id)"
          :options="ACCESS_OPTIONS"
          size="small"
          style="width: 118px"
          @change="(value: any) => setAccess(component.id, value)"
        />
        <a-checkbox
          :checked="modelValue?.[component.id]?.required === true || currentAccess(component.id) === 'required'"
          @change="(event: any) => setRequired(component.id, event.target.checked)"
        >
          必填
        </a-checkbox>
      </article>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-stage-component-view {
  display: flex;
  flex-direction: column;
  gap: 10px;

  header strong,
  header span {
    display: block;
  }

  header strong {
    color: var(--text-1);
    font-size: 13px;
  }

  header span {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-stage-component-view__empty {
  padding: 12px;
  border: 1px dashed var(--border);
  border-radius: 6px;
  color: var(--text-3);
  font-size: 12px;
  text-align: center;
}

.cf-stage-component-view__list {
  display: flex;
  flex-direction: column;
  gap: 8px;

  article {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto auto;
    align-items: center;
    gap: 10px;
    padding: 8px 10px;
    border: 1px solid var(--border);
    border-radius: 6px;
    background: var(--bg-card);
  }
}

.cf-stage-component-view__meta {
  min-width: 0;

  strong,
  span {
    display: block;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
  }

  span {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
  }
}
</style>
