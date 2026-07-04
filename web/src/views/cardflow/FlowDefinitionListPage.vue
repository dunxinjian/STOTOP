<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch, h } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import { isRedirectingToLogin } from '@/api/request'
import type { TableColumnsType, TableProps } from 'ant-design-vue'
import {
  PlusOutlined,
  ReloadOutlined,
  HistoryOutlined,
  EditOutlined,
  SendOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  MoreOutlined,
  CopyOutlined,
  InboxOutlined,
  DeleteOutlined,
  AppstoreOutlined,
  ExclamationCircleOutlined,
  FileAddOutlined,
  DownOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusTag from '@/components/StatusTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import { FLOW_STATUS_META, FLOW_STATUS_OPTIONS, type FlowStatus } from './flowStatusMeta'
import {
  getFlowDefinitions,
  publishFlowDefinition,
  archiveFlowDefinition,
  disableFlowDefinition,
  enableFlowDefinition,
  deleteFlowDefinition,
  getFlowGroups,
  cloneFlowDefinition,
  getFlowTemplates,
  cloneTemplateToOrg,
  saveAsTemplate,
} from '@/api/cardflow'
import type {
  FlowDefinitionDto,
  FlowDefinitionQueryRequest,
  FlowGroupDto,
} from '@/types/cardflow'
import { useOrgContextStore } from '@/stores/orgContext'

const router = useRouter()
const orgContextStore = useOrgContextStore()

// ==================== 数据/分页/筛选 ====================

const loading = ref(false)
const dataSource = ref<FlowDefinitionDto[]>([])
const flowGroups = ref<FlowGroupDto[]>([])
const flowGroupMap = computed(() => {
  const map = new Map<number, FlowGroupDto>()
  flowGroups.value.forEach(g => map.set(g.id, g))
  return map
})

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
})

const searchParams = reactive({
  keyword: '',
  status: [] as FlowStatus[],
  flowGroupId: undefined as number | undefined,
})

const sorter = reactive<{ field?: string; order?: 'ascend' | 'descend' }>({})

// ==================== 选中行（批量） ====================

const selectedRowKeys = ref<number[]>([])
const hasSelection = computed(() => selectedRowKeys.value.length > 0)
const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: (string | number)[]) => {
    selectedRowKeys.value = keys.map(k => Number(k))
  },
  columnWidth: 44,
}))

// ==================== 表格列 ====================

