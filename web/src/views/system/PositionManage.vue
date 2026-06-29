<template>
  <div class="page-container">
    <PageHeader title="岗位管理" description="管理系统岗位及其关联组织和人员">
      <template #actions>
        <a-input-search
          v-model:value="searchForm.keyword"
          placeholder="搜索岗位名称/编码"
          style="width: 240px"
          @search="handleSearch"
          allowClear
        />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增岗位
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="tableData"
        :loading="loading"
        rowKey="id"
        bordered
        :pagination="{
          current: pagination.pageIndex,
          pageSize: pagination.pageSize,
          total: pagination.total,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
          pageSizeOptions: ['10', '20', '50', '100'],
          onChange: handlePageChange,
          onShowSizeChange: (_c: number, s: number) => handleSizeChange(s),
        }"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">{{ index + 1 }}</template>
          <template v-if="column.dataIndex === 'dingTalkBindStatus'">
            <a-tag v-if="record.dingTalkBindStatus === 1" color="success">
              已绑定{{ record.dingTalkPositionId ? ` (${record.dingTalkPositionId})` : '' }}
            </a-tag>
            <a-tag v-else color="default">未绑定</a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <div style="display: flex; align-items: center; justify-content: center; gap: 4px;">
              <a-button type="link" size="small" @click="handleManageDepts(record)" style="color: var(--color-success)">
                <BankOutlined />组织
              </a-button>
              <a-button type="link" size="small" @click="handleManageUsers(record)" style="color: var(--color-warning)">
                <UserOutlined />人员
              </a-button>
              <a-button type="link" size="small" @click="handleViewLogs(record)" style="color: #8c8c8c">
                <FileOutlined />变更
              </a-button>
              <a-divider type="vertical" style="height: 16px; margin: 0 4px;" />
              <a-tooltip title="编辑">
                <a-button type="link" size="small" @click="handleEdit(record)">
                  <template #icon><EditOutlined /></template>
                </a-button>
              </a-tooltip>
              <a-popconfirm
                title="确定删除该岗位吗？"
                okText="确定"
                cancelText="取消"
                @confirm="handleDelete(record)"
              >
                <a-tooltip title="删除">
                  <a-button type="link" size="small" danger>
                    <template #icon><DeleteOutlined /></template>
                  </a-button>
                </a-tooltip>
              </a-popconfirm>
            </div>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无岗位数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增岗位' : '编辑岗位'"
      :width="560"
      :destroyOnClose="true"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="岗位名称" name="name">
          <a-input v-model:value="formData.name" placeholder="请输入岗位名称" :maxlength="50" />
        </a-form-item>
        <a-form-item label="岗位编码" name="code">
          <a-input v-model:value="formData.code" placeholder="请输入岗位编码" :maxlength="50" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea
            v-model:value="formData.description"
            :rows="3"
            placeholder="请输入描述"
            :maxlength="200"
            showCount
          />
        </a-form-item>
        <a-form-item label="状态" name="status">
          <a-radio-group v-model:value="formData.status">
            <a-radio :value="1">启用</a-radio>
            <a-radio :value="0">停用</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item label="排序" name="sort">
          <a-input-number v-model:value="formData.sort" :min="0" :max="9999" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="dialogType === 'add'" label="初始组织">
          <a-tree-select
            v-model:value="formData.organizationIds"
            :tree-data="deptTreeData"
            :fieldNames="{ label: 'name', value: 'id', children: 'children' }"
            placeholder="可选择初始关联组织"
            allowClear
            multiple
            :treeDefaultExpandAll="false"
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 岗位-组织关系弹窗 -->
    <a-modal
      v-model:open="deptDialogVisible"
      title="管理关联组织"
      :width="700"
      :destroyOnClose="true"
    >
      <a-row :gutter="16">
        <a-col :span="12">
          <div class="relation-section">
            <div class="relation-title">已关联组织</div>
            <a-table :columns="deptRelColumns" :dataSource="currentDepts" size="small" bordered :pagination="false" rowKey="organizationId">
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'action'">
                  <a-button type="link" size="small" danger @click="removeDept(record)">移除</a-button>
                </template>
              </template>
            </a-table>
            <EmptyState v-if="currentDepts.length === 0" description="暂无关联组织" />
          </div>
        </a-col>
        <a-col :span="12">
          <div class="relation-section">
            <div class="relation-title">选择组织</div>
            <a-tree
              ref="deptTreeRef"
              :tree-data="deptTreeData"
              :fieldNames="{ title: 'name', key: 'id', children: 'children' }"
              checkable
              :checkStrictly="true"
              v-model:checkedKeys="checkedDeptKeys"
              style="max-height: 350px; overflow-y: auto"
            />
          </div>
        </a-col>
      </a-row>
      <template #footer>
        <a-button @click="deptDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="deptSubmitLoading" @click="handleDeptSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 岗位-人员关系弹窗 -->
    <a-modal
      v-model:open="userDialogVisible"
      title="管理关联人员"
      :width="700"
      :destroyOnClose="true"
    >
      <a-row :gutter="16">
        <a-col :span="12">
          <div class="relation-section">
            <div class="relation-title">已关联人员</div>
            <a-table :columns="userRelColumns" :dataSource="currentUsers" size="small" bordered :pagination="false" rowKey="userId">
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'isPrimary'">
                  <a-tag v-if="record.isPrimary === 1" color="success">是</a-tag>
                  <a-tag v-else color="default">否</a-tag>
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <a-button type="link" size="small" danger @click="removeUser(record)">移除</a-button>
                </template>
              </template>
            </a-table>
            <EmptyState v-if="currentUsers.length === 0" description="暂无关联人员" />
          </div>
        </a-col>
        <a-col :span="12">
          <div class="relation-section">
            <div class="relation-title">选择人员</div>
            <a-input
              v-model:value="userSearchKeyword"
              placeholder="搜索人员"
              allowClear
              size="small"
              style="margin-bottom: 8px"
              @change="handleUserSearch"
            />
            <a-table
              :columns="allUserColumns"
              :dataSource="allUsers"
              size="small"
              bordered
              :pagination="false"
              :scroll="{ y: 310 }"
              :row-selection="{ selectedRowKeys: selectedUserIds, onChange: onUserSelectChange }"
              rowKey="id"
            />
          </div>
        </a-col>
      </a-row>
      <template #footer>
        <a-button @click="userDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="userSubmitLoading" @click="handleUserSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 变更记录弹窗 -->
    <a-modal
      v-model:open="logDialogVisible"
      title="变更记录"
      :width="800"
      :destroyOnClose="true"
      :footer="null"
    >
      <a-table :columns="logColumns" :dataSource="logData" size="small" bordered :pagination="false" :scroll="{ y: 400 }" rowKey="id">
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'dingTalkSyncStatus'">
            <a-tag v-if="record.dingTalkSyncStatus === 1" color="success">已同步</a-tag>
            <a-tag v-else-if="record.dingTalkSyncStatus === 2" color="error">失败</a-tag>
            <a-tag v-else-if="record.dingTalkSyncStatus === 3">无需</a-tag>
            <a-tag v-else color="default">未同步</a-tag>
          </template>
          <template v-if="column.dataIndex === 'changeContent'">
            {{ parseChangeContent(record.changeContent) }}
          </template>
        </template>
      </a-table>
      <EmptyState v-if="logData.length === 0" description="暂无变更记录" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, DeleteOutlined, BankOutlined, UserOutlined, FileOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getPositionList,
  getPositionDetail,
  createPosition,
  updatePosition,
  deletePosition,
  assignPositionOrganizations,
  assignPositionUsers,
  getOrganizationTree,
  getUserList,
  getChangeLogsByBusiness,
  type PositionDto,
  type PositionDepartmentDto,
  type PositionUserDto,
  type ChangeLogDto,
} from '@/api/system'

