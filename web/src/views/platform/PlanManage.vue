<template>
  <div class="page-container">
    <PageHeader title="套餐管理" description="平台层套餐与资源上限">
      <template #left>
        <a-segmented v-model:value="navKey" :options="navOptions" @change="onNav" />
        <a-button size="middle" @click="fetchList">
          <template #icon><ReloadOutlined /></template>刷新
        </a-button>
      </template>
      <template #right>
        <a-button type="primary" size="middle" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新建套餐
        </a-button>
      </template>
    </PageHeader>

    <div class="page-card">
      <DataTable
        v-model:pagination="pagination"
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :scroll="{ x: 900 }"
        row-key="id"
        empty-text="暂无套餐数据"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'maxUsers'">
            {{ record.maxUsers === 0 ? '不限' : record.maxUsers }}
          </template>
          <template v-if="column.dataIndex === 'maxOutlets'">
            {{ record.maxOutlets === 0 ? '不限' : record.maxOutlets }}
          </template>
          <template v-if="column.dataIndex === 'moduleFlags'">
            <span class="mono-ellipsis">{{ record.moduleFlags || '-' }}</span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as PlanDto)">
              <EditOutlined />编辑
            </a-button>
          </template>
        </template>
      </DataTable>
    </div>

    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新建套餐' : '编辑套餐'"
      :width="560"
      :destroyOnClose="true"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical" class="modal-form">
        <div class="form-grid">
          <a-form-item label="名称" name="name">
            <a-input v-model:value="formData.name" placeholder="请输入套餐名称" :maxlength="100" />
          </a-form-item>
          <a-form-item label="编号" name="code">
            <a-input
              v-model:value="formData.code"
              placeholder="唯一编号"
              :maxlength="50"
              :disabled="dialogType === 'edit'"
            />
          </a-form-item>
        </div>
        <div class="form-grid">
          <a-form-item label="最大用户数" name="maxUsers">
            <a-input-number v-model:value="formData.maxUsers" :min="0" style="width: 100%" />
            <div class="form-hint">0 表示不限</div>
          </a-form-item>
          <a-form-item label="最大网点数" name="maxOutlets">
            <a-input-number v-model:value="formData.maxOutlets" :min="0" style="width: 100%" />
            <div class="form-hint">0 表示不限</div>
          </a-form-item>
        </div>
        <a-form-item label="模块开关（JSON，可选）" name="moduleFlags">
          <a-textarea
            v-model:value="formData.moduleFlags"
            placeholder='如 {"cardflow":true,"express":true}'
            :rows="3"
          />
        </a-form-item>
        <div v-if="dialogType === 'edit'" class="form-note">编号在编辑时不可修改（服务端忽略该字段）。</div>
      </a-form>
      <template #footer>
        <div class="modal-footer">
          <a-button @click="dialogVisible = false">取消</a-button>
          <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
        </div>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, ReloadOutlined, EditOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import DataTable from '@/components/DataTable.vue'
import {
  getPlans,
  createPlan,
  updatePlan,
  type PlanDto,
} from '@/api/platform'

const router = useRouter()

const navKey = ref('plans')
const navOptions = [
  { label: '租户', value: 'tenants' },
  { label: '套餐', value: 'plans' },
  { label: '订阅', value: 'subscriptions' },
]
function onNav(v: string | number) {
  router.push(`/platform/${v}`)
}

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '编号', dataIndex: 'code', key: 'code', width: 140 },
  { title: '最大用户数', dataIndex: 'maxUsers', key: 'maxUsers', width: 110, align: 'center' as const },
  { title: '最大网点数', dataIndex: 'maxOutlets', key: 'maxOutlets', width: 110, align: 'center' as const },
  { title: '模块开关', dataIndex: 'moduleFlags', key: 'moduleFlags', width: 220 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 100, align: 'center' as const, fixed: 'right' as const },
]

const loading = ref(false)
const tableData = ref<PlanDto[]>([])
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })

async function fetchList() {
  loading.value = true
  try {
    const res = await getPlans()
    tableData.value = res || []
    pagination.value.total = tableData.value.length
  } finally {
    loading.value = false
  }
}

// ==================== 新建 / 编辑 ====================
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)
const formData = reactive({
  name: '',
  code: '',
  maxUsers: 0,
  maxOutlets: 0,
  moduleFlags: '',
})
const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入套餐名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入唯一编号', trigger: 'blur' }],
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  formData.name = ''
  formData.code = ''
  formData.maxUsers = 0
  formData.maxOutlets = 0
  formData.moduleFlags = ''
  dialogVisible.value = true
}

function handleEdit(record: PlanDto) {
  dialogType.value = 'edit'
  currentId.value = record.id
  formData.name = record.name
  formData.code = record.code
  formData.maxUsers = record.maxUsers
  formData.maxOutlets = record.maxOutlets
  formData.moduleFlags = record.moduleFlags || ''
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const payload = {
      name: formData.name,
      code: formData.code,
      maxUsers: formData.maxUsers,
      maxOutlets: formData.maxOutlets,
      moduleFlags: formData.moduleFlags?.trim() ? formData.moduleFlags.trim() : null,
    }
    if (dialogType.value === 'add') {
      await createPlan(payload)
      message.success('新建成功')
    } else {
      await updatePlan(currentId.value!, payload)
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

onMounted(fetchList)
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 16px;
}

.form-hint {
  margin-top: 4px;
  font-size: $font-size-sm;
  color: $text-secondary;
}

.form-note {
  margin-top: 4px;
  padding: 8px 12px;
  border-radius: 8px;
  background: var(--color-info-light);
  color: var(--color-info-text);
  font-size: $font-size-sm;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.mono-ellipsis {
  display: inline-block;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: bottom;
  font-family: var(--font-mono, monospace);
  font-size: $font-size-sm;
  color: $text-secondary;
}
</style>
