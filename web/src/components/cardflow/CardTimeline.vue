<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  CheckCircleFilled,
  CloseCircleFilled,
  SyncOutlined,
  ClockCircleOutlined,
  MinusCircleOutlined,
  RobotOutlined,
  SendOutlined,
  TeamOutlined,
  BranchesOutlined,
  UserSwitchOutlined,
  DownOutlined,
  UpOutlined,
} from '@ant-design/icons-vue'
import type { CardFlowRuntimeAuditDto, StageInstanceDto } from '@/types/cardflow'
import StatusTag from '@/components/StatusTag.vue'

// ==================== Props ====================

interface Props {
  stages: StageInstanceDto[]
  auditTrail?: CardFlowRuntimeAuditDto[]
  mode?: 'full' | 'compact'
  currentRound?: number
}

const props = withDefaults(defineProps<Props>(), {
  auditTrail: () => [],
  mode: 'full',
  currentRound: 0,
})

// ==================== 状态色映射 ====================

const statusTypeMap: Record<string, 'success' | 'danger' | 'info' | 'default'> = {
  approved:   'success',
  rejected:   'danger',
  processing: 'info',
  pending:    'default',
  skipped:    'default',
}

const statusLabelMap: Record<string, string> = {
  approved:   '已通过',
  rejected:   '已退回',
  processing: '处理中',
  pending:    '待处理',
  skipped:    '已跳过',
}

const typeIconMap: Record<string, any> = {
  approval: TeamOutlined,
  auto: RobotOutlined,
  cc: SendOutlined,
}

const typeLabelMap: Record<string, string> = {
  approval: '审批',
  auto: '自动',
  cc: '抄送',
}

// ==================== 多轮次分组 ====================

interface RoundGroup {
  round: number
  stages: StageInstanceDto[]
  summary: string
}

const activeRound = computed(() => {
  if (props.currentRound > 0) return props.currentRound
  if (props.stages.length === 0) return 1
  return Math.max(...props.stages.map(s => s.round || 1))
})

const roundGroups = computed<RoundGroup[]>(() => {
  if (!props.stages || props.stages.length === 0) return []

  const groupMap = new Map<number, StageInstanceDto[]>()
  for (const stage of props.stages) {
    const round = stage.round || 1
    if (!groupMap.has(round)) groupMap.set(round, [])
    groupMap.get(round)!.push(stage)
  }

  const groups: RoundGroup[] = []
  const sortedRounds = [...groupMap.keys()].sort((a, b) => a - b)
  for (const round of sortedRounds) {
    const stages = groupMap.get(round)!
    const lastStatus = stages[stages.length - 1]?.status || 'pending'
    const summary = `第${round}轮 · ${stages.length}个节点 · ${statusLabelMap[lastStatus] || lastStatus}`
    groups.push({ round, stages, summary })
  }
  return groups
})

// ==================== 折叠状态管理 ====================

const expandedRounds = ref<Set<number>>(new Set())

function isRoundExpanded(round: number): boolean {
  return round === activeRound.value || expandedRounds.value.has(round)
}

function toggleRound(round: number) {
  if (expandedRounds.value.has(round)) {
    expandedRounds.value.delete(round)
  } else {
    expandedRounds.value.add(round)
  }
}

// ==================== 5+节点折叠 ====================

const collapsedCompleted = ref<Set<number>>(new Set())

function shouldCollapseCompleted(group: RoundGroup): boolean {
  if (group.round !== activeRound.value) return false
  return group.stages.length >= 5
}

function getCompletedCount(group: RoundGroup): number {
  return group.stages.filter(s => ['approved', 'rejected', 'skipped'].includes(s.status)).length
}

function getVisibleStages(group: RoundGroup): StageInstanceDto[] {
  if (!shouldCollapseCompleted(group) || collapsedCompleted.value.has(group.round)) {
    return group.stages
  }
  return group.stages.filter(s => s.status === 'processing' || s.status === 'pending')
}

function toggleCompletedCollapse(round: number) {
  if (collapsedCompleted.value.has(round)) {
    collapsedCompleted.value.delete(round)
  } else {
    collapsedCompleted.value.add(round)
  }
}

