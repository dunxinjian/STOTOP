<script setup lang="ts">
/**
 * 发布确认弹窗（plan M6-2，mock B6）：门禁结果 + 变更清单 + 在途卡片策略。
 * 变更清单用 flowVersionDiff 纯函数；在途数为 0 时策略区隐藏直接发布。
 */
import { computed, ref, watch } from 'vue'
import { diffFlowVersions, type VersionSnapshot, type ChangeItem } from '@/utils/flowVersionDiff'
import { getInflightSummary } from '@/api/cardflow'
import type { InflightSummaryDto } from '@/types/cardflow'

const props = defineProps<{
  open: boolean
  definitionId?: number
  nextVersion: number
  /** 已发布版本快照（无则视为首版，diff 全为新增/不展示） */
  oldSnapshot?: VersionSnapshot | null
  /** 当前草稿快照 */
  newSnapshot: VersionSnapshot
  /** 诊断错误数（>0 禁用发布） */
  errorCount: number
  warningCount: number
  publishing?: boolean
}>()

const emit = defineEmits<{
  'update:open': [v: boolean]
  confirm: [inflightPolicy: 'keepOld' | 'migrate']
}>()

const changes = computed<ChangeItem[]>(() =>
  props.oldSnapshot ? diffFlowVersions(props.oldSnapshot, props.newSnapshot) : [],
)

const inflight = ref<InflightSummaryDto | null>(null)
const inflightLoading = ref(false)
const inflightPolicy = ref<'keepOld' | 'migrate'>('keepOld')
const ackWarnings = ref(false)

watch(
  () => props.open,
  async (o) => {
    if (!o || !props.definitionId) return
    inflight.value = null
    inflightPolicy.value = 'keepOld'
    ackWarnings.value = false
    inflightLoading.value = true
    try {
      inflight.value = await getInflightSummary(props.definitionId)
    } catch {
      inflight.value = { total: 0, byVersion: [] }
    } finally {
      inflightLoading.value = false
    }
  },
)

const inflightTotal = computed(() => inflight.value?.total ?? 0)
const canPublish = computed(() => props.errorCount === 0 && (props.warningCount === 0 || ackWarnings.value))

const KIND_TAG: Record<ChangeItem['kind'], { text: string; color: string }> = {
  add: { text: '新增', color: 'green' },
  modify: { text: '修改', color: 'gold' },
  remove: { text: '删除', color: 'red' },
}

function onOk() {
  if (!canPublish.value) return
  emit('confirm', inflightTotal.value > 0 ? inflightPolicy.value : 'keepOld')
}
</script>

<template>
  <a-modal
    :open="props.open"
    :width="560"
    :title="`发布流程 · v${nextVersion}`"
    :confirm-loading="publishing"
    :ok-button-props="{ disabled: !canPublish }"
    ok-text="确认发布"
    cancel-text="取消"
    @update:open="emit('update:open', $event)"
    @ok="onOk"
  >
    <div class="pcm">
      <!-- 门禁结果 -->
      <div v-if="errorCount > 0" class="pcm-gate is-error">
        ✕ 存在 {{ errorCount }} 个错误，修复后才能发布
      </div>
      <div v-else class="pcm-gate is-ok">
        ✓ 发布门禁检查通过<span v-if="warningCount > 0">（{{ warningCount }} 个警告需确认）</span>
      </div>

      <!-- 变更清单 -->
      <div v-if="changes.length" class="pcm-section">
        <div class="pcm-section__title">本次变更（{{ changes.length }}）</div>
        <div class="cfd-list">
          <div v-for="(c, i) in changes" :key="i" class="cfd-list__row">
            <a-tag :color="KIND_TAG[c.kind].color" :bordered="false">{{ KIND_TAG[c.kind].text }}</a-tag>
            <span class="cfd-list__label">{{ c.label }}<span v-if="c.detail" class="pcm-detail"> · {{ c.detail }}</span></span>
          </div>
        </div>
      </div>
      <div v-else-if="oldSnapshot" class="pcm-muted">与已发布版本相比无结构变更</div>
      <div v-else class="pcm-muted">首次发布，无历史版本可对比</div>

      <!-- 警告确认 -->
      <label v-if="warningCount > 0" class="pcm-ack">
        <a-checkbox v-model:checked="ackWarnings" />
        我已知晓上述 {{ warningCount }} 个警告，继续发布
      </label>

      <!-- 在途卡片策略 -->
      <div v-if="inflightTotal > 0" class="pcm-section">
        <div class="pcm-section__title">在途卡片处理（{{ inflightTotal }} 张进行中）</div>
        <a-radio-group v-model:value="inflightPolicy" class="pcm-policy">
          <a-radio value="keepOld">
            <b>按旧版本走完（推荐）</b>
            <div class="pcm-policy__desc">在途卡片继续按当前版本流程，新发起的走新版本</div>
          </a-radio>
          <a-radio value="migrate">
            <b>迁移到新版本</b>
            <div class="pcm-policy__desc">停留在被删节点上的卡片移入后继节点，逐张记录迁移日志</div>
          </a-radio>
        </a-radio-group>
      </div>
      <div v-else-if="inflightLoading" class="pcm-muted">正在核算在途卡片…</div>
    </div>
  </a-modal>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.pcm { display: flex; flex-direction: column; gap: 14px; }

.pcm-gate {
  padding: 9px 12px;
  font-size: $font-size-sm2;
  border-radius: 8px;

  &.is-ok { color: var(--color-success-text); background: var(--color-success-light); border: 1px solid var(--color-success-border); }
  &.is-error { color: var(--color-danger-text); background: var(--color-danger-light); border: 1px solid var(--color-danger-border); }
}

.pcm-section__title { margin-bottom: 8px; font-size: $font-size-sm2; font-weight: 600; }

.pcm-detail { font-family: $font-family-mono; font-size: $font-size-sm; color: $text-secondary; }

.pcm-muted { font-size: $font-size-sm2; color: $text-secondary; }

.pcm-ack { display: flex; gap: 8px; align-items: center; font-size: $font-size-sm2; color: var(--color-warning-text); }

.pcm-policy { display: flex; flex-direction: column; gap: 10px; }
.pcm-policy__desc { margin-top: 2px; font-size: $font-size-sm; color: $text-secondary; }
</style>
