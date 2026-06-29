<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  InboxOutlined,
  LinkOutlined,
  FolderOpenOutlined,
  EllipsisOutlined,
  ArrowRightOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import FlowGroupConnectionEditor from '@/components/cardflow/FlowGroupConnectionEditor.vue'
import {
  getFlowGroups,
  createFlowGroup,
  updateFlowGroup,
  getFlowGroupLinks,
  saveFlowGroupLinks,
  getFlowDefinitions,
  updateFlowDefinition,
} from '@/api/cardflow'
import type {
  FlowGroupDto,
  FlowDefinitionDto,
  FlowGroupLinkDto,
  CreateFlowGroupRequest,
  SaveFlowGroupLinkRequest,
} from '@/types/cardflow'

const orgId = 1
const loading = ref(false)
const groups = ref<FlowGroupDto[]>([])
const allFlows = ref<FlowDefinitionDto[]>([])
const selectedGroupId = ref<number | null>(null)
const links = ref<FlowGroupLinkDto[]>([])
const linksLoading = ref(false)

const selectedGroup = computed(() =>
  groups.value.find(g => g.id === selectedGroupId.value) || null
)

const groupFlows = computed(() =>
  allFlows.value.filter(f => f.flowGroupId === selectedGroupId.value),
)

async function loadGroups(preserveSelection = true) {
  loading.value = true
  try {
    const [g, f] = await Promise.all([
      getFlowGroups(orgId),
      getFlowDefinitions({ pageSize: 500 }),
    ])
    groups.value = (g as FlowGroupDto[]) || []
    allFlows.value = f?.items || []

    if (!preserveSelection || selectedGroupId.value == null) {
      if (groups.value.length) await selectGroup(groups.value[0].id)
    } else if (!groups.value.find(x => x.id === selectedGroupId.value)) {
      const next = groups.value[0]?.id ?? null
      if (next != null) await selectGroup(next)
      else { selectedGroupId.value = null; links.value = [] }
    }
  } catch {
    message.error('加载分组列表失败')
  } finally {
    loading.value = false
  }
}

async function loadLinks(gid: number) {
  linksLoading.value = true
  try {
    const r = await getFlowGroupLinks(gid)
    links.value = (r as FlowGroupLinkDto[]) || []
  } catch {
    message.error('加载连接配置失败')
  } finally {
    linksLoading.value = false
  }
}

async function selectGroup(id: number) {
  selectedGroupId.value = id
  await loadLinks(id)
}

async function refreshAll() {
  await loadGroups(true)
  if (selectedGroupId.value) await loadLinks(selectedGroupId.value)
}

// ====== 新建分组 ======
const createVisible = ref(false)
const creating = ref(false)
const createForm = reactive<CreateFlowGroupRequest>({
  groupName: '', groupCode: '', description: '', orgId,
})
function openCreate() {
  Object.assign(createForm, { groupName: '', groupCode: '', description: '', orgId })
  createVisible.value = true
}
async function submitCreate() {
  if (!createForm.groupName.trim()) return message.warning('请输入分组名称')
  if (!createForm.groupCode.trim()) return message.warning('请输入分组编码')
  creating.value = true
  try {
    const created = await createFlowGroup({ ...createForm }) as FlowGroupDto
    message.success('分组已创建')
    createVisible.value = false
    await loadGroups(false)
    if (created?.id) await selectGroup(created.id)
  } catch { message.error('创建失败') }
  finally { creating.value = false }
}

// ====== 重命名分组 ======
const renameVisible = ref(false)
const renameForm = reactive({ id: 0, groupName: '', description: '' })
function openRename(g: FlowGroupDto) {
  renameForm.id = g.id
  renameForm.groupName = g.groupName
  renameForm.description = g.description || ''
  renameVisible.value = true
}
async function submitRename() {
  if (!renameForm.id) return
  if (!renameForm.groupName.trim()) return message.warning('请输入名称')
  try {
    await updateFlowGroup(renameForm.id, {
      groupName: renameForm.groupName.trim(),
      description: renameForm.description || undefined,
    })
    message.success('已更新')
    renameVisible.value = false
    await loadGroups(true)
  } catch { message.error('更新失败') }
}