const columns: TableColumnsType = [
  {
    title: '流程名称',
    dataIndex: 'flowName',
    key: 'flowName',
    sorter: true,
    ellipsis: true,
  },
  { title: '编码', dataIndex: 'flowCode', key: 'flowCode', width: 120, ellipsis: true },
  { title: '所属流程组', dataIndex: 'flowGroupId', key: 'flowGroupId', width: 130, ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' },
  { title: '当前版本', dataIndex: 'currentVersion', key: 'currentVersion', width: 120, align: 'center' },
  {
    title: '最近发布',
    dataIndex: 'lastPublishedTime',
    key: 'lastPublishedTime',
    width: 160,
    sorter: true,
  },
  { title: '操作', key: 'action', width: 140, align: 'center' },
]

// ==================== 加载数据 ====================

async function loadFlowGroups() {
  const orgId = orgContextStore.currentOrgId
  if (!orgId) return
  try {
    const res = await getFlowGroups(orgId)
    flowGroups.value = (res as FlowGroupDto[]) || []
  } catch {
    /* 静默：流程组列表加载失败不阻塞主表 */
  }
}

async function loadData() {
  loading.value = true
  try {
    // 筛选/排序全部下推后端，避免"只过滤当前页"造成分页与总数错乱
    const req: FlowDefinitionQueryRequest = {
      keyword: searchParams.keyword.trim() || undefined,
      page: pagination.current,
      pageSize: pagination.pageSize,
      orgId: orgContextStore.currentOrgId ?? undefined,
      statuses: searchParams.status.length ? searchParams.status.join(',') : undefined,
      flowGroupId: searchParams.flowGroupId ?? undefined,
      sortField: sorter.field || undefined,
      sortOrder: sorter.order ? (sorter.order === 'ascend' ? 'asc' : 'desc') : undefined,
    }

    const res = await getFlowDefinitions(req)
    dataSource.value = res?.items || []
    pagination.total = res?.total ?? 0
  } catch {
    // 认证失效时拦截器已提示并跳转登录页，此处不再重复报错
    if (!isRedirectingToLogin) {
      message.error('加载流程定义列表失败')
    }
  } finally {
    loading.value = false
  }
}

// ==================== 筛选/分页/排序事件 ====================

function handleSearch() {
  pagination.current = 1
  loadData()
}

function handleReset() {
  searchParams.keyword = ''
  searchParams.status = []
  searchParams.flowGroupId = undefined
  sorter.field = undefined
  sorter.order = undefined
  pagination.current = 1
  loadData()
}

const handleTableChange: TableProps['onChange'] = (pag, _filters, srt) => {
  pagination.current = (pag as any).current ?? 1
  pagination.pageSize = (pag as any).pageSize ?? 20
  const single = Array.isArray(srt) ? srt[0] : srt
  if (single && single.field && single.order) {
    sorter.field = String(single.field)
    sorter.order = single.order as 'ascend' | 'descend'
  } else {
    sorter.field = undefined
    sorter.order = undefined
  }
  loadData()
}

// ==================== 单行操作 ====================

function gotoCreate() {
  router.push({ path: '/cardflow/definition/edit' })
}

function handleEdit(record: FlowDefinitionDto) {
  router.push({ path: '/cardflow/definition/edit', query: { id: String(record.id) } })
}

function handleVersions(record: FlowDefinitionDto) {
  router.push({ path: `/cardflow/definitions/${record.id}/versions` })
}

function handlePublish(record: FlowDefinitionDto) {
  Modal.confirm({
    title: `发布「${record.flowName}」？`,
    icon: h(ExclamationCircleOutlined),
    content: '发布后草稿将成为新版本并立即生效，新发起的卡片使用该版本。',
    okText: '发布',
    async onOk() {
      try {
        await publishFlowDefinition(record.id)
        message.success(`「${record.flowName}」发布成功`)
        loadData()
      } catch (e) {
        // 拦截器已弹出后端具体校验错误（缺草稿/环/不可达等），此处不重复弹泛化提示
        console.error('[FlowDefinition] 发布失败:', e)
      }
    },
  })
}

async function handleDisable(record: FlowDefinitionDto) {
  try {
    await disableFlowDefinition(record.id)
    message.success(`「${record.flowName}」已停用`)
    await loadData()
  } catch (e) {
    // 拦截器已弹出后端具体原因，避免二次泛化报错
    console.error('[FlowDefinition] 停用失败:', e)
  }
}

async function handleEnable(record: FlowDefinitionDto) {
  try {
    await enableFlowDefinition(record.id)
    message.success(`「${record.flowName}」已启用`)
    await loadData()
  } catch (e) {
    console.error('[FlowDefinition] 启用失败:', e)
  }
}

function handleArchive(record: FlowDefinitionDto) {
  Modal.confirm({
    title: '确认归档？',
    icon: h(ExclamationCircleOutlined),
    content: '归档后无法发起新卡片，在途卡片不受影响。',
    okText: '归档',
    okType: 'danger',
    async onOk() {
      try {
        await archiveFlowDefinition(record.id)
        message.success('已归档')
        loadData()
      } catch {
        message.error('归档失败')
      }
    },
  })
}

function handleDeleteDraft(record: FlowDefinitionDto) {
  Modal.confirm({
    title: `确认删除「${record.flowName}」？`,
    icon: h(ExclamationCircleOutlined),
    content: '将彻底删除该草稿流程及其版本、节点配置，此操作不可恢复。',
    okText: '删除',
    okType: 'danger',
    async onOk() {
      try {
        await deleteFlowDefinition(record.id)
        message.success('已删除')
        loadData()
      } catch (e) {
        // 拦截器已弹出后端拒绝原因（存在卡片/已发布版本等）
        console.error('[FlowDefinition] 删除失败:', e)
      }
    },
  })
}

async function handleCopy(record: FlowDefinitionDto) {
  try {
    await cloneFlowDefinition(record.id, {
      flowName: `${record.flowName} - 副本`,
      flowCode: `${record.flowCode}_copy_${Date.now().toString(36)}`,
      description: record.description ?? undefined,
      orgId: orgContextStore.currentOrgId ?? undefined,
    })
    message.success('复制成功（含版本和节点）')
    loadData()
  } catch {
    message.error('复制失败')
  }
}

// ==================== 模板克隆 ====================

const showTemplateModal = ref(false)
const templateList = ref<FlowDefinitionDto[]>([])
const templateLoading = ref(false)

// 打开模板弹窗时加载数据
watch(showTemplateModal, async (val) => {
  if (val) {
    templateLoading.value = true
    try {
      const res = await getFlowTemplates()
      templateList.value = (res as FlowDefinitionDto[]) || []
    } catch {
      message.error('加载模板列表失败')
    } finally {
      templateLoading.value = false
    }
  }
})

async function handleCloneFromTemplate(template: FlowDefinitionDto) {
  try {
    await cloneTemplateToOrg(template.id, {
      flowName: `${template.flowName}`,
      flowCode: `${template.flowCode}_${Date.now().toString(36)}`,
      description: template.description ?? undefined,
      orgId: orgContextStore.currentOrgId ?? undefined,
    })
    message.success('从模板创建成功')
    showTemplateModal.value = false
    loadData()
  } catch {
    message.error('从模板创建失败')
  }
}

async function handleSaveAsTemplate(record: FlowDefinitionDto) {
  try {
    await saveAsTemplate(record.id)
    message.success('已保存为模板')
  } catch {
    message.error('保存为模板失败')
  }
}

// ==================== 批量操作 ====================

async function handleBatchPublish() {
  if (!hasSelection.value) return
  const selectedSet = new Set(selectedRowKeys.value)
  // 已归档的流程后端不允许发布，选中里剔除并提示
  const publishable = dataSource.value.filter(it => selectedSet.has(it.id) && statusOf(it) !== 'archived')
  const skipped = selectedSet.size - publishable.length
  if (!publishable.length) {
    message.warning('选中的流程均已归档，不能发布')
    return
  }
  const ids = publishable.map(it => it.id)
  Modal.confirm({
    title: `批量发布 ${ids.length} 条流程？`,
    icon: h(ExclamationCircleOutlined),
    content: skipped > 0 ? `已跳过 ${skipped} 条已归档流程。发布后各流程草稿将成为新版本并立即生效。` : '发布后各流程草稿将成为新版本并立即生效。',
    okText: '发布',
    async onOk() {
      const results = await Promise.allSettled(ids.map(id => publishFlowDefinition(id)))
      const ok = results.filter(r => r.status === 'fulfilled').length
      const fail = results.length - ok
      if (fail === 0) message.success(`成功发布 ${ok} 条`)
      else message.warning(`发布完成：成功 ${ok} 条，失败 ${fail} 条`)
      selectedRowKeys.value = []
      loadData()
    },
  })
}

function handleBatchArchive() {
  if (!hasSelection.value) return
  const ids = [...selectedRowKeys.value]
  Modal.confirm({
    title: `确认归档 ${ids.length} 条流程？`,
    icon: h(ExclamationCircleOutlined),
    content: '归档后无法发起新卡片，在途卡片不受影响。',
    okText: '归档',
    okType: 'danger',
    async onOk() {
      const results = await Promise.allSettled(ids.map(id => archiveFlowDefinition(id)))
      const ok = results.filter(r => r.status === 'fulfilled').length
      const fail = results.length - ok
      if (fail === 0) message.success(`成功归档 ${ok} 条`)
      else message.warning(`归档完成：成功 ${ok} 条，失败 ${fail} 条`)
      selectedRowKeys.value = []
      loadData()
    },
  })
}

// ==================== 工具方法 ====================

function formatTime(val?: string | null) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function statusOf(record: FlowDefinitionDto): FlowStatus {
  return (record.status as FlowStatus) || 'draft'
}

// ==================== 生命周期 ====================

onMounted(async () => {
  await loadFlowGroups()
  await loadData()
})
</script>

<template>
  <div class="page-container">
    <PageHeader>
      <template #actions>
        <div class="toolbar-row">
          <a-space :size="8">
            <a-button type="primary" @click="gotoCreate">
              <template #icon><PlusOutlined /></template>
              新建流程
            </a-button>

            <a-button @click="showTemplateModal = true">从模板创建</a-button>

            <a-dropdown :disabled="!hasSelection" :trigger="['click']">
              <a-button>
                <template #icon><AppstoreOutlined /></template>
                批量操作
                <DownOutlined class="caret" />
              </a-button>
              <template #overlay>
                <a-menu>
                  <a-menu-item key="batch-publish" @click="handleBatchPublish">
                    <SendOutlined /> 批量发布
                  </a-menu-item>
                  <a-menu-item key="batch-archive" @click="handleBatchArchive">
                    <InboxOutlined /> 批量归档
                  </a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
          </a-space>

          <span class="toolbar-divider" />

          <a-space :size="8">
            <a-input-search
              v-model:value="searchParams.keyword"
              placeholder="名称 / 编码"
              allow-clear
              style="width: 200px"
              @search="handleSearch"
              @press-enter="handleSearch"
            />
            <a-select
              v-model:value="searchParams.status"
              mode="multiple"
              placeholder="状态"
              allow-clear
              :max-tag-count="2"
              :options="FLOW_STATUS_OPTIONS"
              style="width: 140px"
              @change="handleSearch"
            />
            <a-select
              v-model:value="searchParams.flowGroupId"
              placeholder="流程组"
              allow-clear
              show-search
              :filter-option="(input: string, option: any) => String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())"
              :options="flowGroups.map(g => ({ label: g.groupName, value: g.id }))"
              style="width: 160px"
              @change="handleSearch"
            />
            <a-button @click="handleReset">
              <template #icon><ReloadOutlined /></template>
            </a-button>
          </a-space>
        </div>
      </template>
    </PageHeader>

    <a-table
      class="flow-table"
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      :row-selection="rowSelection"
      row-key="id"
      size="middle"
      :pagination="{
        current: pagination.current,
        pageSize: pagination.pageSize,
        total: pagination.total,
        showSizeChanger: true,
        showQuickJumper: true,
        pageSizeOptions: ['10', '20', '50', '100'],
        showTotal: (t: number) => `共 ${t} 条`,
      }"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record: rawRecord }">
        <!-- 流程名称 -->
        <template v-if="column.key === 'flowName'">
          <a class="flow-name" @click="handleEdit(rawRecord as FlowDefinitionDto)">{{ rawRecord.flowName }}</a>
          <div v-if="rawRecord.description" class="flow-desc">{{ rawRecord.description }}</div>
        </template>

        <!-- 所属流程组 -->
        <template v-else-if="column.key === 'flowGroupId'">
          <span v-if="rawRecord.flowGroupId && flowGroupMap.get(rawRecord.flowGroupId)">
            {{ flowGroupMap.get(rawRecord.flowGroupId)?.groupName }}
          </span>
          <span v-else class="text-muted">未分组</span>
        </template>

        <!-- 状态 -->
        <template v-else-if="column.key === 'status'">
          <StatusTag :type="FLOW_STATUS_META[statusOf(rawRecord as FlowDefinitionDto) as FlowStatus]?.tagType ?? 'default'">
            {{ FLOW_STATUS_META[statusOf(rawRecord as FlowDefinitionDto) as FlowStatus]?.text ?? rawRecord.status }}
          </StatusTag>
        </template>

        <!-- 当前版本 -->
        <template v-else-if="column.key === 'currentVersion'">
          <div style="display: flex; align-items: center; gap: 4px; justify-content: center;">
            <span v-if="(rawRecord as any).currentVersion" class="version-badge">v{{ (rawRecord as any).currentVersion }}</span>
            <span v-else class="text-muted">-</span>
            <StatusTag v-if="(rawRecord as any).hasDraft" type="info" style="margin: 0; font-size: 11px; line-height: 1.4; padding: 0 4px;">
              有草稿
            </StatusTag>
          </div>
        </template>

        <!-- 最近发布 -->
        <template v-else-if="column.key === 'lastPublishedTime'">
          {{ formatTime((rawRecord as any).lastPublishedTime) }}
        </template>

        <!-- 操作 -->
        <template v-else-if="column.key === 'action'">
          <div class="row-actions">
            <a-tooltip :title="(rawRecord as any).hasDraft ? `编辑草稿 (v${(rawRecord as any).draftVersion})` : '编辑（将创建新草稿）'">
              <a-button type="link" size="small" @click="handleEdit(rawRecord as FlowDefinitionDto)">
                <EditOutlined />
              </a-button>
            </a-tooltip>

            <a-tooltip title="发布">
              <a-button
                type="link"
                size="small"
                :disabled="statusOf(rawRecord as FlowDefinitionDto) === 'archived'"
                @click="handlePublish(rawRecord as FlowDefinitionDto)"
              >
                <SendOutlined />
              </a-button>
            </a-tooltip>

            <a-tooltip title="版本">
              <a-button type="link" size="small" @click="handleVersions(rawRecord as FlowDefinitionDto)">
                <HistoryOutlined />
              </a-button>
            </a-tooltip>

            <a-tooltip v-if="statusOf(rawRecord as FlowDefinitionDto) === 'published'" title="停用">
              <a-button
                type="link"
                size="small"
                @click="handleDisable(rawRecord as FlowDefinitionDto)"
              >
                <PauseCircleOutlined />
              </a-button>
            </a-tooltip>
            <a-tooltip v-else-if="statusOf(rawRecord as FlowDefinitionDto) === 'disabled'" title="启用">
              <a-button
                type="link"
                size="small"
                @click="handleEnable(rawRecord as FlowDefinitionDto)"
              >
                <PlayCircleOutlined />
              </a-button>
            </a-tooltip>

            <a-dropdown :trigger="['click']">
              <a-tooltip title="更多">
                <a-button type="link" size="small" class="icon-btn">
                  <MoreOutlined />
                </a-button>
              </a-tooltip>
              <template #overlay>
                <a-menu>
                  <a-menu-item key="copy" @click="handleCopy(rawRecord as FlowDefinitionDto)">
                    <CopyOutlined /> 复制
                  </a-menu-item>
                  <a-menu-item key="save-as-template" @click="handleSaveAsTemplate(rawRecord as FlowDefinitionDto)">
                    <FileAddOutlined /> 保存为模板
                  </a-menu-item>
                  <a-menu-item
                    key="archive"
                    :disabled="statusOf(rawRecord as FlowDefinitionDto) === 'archived'"
                    @click="handleArchive(rawRecord as FlowDefinitionDto)"
                  >
                    <InboxOutlined /> 归档
                  </a-menu-item>
                  <a-menu-divider />
                  <a-menu-item
                    key="delete"
                    danger
                    :disabled="statusOf(rawRecord as FlowDefinitionDto) !== 'draft'"
                    @click="handleDeleteDraft(rawRecord as FlowDefinitionDto)"
                  >
                    <DeleteOutlined /> 删除
                  </a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
          </div>
        </template>
      </template>

      <!-- 空状态引导 -->
      <template #emptyText>
        <EmptyState
          title="创建您的第一个卡片流程"
          description="流程定义用于配置审批节点、表单字段与流转规则。"
          :icon="FileAddOutlined"
          size="small"
        >
          <div style="margin-top: 16px;">
            <a-button type="primary" @click="gotoCreate">
              <template #icon><PlusOutlined /></template>
              新建流程
            </a-button>
          </div>
        </EmptyState>
      </template>
    </a-table>

    <!-- 从模板创建弹窗 -->
    <a-modal
      v-model:open="showTemplateModal"
      title="从模板创建流程"
      :footer="null"
      width="600px"
    >
      <a-spin :spinning="templateLoading">
        <div
          v-if="templateList.length === 0 && !templateLoading"
          style="text-align: center; padding: 24px; color: var(--text-3);"
        >
          暂无可用模板
        </div>
        <a-list v-else :data-source="templateList" :split="true">
          <template #renderItem="{ item }">
            <a-list-item>
              <a-list-item-meta
                :title="(item as FlowDefinitionDto).flowName"
                :description="(item as FlowDefinitionDto).description || (item as FlowDefinitionDto).flowCode"
              />
              <template #actions>
                <a-button type="link" size="small" @click="handleCloneFromTemplate(item as FlowDefinitionDto)">
                  使用此模板
                </a-button>
              </template>
            </a-list-item>
          </template>
        </a-list>
      </a-spin>
    </a-modal>
  </div>
