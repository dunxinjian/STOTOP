<template>
  <div class="page-container">
    <PageHeader title="角色管理" description="管理系统角色和权限">
      <template #actions>
        <a-input-search
          v-model:value="searchForm.keyword"
          placeholder="搜索角色名称/编码"
          style="width: 240px"
          @search="handleSearch"
          allowClear
        />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增角色
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
          <template v-if="column.dataIndex === 'scope'">
            <a-tag v-if="record.scope === 'global'" color="orange">全局</a-tag>
            <a-tag v-else color="default">组织级</a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-button type="link" size="small" @click="handleAssignPermission(record)" style="color: var(--color-success)">
              <KeyOutlined />分配权限
            </a-button>
            <a-button type="link" size="small" @click="handleViewRoleUsers(record)" style="color: #8c8c8c">
              <UserOutlined />用户
            </a-button>
            <a-popconfirm
              title="确定删除该角色吗？"
              okText="确定"
              cancelText="取消"
              @confirm="handleDelete(record)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无角色数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增角色' : '编辑角色'"
      :width="500"
      :destroyOnClose="true"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="角色名称" name="name">
          <a-input v-model:value="formData.name" placeholder="请输入角色名称" :maxlength="50" />
        </a-form-item>
        <a-form-item v-if="dialogType === 'edit'" label="角色编码" name="code">
          <a-input v-model:value="formData.code" disabled :maxlength="50" />
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
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 分配权限弹窗 -->
    <a-modal
      v-model:open="permissionDialogVisible"
      title="分配权限"
      :width="600"
      :destroyOnClose="true"
    >
      <div v-if="hasOrgTree" style="margin-bottom: 12px">
        <a-form-item label="生效组织" :label-col="{ style: { width: '80px' } }">
          <a-tree-select
            v-model:value="assignOrgId"
            :tree-data="orgTreeData"
            :fieldNames="{ label: 'name', value: 'id', children: 'children' }"
            placeholder="全局生效（不选则全局）"
            allowClear
            :treeDefaultExpandAll="false"
            style="width: 100%"
          />
        </a-form-item>
      </div>
      <div class="permission-tree-wrapper">
        <a-tree
          ref="permissionTreeRef"
          :tree-data="permissionTreeData"
          :fieldNames="{ title: 'name', key: 'id', children: 'children' }"
          checkable
          :checkStrictly="false"
          v-model:checkedKeys="permissionCheckedKeys"
          :defaultExpandAll="false"
        />
      </div>
      <template #footer>
        <a-button @click="permissionDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="assignLoading" @click="handleAssignSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 角色用户列表弹窗 -->
    <a-modal
      v-model:open="roleUsersDialogVisible"
      title="角色用户列表"
      :width="700"
      :destroyOnClose="true"
      :footer="null"
    >
      <div v-if="hasOrgTree" style="margin-bottom: 12px">
        <a-form-item label="查看组织" :label-col="{ style: { width: '80px' } }">
          <a-select v-model:value="roleUsersOrgFilter" placeholder="全部组织" allowClear style="width: 200px"
            @change="() => { roleUsersPagination.pageIndex = 1; fetchRoleUsers() }"
            :options="[{ label: '全部组织', value: '' }, ...flatOrgList.map(o => ({ label: o.name, value: o.id }))]"
          />
        </a-form-item>
      </div>
      <div class="role-users-table-wrapper">
        <a-table
          :columns="roleUserColumns"
          :dataSource="roleUsers"
          :loading="roleUsersLoading"
          size="small"
          bordered
          :pagination="false"
          rowKey="id"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.dataIndex === 'index'">
              {{ (roleUsersPagination.pageIndex - 1) * roleUsersPagination.pageSize + index + 1 }}
            </template>
            <template v-if="column.dataIndex === 'orgName'">{{ record.orgName || '全局' }}</template>
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="record.status === 1 ? 'success' : 'error'">
                {{ record.status === 1 ? '启用' : '停用' }}
              </a-tag>
            </template>
          </template>
          <template #emptyText>
            <EmptyState description="暂无用户" />
          </template>
        </a-table>
      </div>
      <div v-if="roleUsersPagination.total > 0" style="display: flex; justify-content: flex-end; margin-top: 12px">
        <a-pagination
          v-model:current="roleUsersPagination.pageIndex"
          v-model:pageSize="roleUsersPagination.pageSize"
          :total="roleUsersPagination.total"
          :showSizeChanger="true"
          :pageSizeOptions="['10', '20', '50']"
          :showTotal="(t: number) => `共 ${t} 条`"
          size="small"
          @change="fetchRoleUsers"
          @showSizeChange="(_c: number, s: number) => { roleUsersPagination.pageSize = s; roleUsersPagination.pageIndex = 1; fetchRoleUsers() }"
        />
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, nextTick, computed } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, DeleteOutlined, KeyOutlined, UserOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getRoleList,
  createRole,
  updateRole,
  deleteRole,
  getRoleDetail,
  assignRolePermissions,
  getPermissionTree,
  getOrganizationTree,
  getUserList,
  type RoleItem,
} from '@/api/system'