// ====== 归档分组 ======
async function archiveGroup(g: FlowGroupDto) {
  try {
    await updateFlowGroup(g.id, { status: 'archived' })
    message.success('分组已归档')
    await loadGroups(false)
  } catch { message.error('归档失败') }
}

// ====== 关联流程 ======
const linkFlowsVisible = ref(false)
const linkFlowsLoading = ref(false)
const linkSelected = ref<number[]>([])
const linkSearch = ref('')
const linkCandidates = computed(() => {
  const kw = linkSearch.value.trim().toLowerCase()
  return allFlows.value.filter(f => {
    if (!kw) return true
    return (f.flowName || '').toLowerCase().includes(kw)
      || (f.flowCode || '').toLowerCase().includes(kw)
  })
})
function openLinkFlows() {
  if (!selectedGroupId.value) return
  linkSelected.value = groupFlows.value.map(f => f.id)
  linkSearch.value = ''
  linkFlowsVisible.value = true
}
async function submitLinkFlows() {
  if (!selectedGroupId.value) return
  linkFlowsLoading.value = true
  try {
    const targetId = selectedGroupId.value
    const oldIds = new Set(groupFlows.value.map(f => f.id))
    const newIds = new Set(linkSelected.value)
    const toAdd = [...newIds].filter(id => !oldIds.has(id))
    const toRemove = [...oldIds].filter(id => !newIds.has(id))
    await Promise.all([
      ...toAdd.map(id => updateFlowDefinition(id, { flowGroupId: targetId })),
      ...toRemove.map(id => updateFlowDefinition(id, { flowGroupId: null })),
    ])
    message.success('关联已更新')
    linkFlowsVisible.value = false
    await loadGroups(true)
  } catch { message.error('更新失败') }
  finally { linkFlowsLoading.value = false }
}
async function unlinkFlow(flow: FlowDefinitionDto) {
  try {
    await updateFlowDefinition(flow.id, { flowGroupId: null })
    message.success(`已移除「${flow.flowName}」`)
    await loadGroups(true)
  } catch { message.error('移除失败') }
}

// ====== 连接管理 ======
const connVisible = ref(false)
const connEditing = ref<FlowGroupLinkDto | null>(null)
function openCreateConn() {
  if (!selectedGroupId.value) return
  if (groupFlows.value.length < 2) {
    message.warning('请先关联至少 2 个流程后再添加连接')
    return
  }
  connEditing.value = null
  connVisible.value = true
}
function openEditConn(l: FlowGroupLinkDto) {
  connEditing.value = { ...l }
  connVisible.value = true
}
async function onConnSave(payload: SaveFlowGroupLinkRequest & { id?: number }) {
  if (!selectedGroupId.value) return
  try {
    const editingId = payload.id
    const next: SaveFlowGroupLinkRequest[] = links.value
      .filter(l => l.id !== editingId)
      .map(l => ({
        sourceFlowId: l.sourceFlowId,
        targetFlowId: l.targetFlowId,
        triggerCondition: l.triggerCondition || undefined,
        fieldMappingJson: l.fieldMappingJson || undefined,
        triggerMode: l.triggerMode,
        sortOrder: l.sortOrder,
      }))
    next.push({
      sourceFlowId: payload.sourceFlowId,
      targetFlowId: payload.targetFlowId,
      triggerCondition: payload.triggerCondition,
      fieldMappingJson: payload.fieldMappingJson,
      triggerMode: payload.triggerMode || 'auto',
      sortOrder: payload.sortOrder ?? next.length + 1,
    })
    await saveFlowGroupLinks(selectedGroupId.value, next)
    message.success('已保存')
    connVisible.value = false
    await loadLinks(selectedGroupId.value)
  } catch { message.error('保存失败') }
}
async function deleteConn(l: FlowGroupLinkDto) {
  if (!selectedGroupId.value) return
  try {
    const next: SaveFlowGroupLinkRequest[] = links.value
      .filter(x => x.id !== l.id)
      .map(x => ({
        sourceFlowId: x.sourceFlowId,
        targetFlowId: x.targetFlowId,
        triggerCondition: x.triggerCondition || undefined,
        fieldMappingJson: x.fieldMappingJson || undefined,
        triggerMode: x.triggerMode,
        sortOrder: x.sortOrder,
      }))
    await saveFlowGroupLinks(selectedGroupId.value, next)
    message.success('连接已删除')
    await loadLinks(selectedGroupId.value)
  } catch { message.error('删除失败') }
}