const columns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '名称', dataIndex: 'name', key: 'name', width: 120 },
  { title: '编码', dataIndex: 'code', key: 'code', width: 200, ellipsis: true },
  { title: '组织数', dataIndex: 'departmentCount', key: 'departmentCount', width: 80, align: 'center' as const },
  { title: '人员数', dataIndex: 'userCount', key: 'userCount', width: 80, align: 'center' as const },
  { title: '钉钉绑定', dataIndex: 'dingTalkBindStatus', key: 'dingTalkBindStatus', width: 180, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '排序', dataIndex: 'sort', key: 'sort', width: 70, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 320, align: 'center' as const, fixed: 'right' as const }
]

const deptRelColumns = [
  { title: '组织名称', dataIndex: 'organizationName', key: 'organizationName' },
  { title: '操作', dataIndex: 'action', key: 'action', width: 80, align: 'center' as const },
]

const userRelColumns = [
  { title: '姓名', dataIndex: 'userName', key: 'userName' },
  { title: '主岗', dataIndex: 'isPrimary', key: 'isPrimary', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 80, align: 'center' as const },
]

const allUserColumns = [
  { title: '姓名', dataIndex: 'name', key: 'name' },
  { title: '账号', dataIndex: 'account', key: 'account' },
]

const logColumns = [
  { title: '操作时间', dataIndex: 'operationTime', key: 'operationTime', width: 170 },
  { title: '操作类型', dataIndex: 'operationType', key: 'operationType', width: 100, align: 'center' as const },
  { title: '操作人', dataIndex: 'operatorName', key: 'operatorName', width: 100 },
  { title: '钉钉同步', dataIndex: 'dingTalkSyncStatus', key: 'dingTalkSyncStatus', width: 100, align: 'center' as const },
  { title: '变更内容', dataIndex: 'changeContent', key: 'changeContent' },
]

