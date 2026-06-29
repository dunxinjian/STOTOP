<template>
  <div class="page-container">
    <PageHeader title="菜单管理" description="管理系统菜单和权限">
      <template #actions>
        <a-input-search
          v-model:value="searchForm.keyword"
          placeholder="搜索菜单名称/编码"
          style="width: 240px"
          @search="handleSearch"
          allowClear
        />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增菜单
        </a-button>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :dataSource="filteredTableData"
        :loading="loading"
        rowKey="id"
        :childrenColumnName="'children'"
        :defaultExpandAllRows="true"
        :pagination="false"
        bordered
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'name'">
            <component v-if="record.icon" :is="record.icon" style="margin-right: 6px; vertical-align: middle;" />
            <span>{{ record.name }}</span>
          </template>
          <template v-if="column.dataIndex === 'type'">
            <a-tag v-if="record.type === 1" color="blue">模块</a-tag>
            <a-tag v-else-if="record.type === 2" color="green">菜单</a-tag>
            <a-tag v-else-if="record.type === 3" color="orange">按钮</a-tag>
            <a-tag v-else>未知</a-tag>
          </template>
          <template v-if="column.dataIndex === 'isVisible'">
            <a-tag :color="record.isVisible !== false ? 'success' : 'default'">
              {{ record.isVisible !== false ? '显示' : '隐藏' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleAddChild(record)">
              <PlusOutlined />新增
            </a-button>
            <a-button type="link" size="small" @click="handleEdit(record)">
              <EditOutlined />编辑
            </a-button>
            <a-popconfirm
              title="确定删除该菜单吗？"
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
          <EmptyState description="暂无菜单数据" />
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增菜单' : '编辑菜单'"
      :width="560"
      :destroyOnClose="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 10px 20px"
      >
        <a-form-item label="菜单名称" name="name">
          <a-input v-model:value="formData.name" placeholder="请输入菜单名称" :maxlength="50" />
        </a-form-item>
        <a-form-item label="菜单编码" name="code">
          <a-input v-model:value="formData.code" placeholder="请输入菜单编码" :maxlength="50" />
        </a-form-item>
        <a-form-item label="菜单类型" name="type">
          <a-select v-model:value="formData.type" placeholder="请选择菜单类型" style="width: 100%"
            :options="[
              { label: '模块', value: 1 },
              { label: '菜单', value: 2 },
              { label: '按钮', value: 3 },
            ]"
          />
        </a-form-item>
        <a-form-item label="上级菜单" name="parentId">
          <a-tree-select
            v-model:value="formData.parentId"
            :tree-data="menuTreeData"
            :fieldNames="{ label: 'name', value: 'id', children: 'children' }"
            placeholder="请选择上级菜单"
            allowClear
            :treeDefaultExpandAll="false"
            :disabled="dialogType === 'edit'"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item
          v-if="formData.type === 2"
          label="路由路径"
          name="route"
          :rules="formData.type === 2 ? [{ required: true, message: '请输入路由路径', trigger: 'blur' }] : []"
        >
          <a-input v-model:value="formData.route" placeholder="请输入路由路径，如：/system/user" :maxlength="100" />
        </a-form-item>
        <a-form-item
          v-if="formData.type === 2"
          label="组件路径"
          name="componentPath"
          :rules="formData.type === 2 ? [{ required: true, message: '请输入组件路径', trigger: 'blur' }] : []"
        >
          <a-input v-model:value="formData.componentPath" placeholder="请输入组件路径，如：system/UserManage" :maxlength="100" />
        </a-form-item>
        <a-form-item label="图标" name="icon">
          <a-input v-model:value="formData.icon" placeholder="请输入图标名称，如：User" :maxlength="50" />
          <div class="form-tip">使用 Ant Design 图标名称</div>
        </a-form-item>
        <a-form-item label="排序" name="sort">
          <a-input-number v-model:value="formData.sort" :min="0" :max="9999" style="width: 100%" />
        </a-form-item>
        <a-form-item label="是否可见" name="isVisible">
          <a-radio-group v-model:value="formData.isVisible">
            <a-radio :value="true">显示</a-radio>
            <a-radio :value="false">隐藏</a-radio>
          </a-radio-group>
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
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getPermissionTree,
  createPermission,
  updatePermission,
  deletePermission,
} from '@/api/system'

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 180 },
  { title: '编码', dataIndex: 'code', key: 'code', width: 150 },
  { title: '类型', dataIndex: 'type', key: 'type', width: 100, align: 'center' as const },
  { title: '路由', dataIndex: 'route', key: 'route', width: 150, ellipsis: true },
  { title: '组件路径', dataIndex: 'componentPath', key: 'componentPath', width: 200, ellipsis: true },
  { title: '排序', dataIndex: 'sort', key: 'sort', width: 80, align: 'center' as const },
  { title: '状态', dataIndex: 'isVisible', key: 'isVisible', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 200, align: 'center' as const, fixed: 'right' as const },
]