const columns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '角色名称', dataIndex: 'name', key: 'name', width: 150 },
  { title: '角色编码', dataIndex: 'code', key: 'code', width: 150 },
  { title: '描述', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '作用域', dataIndex: 'scope', key: 'scope', width: 90, align: 'center' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 320, align: 'center' as const, fixed: 'right' as const },
]

const roleUserColumns = [
  { title: '#', dataIndex: 'index', key: 'index', width: 50, align: 'center' as const },
  { title: '姓名', dataIndex: 'name', key: 'name' },
  { title: '账号', dataIndex: 'account', key: 'account' },
  { title: '组织', dataIndex: 'orgName', key: 'orgName' },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
]

// 搜索表单
const searchForm = reactive({ keyword: '' })

// 表格数据
const loading = ref(false)
const tableData = ref<RoleItem[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 })

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentRoleId = ref<number | null>(null)

const formData = reactive({ name: '', code: '', description: '', status: 1 })

const formRules: Record<string, any[]> = {
  name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],

  status: [{ required: true, message: '请选择状态', trigger: 'change' }],
}

// 权限分配相关
const permissionDialogVisible = ref(false)
const permissionTreeRef = ref<any>()
void permissionTreeRef
const permissionTreeData = ref<any[]>([])
const permissionCheckedKeys = ref<{ checked: number[]; halfChecked: number[] }>({ checked: [], halfChecked: [] })
const selectedPermissionIds = ref<number[]>([])
const assignLoading = ref(false)
const assignOrgId = ref<number | null>(null)

// 组织树
const orgTreeData = ref<any[]>([])
const hasOrgTree = computed(() => orgTreeData.value.length > 0)

// 角色用户列表
const roleUsersDialogVisible = ref(false)
const roleUsersLoading = ref(false)
const roleUsers = ref<any[]>([])
const roleUsersOrgFilter = ref<number | string>('')
const currentViewRoleId = ref<number | null>(null)
const roleUsersPagination = reactive({ pageIndex: 1, pageSize: 10, total: 0 })

const flatOrgList = computed(() => {
  const result: any[] = []
  function flatten(nodes: any[]) {
    for (const n of nodes) {
      result.push({ id: n.id, name: n.name })
      if (n.children) flatten(n.children)
    }
  }
  flatten(orgTreeData.value)
  return result
})

async function fetchRoleList() {
  loading.value = true
  try {
    const res = await getRoleList({
      pageIndex: pagination.pageIndex, pageSize: pagination.pageSize, keyword: searchForm.keyword,
    }) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.total = res?.total || res?.length || 0
    }
  } finally { loading.value = false }
}

async function fetchPermissionTree() {
  try {
    const res = await getPermissionTree() as any[]
    if (res) { permissionTreeData.value = res || [] }
  } catch (error) { console.error('获取权限树失败:', error) }
}

async function fetchOrgTree() {
  try {
    const res = await getOrganizationTree() as any[]
    if (res) orgTreeData.value = res || []
  } catch { /* ignore */ }
}

