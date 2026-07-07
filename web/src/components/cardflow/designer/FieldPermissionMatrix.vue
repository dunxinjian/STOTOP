<script setup lang="ts">
/**
 * 字段权限矩阵（plan M4-2，mock 总体屏 6 + E3）。
 * 行=schema 字段（敏感行高亮），列=流程深度优先序节点（分支色带组头/兜底恒组尾）。
 * 权限数据只存 stages 上（viewProfile），矩阵是视图无副本；列头下拉批量（E3/P-1）。
 */
import { computed } from 'vue'
import { buildFlowTree, type FlowTreeNode } from '@/utils/flowGraphProjection'
import { buildRouteFieldIndex } from '@/utils/routeFieldIndex'
import PermissionTri from './PermissionTri.vue'
import type { PermissionValue } from './permissionTriShared'
import type { StageDefinition, StageAccessMode } from '@/components/cardflow/StageDefinitionEditor.vue'
import type { StageRouteRuleRequest, SchemaFieldDefinition } from '@/types/cardflow'

const props = defineProps<{
  stages: StageDefinition[]
  routes: StageRouteRuleRequest[]
  schemaFields: SchemaFieldDefinition[]
}>()

const emit = defineEmits<{
  /** 打开某节点抽屉的字段权限 Tab */
  'open-stage': [stageKey: string]
}>()

interface MatrixColumn {
  stageKey: string
  name: string
  isManual: boolean
  /** 所属分支名（分支组内节点挂组头色带） */
  branchLabel?: string
  branchDefault?: boolean
}

/** 深度优先展开树 → 列序（仅人工节点可配权限，自动节点列出但只读展示） */
const columns = computed<MatrixColumn[]>(() => {
  const { tree } = buildFlowTree(props.stages, props.routes)
  const byId = new Map(props.stages.map((s) => [s.id, s]))
  const out: MatrixColumn[] = []
  const walk = (nodes: FlowTreeNode[], branchLabel?: string, branchDefault?: boolean) => {
    for (const node of nodes) {
      if (node.kind === 'stage' && node.stageId) {
        const stage = byId.get(node.stageId)
        if (!stage) continue
        out.push({
          stageKey: stage.id,
          name: stage.name || stage.id,
          isManual: stage.type === 'manual',
          branchLabel,
          branchDefault,
        })
      } else if (node.kind === 'branchGroup') {
        // 兜底列恒排组尾（投影已保证 default 最右）
        for (const branch of node.branches ?? []) {
          const label = branch.isDefault ? '其他情况' : `分支`
          walk(branch.children, label, branch.isDefault)
        }
      }
    }
  }
  walk(tree)
  return out
})

const routeIndex = computed(() => buildRouteFieldIndex(props.routes))

const stageByKey = computed(() => new Map(props.stages.map((s) => [s.id, s])))

function accessOf(stageKey: string, fieldKey: string): PermissionValue {
  const stage = stageByKey.value.get(stageKey)
  const configured = stage?.viewProfile?.fieldAccess?.[fieldKey]?.access as StageAccessMode | undefined
  const access = configured || (stage?.inputFields?.includes(fieldKey) ? 'editable' : 'readonly')
  return access === 'required' ? 'editable' : access
}

function setAccess(stageKey: string, fieldKey: string, access: PermissionValue) {
  const stage = stageByKey.value.get(stageKey)
  if (!stage || stage.type !== 'manual') return
  stage.viewProfile ||= { fieldAccess: {}, detailAccess: {}, componentAccess: {}, summary: { fields: [] } }
  stage.viewProfile.fieldAccess ||= {}
  const existing = stage.viewProfile.fieldAccess[fieldKey] || { access: 'readonly' as StageAccessMode }
  stage.viewProfile.fieldAccess[fieldKey] = {
    ...existing,
    access,
    required: access === 'hidden' || access === 'masked' ? false : existing.required,
  }
  if (access === 'editable') {
    stage.inputFields = Array.from(new Set([...(stage.inputFields || []), fieldKey]))
  } else {
    stage.inputFields = (stage.inputFields || []).filter((key) => key !== fieldKey)
  }
}

/** 路由字段在人工节点锁 editable/hidden（改值会使分支判定失效；hidden 则条件不可见不可审） */
function lockedStatesOf(fieldKey: string): PermissionValue[] {
  return routeIndex.value.fields.has(fieldKey) ? ['editable', 'hidden'] : []
}

function lockReasonOf(fieldKey: string): string {
  const edges = routeIndex.value.fields.get(fieldKey)
  if (!edges?.length) return ''
  const names = edges
    .map((e) => props.routes.find((r) => r.edgeKey === e)?.routeName || e)
    .slice(0, 3)
    .join('、')
  return `该字段被条件分支引用（${names}），审批节点锁定为只读/脱敏`
}

/** 敏感字段漏配：非隐藏/脱敏的单元格告警（mock 屏 6 红框） */
function isSensitiveLeak(field: SchemaFieldDefinition, stageKey: string): boolean {
  if (!field.sensitive) return false
  const access = accessOf(stageKey, field.key)
  return access === 'editable' || access === 'readonly'
}

