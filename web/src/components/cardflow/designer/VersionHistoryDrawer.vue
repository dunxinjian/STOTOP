<script setup lang="ts">
/**
 * 版本历史抽屉（plan M6-4，mock B7）：历次版本 + 在途数常驻 + 回滚。
 * 回滚 = 以历史版本快照新建草稿（走门禁+发布确认，不绕发布唯一入口）。
 */
import { ref, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import { getFlowVersions, getInflightSummary, createDraftFromVersion } from '@/api/cardflow'
import type { FlowVersionDto, InflightSummaryDto } from '@/types/cardflow'

const props = defineProps<{
  open: boolean
  definitionId?: number
  /** 是否已存在未发布草稿（存在时回滚被拦截，单草稿不变量） */
  hasDraft: boolean
}>()

const emit = defineEmits<{
  'update:open': [v: boolean]
  /** 回滚成功后通知父级重载草稿 */
  'rolled-back': []
}>()

const versions = ref<FlowVersionDto[]>([])
const inflight = ref<InflightSummaryDto | null>(null)
const loading = ref(false)
const rollingBack = ref<number | null>(null)

watch(
  () => props.open,
  async (o) => {
    if (!o || !props.definitionId) return
    loading.value = true
    try {
      const [vs, inf] = await Promise.all([
        getFlowVersions(props.definitionId),
        getInflightSummary(props.definitionId).catch(() => ({ total: 0, byVersion: [] })),
      ])
      versions.value = vs.sort((a, b) => b.versionNumber - a.versionNumber)
      inflight.value = inf
    } finally {
      loading.value = false
    }
  },
)

function inflightOf(versionId: number): number {
  return inflight.value?.byVersion.find(v => v.versionId === versionId)?.count ?? 0
}

function onRollback(v: FlowVersionDto) {
  if (props.hasDraft) {
    message.warning('已存在未发布草稿，请先发布或放弃当前草稿后再回滚')
    return
  }
  Modal.confirm({
    title: `回滚到 v${v.versionNumber}？`,
    content: '将以该版本内容新建一份草稿（仍需走发布门禁与确认才生效）。当前进行中卡片按其所属版本继续走完。',
    okText: '创建回滚草稿',
    cancelText: '取消',
    async onOk() {
      if (!props.definitionId) return
      rollingBack.value = v.id
      try {
        await createDraftFromVersion(props.definitionId, v.id)
        message.success(`已创建 v${v.versionNumber} 的回滚草稿`)
        emit('rolled-back')
        emit('update:open', false)
      } catch (e) {
        console.error('[FlowDefinition] 回滚失败:', e)
      } finally {
        rollingBack.value = null
      }
    },
  })
}

function fmtTime(t: string | null): string {
  if (!t) return '—'
  return t.replace('T', ' ').slice(0, 16)
}
</script>

<template>
  <a-drawer
    :open="props.open"
    title="版本历史"
    placement="right"
    :width="400"
    @update:open="emit('update:open', $event)"
  >
    <a-spin :spinning="loading">
      <div v-if="!versions.length" class="vh-empty">尚未发布过 · 发布后每个版本都会记录在这里</div>
      <div v-for="v in versions" :key="v.id" class="vh-item" :class="{ 'is-current': v.isCurrentVersion }">
        <div class="vh-item__head">
          <a-tag v-if="v.isCurrentVersion" color="blue" :bordered="false">v{{ v.versionNumber }} · 当前</a-tag>
          <a-tag v-else-if="v.status === 'draft'" color="gold" :bordered="false">v{{ v.versionNumber }} · 草稿</a-tag>
          <a-tag v-else :bordered="false">v{{ v.versionNumber }}</a-tag>
          <span class="vh-item__inflight" :class="{ 'is-zero': inflightOf(v.id) === 0 }">在途 {{ inflightOf(v.id) }}</span>
        </div>
        <div class="vh-item__meta">
          {{ v.status === 'published' ? '发布于' : '创建于' }} {{ fmtTime(v.publishTime || v.createdTime) }}
        </div>
        <div v-if="!v.isCurrentVersion && v.status === 'published'" class="vh-item__ops">
          <a-button size="small" :loading="rollingBack === v.id" @click="onRollback(v)">回滚到此版</a-button>
        </div>
      </div>
      <div class="vh-note">
        回滚 = 以历史快照新建草稿，仍需发布门禁与确认才生效——不绕过发布唯一入口。
      </div>
    </a-spin>
  </a-drawer>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.vh-empty { padding: 40px 0; font-size: $font-size-sm2; color: $text-placeholder; text-align: center; }

.vh-item {
  padding: 12px 14px;
  margin-bottom: 10px;
  border: 1px solid $border-color;
  border-radius: 8px;

  &.is-current { background: $color-primary-light; border-color: var(--color-primary-border); }
}

.vh-item__head { display: flex; gap: 10px; align-items: center; }
.vh-item__inflight { margin-left: auto; font-size: $font-size-sm; color: var(--color-success-text); &.is-zero { color: $text-placeholder; } }
.vh-item__meta { margin-top: 4px; font-size: $font-size-sm; color: $text-secondary; }
.vh-item__ops { margin-top: 8px; }

.vh-note { margin-top: 16px; padding: 10px 12px; font-size: $font-size-sm; color: var(--color-warning-text); background: var(--color-warning-light); border: 1px solid var(--color-warning-border); border-radius: 7px; }
</style>
