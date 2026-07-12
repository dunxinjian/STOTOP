<script setup lang="ts">
/**
 * 竖向流程图节点卡片（mock 总体屏 3 .fnode / 设计 C2 状态全集）。
 * 结构类来自 cardflow-designer.scss（.cfd-node 族），本组件只写状态绑定不重写样式。
 */
import { computed } from 'vue'
import { DeleteOutlined } from '@ant-design/icons-vue'
import type { StageDefinition } from '@/components/cardflow/StageDefinitionEditor.vue'
import { formatAssigneeSummary, stageVisualKind } from '@/components/cardflow/stageDefinitionShared'

const props = defineProps<{
  stage: StageDefinition
  selected: boolean
  /** 干跑命中路径高亮（M5-4） */
  hit?: boolean
  /** 该节点的诊断计数（错误+警告，>0 显示角标） */
  issueCount?: number
  /** 该节点的错误级计数（决定角标色） */
  errorCount?: number
}>()

const emit = defineEmits<{ select: []; remove: [] }>()

/** 节点视觉分类：审/自/抄 三类（起/终由竖向图外层渲染）+ 图标字（设计 D8 色盲冗余：色+字+边框三重编码） */
const visualKind = computed(() => stageVisualKind(props.stage))
const kindClass = computed(() => {
  switch (visualKind.value) {
    case 'cc': return 'cfd-node--cc'
    case 'auto': return 'cfd-node--auto is-auto'
    default: return 'cfd-node--appr'
  }
})
const iconChar = computed(() => {
  switch (visualKind.value) {
    case 'cc': return '抄'
    case 'auto': return '自'
    default: return '审'
  }
})

const approvalModeText = computed(() => {
  switch (props.stage.approvalMode) {
    case 'single': return '单人'
    case 'orsign': return '或签'
    case 'countersign': return '会签'
    case 'sequential': return '依次'
    default: return ''
  }
})

const assigneeText = computed(() => formatAssigneeSummary(props.stage))

/** 抄送节点摘要（抄送给谁；解析 ccConfigJson，容错） */
const ccText = computed(() => {
  if (visualKind.value !== 'cc') return ''
  if (!props.stage.ccConfigJson) return '未配置抄送对象'
  try {
    const cfg = JSON.parse(props.stage.ccConfigJson)
    const users: Array<{ userName?: string; userId?: unknown }> = Array.isArray(cfg?.users) ? cfg.users : []
    if (users.length) {
      const names = users.map((u) => u.userName || `#${u.userId}`)
      return names.length > 2 ? `${names.slice(0, 2).join('、')} 等 ${names.length} 人` : names.join('、')
    }
    if (cfg?.roleName || cfg?.roleCode) return `角色·${cfg.roleName || cfg.roleCode}`
    return '未配置抄送对象'
  } catch {
    return '未配置抄送对象'
  }
})

/** 节点类型文案（无障碍播报） */
const kindLabel = computed(() => {
  switch (visualKind.value) {
    case 'cc': return '抄送节点'
    case 'auto': return '自动处理节点'
    default: return '审批节点'
  }
})
</script>

<template>
  <div
    class="cfd-node"
    :class="[kindClass, { 'is-selected': selected, 'is-hit': hit }]"
    role="treeitem"
    :aria-selected="selected"
    tabindex="0"
    :aria-label="`${kindLabel} ${stage.name || '未命名'}${issueCount ? `，${issueCount} 个问题` : ''}，按 Enter 编辑，按 Delete 删除`"
    @click="emit('select')"
    @keydown.enter.prevent="emit('select')"
    @keydown.delete.prevent="emit('remove')"
  >
    <span v-if="issueCount" class="cfd-node__errdot" :class="{ 'is-warn': !errorCount }">{{ issueCount }}</span>
    <span class="cfd-node__tools" @click.stop>
      <span role="button" tabindex="-1" title="删除节点" aria-label="删除节点" @click="emit('remove')"><DeleteOutlined /></span>
    </span>
    <div class="cfd-node__head">
      <span class="cfd-node__icon">{{ iconChar }}</span>
      <span class="cfd-node__title">{{ stage.name || '未命名节点' }}</span>
      <a-tag v-if="visualKind === 'cc'" class="cfd-node__tag" :bordered="false">抄送</a-tag>
      <a-tag v-else-if="stage.type === 'auto'" class="cfd-node__tag" :bordered="false">自动</a-tag>
      <a-tag v-else-if="approvalModeText" class="cfd-node__tag" color="blue" :bordered="false">{{ approvalModeText }}</a-tag>
    </div>
    <div v-if="visualKind === 'cc'" class="cfd-node__body">
      <div class="cfd-node__kv">
        <span class="cfd-node__kk">抄送给</span>
        <span class="cfd-node__vv">{{ ccText }}</span>
      </div>
    </div>
    <div v-else-if="assigneeText" class="cfd-node__body">
      <div class="cfd-node__kv">
        <span class="cfd-node__kk">处理人</span>
        <span class="cfd-node__vv">{{ assigneeText }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
// 结构样式在全局 cardflow-designer.scss（.cfd-node 族）；此处仅补组件内特有微调
.cfd-node {
  cursor: pointer;

  &:focus-visible {
    outline: 2px solid var(--color-primary);
    outline-offset: 2px;
  }

  // 悬停工具条（设计 C2/C7）：默认隐藏，悬停/聚焦浮现
  .cfd-node__tools {
    visibility: hidden;
  }

  &:hover .cfd-node__tools,
  &:focus-within .cfd-node__tools {
    visibility: visible;
  }
}

.cfd-node__errdot.is-warn {
  background: var(--color-warning);
}

// 干跑命中路径高亮（M5-4，设计 D9 命中态绿）
.cfd-node.is-hit {
  border-color: var(--color-success);
  box-shadow: 0 0 0 3px var(--color-success-light), var(--shadow-card);
}

.cfd-node__tag {
  flex: none;
  margin-inline-end: 0;
}

.cfd-node__vv {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
