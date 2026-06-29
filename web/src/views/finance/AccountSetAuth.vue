<template>
  <div class="page-container">
    <PageHeader title="账套授权">
      <template #left>
        <a-select
          v-model:value="selectedAccountSetId"
          placeholder="选择账套"
          style="width: 240px"
          @change="handleAccountSetChange"
        >
          <a-select-option v-for="item in accountSetStore.accountSets" :key="item.id" :value="item.id">
            {{ item.fName }}
          </a-select-option>
        </a-select>
      </template>
      <template #right>
        <a-button type="primary" @click="showGrantModal" :disabled="!selectedAccountSetId">
          <PlusOutlined />添加授权
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="authUsers"
        :loading="loading"
        rowKey="id"
        :pagination="false"
        bordered
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'user'">
            <span>{{ record.userName }}</span>
            <span style="color: #999; margin-left: 6px">({{ record.userAccount }})</span>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-space>
              <a @click="showEditRoleModal(record)">修改角色</a>
              <a-popconfirm title="确定撤销该用户的账套授权？" @confirm="handleRevoke(record.id)">
                <a style="color: var(--color-danger)">撤销</a>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 添加授权弹窗 -->
    <a-modal v-model:open="grantModalVisible" title="添加授权" @ok="handleGrant" :confirmLoading="grantLoading">
      <a-form layout="vertical">
        <a-form-item label="选择用户">
          <a-select
            v-model:value="grantForm.userId"
            placeholder="搜索用户"
            show-search
            :filter-option="false"
            @search="handleUserSearch"
            style="width: 100%"
          >
            <a-select-option v-for="u in userList" :key="u.id" :value="u.id">
              {{ u.fName || u.name }} ({{ u.fAccount || u.account }})
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="账套角色">
          <a-select v-model:value="grantForm.accountSetRoleId" placeholder="选择角色" style="width: 100%">
            <a-select-option v-for="r in roles" :key="r.id" :value="r.id">
              {{ r.name }}
              <template v-if="r.description"> - {{ r.description }}</template>
            </a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 修改角色弹窗 -->
    <a-modal v-model:open="editRoleModalVisible" title="修改角色" @ok="handleUpdateRole" :confirmLoading="editRoleLoading">
      <a-form layout="vertical">
        <a-form-item label="账套角色">
          <a-select v-model:value="editRoleForm.accountSetRoleId" placeholder="选择角色" style="width: 100%">
            <a-select-option v-for="r in roles" :key="r.id" :value="r.id">
              {{ r.name }}
              <template v-if="r.description"> - {{ r.description }}</template>
            </a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useAccountSetStore } from '@/stores/accountSet'
import { get, post, put, del } from '@/api/request'
import { getUserList } from '@/api/system'

const accountSetStore = useAccountSetStore()

// ==================== 状态 ====================
const selectedAccountSetId = ref<number | undefined>(undefined)
const authUsers = ref<any[]>([])
const loading = ref(false)
const roles = ref<any[]>([])
const userList = ref<any[]>([])

// 添加授权
const grantModalVisible = ref(false)
const grantLoading = ref(false)
const grantForm = ref<{ userId: number | undefined; accountSetRoleId: number | undefined }>({
  userId: undefined,
  accountSetRoleId: undefined,
})

// 修改角色
const editRoleModalVisible = ref(false)
const editRoleLoading = ref(false)
const editRoleForm = ref<{ id: number; accountSetRoleId: number | undefined }>({
  id: 0,
  accountSetRoleId: undefined,
})

// ==================== 表格列定义 ====================
const columns = [
  { title: '用户', dataIndex: 'user', key: 'user', width: 150 },
  { title: '账套角色', dataIndex: 'roleName', key: 'roleName', width: 120 },
  { title: '授权人', dataIndex: 'grantedByName', key: 'grantedByName', width: 120 },
  { title: '授权时间', dataIndex: 'createdTime', key: 'createdTime', width: 160, customRender: ({ text }: any) => text ? text.replace('T', ' ').substring(0, 19) : '' },
  { title: '操作', dataIndex: 'action', key: 'action', width: 150 },
]

// ==================== 方法 ====================
async function loadAuthUsers() {
  if (!selectedAccountSetId.value) return
  loading.value = true
  try {
    const res = await get(`/finance/account-set-auth/${selectedAccountSetId.value}/users`)
    authUsers.value = (res as any) || []
  } catch (e: any) {
    message.error(e?.message || '获取授权列表失败')
  } finally {
    loading.value = false
  }
}

async function loadRoles() {
  try {
    const res = await get('/finance/account-set-auth/roles')
    roles.value = (res as any) || []
  } catch (e) {
    console.error('获取角色列表失败', e)
  }
}

async function loadUsers(keyword?: string) {
  try {
    const params: Record<string, any> = { pageSize: 200 }
    if (keyword) {
      params.keyword = keyword
    }
    const res = await getUserList(params) as any
    userList.value = Array.isArray(res) ? res : (res?.items || res?.data || [])
  } catch (e) {
    console.error('获取用户列表失败', e)
  }
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function handleUserSearch(val: string) {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    loadUsers(val || undefined)
  }, 300)
}

function handleAccountSetChange() {
  loadAuthUsers()
}

function showGrantModal() {
  grantForm.value = { userId: undefined, accountSetRoleId: undefined }
  grantModalVisible.value = true
  if (userList.value.length === 0) loadUsers()
  if (roles.value.length === 0) loadRoles()
}

async function handleGrant() {
  if (!grantForm.value.userId || !grantForm.value.accountSetRoleId) {
    message.warning('请选择用户和角色')
    return
  }
  grantLoading.value = true
  try {
    await post('/finance/account-set-auth/grant', {
      userId: grantForm.value.userId,
      accountSetId: selectedAccountSetId.value,
      accountSetRoleId: grantForm.value.accountSetRoleId,
    })
    message.success('授权成功')
    grantModalVisible.value = false
    loadAuthUsers()
  } catch (e: any) {
    message.error(e?.message || '授权失败')
  } finally {
    grantLoading.value = false
  }
}

function showEditRoleModal(record: any) {
  editRoleForm.value = { id: record.id, accountSetRoleId: record.accountSetRoleId || undefined }
  editRoleModalVisible.value = true
  if (roles.value.length === 0) loadRoles()
}

async function handleUpdateRole() {
  if (!editRoleForm.value.accountSetRoleId) {
    message.warning('请选择角色')
    return
  }
  editRoleLoading.value = true
  try {
    await put(`/finance/account-set-auth/${editRoleForm.value.id}`, {
      accountSetRoleId: editRoleForm.value.accountSetRoleId,
    })
    message.success('修改成功')
    editRoleModalVisible.value = false
    loadAuthUsers()
  } catch (e: any) {
    message.error(e?.message || '修改失败')
  } finally {
    editRoleLoading.value = false
  }
}

async function handleRevoke(id: number) {
  try {
    await del(`/finance/account-set-auth/${id}`)
    message.success('已撤销授权')
    loadAuthUsers()
  } catch (e: any) {
    message.error(e?.message || '撤销失败')
  }
}

// ==================== 初始化 ====================
onMounted(async () => {
  if (accountSetStore.accountSets.length === 0) {
    await accountSetStore.fetchAccountSets()
  }
  // 默认选中当前账套
  if (accountSetStore.currentAccountSetId) {
    selectedAccountSetId.value = accountSetStore.currentAccountSetId
    loadAuthUsers()
  }
  loadRoles()
})
</script>

<style scoped>
.page-container {
  padding: 0;
}
</style>