// ====== 连接表格列 ======
const connColumns: TableColumnsType = [
  { title: '源流程', dataIndex: 'sourceFlowName', key: 'src', width: 160, ellipsis: true },
  { title: '目标流程', dataIndex: 'targetFlowName', key: 'tgt', width: 160, ellipsis: true },
  { title: '触发条件摘要', key: 'cond', ellipsis: true },
  { title: '触发方式', key: 'mode', width: 110, align: 'center' },
  { title: '排序', dataIndex: 'sortOrder', key: 'sort', width: 70, align: 'center' },
  { title: '操作', key: 'action', width: 130, align: 'center', fixed: 'right' },
]

const triggerModeMap: Record<string, { label: string; color: string }> = {
  auto: { label: '自动触发', color: 'success' },
  suggest: { label: '建议发起', color: 'processing' },
  manual: { label: '手动触发', color: 'default' },
}

function summarizeCondition(s: string | null): string {
  if (!s) return '—'
  try {
    const obj = JSON.parse(s)
    if (obj && Array.isArray(obj.conditions)) {
      const n = obj.conditions.length
      if (!n) return '—'
      const logic = obj.logic === 'or' ? '或' : '且'
      return `${n} 个条件（${logic}）`
    }
  } catch { /* ignore */ }
  return s.length > 24 ? s.slice(0, 24) + '…' : s
}

const statusBadge: Record<string, { text: string; color: string }> = {
  active: { text: '启用', color: 'success' },
  archived: { text: '已归档', color: 'default' },
}

onMounted(() => loadGroups(false))
</script>