// 搜索表单
const searchForm = reactive({ keyword: '' })

// 表格数据
const loading = ref(false)
const tableData = ref<PositionDto[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

// 组织树数据
const deptTreeData = ref<any[]>([])

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentPositionId = ref<number | null>(null)

const formData = reactive({
  name: '',
  code: '',
  description: '',
  status: 1,
  sort: 0,
  organizationIds: [] as number[],
})

const formRules: Record<string, any[]> = {
  name: [{ required: true, message: '请输入岗位名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入岗位编码', trigger: 'blur' }],
  status: [{ required: true, message: '请选择状态', trigger: 'change' }],
}

// 组织关系弹窗
const deptDialogVisible = ref(false)
const deptTreeRef = ref<any>()
void deptTreeRef
const currentDepts = ref<PositionDepartmentDto[]>([])
const checkedDeptKeys = ref<{ checked: number[]; halfChecked: number[] }>({ checked: [], halfChecked: [] })
const deptSubmitLoading = ref(false)

// 人员关系弹窗
const userDialogVisible = ref(false)
const currentUsers = ref<PositionUserDto[]>([])
const allUsers = ref<any[]>([])
const selectedUserIds = ref<number[]>([])
const userSearchKeyword = ref('')
const userSubmitLoading = ref(false)

// 变更记录弹窗
const logDialogVisible = ref(false)
const logData = ref<ChangeLogDto[]>([])

// 获取岗位列表
async function fetchPositionList() {
  loading.value = true
  try {
    const res = await getPositionList({
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
      keyword: searchForm.keyword,
    }) as any
    if (res) {
      tableData.value = res?.items || []
      pagination.total = res?.total || 0
    }
  } finally {
    loading.value = false
  }
}

// 获取组织树
async function fetchDeptTree() {
  try {
    const res = await getOrganizationTree() as any[]
    if (res) { deptTreeData.value = res || [] }
  } catch (error) {
    console.error('获取组织树失败:', error)
  }
}

function handleSearch() { pagination.pageIndex = 1; fetchPositionList() }
function handleReset() { searchForm.keyword = ''; pagination.pageIndex = 1; fetchPositionList() }
function handleSizeChange(val: number) { pagination.pageSize = val; fetchPositionList() }
function handlePageChange(val: number) { pagination.pageIndex = val; fetchPositionList() }

function handleAdd() {
  dialogType.value = 'add'; currentPositionId.value = null; resetForm(); dialogVisible.value = true
}

function handleEdit(row: any) {
  dialogType.value = 'edit'; currentPositionId.value = row.id
  formData.name = row.name; formData.code = row.code
  formData.description = row.description || ''; formData.status = row.status
  formData.sort = row.sort; formData.organizationIds = []
  dialogVisible.value = true
}

function resetForm() {
  formData.name = ''; formData.code = ''; formData.description = ''
  formData.status = 1; formData.sort = 0; formData.organizationIds = []
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  submitLoading.value = true
  try {
    if (dialogType.value === 'add') {
      await createPosition({
        name: formData.name, code: formData.code,
        description: formData.description || undefined, status: formData.status,
        sort: formData.sort,
        organizationIds: formData.organizationIds.length > 0 ? formData.organizationIds : undefined,
      })
      message.success('新增成功'); dialogVisible.value = false; fetchPositionList()
    } else {
      await updatePosition(currentPositionId.value!, {
        name: formData.name, code: formData.code,
        description: formData.description || undefined, status: formData.status, sort: formData.sort,
      })
      message.success('更新成功'); dialogVisible.value = false; fetchPositionList()
    }
  } finally { submitLoading.value = false }
}

async function handleDelete(row: any) {
  try { await deletePosition(row.id); message.success('删除成功'); fetchPositionList() }
  catch (error) { console.error('删除失败:', error) }
}

async function handleManageDepts(row: any) {
  currentPositionId.value = row.id
  try {
    const res = await getPositionDetail(row.id) as any
    if (res) {
      currentDepts.value = res.departments || []
      const ids = currentDepts.value.map((d: PositionDepartmentDto) => d.organizationId)
      checkedDeptKeys.value = { checked: ids, halfChecked: [] }
    }
  } catch {
    currentDepts.value = []
    checkedDeptKeys.value = { checked: [], halfChecked: [] }
  }
  deptDialogVisible.value = true
}

function removeDept(dept: any) {
  currentDepts.value = currentDepts.value.filter(d => d.organizationId !== dept.organizationId)
  checkedDeptKeys.value = { checked: currentDepts.value.map(d => d.organizationId), halfChecked: [] }
}

async function handleDeptSubmit() {
  if (!currentPositionId.value) return
  deptSubmitLoading.value = true
  try {
    const checkedKeys = checkedDeptKeys.value.checked || []
    await assignPositionOrganizations(currentPositionId.value, { organizationIds: checkedKeys })
    message.success('组织关联更新成功'); deptDialogVisible.value = false; fetchPositionList()
  } finally { deptSubmitLoading.value = false }
}

async function handleManageUsers(row: any) {
  currentPositionId.value = row.id; userSearchKeyword.value = ''
  try {
    const res = await getPositionDetail(row.id) as any
    if (res) { currentUsers.value = res.users || [] }
  } catch { currentUsers.value = [] }
  await fetchAllUsers()
  userDialogVisible.value = true
}

async function fetchAllUsers() {
  try {
    const res = await getUserList({ pageIndex: 1, pageSize: 200, keyword: userSearchKeyword.value }) as any
    if (res) { allUsers.value = res?.items || [] }
  } catch { allUsers.value = [] }
}

function handleUserSearch() { fetchAllUsers() }

function onUserSelectChange(keys: (string | number)[]) {
  selectedUserIds.value = keys as number[]
}

function removeUser(user: any) {
  currentUsers.value = currentUsers.value.filter(u => u.userId !== user.userId)
}

async function handleUserSubmit() {
  if (!currentPositionId.value) return
  userSubmitLoading.value = true
  try {
    const existingIds = currentUsers.value.map(u => u.userId)
    const newIds = selectedUserIds.value.filter(id => !existingIds.includes(id))
    const allIds = [...existingIds, ...newIds]
    await assignPositionUsers(currentPositionId.value, { userIds: allIds })
    message.success('人员关联更新成功'); userDialogVisible.value = false; fetchPositionList()
  } finally { userSubmitLoading.value = false }
}

async function handleViewLogs(row: any) {
  try {
    const res = await getChangeLogsByBusiness('Position', row.id) as any
    logData.value = Array.isArray(res) ? res : (res?.items || [])
  } catch { logData.value = [] }
  logDialogVisible.value = true
}

function parseChangeContent(content: string): string {
  if (!content) return '-'
  try {
    const changes = JSON.parse(content)
    if (Array.isArray(changes)) {
      return changes.map((c: any) => `${c.field || c.fieldName}: ${c.oldValue ?? ''} → ${c.newValue ?? ''}`).join('；')
    }
    if (typeof changes === 'object') {
      return Object.entries(changes).map(([k, v]) => `${k}: ${v}`).join('；')
    }
    return content
  } catch { return content }
}

onMounted(() => { fetchPositionList(); fetchDeptTree() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.relation-section {
  .relation-title {
    font-size: 14px;
    font-weight: 500;
    margin-bottom: 8px;
    color: $text-primary;
  }
}
</style>