// 搜索表单
const searchForm = reactive({
  keyword: '',
})

// 表格数据
const loading = ref(false)
const tableData = ref<any[]>([])
const menuTreeData = ref<any[]>([])

// 过滤后的数据
const filteredTableData = computed(() => {
  if (!searchForm.keyword) return tableData.value
  return filterTreeData(tableData.value, searchForm.keyword.toLowerCase())
})

// 递归过滤树形数据
function filterTreeData(data: any[], keyword: string): any[] {
  const result: any[] = []
  for (const item of data) {
    const match =
      item.name?.toLowerCase().includes(keyword) ||
      item.code?.toLowerCase().includes(keyword)
    const children = item.children ? filterTreeData(item.children, keyword) : []
    if (match || children.length > 0) {
      result.push({
        ...item,
        children: children.length > 0 ? children : item.children,
      })
    }
  }
  return result
}

// 弹窗相关
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentMenuId = ref<number | null>(null)

const formData = reactive({
  name: '',
  code: '',
  type: 2,
  parentId: null as number | null,
  route: '',
  componentPath: '',
  icon: '',
  sort: 0,
  isVisible: true,
})

const formRules: Record<string, any[]> = {
  name: [{ required: true, message: '请输入菜单名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入菜单编码', trigger: 'blur' }],
  type: [{ required: true, message: '请选择菜单类型', trigger: 'change' }],
}

// 获取权限树
async function fetchPermissionTree() {
  loading.value = true
  try {
    const res = await getPermissionTree() as any[]
    if (res) {
      tableData.value = res || []
      menuTreeData.value = res || []
    }
  } finally {
    loading.value = false
  }
}

// 搜索
function handleSearch() {
  // 过滤已在 computed 中处理
}

// 重置搜索
function handleReset() {
  searchForm.keyword = ''
}

// 新增
function handleAdd() {
  dialogType.value = 'add'
  currentMenuId.value = null
  resetForm()
  formData.parentId = null
  dialogVisible.value = true
}

// 新增子菜单
function handleAddChild(parent: any) {
  dialogType.value = 'add'
  currentMenuId.value = null
  resetForm()
  formData.parentId = parent.id
  // 根据父级类型自动设置子级类型
  if (parent.type === 1) {
    formData.type = 2
  } else if (parent.type === 2) {
    formData.type = 3
  }
  dialogVisible.value = true
}

// 编辑
function handleEdit(row: any) {
  dialogType.value = 'edit'
  currentMenuId.value = row.id
  formData.name = row.name
  formData.code = row.code
  formData.type = row.type
  formData.parentId = row.parentId
  formData.route = row.route || ''
  formData.componentPath = row.componentPath || ''
  formData.icon = row.icon || ''
  formData.sort = row.sort || 0
  formData.isVisible = row.isVisible !== false
  dialogVisible.value = true
}

// 重置表单
function resetForm() {
  formData.name = ''
  formData.code = ''
  formData.type = 2
  formData.parentId = null
  formData.route = ''
  formData.componentPath = ''
  formData.icon = ''
  formData.sort = 0
  formData.isVisible = true
}

// 提交表单
async function handleSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  submitLoading.value = true
  try {
    const data: any = {
      name: formData.name,
      code: formData.code,
      type: formData.type,
      parentId: formData.parentId ?? undefined,
      route: formData.type === 2 ? formData.route : undefined,
      componentPath: formData.type === 2 ? formData.componentPath : undefined,
      icon: formData.icon,
      sort: formData.sort,
      isVisible: formData.isVisible,
    }

    if (dialogType.value === 'add') {
      await createPermission(data)
      message.success('新增成功')
      dialogVisible.value = false
      fetchPermissionTree()
    } else {
      await updatePermission(currentMenuId.value!, data)
      message.success('更新成功')
      dialogVisible.value = false
      fetchPermissionTree()
    }
  } finally {
    submitLoading.value = false
  }
}

// 删除
async function handleDelete(row: any) {
  try {
    await deletePermission(row.id)
    message.success('删除成功')
    fetchPermissionTree()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

onMounted(() => {
  fetchPermissionTree()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.form-tip {
  font-size: $font-size-sm;
  color: $text-secondary;
  margin-top: $spacing-xs;
}
</style>
