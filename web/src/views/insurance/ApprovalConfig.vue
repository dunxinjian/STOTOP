<template>
  <div class="page-container">
    <PageHeader title="审批流配置" description="配置理赔审批流程环节">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增环节
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="tableData"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="Id"
        bordered
        :scroll="{ x: 900 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'StepOrder'">
            <a-tag color="blue">{{ record.StepOrder }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'ApproverType'">
            {{ approverTypeMap[record.ApproverType] || '未知' }}
          </template>
          <template v-if="column.dataIndex === 'Status'">
            <a-tag :color="record.Status === 1 ? 'success' : 'default'">
              {{ record.Status === 1 ? '启用' : '禁用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as InsApprovalConfigListItemDto)">
              <EditOutlined />编辑
            </a-button>
            <a-button type="link" size="small" :danger="record.Status === 1" @click="handleToggleStatus(record as InsApprovalConfigListItemDto)">
              {{ record.Status === 1 ? '禁用' : '启用' }}
            </a-button>
            <a-button type="link" size="small" @click="handleMoveUp(record as InsApprovalConfigListItemDto)" :disabled="record.StepOrder <= 1">
              <ArrowUpOutlined />上移
            </a-button>
            <a-button type="link" size="small" @click="handleMoveDown(record as InsApprovalConfigListItemDto)" :disabled="record.StepOrder >= tableData.length">
              <ArrowDownOutlined />下移
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无审批环节配置" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增审批环节' : '编辑审批环节'"
      width="600px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" :label-col="{ style: { width: '110px' } }" style="padding: 10px 20px">
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="环节名称" name="StepName">
              <a-input v-model:value="formData.StepName" placeholder="请输入" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="环节编码" name="StepCode">
              <a-input v-model:value="formData.StepCode" placeholder="请输入" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="序号" name="StepOrder">
              <a-input-number v-model:value="formData.StepOrder" placeholder="请输入" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="审批人类型" name="ApproverType">
              <a-select v-model:value="formData.ApproverType" placeholder="请选择" :options="approverTypeOptions" @change="onApproverTypeChange" />
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 指定人员 -->
        <a-row v-if="formData.ApproverType === 1" :gutter="20">
          <a-col :span="12">
            <a-form-item label="审批人ID">
              <a-input-number v-model:value="formData.ApproverId" placeholder="请输入" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="审批人姓名">
              <a-input v-model:value="formData.ApproverName" placeholder="请输入" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 角色 -->
        <a-row v-if="formData.ApproverType === 2" :gutter="20">
          <a-col :span="12">
            <a-form-item label="角色编码">
              <a-input v-model:value="formData.ApproverRoleCode" placeholder="请输入角色编码" :maxlength="50" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="角色名称">
              <a-input v-model:value="formData.ApproverName" placeholder="请输入角色名称" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 部门负责人 -->
        <a-row v-if="formData.ApproverType === 3" :gutter="20">
          <a-col :span="12">
            <a-form-item label="审批人名称">
              <a-input v-model:value="formData.ApproverName" placeholder="部门负责人（自动匹配）" :maxlength="50" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="20">
          <a-col :span="12">
            <a-form-item label="可驳回">
              <a-switch v-model:checked="formData.CanReject" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item v-if="formData.CanReject" label="驳回目标步骤">
              <a-input-number v-model:value="formData.RejectTargetStep" placeholder="步骤序号" style="width: 100%" :min="1" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="20">
          <a-col :span="24">
            <a-form-item label="备注">
              <a-textarea v-model:value="formData.Remark" :rows="2" placeholder="请输入备注" :maxlength="500" show-count />
            </a-form-item>
          </a-col>
        </a-row>
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
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getApprovalConfigList,
  createApprovalConfig,
  updateApprovalConfig,
  updateApprovalConfigStatus,
  reorderApprovalConfig,
  type InsApprovalConfigListItemDto,
} from '@/api/insurance'

const approverTypeMap: Record<number, string> = { 1: '指定人员', 2: '角色', 3: '部门负责人' }
const approverTypeOptions = [
  { label: '指定人员', value: 1 },
  { label: '角色', value: 2 },
  { label: '部门负责人', value: 3 },
]

const tableColumns = [
  { title: '序号', dataIndex: 'StepOrder', width: 70, align: 'center' as const },
  { title: '环节名称', dataIndex: 'StepName', width: 150 },
  { title: '环节编码', dataIndex: 'StepCode', width: 120 },
  { title: '审批人类型', dataIndex: 'ApproverType', width: 110 },
  { title: '审批人', dataIndex: 'ApproverName', width: 120 },
  { title: '状态', dataIndex: 'Status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', width: 260, align: 'center' as const, fixed: 'right' as const },
]

const loading = ref(false)
const tableData = ref<InsApprovalConfigListItemDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 50, total: 0 })
const paginationConfig = computed(() => ({
  current: pagination.pageIndex, pageSize: pagination.pageSize, total: pagination.total,
  showSizeChanger: true, pageSizeOptions: ['20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  StepOrder: 1,
  StepName: '',
  StepCode: '',
  ApproverType: 1,
  ApproverId: undefined as number | undefined,
  ApproverName: '',
  ApproverRoleCode: '',
  CanReject: true,
  RejectTargetStep: undefined as number | undefined,
  Remark: '',
})

const formRules: Record<string, Rule[]> = {
  StepName: [{ required: true, message: '请输入环节名称', trigger: 'blur' }],
  StepCode: [{ required: true, message: '请输入环节编码', trigger: 'blur' }],
  StepOrder: [{ required: true, message: '请输入序号', trigger: 'blur' }],
  ApproverType: [{ required: true, message: '请选择审批人类型', trigger: 'change' }],
}

function onApproverTypeChange() {
  formData.ApproverId = undefined
  formData.ApproverName = ''
  formData.ApproverRoleCode = ''
}

async function fetchList() {
  loading.value = true
  try {
    const res = await getApprovalConfigList({ pageIndex: pagination.pageIndex, pageSize: pagination.pageSize })
    if (res) { tableData.value = res.items || []; pagination.total = res.total || 0 }
  } finally { loading.value = false }
}

function handleTableChange(pag: any) { pagination.pageIndex = pag.current; pagination.pageSize = pag.pageSize; fetchList() }

function resetForm() {
  formData.StepOrder = tableData.value.length + 1
  formData.StepName = ''; formData.StepCode = ''; formData.ApproverType = 1
  formData.ApproverId = undefined; formData.ApproverName = ''; formData.ApproverRoleCode = ''
  formData.CanReject = true; formData.RejectTargetStep = undefined; formData.Remark = ''
}

function handleAdd() {
  dialogType.value = 'add'; currentId.value = null; resetForm(); dialogVisible.value = true
}

function handleEdit(row: InsApprovalConfigListItemDto) {
  dialogType.value = 'edit'; currentId.value = row.Id
  formData.StepOrder = row.StepOrder; formData.StepName = row.StepName; formData.StepCode = row.StepCode
  formData.ApproverType = row.ApproverType; formData.ApproverName = row.ApproverName || ''
  formData.ApproverId = undefined; formData.ApproverRoleCode = ''
  formData.CanReject = true; formData.RejectTargetStep = undefined; formData.Remark = ''
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitLoading.value = true
  try {
    const data: any = { ...formData }
    if (dialogType.value === 'add') { await createApprovalConfig(data); message.success('新增成功') }
    else { await updateApprovalConfig(currentId.value!, data); message.success('更新成功') }
    dialogVisible.value = false; fetchList()
  } catch { message.error('提交失败') } finally { submitLoading.value = false }
}

async function handleToggleStatus(row: InsApprovalConfigListItemDto) {
  try {
    await updateApprovalConfigStatus(row.Id, row.Status === 1 ? 0 : 1)
    message.success('状态更新成功'); fetchList()
  } catch { message.error('状态更新失败') }
}

async function handleMoveUp(row: InsApprovalConfigListItemDto) {
  const idx = tableData.value.findIndex(i => i.Id === row.Id)
  if (idx <= 0) return
  const items = tableData.value.map((item, i) => ({
    Id: item.Id,
    StepOrder: i === idx - 1 ? item.StepOrder + 1 : i === idx ? item.StepOrder - 1 : item.StepOrder,
  }))
  try { await reorderApprovalConfig({ Items: items }); message.success('排序已更新'); fetchList() }
  catch { message.error('排序失败') }
}

async function handleMoveDown(row: InsApprovalConfigListItemDto) {
  const idx = tableData.value.findIndex(i => i.Id === row.Id)
  if (idx < 0 || idx >= tableData.value.length - 1) return
  const items = tableData.value.map((item, i) => ({
    Id: item.Id,
    StepOrder: i === idx ? item.StepOrder + 1 : i === idx + 1 ? item.StepOrder - 1 : item.StepOrder,
  }))
  try { await reorderApprovalConfig({ Items: items }); message.success('排序已更新'); fetchList() }
  catch { message.error('排序失败') }
}

onMounted(() => { fetchList() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
