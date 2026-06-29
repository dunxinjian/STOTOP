<script setup lang="ts">
import { computed } from 'vue'
import type { DynamicStagePolicyRequest, SchemaFieldDefinition, StageRouteRuleRequest } from '@/types/cardflow'
import type { StageDefinition } from '../StageDefinitionEditor.vue'

const props = defineProps<{
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  dynamicPolicies: DynamicStagePolicyRequest[]
  fields: SchemaFieldDefinition[]
}>()

type HealthLevel = 'error' | 'warning' | 'ok'

interface HealthItem {
  level: HealthLevel
  title: string
  detail: string
}

const stageKeys = computed(() => new Set(props.stages.map(stage => stage.id)))
const fieldMap = computed(() => new Map(props.fields.map(field => [field.key, field])))

const items = computed<HealthItem[]>(() => {
  const result: HealthItem[] = []
  result.push(...checkDefaultRoutes())
  result.push(...checkOverlap())
  result.push(...checkGraph())
  result.push(...checkHandlers())
  result.push(...checkTypeMismatch())
  if (!result.length) {
    result.push({ level: 'ok', title: '规则健康', detail: '默认分支、规则重叠、死路节点、循环路径、无法到达节点和处理人策略均未发现明显问题。' })
  }
  return result
})

function stageName(stageKey: string) {
  return props.stages.find(stage => stage.id === stageKey)?.name || stageKey
}

function checkDefaultRoutes(): HealthItem[] {
  const result: HealthItem[] = []
  const fromKeys = new Set(props.routes.map(route => route.fromStageKey))
  fromKeys.forEach(fromStageKey => {
    if (!props.routes.some(route => route.fromStageKey === fromStageKey && route.isDefault)) {
      result.push({
        level: 'error',
        title: '缺少默认分支',
        detail: `节点「${stageName(fromStageKey)}」配置了条件流转，但没有“其他情况”默认分支。`,
      })
    }
  })
  return result
}

function parseCondition(json?: string | null): any {
  if (!json) return null
  try { return JSON.parse(json) } catch { return null }
}

function flattenConditions(condition: any): any[] {
  if (!condition) return []
  if (Array.isArray(condition.conditions)) {
    return condition.conditions.flatMap(flattenConditions)
  }
  return condition.field ? [condition] : []
}

function checkOverlap(): HealthItem[] {
  const result: HealthItem[] = []
  const groups = new Map<string, StageRouteRuleRequest[]>()
  props.routes.filter(route => !route.isDefault).forEach(route => {
    groups.set(route.fromStageKey, [...(groups.get(route.fromStageKey) || []), route])
  })
  groups.forEach(routes => {
    for (let i = 0; i < routes.length; i++) {
      for (let j = i + 1; j < routes.length; j++) {
        const left = flattenConditions(parseCondition(routes[i].conditionJson))
        const right = flattenConditions(parseCondition(routes[j].conditionJson))
        const sharedAmountGt = left.some(a => a.field === 'amount' && ['gt', 'gte'].includes(a.operator))
          && right.some(a => a.field === 'amount' && ['gt', 'gte'].includes(a.operator))
        if (sharedAmountGt) {
          result.push({
            level: 'warning',
            title: '规则重叠',
            detail: `「${routes[i].routeName}」和「${routes[j].routeName}」都可能命中金额大于类条件，请确认优先级。`,
          })
        }
      }
    }
  })
  return result
}

