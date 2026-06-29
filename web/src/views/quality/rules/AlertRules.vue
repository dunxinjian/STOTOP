<template>
  <div class="page-container">
    <PageHeader title="预警规则">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增预警
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.thresholdType" size="small" placeholder="阈值类型" allow-clear style="width: 140px" :options="thresholdTypeOptions" />
          <a-select v-model:value="searchForm.status" size="small" placeholder="状态" allow-clear style="width: 100px" :options="statusOptions" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        :scroll="{ x: 1200 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'name'">
            <a-tooltip :title="record.name">
              <span>{{ record.name }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'thresholdType'">
            <a-tag :color="thresholdTypeColor(record.thresholdType)">
              {{ record.thresholdType }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'threshold'">
            {{ record.threshold }}
          </template>
          <template v-if="column.dataIndex === 'notifyMethod'">
            <a-tag :color="notifyMethodColor(record.notifyMethod)">
              {{ record.notifyMethod }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'notifyTargets'">
            <a-tooltip :title="record.notifyTargets">
              <span>{{ truncate(record.notifyTargets, 20) }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-switch
              :checked="record.status === 1"
              checked-children="启用"
              un-checked-children="禁用"
              @change="handleToggle(record)"
            />
          </template>
          <template v-if="column.dataIndex === 'createTime'">
            {{ record.createTime || '-' }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
            <a-button type="link" size="small" danger @click="handleDelete(record)">删除</a-button>
          </template>
        </template>
        <template #emptyText>
          <a-empty description="暂无预警规则" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增预警' : '编辑预警'"
      width="600px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '90px' } }"
        style="padding: 10px 20px; max-height: 65vh; overflow-y: auto"
      >
        <a-form-item label="配置名称" name="name">
          <a-input v-model:value="formData.name" placeholder="请输入配置名称" :maxlength="100" />
        </a-form-item>
        <a-form-item label="阈值类型" name="thresholdType">
          <a-select v-model:value="formData.thresholdType" placeholder="请选择阈值类型" :options="thresholdTypeOptions" />
        </a-form-item>
        <a-form-item label="阈值" name="threshold">
          <a-input-number
            v-model:value="formData.threshold"
            placeholder="请输入阈值"
            :precision="2"
            :min="0"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="通知方式" name="notifyMethod">
          <a-select v-model:value="formData.notifyMethod" placeholder="请选择通知方式" :options="notifyMethodOptions" />
        </a-form-item>
        <a-form-item label="通知对象">
          <a-textarea
            v-model:value="formData.notifyTargets"
            placeholder="请输入通知对象ID，多个用逗号分隔"
            :rows="3"
            :maxlength="500"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getAlertConfigs,
  getAlertConfigDetail,
  createAlertConfig,
  updateAlertConfig,
  deleteAlertConfig,
  toggleAlertConfig,
} from '@/api/quality'

// ====== 枚举选项 ======
const thresholdTypeList = ['异常率', '超时率', '导入失败率', '处理时长超标']
const notifyMethodList = ['消息', '短信', '钉钉', '邮件']

const thresholdTypeOptions = thresholdTypeList.map(v => ({ label: v, value: v }))
const notifyMethodOptions = notifyMethodList.map(v => ({ label: v, value: v }))
const statusOptions = [
  { label: '启用', value: 1 },
  { label: '禁用', value: 0 },
]

function thresholdTypeColor(v: string) {
  const map: Record<string, string> = { '异常率': 'red', '超时率': 'orange', '导入失败率': 'blue', '处理时长超标': 'purple' }
  return map[v] || 'default'
}

function notifyMethodColor(v: string) {
  const map: Record<string, string> = { '消息': 'blue', '短信': 'green', '钉钉': 'cyan', '邮件': 'orange' }
  return map[v] || 'default'
}

function truncate(text: string | null | undefined, len: number) {
  if (!text) return '-'
  return text.length > len ? text.slice(0, len) + '...' : text
}

// ====== 表格列 ======
const tableColumns = [
  { title: '配置名称', dataIndex: 'name', key: 'name', width: 180, ellipsis: true },
  { title: '阈值类型', dataIndex: 'thresholdType', key: 'thresholdType', width: 120, align: 'center' as const },
  { title: '阈值', dataIndex: 'threshold', key: 'threshold', width: 100, align: 'center' as const },
  { title: '通知方式', dataIndex: 'notifyMethod', key: 'notifyMethod', width: 100, align: 'center' as const },
  { title: '通知对象', dataIndex: 'notifyTargets', key: 'notifyTargets', width: 160, ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 170 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const, fixed: 'right' as const },
]

// ====== 搜索 ======
const searchForm = reactive({
  thresholdType: undefined as string | undefined,
  status: undefined as number | undefined,
})

// ====== 表格数据 ======
const loading = ref(false)
const tableData = ref<any[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.pageIndex,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

function handleTableChange(pag: any) {
  pagination.pageIndex = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.thresholdType) params.thresholdType = searchForm.thresholdType
    if (searchForm.status !== undefined) params.status = searchForm.status
    const res = await getAlertConfigs(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

function handleSearch() { pagination.pageIndex = 1; fetchList() }

function handleReset() {
  searchForm.thresholdType = undefined
  searchForm.status = undefined
  pagination.pageIndex = 1
  fetchList()
}

// ====== 启用/禁用 ======
async function handleToggle(record: any) {
  try {
    await toggleAlertConfig(record.id)
    message.success(record.status === 1 ? '已禁用' : '已启用')
    fetchList()
  } catch (e) {
    console.error('切换状态失败:', e)
  }
}

// ====== 弹窗 ======
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  name: '',
  thresholdType: undefined as string | undefined,
  threshold: undefined as number | undefined,
  notifyMethod: undefined as string | undefined,
  notifyTargets: '',
})

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入配置名称', trigger: 'blur' }],
  thresholdType: [{ required: true, message: '请选择阈值类型', trigger: 'change' }],
  threshold: [{ required: true, message: '请输入阈值', trigger: 'blur' }],
  notifyMethod: [{ required: true, message: '请选择通知方式', trigger: 'change' }],
}

function resetForm() {
  formData.name = ''
  formData.thresholdType = undefined
  formData.threshold = undefined
  formData.notifyMethod = undefined
  formData.notifyTargets = ''
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentId.value = row.id
  resetForm()
  dialogVisible.value = true
  try {
    const detail = await getAlertConfigDetail(row.id) as any
    if (detail) {
      formData.name = detail.name || ''
      formData.thresholdType = detail.thresholdType
      formData.threshold = detail.threshold
      formData.notifyMethod = detail.notifyMethod
      formData.notifyTargets = detail.notifyTargets || ''
    }
  } catch (e) {
    console.error('获取预警配置详情失败:', e)
  }
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const payload: any = {
      name: formData.name,
      thresholdType: formData.thresholdType,
      threshold: formData.threshold,
      notifyMethod: formData.notifyMethod,
      notifyTargets: formData.notifyTargets || undefined,
    }
    if (dialogType.value === 'add') {
      await createAlertConfig(payload)
      message.success('新增成功')
    } else {
      await updateAlertConfig(currentId.value!, payload)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

// ====== 删除 ======
function handleDelete(record: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除预警配置「${record.name}」吗？`,
    okText: '确定',
    cancelText: '取消',
    okType: 'danger',
    async onOk() {
      try {
        await deleteAlertConfig(record.id)
        message.success('删除成功')
        fetchList()
      } catch (e) {
        console.error('删除失败:', e)
      }
    },
  })
}

onMounted(() => {
  fetchList()
})
</script>
