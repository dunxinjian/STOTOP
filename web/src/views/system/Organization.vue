<template>
  <div class="page-container">
    <PageHeader title="组织架构" description="管理公司部门组织结构">
      <template #actions>
        <a-button @click="handleViewChangeLogs">
          <template #icon><FileOutlined /></template>变更记录
        </a-button>
      </template>
    </PageHeader>

    <a-row :gutter="16">
      <!-- 左侧部门树 -->
      <a-col :span="8">
        <a-card class="tree-card" :bordered="false">
          <template #title>
            <div class="card-header">
              <span>组织架构</span>
              <a-button type="link" @click="handleAddRoot">
                <PlusOutlined />新增根部门
              </a-button>
            </div>
          </template>
          <a-tree
            :tree-data="deptTreeData"
            :fieldNames="{ title: 'name', key: 'id', children: 'children' }"
            v-model:selectedKeys="selectedTreeKeys"
            :defaultExpandAll="true"
            @select="handleNodeSelect"
          >
            <template #title="{ name, id, ...nodeData }">
              <div class="custom-tree-node">
                <span class="node-label">
                  {{ name }}
                </span>
                <span class="node-actions" @click.stop>
                  <a-button type="link" size="small" @click.stop="handleAddChild({ id, name, ...nodeData })">
                    <PlusOutlined />
                  </a-button>
                  <a-button type="link" size="small" @click.stop="handleEdit({ id, name, ...nodeData })">
                    <EditOutlined />
                  </a-button>
                  <a-popconfirm
                    title="确定删除该部门吗？"
                    okText="确定"
                    cancelText="取消"
                    @confirm="handleDelete({ id, name, ...nodeData })"
                  >
                    <a-button type="link" size="small" danger @click.stop>
                      <DeleteOutlined />
                    </a-button>
                  </a-popconfirm>
                </span>
              </div>
            </template>
          </a-tree>
        </a-card>
      </a-col>

      <!-- 右侧详情 -->
      <a-col :span="16">
        <a-card v-if="selectedDept" class="detail-card" :bordered="false">
          <template #title>
            <div class="card-header">
              <span>部门详情</span>
              <div class="card-header-actions">
                <a-button type="link" @click="handleEdit(selectedDept)">
                  <EditOutlined />编辑
                </a-button>
              </div>
            </div>
          </template>
          <a-descriptions :column="2" bordered>
            <a-descriptions-item label="部门名称">{{ selectedDept.name }}</a-descriptions-item>
            <a-descriptions-item label="部门编码">{{ selectedDept.code }}</a-descriptions-item>
            <a-descriptions-item label="组织类型">
              <a-tag :color="orgTypeTagColor(selectedDept.typeCode)">{{ selectedDept.typeName || '未设置' }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="排序">{{ selectedDept.sort }}</a-descriptions-item>
            <a-descriptions-item label="上级部门">{{ selectedDept.parentName || '-' }}</a-descriptions-item>
            <a-descriptions-item label="负责人">{{ selectedDept.managerName || '-' }}</a-descriptions-item>
            <a-descriptions-item label="编制 / 实际">
              {{ selectedDept.headcount ?? '-' }} / {{ selectedDept.actualCount ?? '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="创建时间">{{ selectedDept.createTime || selectedDept.createdAt || '-' }}</a-descriptions-item>
            <a-descriptions-item v-if="isSwitchableOrgType" label="列入组织切换列表">
              <a-tag :color="selectedDept.isSwitchable ? 'success' : 'default'">
                {{ selectedDept.isSwitchable ? '是' : '否' }}
              </a-tag>
            </a-descriptions-item>
          </a-descriptions>

          <!-- 关联账套 -->
          <template v-if="isSwitchableOrgType">
            <div class="section-title">关联账套</div>
            <a-spin :spinning="accountSetsLoading">
              <div v-if="accountSets.length > 0" class="account-sets-list">
                <a-descriptions v-for="item in accountSets" :key="item.id" :column="3" bordered size="small" style="margin-bottom: 8px">
                  <a-descriptions-item label="账套名称">{{ item.name }}</a-descriptions-item>
                  <a-descriptions-item label="编码">{{ item.code }}</a-descriptions-item>
                  <a-descriptions-item label="状态">
                    <a-tag :color="item.status === 1 ? 'success' : 'error'">{{ item.status === 1 ? '启用' : '停用' }}</a-tag>
                  </a-descriptions-item>
                </a-descriptions>
              </div>
              <EmptyState v-else description="暂无关联账套" />
            </a-spin>
          </template>

          <div class="section-title">部门成员</div>
          <a-table
            :columns="memberColumns"
            :dataSource="deptUsers"
            :loading="loading"
            size="small"
            bordered
            :pagination="false"
            rowKey="id"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.dataIndex === 'index'">{{ index + 1 }}</template>
              <template v-if="column.dataIndex === 'status'">
                <a-tag :color="record.status === 1 ? 'success' : 'error'">
                  {{ record.status === 1 ? '启用' : '停用' }}
                </a-tag>
              </template>
            </template>
          </a-table>
          <EmptyState v-if="deptUsers.length === 0" description="暂无成员" />
        </a-card>

        <a-card v-else class="detail-card empty-card" :bordered="false">
          <EmptyState description="请选择部门查看详情" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增部门' : '编辑部门'"
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
        <a-form-item label="部门名称" name="name">
          <a-input v-model:value="formData.name" placeholder="请输入部门名称" :maxlength="50" />
        </a-form-item>
        <a-form-item label="部门编码" name="code">
          <a-input v-model:value="formData.code" placeholder="请输入部门编码" :maxlength="50" />
        </a-form-item>
        <a-form-item label="上级部门" name="parentId">
          <a-tree-select
            v-model:value="formData.parentId"
            :tree-data="deptTreeData"
            :fieldNames="{ label: 'name', value: 'id', children: 'children' }"
            placeholder="请选择上级部门"
            allowClear
            :treeDefaultExpandAll="false"
            :disabled="dialogType === 'edit'"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="组织类型" name="typeId">
          <a-select v-model:value="formData.typeId" placeholder="请选择组织类型" style="width: 100%">
            <a-select-option v-for="t in orgTypes" :key="t.id" :value="t.id">
              {{ t.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="负责人">
          <a-select
            v-model:value="formData.managerId"
            placeholder="请选择负责人"
            allowClear
            showSearch
            :filterOption="false"
            @search="searchUsers"
            :loading="userSearchLoading"
            :options="userOptions.map(u => ({ label: u.name, value: u.id }))"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="编制人数">
          <a-input-number v-model:value="formData.headcount" :min="0" :max="99999" placeholder="编制人数" style="width: 100%" />
        </a-form-item>
        <a-form-item label="排序" name="sort">
          <a-input-number v-model:value="formData.sort" :min="0" :max="9999" style="width: 100%" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="formData.description" :rows="3" placeholder="请输入描述" :maxlength="500" showCount />
        </a-form-item>
        <a-form-item v-if="isFormSwitchableType" label="列入组织切换">
          <a-switch v-model:checked="formData.isSwitchable" checked-children="是" un-checked-children="否" />
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
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, DeleteOutlined, FileOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import type { OrgType } from '@/api/system'
import {
  getOrganizationTree,
  createOrg,
  updateOrg,
  deleteOrganization,
  getUserList,
  getOrgAccountSets,
  getOrgTypes,
} from '@/api/system'

const router = useRouter()

const memberColumns = [
  { title: '#', dataIndex: 'index', key: 'index', width: 50, align: 'center' as const },
  { title: '姓名', dataIndex: 'name', key: 'name', width: 100 },
  { title: '账号', dataIndex: 'account', key: 'account', width: 120 },
  { title: '手机号', dataIndex: 'phone', key: 'phone', width: 120 },
  { title: '邮箱', dataIndex: 'email', key: 'email', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
]

// 部门树数据
const loading = ref(false)
const deptTreeData = ref<any[]>([])
const selectedDept = ref<any>(null)
const deptUsers = ref<any[]>([])
const selectedTreeKeys = ref<number[]>([])
const orgTypes = ref<OrgType[]>([])

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentDeptId = ref<number | null>(null)

// 用户搜索
const userSearchLoading = ref(false)
const userOptions = ref<any[]>([])

const formData = reactive({
  name: '',
  code: '',
  parentId: undefined as number | undefined,
  typeId: 5,
  sort: 0,
  status: 1,
  managerId: undefined as number | undefined,
  headcount: undefined as number | undefined,
  description: '',
  isSwitchable: false,
})

const defaultOrgTypeId = computed(() => {
  return orgTypes.value.find(item => item.code === 'DEPT')?.id ?? 5
})

function getOrgTypeById(typeId?: number) {
  return orgTypes.value.find(item => item.id === typeId)
}

const formRules: Record<string, any[]> = {
  name: [{ required: true, message: '请输入部门名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入部门编码', trigger: 'blur' }],
  typeId: [{ required: true, message: '请选择组织类型', trigger: 'change' }],
}

// 关联账套
const accountSets = ref<any[]>([])
const accountSetsLoading = ref(false)

// 判断是否为集团/子公司类型
const isSwitchableOrgType = computed(() => {
  return selectedDept.value?.canSwitch === true
})

const isFormSwitchableType = computed(() => {
  return getOrgTypeById(formData.typeId)?.canSwitch === true
})

// 获取组织关联账套
async function fetchAccountSets(orgId: number) {
  accountSetsLoading.value = true
  try {
    const res = await getOrgAccountSets(orgId) as any[]
    accountSets.value = Array.isArray(res) ? res : []
  } catch {
    accountSets.value = []
  } finally {
    accountSetsLoading.value = false
  }
}

function orgTypeTagColor(typeCode?: string) {
  const colorMap: Record<string, string> = {
    GROUP: 'blue',
    SUBSIDIARY: 'green',
    CENTER: 'orange',
    BRANCH: 'cyan',
    DEPT: 'default',
    TEAM: 'red',
  }
  return typeCode ? colorMap[typeCode] || 'default' : 'default'
}

// 获取部门树
async function fetchDeptTree() {
  try {
    const res = await getOrganizationTree() as any[]
    if (res) {
      deptTreeData.value = res || []
    }
  } catch (error) {
    console.error('获取部门树失败:', error)
  }
}

// 获取部门用户
async function fetchDeptUsers(deptId: number) {
  loading.value = true
  try {
    const res = await getUserList({
      departmentId: deptId,
      pageIndex: 1,
      pageSize: 100,
    }) as any
    if (res) {
      deptUsers.value = res?.items || []
    }
  } catch {
    deptUsers.value = []
  } finally {
    loading.value = false
  }
}

// 搜索用户（负责人选择器）
async function searchUsers(query: string) {
  userSearchLoading.value = true
  try {
    const params: any = { pageIndex: 1, pageSize: 30 }
    if (query) params.keyword = query
    const res = await getUserList(params) as any
    userOptions.value = res?.items || []
  } catch {
    userOptions.value = []
  } finally {
    userSearchLoading.value = false
  }
}

// 打开弹窗时预加载用户列表
async function preloadUserOptions(existingId?: number, existingName?: string) {
  // 先用已有负责人填充，避免闪烁
  if (existingId && existingName) {
    userOptions.value = [{ id: existingId, name: existingName }]
  } else {
    userOptions.value = []
  }
  // 后台加载默认列表
  await searchUsers('')
}

// 节点选择
function handleNodeSelect(_keys: (string | number)[], info: any) {
  if (info.selected && info.node) {
    const data = info.node.dataRef || info.node
    selectedDept.value = data
    fetchDeptUsers(data.id)
    // 如果允许关联账套，加载关联账套
    if (data.canBindAccountSet === true) {
      fetchAccountSets(data.id)
    } else {
      accountSets.value = []
    }
  }
}

// 新增根部门
function handleAddRoot() {
  dialogType.value = 'add'
  currentDeptId.value = null
  resetForm()
  formData.parentId = undefined
  dialogVisible.value = true
}

// 新增子部门
function handleAddChild(parent: any) {
  dialogType.value = 'add'
  currentDeptId.value = null
  resetForm()
  formData.parentId = parent.id
  dialogVisible.value = true
}

// 编辑
function handleEdit(data: any) {
  dialogType.value = 'edit'
  currentDeptId.value = data.id
  formData.name = data.name
  formData.code = data.code
  formData.parentId = data.parentId
  formData.typeId = data.typeId ?? defaultOrgTypeId.value
  formData.sort = data.sort || 0
  formData.managerId = data.managerId || undefined
  formData.headcount = data.headcount ?? undefined
  formData.description = data.description || ''
  formData.isSwitchable = data.isSwitchable ?? false
  formData.status = data.status ?? 1
  dialogVisible.value = true
  // 预加载负责人选项（异步，不阻塞弹窗显示）
  preloadUserOptions(data.managerId, data.managerName)
}

// 重置表单
function resetForm() {
  formData.name = ''
  formData.code = ''
  formData.parentId = undefined
  formData.typeId = defaultOrgTypeId.value
  formData.sort = 0
  formData.managerId = undefined
  formData.headcount = undefined
  formData.description = ''
  formData.isSwitchable = false
  formData.status = 1
  // 预加载用户列表供新增时选择
  preloadUserOptions()
}

// 提交表单
async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch { return }

  submitLoading.value = true
  try {
    const data: any = {
      name: formData.name,
      code: formData.code,
      parentId: formData.parentId ?? undefined,
      typeId: formData.typeId,
      sort: formData.sort,
      status: formData.status,
      managerId: formData.managerId ?? undefined,
      headcount: formData.headcount ?? undefined,
      description: formData.description || undefined,
      isSwitchable: formData.isSwitchable,
    }

    if (dialogType.value === 'add') {
      await createOrg(data)
      message.success('新增成功')
      dialogVisible.value = false
      fetchDeptTree()
    } else {
      await updateOrg(currentDeptId.value!, data)
      message.success('更新成功')
      dialogVisible.value = false
      fetchDeptTree()
      if (selectedDept.value && selectedDept.value.id === currentDeptId.value) {
        const selectedOrgType = getOrgTypeById(formData.typeId)
        selectedDept.value = {
          ...selectedDept.value,
          ...data,
          typeName: selectedOrgType?.name ?? selectedDept.value.typeName,
          typeCode: selectedOrgType?.code ?? selectedDept.value.typeCode,
          typeLevel: selectedOrgType?.level ?? selectedDept.value.typeLevel,
          canBindAccountSet: selectedOrgType?.canBindAccountSet ?? selectedDept.value.canBindAccountSet,
          canSwitch: selectedOrgType?.canSwitch ?? selectedDept.value.canSwitch,
        }
      }
    }
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(data: any) {
  try {
    await deleteOrganization(data.id)
    message.success('删除成功')
    if (selectedDept.value && selectedDept.value.id === data.id) {
      selectedDept.value = null
      deptUsers.value = []
    }
    fetchDeptTree()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

function handleViewChangeLogs() {
  const query: Record<string, string> = { businessType: 'Organization' }
  if (selectedDept.value) {
    query.businessId = String(selectedDept.value.id)
  }
  router.push({ path: '/system/change-logs', query })
}

onMounted(async () => {
  try {
    const [typeRes] = await Promise.all([
      getOrgTypes(),
      fetchDeptTree(),
    ])
    orgTypes.value = Array.isArray(typeRes) ? typeRes : []
    if (!formData.typeId) {
      formData.typeId = defaultOrgTypeId.value
    }
  } catch {
    orgTypes.value = []
  }
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.tree-card {
  .card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .custom-tree-node {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-right: $spacing-sm;

    .node-label {
      font-size: $font-size-base;
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .node-actions {
      display: none;
    }

    &:hover .node-actions {
      display: flex;
      gap: $spacing-xs;
    }
  }
}

.detail-card {
  min-height: 500px;

  .card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;

    .card-header-actions {
      display: flex;
      gap: 8px;
    }
  }

  &.empty-card {
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .section-title {
    margin: $spacing-lg 0 $spacing-md;
    font-size: $font-size-lg;
    font-weight: 500;
    color: $text-primary;
    padding-left: $spacing-sm + 4px;
    border-left: 4px solid var(--border);
  }
}
</style>