<template>
  <div class="page-container">
    <PageHeader title="流程分组管理">
      <template #actions>
        <a-space>
          <a-button type="primary" @click="openCreate">
            <template #icon><PlusOutlined /></template>新建分组
          </a-button>
        </a-space>
      </template>
    </PageHeader>

    <div class="fg-shell">
      <!-- 左侧分组列表 -->
      <aside class="fg-sidebar">
        <div class="fg-sidebar-head">
          <span class="fg-sidebar-title">分组</span>
          <span class="fg-sidebar-count">{{ groups.length }}</span>
        </div>

        <a-spin :spinning="loading" wrapper-class-name="fg-sidebar-spin">
          <div v-if="!loading && !groups.length" class="fg-sidebar-empty">
            <FolderOpenOutlined class="fg-sidebar-empty-icon" />
            <div class="fg-sidebar-empty-text">暂无分组</div>
            <a-button type="link" size="small" @click="openCreate">立即创建</a-button>
          </div>

          <ul v-else class="fg-list">
            <li
              v-for="g in groups"
              :key="g.id"
              class="fg-list-item"
              :class="{ active: g.id === selectedGroupId, archived: g.status === 'archived' }"
              @click="selectGroup(g.id)"
            >
              <a-dropdown :trigger="['contextmenu']">
                <div class="fg-list-row">
                  <span class="fg-list-bar"></span>
                  <span class="fg-list-name" :title="g.groupName">{{ g.groupName }}</span>
                  <span v-if="g.status === 'archived'" class="fg-list-archived">归档</span>
                  <span class="fg-list-counts">
                    <span class="fg-list-counts-flow">{{ g.flowCount }}</span>
                    <span class="fg-list-counts-sep">·</span>
                    <span class="fg-list-counts-link">{{ g.linkCount }}</span>
                  </span>
                  <a-dropdown :trigger="['click']" placement="bottomRight">
                    <a-button
                      type="text"
                      size="small"
                      class="fg-list-more"
                      @click.stop
                    >
                      <EllipsisOutlined />
                    </a-button>
                    <template #overlay>
                      <a-menu>
                        <a-menu-item key="rename" @click="openRename(g)">
                          <EditOutlined /> 重命名
                        </a-menu-item>
                        <a-menu-item key="archive" v-if="g.status !== 'archived'">
                          <a-popconfirm
                            title="归档后该分组将不再展示新流程关联，确认归档？"
                            ok-text="归档"
                            cancel-text="取消"
                            @confirm="archiveGroup(g)"
                          >
                            <span class="fg-menu-danger"><InboxOutlined /> 归档</span>
                          </a-popconfirm>
                        </a-menu-item>
                      </a-menu>
                    </template>
                  </a-dropdown>
                </div>
                <template #overlay>
                  <a-menu>
                    <a-menu-item key="rename" @click="openRename(g)">
                      <EditOutlined /> 重命名
                    </a-menu-item>
                    <a-menu-item key="archive" v-if="g.status !== 'archived'">
                      <a-popconfirm
                        title="归档后该分组将不再展示新流程关联，确认归档？"
                        ok-text="归档"
                        cancel-text="取消"
                        @confirm="archiveGroup(g)"
                      >
                        <span class="fg-menu-danger"><InboxOutlined /> 归档</span>
                      </a-popconfirm>
                    </a-menu-item>
                  </a-menu>
                </template>
              </a-dropdown>
            </li>
          </ul>
        </a-spin>

        <div class="fg-sidebar-foot">
          <a-button type="link" block @click="openCreate">
            <PlusOutlined /> 新建分组
          </a-button>
        </div>
      </aside>

      <!-- 右侧详情面板 -->
      <main class="fg-detail">
        <div v-if="!selectedGroup" class="fg-empty fg-empty-noselect">
          <ArrowRightOutlined class="fg-empty-arrow" :rotate="180" />
          <div class="fg-empty-text">← 请选择一个流程组</div>
        </div>

        <template v-else>
          <header class="fg-detail-head">
            <div class="fg-detail-titles">
              <h2 class="fg-detail-name">
                {{ selectedGroup.groupName }}
                <a-button
                  type="text"
                  size="small"
                  class="fg-detail-edit"
                  :title="'编辑名称'"
                  @click="openRename(selectedGroup)"
                >
                  <EditOutlined />
                </a-button>
              </h2>
              <div class="fg-detail-meta">
                <span class="fg-detail-code">{{ selectedGroup.groupCode }}</span>
                <a-tag
                  :color="statusBadge[selectedGroup.status]?.color || 'default'"
                  class="fg-detail-status"
                >
                  {{ statusBadge[selectedGroup.status]?.text || selectedGroup.status }}
                </a-tag>
                <span v-if="selectedGroup.description" class="fg-detail-desc">
                  {{ selectedGroup.description }}
                </span>
              </div>
            </div>
          </header>

          <section class="fg-section">
            <div class="fg-section-head">
              <span class="fg-section-title">
                组内流程
                <span class="fg-section-count">{{ groupFlows.length }}</span>
              </span>
              <a-button type="primary" ghost size="small" @click="openLinkFlows">
                <template #icon><LinkOutlined /></template>关联流程
              </a-button>
            </div>

            <div v-if="!groupFlows.length" class="fg-flow-empty">
              该分组暂无流程，点击上方 [+ 关联流程] 添加
            </div>
            <div v-else class="fg-flow-tags">
              <a-tag
                v-for="f in groupFlows"
                :key="f.id"
                color="processing"
                closable
                class="fg-flow-tag"
                @close.prevent="unlinkFlow(f)"
              >
                <FolderOpenOutlined class="fg-flow-tag-icon" />
                {{ f.flowName }}
                <span class="fg-flow-tag-code">{{ f.flowCode }}</span>
              </a-tag>
            </div>
          </section>

          <div class="fg-divider"></div>

          <section class="fg-section">
            <div class="fg-section-head">
              <span class="fg-section-title">
                流程连接
                <span class="fg-section-count">{{ links.length }}</span>
              </span>
              <a-button type="primary" ghost size="small" @click="openCreateConn">
                <template #icon><PlusOutlined /></template>添加连接
              </a-button>
            </div>

            <a-table
              :columns="connColumns"
              :data-source="links"
              :loading="linksLoading"
              row-key="id"
              :pagination="false"
              size="small"
              :scroll="{ x: 760 }"
              class="fg-conn-table"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'cond'">
                  <span class="fg-cond-summary">{{ summarizeCondition(record.triggerCondition) }}</span>
                </template>
                <template v-if="column.key === 'mode'">
                  <a-tag :color="triggerModeMap[record.triggerMode]?.color || 'default'">
                    {{ triggerModeMap[record.triggerMode]?.label || record.triggerMode }}
                  </a-tag>
                </template>
                <template v-if="column.key === 'action'">
                  <a-space :size="4">
                    <a-button type="link" size="small" @click="openEditConn(record as FlowGroupLinkDto)">
                      <EditOutlined /> 编辑
                    </a-button>
                    <a-popconfirm
                      title="确认删除该连接？"
                      ok-text="删除"
                      ok-type="danger"
                      cancel-text="取消"
                      @confirm="deleteConn(record as FlowGroupLinkDto)"
                    >
                      <a-button type="link" size="small" danger>
                        <DeleteOutlined /> 删除
                      </a-button>
                    </a-popconfirm>
                  </a-space>
                </template>
              </template>
              <template #emptyText>
                <div class="fg-conn-empty">
                  <span>暂无连接配置</span>
                  <a-button type="link" size="small" @click="openCreateConn">+ 添加连接</a-button>
                </div>
              </template>
            </a-table>
          </section>
        </template>
      </main>
    </div>

    <!-- 新建分组 -->
    <a-modal
      v-model:open="createVisible"
      title="新建分组"
      :width="460"
      :confirm-loading="creating"
      @ok="submitCreate"
    >
      <a-form layout="vertical">
        <a-form-item label="分组名称" required>
          <a-input v-model:value="createForm.groupName" placeholder="如：费用报销组" />
        </a-form-item>
        <a-form-item label="分组编码" required>
          <a-input v-model:value="createForm.groupCode" placeholder="如：expense_group" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="createForm.description" :rows="3" placeholder="可选" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 重命名分组 -->
    <a-modal
      v-model:open="renameVisible"
      title="编辑分组名称"
      :width="460"
      @ok="submitRename"
    >
      <a-form layout="vertical">
        <a-form-item label="分组名称" required>
          <a-input v-model:value="renameForm.groupName" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="renameForm.description" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 关联流程 -->
    <a-modal
      v-model:open="linkFlowsVisible"
      title="关联流程"
      :width="560"
      :confirm-loading="linkFlowsLoading"
      @ok="submitLinkFlows"
    >
      <div class="fg-link-modal">
        <a-input-search
          v-model:value="linkSearch"
          placeholder="按名称或编码搜索"
          allow-clear
          class="fg-link-search"
        />
        <div class="fg-link-list">
          <a-checkbox-group v-model:value="linkSelected" class="fg-link-cbgroup">
            <div
              v-for="f in linkCandidates"
              :key="f.id"
              class="fg-link-row"
              :class="{ disabled: f.flowGroupId && f.flowGroupId !== selectedGroupId }"
            >
              <a-checkbox
                :value="f.id"
                :disabled="!!f.flowGroupId && f.flowGroupId !== selectedGroupId"
              >
                <span class="fg-link-name">{{ f.flowName }}</span>
                <span class="fg-link-code">{{ f.flowCode }}</span>
                <span
                  v-if="f.flowGroupId && f.flowGroupId !== selectedGroupId"
                  class="fg-link-tip"
                >已属其他分组</span>
              </a-checkbox>
            </div>
            <div v-if="!linkCandidates.length" class="fg-link-empty">未找到匹配的流程</div>
          </a-checkbox-group>
        </div>
      </div>
    </a-modal>

    <!-- 连接编辑器 -->
    <FlowGroupConnectionEditor
      v-if="selectedGroupId != null"
      v-model:visible="connVisible"
      :connection="connEditing"
      :group-id="String(selectedGroupId)"
      @save="onConnSave"
    />
  </div>
