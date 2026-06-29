<template>
  <div class="page-container">
    <PageHeader title="业务小组管理">
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-input v-model:value="searchForm.keyword" placeholder="请输入组织名称" allow-clear size="small" style="width: 200px" @keyup.enter="handleSearch" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="tableColumns"
        :data-source="filteredOrgList"
        :loading="loading"
        :pagination="false"
        row-key="id"
        bordered
        :scroll="{ x: 800 }"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.dataIndex === 'index'">
            {{ index + 1 }}
          </template>
          <template v-if="column.dataIndex === 'bdCount'">
            {{ getRoleCount(record.id, 1) }}
          </template>
          <template v-if="column.dataIndex === 'maintenanceCount'">
            {{ getRoleCount(record.id, 2) }}
          </template>
          <template v-if="column.dataIndex === 'totalMappings'">
            {{ getMappingsForOrg(record.id).length }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button v-if="has(CrmPermissions.GroupManage)" type="link" size="small" @click="handleManageRoles(record as { id: number; name: string })">
              <SettingOutlined />管理角色
            </a-button>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无组织数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 角色映射抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`角色管理 - ${currentOrg?.name || ''}`"
      :width="600"
      :destroy-on-close="true"
    >
      <!-- 添加映射表单 -->
      <div class="mapping-form">
        <a-row :gutter="12" align="middle">
          <a-col :span="10">
            <a-select
              v-model:value="newMapping.employeeId"
              placeholder="选择员工"
              allow-clear
              show-search
              :filter-option="filterEmployee"
              :options="employeeOptions"
              style="width: 100%"
            />
          </a-col>
          <a-col :span="8">
            <a-select
              v-model:value="newMapping.role"
              placeholder="选择角色"
              :options="roleOptions"
              style="width: 100%"
            />
          </a-col>
          <a-col :span="6">
            <a-button type="primary" :loading="addLoading" @click="handleAddMapping">
              <template #icon><PlusOutlined /></template>添加
            </a-button>
          </a-col>
        </a-row>
      </div>

      <!-- 已有映射列表 -->
      <a-table
        :columns="mappingColumns"
        :data-source="currentOrgMappings"
        :pagination="false"
        row-key="id"
        bordered
        size="small"
        style="margin-top: 16px"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'employeeName'">
            {{ getEmployeeName(record.employeeId) }}
          </template>
          <template v-if="column.dataIndex === 'role'">
            <a-tag :color="record.role === 1 ? 'blue' : 'green'">
              {{ record.role === 1 ? 'BD' : '运维' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-popconfirm
              title="确定删除该角色映射吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="handleDeleteMapping(record as CrmRoleMappingListItemDto)"
            >
              <a-button type="link" size="small" danger>
                <DeleteOutlined />删除
              </a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <EmptyState description="暂无角色映射" />
        </template>
      </a-table>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, DeleteOutlined, SettingOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { usePermission, CrmPermissions } from '@/utils/permission'
import {
  getRoleMappings,
  createRoleMapping,
  deleteRoleMapping,
  type CrmRoleMappingListItemDto,
  type CreateRoleMappingRequest,
} from '@/api/crm'
import { getOrganizationTree, type OrgTreeNode } from '@/api/system'
import { getEmployeeList, type EmployeeDto } from '@/api/hr'

const { has } = usePermission()

const roleOptions = [
  { label: 'BD', value: 1 },
  { label: '运维', value: 2 },
]

const tableColumns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 60, align: 'center' as const },
  { title: '组织名称', dataIndex: 'name', key: 'name', width: 200 },
  { title: '角色映射数量', dataIndex: 'totalMappings', key: 'totalMappings', width: 120, align: 'center' as const },
  { title: 'BD数量', dataIndex: 'bdCount', key: 'bdCount', width: 100, align: 'center' as const },
  { title: '运维数量', dataIndex: 'maintenanceCount', key: 'maintenanceCount', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 140, align: 'center' as const },
]

const mappingColumns = [
  { title: '员工姓名', dataIndex: 'employeeName', key: 'employeeName' },
  { title: '角色', dataIndex: 'role', key: 'role', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 100, align: 'center' as const },
]

// 搜索
const searchForm = reactive({ keyword: '' })

// 数据
const loading = ref(false)
const orgList = ref<{ id: number; name: string }[]>([])
const allMappings = ref<CrmRoleMappingListItemDto[]>([])
const employeeOptions = ref<{ label: string; value: number }[]>([])
const employeeMap = ref<Record<number, string>>({})

// 抽屉
const drawerVisible = ref(false)
const currentOrg = ref<{ id: number; name: string } | null>(null)
const addLoading = ref(false)
const newMapping = reactive<{ employeeId: number | undefined; role: number | undefined }>({
  employeeId: undefined,
  role: undefined,
})

const filteredOrgList = computed(() => {
  if (!searchForm.keyword) return orgList.value
  const kw = searchForm.keyword.toLowerCase()
  return orgList.value.filter((o) => o.name.toLowerCase().includes(kw))
})

const currentOrgMappings = computed(() => {
  if (!currentOrg.value) return []
  return getMappingsForOrg(currentOrg.value.id)
})

function getMappingsForOrg(orgId: number) {
  return allMappings.value.filter((m) => m.orgId === orgId)
}

function getRoleCount(orgId: number, role: number) {
  return allMappings.value.filter((m) => m.orgId === orgId && m.role === role).length
}

function getEmployeeName(id?: number) {
  return id ? (employeeMap.value[id] || '-') : '-'
}

function filterEmployee(input: string, option: any) {
  return option.label?.toLowerCase().includes(input.toLowerCase())
}

function flattenOrgs(nodes: OrgTreeNode[], result: { id: number; name: string }[] = []) {
  for (const n of nodes) {
    result.push({ id: n.id, name: n.name })
    if (n.children?.length) flattenOrgs(n.children, result)
  }
  return result
}

async function fetchOrgs() {
  loading.value = true
  try {
    const res = await getOrganizationTree() as any
    orgList.value = flattenOrgs(res || [])
  } catch { /* ignore */ }
  finally { loading.value = false }
}

async function fetchMappings() {
  try {
    const res = await getRoleMappings({ pageSize: 9999 }) as any
    allMappings.value = res?.items || res || []
  } catch { /* ignore */ }
}

async function fetchEmployees() {
  try {
    const res = await getEmployeeList({ pageSize: 9999 }) as any
    const items: EmployeeDto[] = res?.items || res || []
    employeeOptions.value = items.map((e) => ({ label: e.name, value: e.id }))
    employeeMap.value = Object.fromEntries(items.map((e) => [e.id, e.name]))
  } catch { /* ignore */ }
}

function handleSearch() {
  // 前端筛选，无需请求
}

function handleReset() {
  searchForm.keyword = ''
}

function handleManageRoles(org: { id: number; name: string }) {
  currentOrg.value = org
  newMapping.employeeId = undefined
  newMapping.role = undefined
  drawerVisible.value = true
}

async function handleAddMapping() {
  if (!newMapping.employeeId || !newMapping.role || !currentOrg.value) {
    message.warning('请选择员工和角色')
    return
  }
  addLoading.value = true
  try {
    const data: CreateRoleMappingRequest = {
      orgId: currentOrg.value.id,
      employeeId: newMapping.employeeId,
      role: newMapping.role,
    }
    await createRoleMapping(data)
    message.success('添加成功')
    newMapping.employeeId = undefined
    newMapping.role = undefined
    await fetchMappings()
  } catch (error) {
    console.error('添加角色映射失败:', error)
  } finally {
    addLoading.value = false
  }
}

async function handleDeleteMapping(record: CrmRoleMappingListItemDto) {
  try {
    await deleteRoleMapping(record.id)
    message.success('删除成功')
    await fetchMappings()
  } catch (error) {
    console.error('删除角色映射失败:', error)
  }
}

onMounted(() => {
  fetchOrgs()
  fetchMappings()
  fetchEmployees()
})
</script>

<style scoped lang="scss">
.mapping-form {
  padding: 12px;
  background: #fafafa;
  border-radius: 6px;
  border: 1px solid #f0f0f0;
}
</style>