// ==================== 工具方法 ====================

function formatTime(time: string | null | undefined): string {
  if (!time) return ''
  const d = new Date(time)
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function computeDuration(activated: string | null | undefined, completed: string | null | undefined): string {
  if (!activated || !completed) return ''
  const diff = new Date(completed).getTime() - new Date(activated).getTime()
  if (diff <= 0) return ''
  const hours = Math.floor(diff / 3600000)
  const minutes = Math.floor((diff % 3600000) / 60000)
  if (hours > 0) return `${hours}小时${minutes > 0 ? minutes + '分' : ''}`
  return `${minutes}分钟`
}

function getAssigneeNames(stage: StageInstanceDto): string {
  if (!stage.assignees || stage.assignees.length === 0) return ''
  return stage.assignees.map(a => a.userName).join('、')
}

// 图标/内联样式与 timeline 圆点统一用 CSS var()
// ant-design Timeline 的 :color 会作为内联 borderColor/color 直接套用，var() 在内联样式中可正常解析
const statusIconColorMap: Record<string, string> = {
  approved:   'var(--color-success)',
  rejected:   'var(--color-danger)',
  processing: 'var(--color-info)',
  pending:    'var(--text-3)',
  skipped:    'var(--border-strong)',
}

function getStatusColor(status: string): string {
  return statusIconColorMap[status] || 'var(--text-3)'
}

function getTimelineColor(status: string): string {
  return statusIconColorMap[status] || 'var(--text-3)'
}

function getStageAudit(stage: StageInstanceDto): CardFlowRuntimeAuditDto[] {
  return (props.auditTrail || []).filter((audit) => {
    const sourceStageInstanceId = Number(audit.metadata?.sourceStageInstanceId || 0)
    if (audit.snapshotType === 'routeDecision') {
      return audit.stageInstanceId === stage.id || sourceStageInstanceId === stage.id
    }
    if (audit.snapshotType === 'dynamicApprover') {
      return audit.stageInstanceId === stage.id
    }
    return audit.stageInstanceId === stage.id
  })
}

function getAuditIcon(audit: CardFlowRuntimeAuditDto) {
  return audit.snapshotType === 'dynamicApprover' ? UserSwitchOutlined : BranchesOutlined
}

function getAuditLabel(audit: CardFlowRuntimeAuditDto): string {
  if (audit.snapshotType === 'dynamicApprover') return '动态审批'
  if (audit.snapshotType === 'routeDecision') return '条件流转'
  return '运行审计'
}

function getAuditColor(audit: CardFlowRuntimeAuditDto): string {
  return audit.snapshotType === 'dynamicApprover' ? 'var(--biz-approval)' : 'var(--color-info)'
}

function auditMetaText(audit: CardFlowRuntimeAuditDto): string {
  const parts: string[] = []
  const edgeKey = audit.edgeKey || audit.metadata?.edgeKey || audit.metadata?.selectedRouteEdgeKey
  const policyKey = audit.policyKey || audit.metadata?.policyKey
  if (edgeKey) parts.push(`分支 ${edgeKey}`)
  if (policyKey) parts.push(`策略 ${policyKey}`)
  const handlerNames = Array.isArray(audit.metadata?.handlerNames) ? audit.metadata.handlerNames : []
  if (audit.snapshotType === 'dynamicApprover' && handlerNames.length > 0) {
    parts.push(`处理人 ${handlerNames.join('、')}`)
  }
  return parts.join(' · ')
}

// ==================== 简洁模式意见展开 ====================

const expandedOpinions = ref<Set<number>>(new Set())

function toggleOpinion(id: number) {
  if (expandedOpinions.value.has(id)) {
    expandedOpinions.value.delete(id)
  } else {
    expandedOpinions.value.add(id)
  }
}
</script>

<template>
  <div class="card-timeline" :class="[`card-timeline--${mode}`]">
    <!-- 空状态 -->
    <div v-if="!stages || stages.length === 0" class="card-timeline__empty">
      <a-empty description="暂无审批记录" :image="undefined" />
    </div>

    <!-- 完整模式 -->
    <template v-else-if="mode === 'full'">
      <div
        v-for="group in roundGroups"
        :key="group.round"
        class="round-group"
        :class="{ 'round-group--active': group.round === activeRound }"
      >
        <!-- 轮次标题（多轮时显示） -->
        <div
          v-if="roundGroups.length > 1"
          class="round-group__header"
          @click="toggleRound(group.round)"
        >
          <span class="round-group__title">第{{ group.round }}轮</span>
          <span class="round-group__summary">{{ group.stages.length }}个节点</span>
          <component
            :is="isRoundExpanded(group.round) ? UpOutlined : DownOutlined"
            class="round-group__toggle"
          />
        </div>

        <!-- 轮次内容（展开/当前轮） -->
        <transition name="collapse">
          <div v-show="isRoundExpanded(group.round)" class="round-group__content">
            <!-- 5+节点折叠提示 -->
            <div
              v-if="shouldCollapseCompleted(group) && !collapsedCompleted.has(group.round)"
              class="completed-collapse-hint"
              @click="toggleCompletedCollapse(group.round)"
            >
              <CheckCircleFilled style="color: var(--color-success)" />
              <span>已完成 {{ getCompletedCount(group) }} 个节点</span>
              <a-button type="link" size="small">展开全部</a-button>
            </div>
            <div
              v-else-if="shouldCollapseCompleted(group) && collapsedCompleted.has(group.round)"
              class="completed-collapse-hint"
              @click="toggleCompletedCollapse(group.round)"
            >
              <a-button type="link" size="small">收起已完成</a-button>
            </div>

            <!-- 时间线 -->
            <a-timeline>
              <a-timeline-item
                v-for="stage in getVisibleStages(group)"
                :key="stage.id"
                :color="getTimelineColor(stage.status)"
              >
                <template #dot>
                  <component
                    :is="stage.status === 'approved' ? CheckCircleFilled
                      : stage.status === 'rejected' ? CloseCircleFilled
                      : stage.status === 'processing' ? SyncOutlined
                      : stage.status === 'skipped' ? MinusCircleOutlined
                      : ClockCircleOutlined"
                    :style="{ fontSize: '16px', color: getStatusColor(stage.status) }"
                  />
                </template>

                <div class="timeline-node">
                  <!-- 节点名称行 -->
                  <div class="timeline-node__header">
                    <span class="timeline-node__name">{{ stage.stageName }}</span>
                    <a-tag
                      v-if="stage.type"
                      :bordered="false"
                      size="small"
                      color="default"
                    >
                      <template #icon>
                        <component :is="typeIconMap[stage.type]" />
                      </template>
                      {{ typeLabelMap[stage.type] || stage.type }}
                    </a-tag>
                    <span class="timeline-node__duration">
                      {{ computeDuration(stage.activatedTime, stage.completedTime) }}
                    </span>
                  </div>

                  <!-- 处理人 + 状态 -->
                  <div v-if="stage.assignees && stage.assignees.length > 0" class="timeline-node__assignees">
                    <span class="timeline-node__assignee-names">{{ getAssigneeNames(stage) }}</span>
                    <StatusTag :type="statusTypeMap[stage.status] ?? 'default'">
                      {{ statusLabelMap[stage.status] || stage.status }}
                    </StatusTag>
                  </div>
                  <div v-else class="timeline-node__assignees">
                    <StatusTag :type="statusTypeMap[stage.status] ?? 'default'">
                      {{ statusLabelMap[stage.status] || stage.status }}
                    </StatusTag>
                  </div>

                  <!-- 审批意见 -->
                  <div v-if="stage.opinion" class="timeline-node__opinion">
                    {{ stage.opinion }}
                  </div>
                  <!-- 多人审批意见 -->
                  <template v-if="stage.assignees && stage.assignees.length > 1">
                    <div
                      v-for="assignee in stage.assignees.filter(a => a.opinion)"
                      :key="assignee.id"
                      class="timeline-node__opinion timeline-node__opinion--sub"
                    >
                      <span class="timeline-node__opinion-author">{{ assignee.userName }}：</span>
                      {{ assignee.opinion }}
                    </div>
                  </template>

                  <!-- 条件流转 / 动态审批运行审计 -->
                  <div
                    v-for="audit in getStageAudit(stage)"
                    :key="`${audit.snapshotType}-${audit.id}`"
                    class="timeline-node__audit"
                    :style="{ borderColor: getAuditColor(audit) }"
                  >
                    <div class="timeline-node__audit-head">
                      <component
                        :is="getAuditIcon(audit)"
                        class="timeline-node__audit-icon"
                        :style="{ color: getAuditColor(audit) }"
                      />
                      <span class="timeline-node__audit-title">{{ audit.title || getAuditLabel(audit) }}</span>
                      <StatusTag type="info">{{ getAuditLabel(audit) }}</StatusTag>
                    </div>
                    <div class="timeline-node__audit-reason">{{ audit.reason }}</div>
                    <div v-if="auditMetaText(audit)" class="timeline-node__audit-meta">
                      {{ auditMetaText(audit) }}
                    </div>
                  </div>

                  <!-- 处理时间 -->
                  <div v-if="stage.completedTime" class="timeline-node__time">
                    {{ formatTime(stage.completedTime) }}
                  </div>
                  <div v-else-if="stage.activatedTime" class="timeline-node__time">
                    {{ formatTime(stage.activatedTime) }} 开始处理
                  </div>
                </div>
              </a-timeline-item>
            </a-timeline>
          </div>
        </transition>

        <!-- 折叠状态的摘要（非当前轮） -->
        <div
          v-if="roundGroups.length > 1 && !isRoundExpanded(group.round)"
          class="round-group__collapsed-summary"
          @click="toggleRound(group.round)"
        >
          {{ group.summary }}
          <a-button type="link" size="small">展开</a-button>
        </div>
      </div>
    </template>

    <!-- 简洁模式 -->
    <template v-else>
      <div class="compact-timeline">
        <div
          v-for="stage in stages"
          :key="stage.id"
          class="compact-timeline__item"
        >
          <div class="compact-timeline__row">
            <component
              :is="stage.status === 'approved' ? CheckCircleFilled
                : stage.status === 'rejected' ? CloseCircleFilled
                : stage.status === 'processing' ? SyncOutlined
                : stage.status === 'skipped' ? MinusCircleOutlined
                : ClockCircleOutlined"
              :style="{ color: getStatusColor(stage.status), fontSize: '14px' }"
            />
            <span class="compact-timeline__name">{{ stage.stageName }}</span>
            <span class="compact-timeline__assignee">{{ getAssigneeNames(stage) }}</span>
            <StatusTag :type="statusTypeMap[stage.status] ?? 'default'">
              {{ statusLabelMap[stage.status] || stage.status }}
            </StatusTag>
            <DownOutlined
              v-if="stage.opinion || (stage.assignees && stage.assignees.some(a => a.opinion))"
              class="compact-timeline__expand-icon"
              :class="{ 'compact-timeline__expand-icon--active': expandedOpinions.has(stage.id) }"
              @click.stop="toggleOpinion(stage.id)"
            />
          </div>
          <!-- 折叠的意见 -->
          <transition name="collapse">
            <div v-if="expandedOpinions.has(stage.id)" class="compact-timeline__opinion">
              <template v-if="stage.opinion">{{ stage.opinion }}</template>
              <template v-for="assignee in (stage.assignees || []).filter(a => a.opinion)" :key="assignee.id">
                <div class="compact-timeline__opinion-item">
                  <span>{{ assignee.userName }}：</span>{{ assignee.opinion }}
                </div>
              </template>
            </div>
          </transition>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="scss">
.card-timeline {
  padding: 12px 0;

  &__empty {
    padding: 32px 0;
  }
}

// ==================== 轮次分组 ====================

.round-group {
  margin-bottom: 12px;
  border-radius: 8px;
  background: var(--bg-muted);
  padding: 12px 16px;

  &--active {
    background: var(--bg-card);
    border: 1px solid var(--border);
  }

  &__header {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
    padding: 4px 0 8px;
    user-select: none;
  }

  &__title {
    font-weight: 600;
    font-size: 14px;
    color: var(--text-1);
  }

  &__summary {
    font-size: 12px;
    color: var(--text-3);
  }

  &__toggle {
    margin-left: auto;
    font-size: 12px;
    color: var(--text-3);
  }

  &__content {
    overflow: hidden;
  }

  &__collapsed-summary {
    font-size: 13px;
    color: var(--text-3);
    cursor: pointer;
    padding: 4px 0;

    &:hover {
      color: var(--color-primary);
    }
  }
}

// ==================== 5+节点折叠提示 ====================

.completed-collapse-hint {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  margin-bottom: 12px;
  background: var(--color-success-light);
  border-radius: 6px;
  font-size: 13px;
  color: var(--text-2);
  cursor: pointer;

  &:hover {
    background: color-mix(in srgb, var(--color-success) 20%, transparent);
  }
}

// ==================== 完整模式节点 ====================

.timeline-node {
  &__header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 4px;
  }

  &__name {
    font-weight: 600;
    font-size: 14px;
    color: var(--text-1);
  }

  &__duration {
    margin-left: auto;
    font-size: 12px;
    color: var(--text-3);
  }

  &__assignees {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-bottom: 4px;
  }

  &__assignee-names {
    font-size: 13px;
    color: var(--text-2);
  }

  &__opinion {
    margin-top: 6px;
    padding: 8px 12px;
    border-left: 3px solid var(--border);
    background: var(--bg-muted);
    border-radius: 0 4px 4px 0;
    font-style: italic;
    font-size: 13px;
    color: var(--text-2);
    line-height: 1.5;

    &--sub {
      margin-top: 4px;
    }
  }

  &__opinion-author {
    font-style: normal;
    font-weight: 500;
    color: var(--text-1);
  }

  &__audit {
    margin-top: 8px;
    padding: 8px 10px;
    border-left: 3px solid var(--color-info);
    background: var(--bg-muted);
    border-radius: 0 4px 4px 0;
  }

  &__audit-head {
    display: flex;
    align-items: center;
    gap: 6px;
    min-width: 0;
  }

  &__audit-icon {
    font-size: 14px;
    flex: 0 0 auto;
  }

  &__audit-title {
    font-size: 13px;
    font-weight: 600;
    color: var(--text-1);
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__audit-reason {
    margin-top: 4px;
    font-size: 12px;
    color: var(--text-1);
    line-height: 1.5;
  }

  &__audit-meta {
    margin-top: 3px;
    font-size: 12px;
    color: var(--text-3);
    line-height: 1.4;
  }

  &__time {
    margin-top: 4px;
    font-size: 12px;
    color: var(--text-3);
  }
}

// ==================== 简洁模式 ====================

.compact-timeline {
  &__item {
    padding: 6px 0;
    border-bottom: 1px solid var(--border);

    &:last-child {
      border-bottom: none;
    }
  }

  &__row {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__name {
    font-size: 13px;
    font-weight: 500;
    color: var(--text-1);
  }

  &__assignee {
    font-size: 12px;
    color: var(--text-3);
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__expand-icon {
    font-size: 10px;
    color: var(--text-3);
    cursor: pointer;
    transition: transform 200ms;

    &--active {
      transform: rotate(180deg);
    }
  }

  &__opinion {
    padding: 6px 8px 6px 22px;
    font-size: 12px;
    color: var(--text-2);
    font-style: italic;
    overflow: hidden;
  }

  &__opinion-item {
    margin-top: 2px;
  }
}

// ==================== 动画 ====================

.collapse-enter-active,
.collapse-leave-active {
  transition: all 200ms ease;
  max-height: 2000px;
  opacity: 1;
}

.collapse-enter-from,
.collapse-leave-to {
  max-height: 0;
  opacity: 0;
  overflow: hidden;
}
</style>