</template>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.page-container {
  padding: 0;
  background: $bg-page;
  min-height: 100%;
}

.fg-shell {
  display: flex;
  align-items: stretch;
  min-height: calc(100vh - 110px);
  background: $bg-card;
}

/* ===== 左侧分组列表 ===== */
.fg-sidebar {
  width: 240px;
  flex-shrink: 0;
  border-right: 1px solid $border-color;
  display: flex;
  flex-direction: column;
  background: linear-gradient(180deg, var(--bg-muted) 0%, var(--bg-card) 64%);
}

.fg-sidebar-head {
  height: 44px;
  padding: 0 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid $border-color-lighter;
}

.fg-sidebar-title {
  font-size: 13px;
  font-weight: 600;
  color: $text-primary;
  letter-spacing: 0.4px;
}

.fg-sidebar-count {
  font-size: 12px;
  color: $text-secondary;
  background: var(--bg-muted);
  padding: 1px 8px;
  border-radius: 10px;
}

:deep(.fg-sidebar-spin) {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.fg-sidebar-empty {
  padding: 36px 16px;
  text-align: center;
  color: $text-secondary;

  .fg-sidebar-empty-icon {
    font-size: 28px;
    color: var(--text-3);
    margin-bottom: 8px;
  }
  .fg-sidebar-empty-text {
    font-size: 13px;
    margin-bottom: 4px;
  }
}

.fg-list {
  list-style: none;
  margin: 0;
  padding: 6px 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.fg-list-item {
  position: relative;
  border-radius: 6px;
  cursor: pointer;
  transition: $transition-fast;

  &:hover .fg-list-row {
    background: var(--color-primary-light);
  }

  &.active .fg-list-row {
    background: $color-primary-light;
    color: $color-primary;
  }
  &.active .fg-list-bar {
    background: $color-primary;
  }
  &.active .fg-list-name {
    color: $color-primary;
    font-weight: 600;
  }

  &.archived {
    opacity: 0.62;
    .fg-list-name {
      color: $text-secondary;
      font-style: italic;
    }
  }
}

.fg-list-row {
  height: 36px;
  padding: 0 8px 0 4px;
  display: flex;
  align-items: center;
  gap: 6px;
  border-radius: 6px;
  transition: background 0.18s ease;
  position: relative;
}

.fg-list-bar {
  width: 3px;
  height: 18px;
  border-radius: 2px;
  background: transparent;
  flex-shrink: 0;
  transition: background 0.18s ease;
}

.fg-list-name {
  flex: 1;
  font-size: 13px;
  color: $text-primary;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.fg-list-archived {
  font-size: 10px;
  color: $text-placeholder;
  border: 1px solid $border-color;
  border-radius: 3px;
  padding: 0 4px;
  line-height: 16px;
}

.fg-list-counts {
  display: flex;
  align-items: center;
  gap: 3px;
  font-size: 11px;
  color: $text-placeholder;
  font-variant-numeric: tabular-nums;

  .fg-list-counts-flow { color: $text-secondary; }
  .fg-list-counts-link { color: $text-secondary; opacity: 0.7; }
}

.fg-list-more {
  width: 22px;
  height: 22px;
  padding: 0 !important;
  opacity: 0;
  transition: opacity 0.15s ease;
}

.fg-list-item:hover .fg-list-more,
.fg-list-item.active .fg-list-more {
  opacity: 1;
}

.fg-menu-danger {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: $color-danger;
}

.fg-sidebar-foot {
  border-top: 1px solid $border-color-lighter;
  padding: 4px 8px;

  :deep(.ant-btn-link) {
    text-align: left;
    padding-left: 8px;
    color: $color-primary;
    font-size: 13px;
  }
}

/* ===== 右侧详情面板 ===== */
.fg-detail {
  flex: 1;
  min-width: 0;
  padding: 20px 24px 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow: auto;
}

.fg-empty-noselect {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: $text-placeholder;

  .fg-empty-arrow {
    font-size: 28px;
    margin-bottom: 12px;
    color: var(--text-3);
    transform: rotate(180deg);
  }
  .fg-empty-text {
    font-size: 14px;
    letter-spacing: 0.5px;
  }
}

.fg-detail-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 4px;
}

.fg-detail-titles {
  flex: 1;
  min-width: 0;
}

.fg-detail-name {
  margin: 0 0 6px;
  font-size: 18px;
  font-weight: 600;
  color: $text-primary;
  display: flex;
  align-items: center;
  gap: 4px;
}

.fg-detail-edit {
  color: $text-secondary;
  &:hover { color: $color-primary; }
}

.fg-detail-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 12px;
  color: $text-secondary;
}

.fg-detail-code {
  font-family: 'JetBrains Mono', 'SFMono-Regular', Consolas, monospace;
  font-size: 11px;
  background: var(--bg-muted);
  padding: 1px 6px;
  border-radius: 3px;
  color: $text-regular;
}

.fg-detail-desc {
  color: $text-secondary;
  font-size: 12px;
  border-left: 2px solid $border-color-lighter;
  padding-left: 8px;
  line-height: 1.5;
}

/* ===== 区块通用 ===== */
.fg-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.fg-section-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.fg-section-title {
  font-size: 14px;
  font-weight: 600;
  color: $text-primary;
  display: flex;
  align-items: center;
  gap: 8px;

  .fg-section-count {
    background: var(--bg-muted);
    color: $text-secondary;
    font-size: 11px;
    font-weight: 500;
    padding: 0 8px;
    border-radius: 10px;
    line-height: 18px;
    min-width: 18px;
    text-align: center;
  }
}

.fg-divider {
  height: 1px;
  background: linear-gradient(90deg, transparent, $border-color-lighter 20%, $border-color-lighter 80%, transparent);
  margin: 4px 0;
}

/* ===== 关联流程 Tag ===== */
.fg-flow-empty {
  text-align: center;
  color: $text-placeholder;
  font-size: 13px;
  padding: 24px 16px;
  border: 1px dashed $border-color-lighter;
  border-radius: $border-radius-md;
  background: var(--bg-muted);
}

.fg-flow-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.fg-flow-tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px 4px 8px;
  font-size: 13px;
  border-radius: 14px;

  .fg-flow-tag-icon {
    font-size: 12px;
    opacity: 0.7;
  }
  .fg-flow-tag-code {
    font-size: 11px;
    color: $text-placeholder;
    border-left: 1px solid var(--color-info-light);
    padding-left: 6px;
    margin-left: 4px;
  }
}