</template>

<style scoped lang="scss">
.page-container {
  padding: 0;
}

.toolbar-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.toolbar-divider {
  width: 1px;
  height: 20px;
  background: var(--border);
  margin: 0 8px;
  flex-shrink: 0;
}

.caret {
  margin-left: 2px;
  font-size: 12px;
  color: var(--text-3);
}

// 表格整体
.flow-table {
  :deep(.ant-table) {
    font-size: 13px;
  }
  :deep(.ant-table-thead > tr > th) {
    font-weight: 600;
    font-size: 12px;
    color: var(--text-3);
    text-transform: uppercase;
    letter-spacing: 0.02em;
    background: var(--bg-muted);
  }
  :deep(.ant-table-tbody > tr > td) {
    padding-top: 12px;
    padding-bottom: 12px;
    transition: background-color 0.15s ease;
  }
  :deep(.ant-table-tbody > tr:hover > td) {
    background: var(--bg-muted);
  }
  // 操作列渐显
  :deep(.ant-table-tbody > tr .row-actions) {
    opacity: 0.7;
    transition: opacity 0.2s ease;
  }
  :deep(.ant-table-tbody > tr:hover .row-actions) {
    opacity: 1;
  }
  :deep(.ant-table-row-selected .row-actions) {
    opacity: 1;
  }
}

// 操作按钮组
.row-actions {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  flex-wrap: nowrap;

  :deep(.ant-btn-link) {
    padding-inline: 6px;
    height: 28px;
    border-radius: 4px;
    color: var(--text-2);
    transition: background-color 0.15s ease;

    &:hover {
      background: var(--bg-muted);
      color: var(--color-primary);
    }

    &:disabled {
      color: var(--text-3);
    }
  }

  .icon-btn {
    width: 28px;
    padding: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
  }
}

// 流程名称
.flow-name {
  color: var(--text-1);
  cursor: pointer;
  font-weight: 600;
  font-size: 13px;
  line-height: 1.4;

  &:hover {
    color: var(--color-primary);
    text-decoration: none;
  }
}

.flow-desc {
  font-size: 12px;
  color: var(--text-3);
  margin-top: 2px;
  line-height: 1.3;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 280px;
}

// 版本标签
.version-badge {
  display: inline-block;
  padding: 1px 8px;
  font-size: 12px;
  font-weight: 500;
  color: var(--text-2);
  background: var(--bg-muted);
  border-radius: 10px;
  line-height: 1.6;
}

.text-muted {
  color: var(--text-3);
}

</style>
