<template>
  <div class="page-container">
    <PageHeader title="来源分类管理" description="管理积分来源分类">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增来源
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <!-- 预置来源说明 -->
      <a-alert
        type="info"
        show-icon
        closable
        style="margin-bottom: 16px"
        message="系统预置6个来源分类"
        description="工作任务（日常工作完成情况）、质量结果（工作质量评估）、学习成长（培训与学习）、文化行为（企业文化践行）、创新贡献（创新与改善）、特别奖惩（额外奖励或处罚）"
      />

      <a-table
        :columns="columns"
        :dataSource="tableData"
        :loading="loading"
        rowKey="id"
        bordered
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'icon'">
            <span v-if="record.icon" style="font-size: 18px">{{ record.icon }}</span>
            <span v-else style="color: #bfbfbf">—</span>
          </template>
          <template v-if="column.dataIndex === 'color'">
            <div v-if="record.color" style="display: flex; align-items: center; gap: 6px">
              <span
                :style="{ display: 'inline-block', width: '16px', height: '16px', borderRadius: '3px', background: record.color }"
              />
              {{ record.color }}
            </div>
            <span v-else style="color: #bfbfbf">—</span>
          </template>
          <template v-if="column.dataIndex === 'isEnabled'">
            <a-switch
              :checked="record.isEnabled"
              checked-children="启用"
              un-checked-children="禁用"
              :loading="record._toggling"
              @change="handleToggle(record as PointSourceDto & { _toggling?: boolean })"
            />
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as PointSourceDto)">
              <EditOutlined />编辑
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无来源数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增来源' : '编辑来源'"
      :width="520"
      :destroyOnClose="true"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="来源名称" name="sourceName">
          <a-input v-model:value="formData.sourceName" placeholder="请输入来源名称" :maxlength="50" />
        </a-form-item>
        <a-form-item label="来源编码" name="sourceCode">
          <a-input
            v-model:value="formData.sourceCode"
            placeholder="请输入来源编码（如 WORK_TASK）"
            :maxlength="50"
            :disabled="dialogType === 'edit'"
          />
        </a-form-item>
        <a-form-item label="图标" name="icon">
          <a-input v-model:value="formData.icon" placeholder="请输入图标（emoji或图标名）" :maxlength="20" />
        </a-form-item>
        <a-form-item label="颜色" name="color">
          <a-input v-model:value="formData.color" placeholder="请输入颜色值（如 #1890ff）" :maxlength="20">
            <template #suffix>
              <span
                v-if="formData.color"
                :style="{ display: 'inline-block', width: '14px', height: '14px', borderRadius: '2px', background: formData.color, border: '1px solid #d9d9d9' }"
              />
            </template>
          </a-input>
        </a-form-item>
        <a-form-item label="说明" name="description">
          <a-textarea
            v-model:value="formData.description"
            :rows="3"
            placeholder="请输入来源说明"
            :maxlength="200"
            showCount
          />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formData.sortOrder" :min="0" :max="9999" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="dialogType === 'edit'" label="状态" name="isEnabled">
          <a-switch v-model:checked="formData.isEnabled" checked-children="启用" un-checked-children="禁用" />
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
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, EditOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getPointSources,
  createPointSource,
  updatePointSource,
  togglePointSource,
} from '@/api/points'
import type { PointSourceDto } from '@/types/points'

const columns = [
  { title: '名称', dataIndex: 'sourceName', key: 'sourceName', width: 140 },
  { title: '编码', dataIndex: 'sourceCode', key: 'sourceCode', width: 160 },
  { title: '图标', dataIndex: 'icon', key: 'icon', width: 70, align: 'center' as const },
  { title: '颜色', dataIndex: 'color', key: 'color', width: 130 },
  { title: '说明', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '排序', dataIndex: 'sortOrder', key: 'sortOrder', width: 70, align: 'center' as const },
  { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 80, align: 'center' as const, fixed: 'right' as const },
]

// 表格数据
const loading = ref(false)
const tableData = ref<(PointSourceDto & { _toggling?: boolean })[]>([])

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  sourceName: '',
  sourceCode: '',
  icon: '' as string | undefined,
  color: '' as string | undefined,
  description: '' as string | undefined,
  sortOrder: 0,
  isEnabled: true,
})

const formRules: Record<string, any[]> = {
  sourceName: [{ required: true, message: '请输入来源名称', trigger: 'blur' }],
  sourceCode: [{ required: true, message: '请输入来源编码', trigger: 'blur' }],
  sortOrder: [{ required: true, message: '请输入排序', trigger: 'blur' }],
}

async function fetchList() {
  loading.value = true
  try {
    const res = await getPointSources() as any
    tableData.value = res || []
  } finally {
    loading.value = false
  }
}

async function handleToggle(record: PointSourceDto & { _toggling?: boolean }) {
  record._toggling = true
  try {
    await togglePointSource(record.id)
    record.isEnabled = !record.isEnabled
    message.success(record.isEnabled ? '已启用' : '已禁用')
  } finally {
    record._toggling = false
  }
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

function handleEdit(row: PointSourceDto) {
  dialogType.value = 'edit'
  currentId.value = row.id
  formData.sourceName = row.sourceName
  formData.sourceCode = row.sourceCode
  formData.icon = row.icon || ''
  formData.color = row.color || ''
  formData.description = row.description || ''
  formData.sortOrder = row.sortOrder
  formData.isEnabled = row.isEnabled
  dialogVisible.value = true
}

function resetForm() {
  formData.sourceName = ''
  formData.sourceCode = ''
  formData.icon = ''
  formData.color = ''
  formData.description = ''
  formData.sortOrder = 0
  formData.isEnabled = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  submitLoading.value = true
  try {
    if (dialogType.value === 'add') {
      await createPointSource({
        sourceName: formData.sourceName,
        sourceCode: formData.sourceCode,
        icon: formData.icon || null,
        color: formData.color || null,
        description: formData.description || null,
        sortOrder: formData.sortOrder,
      })
      message.success('新增成功')
    } else {
      await updatePointSource(currentId.value!, {
        sourceName: formData.sourceName,
        icon: formData.icon || null,
        color: formData.color || null,
        description: formData.description || null,
        sortOrder: formData.sortOrder,
        isEnabled: formData.isEnabled,
      })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
</style>