/* ===== 连接表 ===== */
.fg-conn-table {
  :deep(.ant-table-thead > tr > th) {
    background: var(--bg-muted);
    font-size: 12px;
    color: $text-secondary;
    font-weight: 500;
  }
}

.fg-cond-summary {
  font-size: 12px;
  color: $text-regular;
}

.fg-conn-empty {
  padding: 18px 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  color: $text-placeholder;
  font-size: 13px;
}

/* ===== 关联流程弹窗 ===== */
.fg-link-modal {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.fg-link-search { width: 100%; }

.fg-link-list {
  max-height: 360px;
  overflow: auto;
  border: 1px solid $border-color-lighter;
  border-radius: $border-radius-md;
  padding: 4px 0;
  background: var(--bg-muted);
}

.fg-link-cbgroup {
  display: flex;
  flex-direction: column;
  width: 100%;
}

.fg-link-row {
  padding: 8px 14px;
  border-bottom: 1px solid var(--bg-page);

  &:last-child { border-bottom: none; }
  &:hover { background: var(--color-primary-light); }
  &.disabled { background: var(--bg-muted); }

  :deep(.ant-checkbox-wrapper) {
    width: 100%;
    display: flex;
    align-items: center;
    gap: 8px;
  }
}

.fg-link-name { font-size: 13px; color: $text-primary; }
.fg-link-code {
  font-family: 'JetBrains Mono', monospace;
  font-size: 11px;
  color: $text-placeholder;
  margin-left: 8px;
}
.fg-link-tip {
  margin-left: auto;
  font-size: 11px;
  color: $color-warning;
}

.fg-link-empty {
  padding: 24px;
  text-align: center;
  color: $text-placeholder;
  font-size: 13px;
}
</style>