function handleSearch() { pagination.pageIndex = 1; fetchRoleList() }
function handleReset() { searchForm.keyword = ''; pagination.pageIndex = 1; fetchRoleList() }
function handleSizeChange(val: number) { pagination.pageSize = val; fetchRoleList() }
function handlePageChange(val: number) { pagination.pageIndex = val; fetchRoleList() }

function handleAdd() {
  dialogType.value = 'add'; currentRoleId.value = null; resetForm(); dialogVisible.value = true
}

function handleEdit(row: any) {
  dialogType.value = 'edit'; currentRoleId.value = row.id
  formData.name = row.name; formData.code = row.code
  formData.description = row.description || ''; formData.status = row.status
  dialogVisible.value = true
}

function resetForm() {
  formData.name = ''; formData.code = ''; formData.description = ''; formData.status = 1
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  submitLoading.value = true
  try {
    if (dialogType.value === 'add') {
      const data = { name: formData.name, description: formData.description, status: formData.status }
      await createRole(data); message.success('新增成功')
    } else {
      const data = { name: formData.name, description: formData.description, status: formData.status }
      await updateRole(currentRoleId.value!, data); message.success('更新成功')
    }
    dialogVisible.value = false; fetchRoleList()
  } finally { submitLoading.value = false }
}

async function handleDelete(row: any) {
  try { await deleteRole(row.id); message.success('删除成功'); fetchRoleList() }
  catch (error) { console.error('删除失败:', error) }
}

async function handleAssignPermission(row: any) {
  currentRoleId.value = row.id
  await fetchPermissionTree()

  try {
    const res = await getRoleDetail(row.id) as any
    if (res) { selectedPermissionIds.value = res?.permissionIds || [] }
  } catch { selectedPermissionIds.value = [] }

  permissionDialogVisible.value = true

  nextTick(() => {
    permissionCheckedKeys.value = { checked: selectedPermissionIds.value, halfChecked: [] }
  })
}

async function handleAssignSubmit() {
  if (!currentRoleId.value) return

  assignLoading.value = true
  try {
    const checkedKeys = permissionCheckedKeys.value.checked || []
    const halfCheckedKeys = permissionCheckedKeys.value.halfChecked || []
    const allCheckedKeys = [...checkedKeys, ...halfCheckedKeys]

    const res = await assignRolePermissions(currentRoleId.value, {
      permissionIds: allCheckedKeys,
      orgId: assignOrgId.value ?? undefined,
    } as any) as any
    if (res) {
      message.success('权限分配成功')
      permissionDialogVisible.value = false
    }
  } finally { assignLoading.value = false }
}

async function handleViewRoleUsers(row: any) {
  currentViewRoleId.value = row.id
  roleUsersOrgFilter.value = ''
  roleUsersPagination.pageIndex = 1
  roleUsersPagination.total = 0
  roleUsersDialogVisible.value = true
  await fetchRoleUsers()
}

async function fetchRoleUsers() {
  if (!currentViewRoleId.value) return
  roleUsersLoading.value = true
  try {
    const params: any = {
      roleId: currentViewRoleId.value,
      pageIndex: roleUsersPagination.pageIndex,
      pageSize: roleUsersPagination.pageSize,
    }
    if (roleUsersOrgFilter.value) params.orgId = roleUsersOrgFilter.value
    const res = await getUserList(params) as any
    roleUsers.value = res?.items || res || []
    roleUsersPagination.total = res?.total ?? (res?.items?.length ?? 0)
  } catch { roleUsers.value = [] }
  finally { roleUsersLoading.value = false }
}

onMounted(() => { fetchRoleList(); fetchOrgTree() })
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.role-users-table-wrapper {
  max-height: 400px;
  overflow-y: auto;
}

.permission-tree-wrapper {
  max-height: 400px;
  overflow-y: auto;
  padding: 10px;
  border: 1px solid $border-color-lighter;
  border-radius: $border-radius-sm;
}
</style>