function checkGraph(): HealthItem[] {
  const result: HealthItem[] = []
  const outgoing = new Map<string, string[]>()
  props.routes.forEach(route => {
    outgoing.set(route.fromStageKey, [...(outgoing.get(route.fromStageKey) || []), route.toStageKey])
  })
  props.stages.slice(0, -1).forEach(stage => {
    if (props.routes.length > 0 && !outgoing.has(stage.id)) {
      result.push({
        level: 'warning',
        title: '死路节点',
        detail: `节点「${stage.name || stage.id}」没有后续条件边，运行时可能停在该节点。`,
      })
    }
  })

  const first = props.stages[0]?.id
  if (first) {
    const visited = new Set<string>()
    const visiting = new Set<string>()
    const dfs = (key: string) => {
      if (visiting.has(key)) {
        result.push({ level: 'error', title: '循环路径', detail: `检测到从「${stageName(key)}」回到已访问路径的循环。` })
        return
      }
      if (visited.has(key)) return
      visiting.add(key)
      ;(outgoing.get(key) || []).forEach(next => dfs(next))
      visiting.delete(key)
      visited.add(key)
    }
    dfs(first)
    props.stages.forEach(stage => {
      if (!visited.has(stage.id)) {
        result.push({
          level: 'warning',
          title: '无法到达',
          detail: `节点「${stage.name || stage.id}」不在当前条件边可达路径上。`,
        })
      }
    })
  }
  return result
}

function checkHandlers(): HealthItem[] {
  const result: HealthItem[] = []
  props.dynamicPolicies.forEach(policy => {
    const fallback = parseCondition(policy.fallbackJson)
    if (!fallback?.type) {
      result.push({
        level: 'error',
        title: '处理人兜底缺失',
        detail: `动态策略「${policy.policyName}」没有 fallback，处理人解析失败时无法安全兜底。`,
      })
    }
  })
  props.stages.filter(stage => stage.type === 'manual').forEach(stage => {
    if (!stage.assigneeStrategy) {
      result.push({
        level: 'error',
        title: '处理人策略缺失',
        detail: `人工节点「${stage.name || stage.id}」没有配置处理人策略。`,
      })
    }
  })
  return result
}

function checkTypeMismatch(): HealthItem[] {
  const result: HealthItem[] = []
  const numericOperators = new Set(['gt', 'gte', 'lt', 'lte'])
  const inspect = (owner: string, json?: string | null) => {
    flattenConditions(parseCondition(json)).forEach(condition => {
      const field = fieldMap.value.get(condition.field)
      if (!field) {
        result.push({ level: 'error', title: '字段不存在', detail: `${owner} 引用了不存在的字段「${condition.field}」。` })
        return
      }
      if (numericOperators.has(condition.operator) && !['money', 'number'].includes(field.type)) {
        result.push({ level: 'error', title: '字段类型不匹配', detail: `${owner} 使用 ${condition.operator} 比较非金额/数字字段「${field.label}」。` })
      }
    })
  }
  props.routes.forEach(route => inspect(`流转规则「${route.routeName}」`, route.conditionJson))
  props.dynamicPolicies.forEach(policy => inspect(`动态策略「${policy.policyName}」`, policy.conditionJson))
  return result
}
</script>

<template>
  <section class="cf-rule-health">
    <header class="cf-rule-health__head">
      <strong>规则健康检查</strong>
      <span>默认分支 · 规则重叠 · 死路节点 · 循环路径 · 无法到达 · 处理人</span>
    </header>
    <div class="cf-rule-health__list">
      <article
        v-for="item in items"
        :key="`${item.title}-${item.detail}`"
        class="cf-rule-health__item"
        :class="`cf-rule-health__item--${item.level}`"
      >
        <strong>{{ item.title }}</strong>
        <span>{{ item.detail }}</span>
      </article>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-rule-health {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.cf-rule-health__head {
  strong,
  span {
    display: block;
  }

  strong {
    color: var(--text-1);
    font-size: 14px;
  }

  span {
    margin-top: 2px;
    color: var(--text-2);
    font-size: 12px;
  }
}

.cf-rule-health__list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cf-rule-health__item {
  padding: 9px 10px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg-card);

  strong,
  span {
    display: block;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
  }

  span {
    margin-top: 3px;
    color: var(--text-2);
    font-size: 12px;
    line-height: 1.5;
  }

  &--error {
    border-color: var(--color-danger);
    background: var(--color-danger-light);
  }

  &--warning {
    border-color: var(--color-warning);
    background: var(--color-warning-light);
  }

  &--ok {
    border-color: var(--color-success);
    background: var(--color-success-light);
  }
}
</style>