/** 列头批量：整列设置（锁定字段豁免） */
function batchColumn(stageKey: string, access: PermissionValue) {
  let skipped = 0
  for (const field of props.schemaFields) {
    if (lockedStatesOf(field.key).includes(access)) { skipped++; continue }
    setAccess(stageKey, field.key, access)
  }
  return skipped
}
</script>

<template>
  <div class="cfd-matrix">
    <div class="cfd-matrix__hint">
      点单元格切换权限 · 🔒 敏感字段建议隐藏/脱敏（漏配红框告警）· 🔗 路由字段锁只读 · 数据直存节点（与抽屉字段权限 Tab 同源）
    </div>
    <div class="cfd-matrix__scroll">
      <table class="cfd-matrix__table">
        <thead>
          <tr>
            <th class="cfd-matrix__field-col">字段 ＼ 节点</th>
            <th v-for="col in columns" :key="col.stageKey" :class="{ 'is-branch': col.branchLabel, 'is-default-branch': col.branchDefault }">
              <div v-if="col.branchLabel" class="cfd-matrix__branch-band">{{ col.branchLabel }}</div>
              <div class="cfd-matrix__col-head">
                <span class="cfd-matrix__col-name" :title="col.name">{{ col.name }}</span>
                <a-dropdown v-if="col.isManual" :trigger="['click']">
                  <span class="cfd-matrix__col-menu" role="button" tabindex="0" aria-label="列批量操作">▾</span>
                  <template #overlay>
                    <a-menu>
                      <a-menu-item @click="batchColumn(col.stageKey, 'readonly')">整列 → 全部只读</a-menu-item>
                      <a-menu-item @click="batchColumn(col.stageKey, 'hidden')">整列 → 全部隐藏</a-menu-item>
                      <a-menu-item @click="emit('open-stage', col.stageKey)">打开该节点抽屉</a-menu-item>
                    </a-menu>
                  </template>
                </a-dropdown>
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="field in schemaFields" :key="field.key" :class="{ 'is-sensitive': field.sensitive }">
            <th class="cfd-matrix__field-col">
              <span :title="field.label">{{ field.label }}</span>
              <span v-if="field.sensitive" class="cfd-matrix__lock" title="敏感字段">🔒</span>
              <span v-if="routeIndex.fields.has(field.key)" class="cfd-matrix__link" title="被条件分支引用">🔗</span>
            </th>
            <td
              v-for="col in columns"
              :key="col.stageKey"
              :class="{ 'is-leak': col.isManual && isSensitiveLeak(field, col.stageKey) }"
            >
              <PermissionTri
                v-if="col.isManual"
                :value="accessOf(col.stageKey, field.key)"
                :locked-states="lockedStatesOf(field.key)"
                :lock-reason="lockReasonOf(field.key)"
                @update:value="(v: PermissionValue) => setAccess(col.stageKey, field.key, v)"
              />
              <span v-else class="cfd-matrix__auto">自动</span>
            </td>
          </tr>
          <tr v-if="!schemaFields.length">
            <td :colspan="columns.length + 1" class="cfd-matrix__empty">先在「字段设计」步骤添加卡片字段</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.cfd-matrix {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
}

.cfd-matrix__hint {
  font-size: $font-size-sm;
  color: $text-secondary;
}

.cfd-matrix__scroll {
  overflow: auto;
  border: 1px solid $border-color;
  border-radius: 8px;
}

.cfd-matrix__table {
  width: 100%;
  font-size: $font-size-sm;
  border-collapse: collapse;

  th,
  td {
    padding: 7px 10px;
    text-align: center;
    white-space: nowrap;
    border: 1px solid $border-color-faint;
  }

  thead th {
    position: sticky;
    top: 0;
    z-index: 2;
    background: $bg-page;
  }

  thead th.is-branch {
    background: $color-primary-light;
  }

  thead th.is-default-branch {
    background: var(--bg-muted);
  }
}

.cfd-matrix__branch-band {
  padding-bottom: 2px;
  font-size: $font-size-xs;
  color: $color-primary;

  .is-default-branch & {
    color: $text-secondary;
  }
}

.cfd-matrix__col-head {
  display: flex;
  gap: 5px;
  align-items: center;
  justify-content: center;
}

.cfd-matrix__col-name {
  max-width: 110px;
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
}

.cfd-matrix__col-menu {
  color: $text-secondary;
  cursor: pointer;

  &:hover { color: $color-primary; }
}

.cfd-matrix__field-col {
  position: sticky;
  left: 0;
  z-index: 1;
  max-width: 170px;
  overflow: hidden;
  text-align: left !important;
  text-overflow: ellipsis;
  background: $bg-card;

  thead & { z-index: 3; background: $bg-page; }
  tr.is-sensitive & { color: var(--color-danger-text); }
}

tr.is-sensitive td { background: var(--color-danger-light); }

td.is-leak {
  outline: 2px solid var(--color-danger);
  outline-offset: -2px;
}

.cfd-matrix__auto {
  font-size: $font-size-xs;
  color: $text-placeholder;
}

.cfd-matrix__lock,
.cfd-matrix__link {
  margin-left: 3px;
  font-size: $font-size-xs;
}

.cfd-matrix__empty {
  padding: 24px !important;
  color: $text-placeholder;
}
</style>
