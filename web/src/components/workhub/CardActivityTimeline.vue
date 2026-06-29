<script setup lang="ts">
/**
 * CardActivityTimeline.vue — CardFlow 卡片活动时间轴
 *
 * 按 cardId 并行拉取卡片详情（审批进度）与操作日志，合并去重为统一时间轴，
 * 底部高亮当前待审节点。供工作台右栏 WorkHubDetail 内嵌（source='cardflow'）。
 */
import { ref, watch } from 'vue'
import {
  CheckOutlined,
  CloseOutlined,
  ClockCircleOutlined,
  ArrowDownOutlined,
  BranchesOutlined,
  FlagOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { getCard, getCardLogs } from '@/api/cardflow'
import type { ActionLogDto } from '@/types/cardflow'
import {
  buildActivityEvents,
  buildPendingStages,
  type ActivityEvent,
  type PendingStage,
} from './cardActivity'

const props = defineProps<{ cardId: number }>()

const loading = ref(false)
const hasError = ref(false)
const events = ref<ActivityEvent[]>([])
const pendingStages = ref<PendingStage[]>([])

// 模块级小缓存（带 TTL）：避免 J/K 来回切重复拉；TTL 防止流转中卡片长期陈旧
const CACHE_TTL = 60_000
const cache = new Map<number, { events: ActivityEvent[]; pending: PendingStage[]; ts: number }>()

// 实例级请求序号：latest-wins，丢弃被新选中卡取代的旧请求结果
let loadSeq = 0

async function load(id: number) {
  if (!id) {
    events.value = []
    pendingStages.value = []
    hasError.value = false
    return
  }
  const cached = cache.get(id)
  if (cached && Date.now() - cached.ts < CACHE_TTL) {
    events.value = cached.events
    pendingStages.value = cached.pending
    hasError.value = false
    return
  }
  const seq = ++loadSeq
  loading.value = true
  hasError.value = false
  try {
    let detailOk = true
    const [detailRes, logsRes] = await Promise.all([
      getCard(id).catch(() => {
        detailOk = false
        return null
      }),
      getCardLogs(id).catch(() => [] as ActionLogDto[]),
    ])
    if (seq !== loadSeq) return // 已被更新的请求取代，丢弃旧结果
    const evs = buildActivityEvents(
      detailRes?.stageInstances ?? [],
      detailRes?.auditTrail ?? [],
      logsRes ?? [],
    )
    const pend = buildPendingStages(detailRes?.stageInstances ?? [])
    events.value = evs
    pendingStages.value = pend
    hasError.value = !detailOk && evs.length === 0 && pend.length === 0
    // 仅在卡片详情成功时缓存，避免把失败的空结果固化
    if (detailOk) {
      cache.set(id, { events: evs, pending: pend, ts: Date.now() })
    }
  } finally {
    if (seq === loadSeq) loading.value = false
  }
}

function fmtTime(v?: string) {
  return v ? dayjs(v).format('MM-DD HH:mm') : ''
}

function iconFor(ev: ActivityEvent) {
  if (ev.kind === 'decision') {
    return ev.status === 'rejected' ? CloseOutlined : CheckOutlined
  }
  if (ev.kind === 'node-enter') return ArrowDownOutlined
  if (ev.kind === 'system') return BranchesOutlined
  return FlagOutlined
}

watch(() => props.cardId, (id) => load(id), { immediate: true })
</script>

<template>
  <div class="card-activity">
    <div class="ca-section-label">活动 · 审批进度与操作日志</div>

    <div v-if="loading" class="ca-state"><a-spin size="small" /></div>

    <template v-else>
      <div v-if="hasError" class="ca-state">加载失败，可刷新重试</div>

      <div v-else-if="events.length === 0 && pendingStages.length === 0" class="ca-state">
        暂无活动记录
      </div>

      <div v-else class="ca-timeline">
        <div
          v-for="(ev, idx) in events"
          :key="`${ev.kind}-${ev.time}-${idx}`"
          class="ca-item"
          :class="`ca-item--${ev.kind}`"
        >
          <span class="ca-dot" :class="ev.status ? `ca-dot--${ev.status}` : ''"></span>
          <div class="ca-body">
            <div class="ca-line">
              <component :is="iconFor(ev)" class="ca-icon" />
              <span class="ca-title">{{ ev.title }}</span>
              <span v-if="ev.actor" class="ca-actor">· {{ ev.actor }}</span>
              <span v-if="ev.node && ev.kind === 'decision'" class="ca-node">（{{ ev.node }}）</span>
              <span v-if="ev.kind === 'system'" class="ca-sys-tag">系统</span>
            </div>
            <div v-if="ev.opinion" class="ca-opinion">意见：{{ ev.opinion }}</div>
            <div class="ca-time">{{ fmtTime(ev.time) }}</div>
          </div>
        </div>

        <!-- 当前待审 -->
        <div
          v-for="(p, idx) in pendingStages"
          :key="`p-${idx}`"
          class="ca-item ca-item--pending"
        >
          <span class="ca-dot ca-dot--pending-now"></span>
          <div class="ca-body">
            <div class="ca-line">
              <ClockCircleOutlined class="ca-icon ca-icon--pending" />
              <span class="ca-title ca-title--pending">等待「{{ p.stageName }}」审批</span>
            </div>
            <div v-if="p.assignees.length" class="ca-pending-assignees">
              审批人：{{ p.assignees.join('、') }}
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.card-activity {
  padding: 4px 0;
}
.ca-section-label {
  font-size: 12px;
  color: var(--text-3);
  margin: 0 0 8px;
}
.ca-state {
  padding: 16px 0;
  text-align: center;
  color: var(--text-3);
  font-size: 13px;
}
.ca-timeline {
  position: relative;
  padding-left: 18px;
}
.ca-timeline::before {
  content: '';
  position: absolute;
  left: 4px;
  top: 4px;
  bottom: 10px;
  width: 1.5px;
  background: var(--border);
}
.ca-item {
  position: relative;
  margin-bottom: 14px;
}
.ca-item:last-child {
  margin-bottom: 0;
}
.ca-dot {
  position: absolute;
  left: -18px;
  top: 3px;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--bg-card);
  border: 2px solid var(--border-strong);
}
.ca-dot--approved {
  background: var(--color-success);
  border-color: var(--color-success);
}
.ca-dot--rejected {
  background: var(--color-danger);
  border-color: var(--color-danger);
}
.ca-dot--pending-now {
  background: var(--color-info);
  border-color: var(--color-info);
}
.ca-body {
  min-width: 0;
}
.ca-line {
  display: flex;
  align-items: center;
  gap: 5px;
  flex-wrap: wrap;
  font-size: 13px;
  color: var(--text-1);
}
.ca-icon {
  font-size: 13px;
  color: var(--text-3);
}
.ca-actor {
  color: var(--text-2);
}
.ca-node {
  color: var(--text-3);
  font-size: 12px;
}
.ca-sys-tag {
  font-size: 11px;
  color: var(--text-3);
  background: var(--bg-muted);
  border-radius: var(--radius-md);
  padding: 0 5px;
}
.ca-opinion {
  font-size: 12px;
  color: var(--text-2);
  margin-top: 3px;
  padding: 4px 8px;
  background: var(--bg-muted);
  border-radius: var(--radius-md);
}
.ca-time {
  font-size: 11px;
  color: var(--text-3);
  margin-top: 2px;
}
.ca-item--system .ca-title,
.ca-item--node-enter .ca-title {
  color: var(--text-2);
}
.ca-title--pending {
  color: var(--color-info);
  font-weight: 500;
}
.ca-icon--pending {
  color: var(--color-info);
}
.ca-pending-assignees {
  font-size: 12px;
  color: var(--text-2);
  margin-top: 1px;
}
</style>
