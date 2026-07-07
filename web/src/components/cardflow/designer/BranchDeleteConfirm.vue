<script setup lang="ts">
/**
 * 分支删除确认（设计 B5 三要素：支内节点去向 / 流量去向 / 在途影响）。
 * 在途卡片数 M6-3 接真数，本批显示占位说明（桥接契约见总体 plan §六.五）。
 */
const props = defineProps<{
  open: boolean
  branchName: string
  /** 支内独占节点名 */
  stageNames: string[]
}>()

const emit = defineEmits<{ 'update:open': [v: boolean]; confirm: [] }>()
</script>

<template>
  <a-modal
    :open="props.open"
    :title="`删除分支「${branchName}」？`"
    :width="420"
    ok-text="删除分支"
    :ok-button-props="{ danger: true }"
    cancel-text="取消"
    @update:open="emit('update:open', $event)"
    @ok="emit('confirm')"
  >
    <div class="bdc-body">
      <p v-if="stageNames.length">
        该分支包含 <b>{{ stageNames.length }} 个节点</b>（{{ stageNames.join('、') }}），将一并删除。
      </p>
      <p v-else>该分支内没有节点。</p>
      <p>删除后原本命中此分支的卡片将按优先级落入 <b>「其他情况」兜底分支</b>。</p>
      <div class="bdc-warn">⚠ 在途卡片影响：发布新版本时按「在途卡片策略」处理（发布确认弹窗中核算）。</div>
    </div>
  </a-modal>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.bdc-body {
  font-size: $font-size-sm2;
  color: $text-regular;

  p { margin-bottom: 8px; }
}

.bdc-warn {
  padding: 8px 10px;
  margin-top: 10px;
  font-size: $font-size-sm;
  color: var(--color-warning-text);
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  border-radius: 7px;
}
</style>
