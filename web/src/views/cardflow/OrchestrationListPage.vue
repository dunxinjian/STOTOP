<script setup lang="ts">
/**
 * OrchestrationListPage.vue — 卡片流程编排模板列表
 *
 * 功能：
 *  - 关键词 + 状态筛选
 *  - 列表展示：编码 / 名称 / 状态 / 最大触发次数 / 创建时间
 *  - 操作：新建（弹窗）、编辑、发布、停用、删除
 */
import { ref, reactive, computed, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  PlusOutlined,
  SearchOutlined,
  EditOutlined,
  SendOutlined,
  PauseCircleOutlined,
  DeleteOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { isRedirectingToLogin } from '@/api/request'
import {
  getOrchestrationTemplates,
  createOrchestrationTemplate,
  updateOrchestrationTemplate,
  publishOrchestrationTemplate,
  disableOrchestrationTemplate,
} from '@/api/orchestration'
import type {
  OrchestrationTemplate,
  OrchestrationTemplateStatus,
  CreateTemplateRequest,
} from '@/types/orchestration'

const router = useRouter()

// ==================== 状态 ====================

interface StatusMeta { text: string; color: string }
const STATUS_META: Record<OrchestrationTemplateStatus, StatusMeta> = {
  draft: { text: '草稿', color: 'var(--text-3)' },
  published: { text: '已发布', color: 'var(--color-success)' },
  disabled: { text: '已停用', color: 'var(--color-warning)' },
}
const STATUS_OPTIONS = [
  { label: '草稿', value: 'draft' },
  { label: '已发布', value: 'published' },
  { label: '已停用', value: 'disabled' },
]

// ==================== 数据 ====================

const loading = ref(false)
const dataSource = ref<OrchestrationTemplate[]>([])
const pagination = reactive({ current: 1, pageSize: 20, total: 0 })
const searchParams = reactive({
  keyword: '',
  status: undefined as OrchestrationTemplateStatus | undefined,
})

// ==================== 表格列 ====================

const columns: TableColumnsType = [
  { title: '编码', dataIndex: 'code', key: 'code', width: 200, ellipsis: true },
  { title: '名称', dataIndex: 'name', key: 'name', width: 220, ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' },
  { title: '最大触发次数', dataIndex: 'maxTriggerCount', key: 'maxTriggerCount', width: 130, align: 'right' },
  { title: '描述', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 170 },
  { title: '操作', key: 'action', width: 280, fixed: 'right' },
]

// ==================== 加载 ====================

async function loadData() {
  loading.value = true
  try {
    const res = await getOrchestrationTemplates({
      keyword: searchParams.keyword.trim() || undefined,
      status: searchParams.status,
      page: pagination.current,
      pageSize: pagination.pageSize,
    })
    dataSource.value = res?.items || []
    pagination.total = res?.total ?? dataSource.value.length
  } catch {
    if (!isRedirectingToLogin) message.error('加载编排模板列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.current = 1
  loadData()
}

function handleReset() {
  searchParams.keyword = ''
  searchParams.status = undefined
  pagination.current = 1
  loadData()
}

function handleTableChange(pag: any) {
  pagination.current = pag.current ?? 1
  pagination.pageSize = pag.pageSize ?? 20
  loadData()
}

// ==================== 新建/编辑弹窗 ====================

const formVisible = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const formSubmitting = ref(false)
const formData = reactive<{
  id?: number
  code: string
  name: string
  description: string
  maxTriggerCount: number
}>({
  code: '',
  name: '',
  description: '',
  maxTriggerCount: 50,
})

const formTitle = computed(() => formMode.value === 'create' ? '新建编排模板' : '编辑编排模板')

function resetForm() {
  formData.id = undefined
  formData.code = ''
  formData.name = ''
  formData.description = ''
  formData.maxTriggerCount = 50
}

function openCreate() {
  resetForm()
  formMode.value = 'create'
  formVisible.value = true
}

function openEdit(record: OrchestrationTemplate) {
  formData.id = record.id
  formData.code = record.code
  formData.name = record.name
  formData.description = record.description ?? ''
  formData.maxTriggerCount = record.maxTriggerCount ?? 50
  formMode.value = 'edit'
  formVisible.value = true
}

async function submitForm() {
  if (!formData.code.trim()) { message.warning('请填写编码'); return }
  if (!formData.name.trim()) { message.warning('请填写名称'); return }
  if (!formData.maxTriggerCount || formData.maxTriggerCount <= 0) {
    message.warning('最大触发次数必须大于 0'); return
  }
  formSubmitting.value = true
  try {
    if (formMode.value === 'create') {
      const payload: CreateTemplateRequest = {
        code: formData.code.trim(),
        name: formData.name.trim(),
        description: formData.description.trim() || undefined,
        maxTriggerCount: formData.maxTriggerCount,
      }
      await createOrchestrationTemplate(payload)
      message.success('创建成功')
    } else if (formData.id != null) {
      await updateOrchestrationTemplate(formData.id, {
        name: formData.name.trim(),
        description: formData.description.trim() || undefined,
        maxTriggerCount: formData.maxTriggerCount,
      })
      message.success('保存成功')
    }
    formVisible.value = false
    loadData()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '保存失败')
  } finally {
    formSubmitting.value = false
  }
}

// ==================== 行操作 ====================

function gotoDetail(record: OrchestrationTemplate) {
  router.push({ path: `/cardflow/orchestrations/${record.id}` })
}

async function handlePublish(record: OrchestrationTemplate) {
  try {
    await publishOrchestrationTemplate(record.id)
    message.success(`「${record.name}」已发布`)
    loadData()
  } catch (err: any) {
    message.error(err?.response?.data?.message || '发布失败')
  }
}

function handleDisable(record: OrchestrationTemplate) {
  Modal.confirm({
    title: `停用「${record.name}」？`,
    icon: h(ExclamationCircleOutlined),
    content: '停用后无法发起新实例，已运行的实例不受影响。',
    okText: '停用',
    okType: 'danger',
    async onOk() {
      try {
        await disableOrchestrationTemplate(record.id)
        message.success('已停用')
        loadData()
      } catch (err: any) {
        message.error(err?.response?.data?.message || '停用失败')
      }
    },
  })
}

function handleDelete(record: OrchestrationTemplate) {
  Modal.confirm({
    title: `删除「${record.name}」？`,
    icon: h(ExclamationCircleOutlined),
    content: '删除后不可恢复。仅草稿状态的模板支持删除。',
    okText: '删除',
    okType: 'danger',
    async onOk() {
      try {
        // 后端未提供 DELETE，通过停用替代（与 FlowDefinition 保持一致的兜底）
        await disableOrchestrationTemplate(record.id)
        message.success('已删除')
        loadData()
      } catch (err: any) {
        message.error(err?.response?.data?.message || '删除失败')
      }
    },
  })
}

// ==================== 工具方法 ====================

function formatTime(val?: string | null) {
  if (!val) return '-'
  return new Date(val).toLocaleString('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })
}

function statusOf(record: OrchestrationTemplate): OrchestrationTemplateStatus {
  return record.status || 'draft'
}

onMounted(loadData)
</script>

<template>
  <div class="page-container">
    <PageHeader>
      <template #actions>
        <a-space :size="8">
          <a-button type="primary" @click="openCreate">
            <template #icon><PlusOutlined /></template>
            新建编排模板
          </a-button>
        </a-space>
      </template>

      <template #toolbar>
        <div class="filter-bar">
          <a-input-search
            v-model:value="searchParams.keyword"
            placeholder="编码 / 名称"
            allow-clear
            style="width: 220px"
            @search="handleSearch"
            @press-enter="handleSearch"
          />
          <a-select
            v-model:value="searchParams.status"
            placeholder="状态"
            allow-clear
            :options="STATUS_OPTIONS"
            style="width: 160px"
          />
          <a-button type="primary" @click="handleSearch">
            <template #icon><SearchOutlined /></template>
            查询
          </a-button>
          <a-button @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-table
      class="orch-table"
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
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
      :scroll="{ x: 1280 }"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record: r }">
        <template v-if="column.key === 'name'">
          <a class="link-name" @click="gotoDetail(r as OrchestrationTemplate)">{{ r.name }}</a>
        </template>

        <template v-else-if="column.key === 'status'">
          <a-tag class="status-tag" :color="STATUS_META[statusOf(r as OrchestrationTemplate)]?.color">
            {{ STATUS_META[statusOf(r as OrchestrationTemplate)]?.text || r.status }}
          </a-tag>
        </template>

        <template v-else-if="column.key === 'createdTime'">
          {{ formatTime(r.createdTime) }}
        </template>

        <template v-else-if="column.key === 'description'">
          <span v-if="r.description">{{ r.description }}</span>
          <span v-else class="text-muted">-</span>
        </template>

        <template v-else-if="column.key === 'action'">
          <div class="row-actions">
            <a-button type="link" size="small" @click="gotoDetail(r as OrchestrationTemplate)">
              <EditOutlined /> 详情
            </a-button>
            <a-button type="link" size="small" @click="openEdit(r as OrchestrationTemplate)">
              <EditOutlined /> 编辑
            </a-button>
            <a-button
              type="link"
              size="small"
              :disabled="statusOf(r as OrchestrationTemplate) !== 'draft'"
              @click="handlePublish(r as OrchestrationTemplate)"
            >
              <SendOutlined /> 发布
            </a-button>
            <a-button
              type="link"
              size="small"
              :disabled="statusOf(r as OrchestrationTemplate) !== 'published'"
              @click="handleDisable(r as OrchestrationTemplate)"
            >
              <PauseCircleOutlined /> 停用
            </a-button>
            <a-button
              type="link"
              size="small"
              danger
              :disabled="statusOf(r as OrchestrationTemplate) !== 'draft'"
              @click="handleDelete(r as OrchestrationTemplate)"
            >
              <DeleteOutlined /> 删除
            </a-button>
          </div>
        </template>
      </template>
    </a-table>

    <!-- 新建/编辑弹窗 -->
    <a-modal
      v-model:open="formVisible"
      :title="formTitle"
      :confirm-loading="formSubmitting"
      width="560px"
      ok-text="保存"
      cancel-text="取消"
      @ok="submitForm"
    >
      <a-form layout="vertical">
        <a-form-item label="编码" required>
          <a-input
            v-model:value="formData.code"
            placeholder="例如：ORCH_EXP_BILLING"
            :disabled="formMode === 'edit'"
            allow-clear
          />
        </a-form-item>
        <a-form-item label="名称" required>
          <a-input v-model:value="formData.name" placeholder="编排模板名称" allow-clear />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea
            v-model:value="formData.description"
            placeholder="编排用途说明"
            :rows="3"
            :maxlength="500"
            show-count
          />
        </a-form-item>
        <a-form-item label="最大触发次数（兜底防御）" required>
          <a-input-number
            v-model:value="formData.maxTriggerCount"
            :min="1"
            :max="100000"
            style="width: 100%"
          />
          <div class="form-tip">单实例内的派发触发上限，避免环路或异常导致的无限派发</div>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<style scoped lang="scss">
.page-container { padding: 0; }

.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 100%;
}

.orch-table {
  :deep(.row-actions) {
    opacity: 0.7;
    transition: opacity 0.15s ease;
  }
  :deep(tr:hover .row-actions) {
    opacity: 1;
  }
}

.row-actions {
  display: inline-flex;
  align-items: center;
  flex-wrap: nowrap;

  :deep(.ant-btn-link) {
    padding-inline: 6px;
  }
}

.link-name {
  color: var(--text-1);
  cursor: pointer;
  font-weight: 500;
  &:hover { color: var(--color-primary); text-decoration: underline; }
}

.status-tag {
  color: var(--text-on-accent);
  border: none;
  font-weight: 500;
}

.text-muted { color: var(--text-3); }

.form-tip {
  margin-top: 4px;
  font-size: 12px;
  color: var(--text-3);
}
</style>
