<script setup lang="ts">
/**
 * 三态权限胶囊（设计 A6/E3）：可编辑/只读/隐藏。
 * 锁定项划线+tooltip 原因+点击无效（设计 C2「禁用永远给原因」）。
 * mock 对应 .tri（mock-shared.css）：胶囊三格、选中格按语义着色。
 */
import { computed } from 'vue'
import { buildTriStates, type PermissionValue } from './permissionTriShared'

const props = withDefaults(defineProps<{
  value: PermissionValue
  lockedStates?: PermissionValue[]
  lockReason?: string
}>(), {
  lockedStates: () => [],
  lockReason: '该项被锁定',
})

const emit = defineEmits<{ 'update:value': [v: PermissionValue] }>()

const states = computed(() => buildTriStates(props.value, props.lockedStates))

function onSelect(v: PermissionValue, locked: boolean) {
  if (locked || v === props.value) return
  emit('update:value', v)
}
</script>

<template>
  <span class="cfd-tri">
    <template v-for="s in states" :key="s.value">
      <a-tooltip v-if="s.locked" :title="lockReason">
        <span
          class="cfd-tri__item is-locked"
          :class="{ [`is-active-${s.value}`]: s.active }"
          role="radio"
          :aria-checked="s.active"
          aria-disabled="true"
        >{{ s.label }}</span>
      </a-tooltip>
      <span
        v-else
        class="cfd-tri__item"
        :class="{ [`is-active-${s.value}`]: s.active }"
        role="radio"
        :aria-checked="s.active"
        tabindex="0"
        @click="onSelect(s.value, s.locked)"
        @keydown.enter.prevent="onSelect(s.value, s.locked)"
        @keydown.space.prevent="onSelect(s.value, s.locked)"
      >{{ s.label }}</span>
    </template>
  </span>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

// mock .tri 规格：内联胶囊、5px 圆角、格间 1px 分隔、11.5px 字号
.cfd-tri {
  display: inline-flex;
  overflow: hidden;
  border: 1px solid $border-color;
  border-radius: 5px;

  &__item {
    padding: 2px 9px;
    font-size: 11.5px;
    line-height: 1.5;
    color: $text-regular;
    cursor: pointer;
    background: $bg-card;
    border-right: 1px solid $border-color;

    &:last-child {
      border-right: 0;
    }

    &:focus-visible {
      outline: 2px solid $color-primary;
      outline-offset: -2px;
    }

    &.is-active-edit {
      font-weight: 600;
      color: $color-primary;
      background: $color-primary-light;
    }

    &.is-active-read {
      font-weight: 600;
      color: $text-secondary;
      background: $bg-page;
    }

    &.is-active-hidden {
      font-weight: 600;
      color: var(--color-danger-text);
      background: var(--color-danger-light);
    }

    &.is-locked {
      color: $text-placeholder;
      text-decoration: line-through;
      cursor: not-allowed;

      &.is-active-edit,
      &.is-active-read,
      &.is-active-hidden {
        text-decoration: none; // 锁定但为当前值：保持可读
      }
    }
  }
}
</style>
