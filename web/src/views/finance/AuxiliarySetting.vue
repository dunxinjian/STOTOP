<template>
  <div class="page-container">
    <PageHeader title="辅助核算设置">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #actions>
        <a-input
          v-model:value="searchKeyword"
          placeholder="搜索"
          style="width: 200px; margin-right: 8px;"
          allow-clear
        >
          <template #suffix>
            <SearchOutlined />
          </template>
        </a-input>
        <a-button @click="handleAmoebaMode">阿米巴模式</a-button>
        <a-button @click="handleRegretMode">反悔模式</a-button>
      </template>
    </PageHeader>

    <!-- 工具栏 -->
    <div class="toolbar-section">
        <div class="toolbar-left">
          <a-radio-group
            :value="currentCategory"
            @change="(e: any) => handleCategoryChange(e.target.value)"
            button-style="solid"
            class="category-radio-group"
          >
            <a-radio-button
              v-for="cat in categories"
              :key="cat.value"
              :value="cat.value"
            >
              {{ cat.label }}
            </a-radio-button>
          </a-radio-group>
          <a-dropdown :trigger="['click']">
            <a-button>
              {{ customCategoryLabel }} <DownOutlined />
            </a-button>
            <template #overlay>
              <a-menu @click="onCustomCategoryMenuClick">
                <a-menu-item
                  v-for="cat in customCategories"
                  :key="cat.value"
                >
                  {{ cat.label }}
                </a-menu-item>
                <a-menu-divider />
                <a-menu-item key="add">
                  <span style="color: var(--color-primary);">+ 新增</span>
                </a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
        <div class="toolbar-right">
          <a-button type="primary" @click="openExternalPicker" v-if="isExternalType">
            {{ externalAddLabel }}
          </a-button>
          <a-button type="primary" @click="handleAdd" v-if="!isExternalType">新增</a-button>
          <a-button @click="handleDelete" :disabled="selectedRows.length === 0">删除</a-button>
        </div>
    </div>

    <div class="main-content">
      <!-- 左侧内容区 -->
      <div class="left-content">

        <!-- 外部类型：提示信息 -->
        <a-alert
          v-if="isExternalType"
          :message="externalTypeMessage"
          type="info"
          show-icon
          :closable="false"
          style="margin: 8px 12px 0;"
        />

        <!-- 数据表格 -->
        <div class="table-card">
          <a-table
            :columns="tableColumns"
            :data-source="filteredTableData"
            row-key="id"
            :loading="loading"
            :row-selection="rowSelection"
            :pagination="false"
            size="small"
            :bordered="true"
            :custom-row="(record: any) => ({ onClick: () => handleCurrentChange(record) })"
            :row-class-name="(record: any) => record.id === currentRow?.id ? 'current-row' : ''"
          >
            <template #emptyText>
              <EmptyState />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'name'">
                <a class="name-link" @click.stop="handleEdit(record)">{{ record.name }}</a>
              </template>
              <template v-if="column.dataIndex === 'enabled'">
                <a-switch
                  v-model:checked="record.enabled"
                  @change="(checked: any) => handleStatusChange(record, checked)"
                />
              </template>
            </template>
          </a-table>
        </div>

        <!-- 分页 -->
        <div class="pagination-container">
          <a-pagination
            v-model:current="currentPage"
            v-model:pageSize="pageSize"
            :total="total"
            :show-size-changer="false"
            size="small"
            @change="handlePageChange"
          />
        </div>
      </div>

      <!-- 右侧关联科目面板 -->
      <div class="right-panel">
        <div class="panel-header">
          <span class="panel-title">关联科目</span>
          <EditOutlined class="edit-icon" @click="handleEditAccounts" />
        </div>

        <div class="panel-tips">
          <div class="tip-line">
            <span class="tip-bar"></span>
            <span class="tip-text">每个辅助类别若要关联科目，则其至少要有一个辅助核算编码；</span>
          </div>
          <div class="tip-line">
            <span class="tip-bar"></span>
            <span class="tip-text">每个科目只支持关联两个辅助核算类别。</span>
          </div>
        </div>

        <a-spin :spinning="relatedAccountsLoading">
          <div class="account-list">
            <div v-if="relatedAccounts.length === 0 && !relatedAccountsLoading" class="no-related-accounts">
              <span>暂无关联科目</span>
            </div>
            <div v-for="account in relatedAccounts" :key="account.code" class="account-item">
              <span class="account-code">{{ account.code }}</span>
              <span class="account-name">{{ account.name }}</span>
            </div>
          </div>
        </a-spin>
      </div>
    </div>

    <!-- 新增辅助核算弹窗（顶部新增按钮触发） -->
    <a-modal
      v-model:open="addAuxDialogVisible"
      title="新增辅助核算"
      width="550px"
      :destroy-on-close="true"
      centered
    >
      <a-form
        ref="addAuxFormRef"
        :model="addAuxFormData"
        :rules="addAuxFormRules"
        :label-col="{ style: { width: '60px' } }"
        class="add-dialog-form"
      >
        <a-form-item label="编码" v-if="isManualCodeType">
          <a-input v-model:value="addAuxFormData.code" placeholder="请输入编码" />
        </a-form-item>
        <a-form-item label="编码" v-else>
          <a-input :value="'保存后自动生成'" disabled class="code-readonly" />
        </a-form-item>
        <a-form-item label="名称" name="name">
          <a-input v-model:value="addAuxFormData.name" />
        </a-form-item>
        <a-form-item label="类别">
          <div class="category-tag-selector">
            <a-tag
              v-for="cat in addAuxFormData.selectedCategories"
              :key="cat.value"
              closable
              @close="handleRemoveAuxCategory(cat.value)"
              class="selected-cat-tag"
            >
              {{ cat.label }}
            </a-tag>
            <a-select
              v-model:value="addAuxCategorySelectVal"
              placeholder="+ 添加类别"
              class="cat-inline-select"
              @change="handleAuxCategorySelect"
              size="small"
              :options="availableCategoryOptions"
            />
          </div>
        </a-form-item>
      </a-form>
      <div class="add-dialog-tips">
        <div class="add-tip-item">系统预置"客户"、"供应商"、"部门"、"项目"、"员工"、"经营单元"、"快递品牌"七种辅助核算类别，这七种类别不能被修改或删除；</div>
        <div class="add-tip-item">用户可自定义辅助核算类别，请在"类别"栏输入需要自定义的类型名称即可；</div>
        <div class="add-tip-item">用户自定义的辅助核算类别下应至少有一个"编码"，否则该类别将被自动清除。</div>
      </div>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="addAuxDialogVisible = false">取消</a-button>
          <a-button @click="handleAuxSaveAndClose">保存</a-button>
          <a-button type="primary" @click="handleAuxSaveAndAdd">保存并新增</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 新增自定义类型弹窗（自定义类别下拉新增触发） -->
    <a-modal
      v-model:open="addDialogVisible"
      :title="addDialogTitle"
      width="550px"
      :destroy-on-close="true"
      centered
    >
      <a-form
        ref="addFormRef"
        :model="addFormData"
        :rules="addFormRules"
        :label-col="{ style: { width: '60px' } }"
        class="add-dialog-form"
      >
        <a-form-item label="类别" name="categoryText">
          <a-input
            v-model:value="addFormData.categoryText"
            :readonly="addFormData.categoryReadonly"
            :class="{ 'category-readonly': addFormData.categoryReadonly }"
          />
        </a-form-item>
        <a-form-item label="编码" v-if="isManualCodeType">
          <a-input v-model:value="addFormData.code" placeholder="请输入编码" />
        </a-form-item>
        <a-form-item label="编码" v-else>
          <a-input :value="'保存后自动生成'" disabled class="code-readonly" />
        </a-form-item>
        <a-form-item label="名称" name="name">
          <a-input v-model:value="addFormData.name" />
        </a-form-item>
      </a-form>
      <div class="add-dialog-tips">
        <div class="add-tip-item">系统预置"客户"、"供应商"、"部门"、"项目"、"员工"、"经营单元"、"快递品牌"七种辅助核算类别，这七种类别不能被修改或删除；</div>
        <div class="add-tip-item">用户可自定义辅助核算类别，请在"类别"栏输入需要自定义的类型名称即可；</div>
        <div class="add-tip-item">用户自定义的辅助核算类别下应至少有一个"编码"，否则该类别将被自动清除。</div>
      </div>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="addDialogVisible = false">取消</a-button>
          <a-button @click="handleSaveAndClose">保存</a-button>
          <a-button type="primary" @click="handleSaveAndAdd">保存并新增</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 编辑辅助核算弹窗 -->
    <a-modal
      v-model:open="editDialogVisible"
      title="编辑辅助核算"
      width="500px"
      :destroy-on-close="true"
      centered
    >
      <a-alert v-if="isExternalSourceItem" type="info" show-icon
        message="该项目来源于外部数据，编码和名称由数据源自动同步，不可手动修改"
        style="margin-bottom: 12px" />
      <a-form
        ref="editFormRef"
        :model="editFormData"
        :rules="addFormRules"
        :label-col="{ style: { width: '80px' } }"
      >
        <a-form-item label="编码">
          <a-input :value="editFormData.code" :disabled="true" class="code-readonly" />
        </a-form-item>
        <a-form-item label="名称" name="name">
          <a-input v-model:value="editFormData.name" placeholder="请输入名称"
            :disabled="isExternalSourceItem" />
        </a-form-item>
        <a-form-item label="类别">
          <div class="category-tag">
            <a-tag>{{ getCategoryLabel(editFormData.category) }}</a-tag>
          </div>
        </a-form-item>
        <a-form-item label="备注">
          <a-input v-model:value="editFormData.remark" placeholder="请输入备注" />
        </a-form-item>
      </a-form>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="editDialogVisible = false">取消</a-button>
          <a-button type="primary" @click="handleEditSubmit">保存</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 编辑关联科目弹窗 -->
    <a-modal
      v-model:open="accountDialogVisible"
      title="编辑关联科目"
      width="600px"
      :destroy-on-close="true"
      centered
    >
      <div class="account-dialog-content">
        <div class="selected-accounts">
          <span class="label">添加科目：</span>
          <div class="tags-container">
            <a-tag
              v-for="account in selectedAccountTags"
              :key="account.code"
              closable
              @close="removeSelectedAccount(account)"
              class="account-tag-item"
            >
              {{ account.code }} {{ account.name }}
            </a-tag>
          </div>
        </div>
        <div class="expand-toggle" @click="isAccountListExpanded = !isAccountListExpanded">
          {{ isAccountListExpanded ? '收起' : '展开' }}
          <DownOutlined v-if="!isAccountListExpanded" /><UpOutlined v-else />
        </div>

        <div v-show="isAccountListExpanded" class="account-tabs-container">
          <a-tabs v-model:activeKey="activeAccountTab" class="account-tabs">
            <a-tab-pane key="asset" tab="资产" />
            <a-tab-pane key="liability" tab="负债" />
            <a-tab-pane key="equity" tab="权益" />
            <a-tab-pane key="cost" tab="成本" />
            <a-tab-pane key="profit" tab="损益" />
          </a-tabs>

          <div class="account-tree-container">
            <a-tree
              v-model:checkedKeys="checkedAccountKeys"
              :tree-data="filteredAccountTree as any"
              checkable
              :field-names="{ title: 'label', key: 'code', children: 'children' }"
              @check="handleAccountCheck"
            />
          </div>
        </div>
      </div>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="accountDialogVisible = false">取消</a-button>
          <a-button type="primary" @click="handleAccountSubmit">确定</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 阿米巴类别设置弹窗 -->
    <a-modal
      v-model:open="amoebaDialogVisible"
      title="阿米巴类别设置"
      width="500px"
      :destroy-on-close="true"
      centered
    >
      <a-form :label-col="{ style: { width: '100px' } }">
        <a-form-item label="默认类别：">
          <a-select v-model:value="amoebaDefaultCategory" placeholder="请选择" style="width: 200px;" :options="categories" />
        </a-form-item>
      </a-form>
      <div class="info-tip dialog-tip">
        <p>前提：阿米巴类别必须关联损益类科目；</p>
        <p>可将任一关联类损益类科目的辅助核算类别开通阿米巴功能；</p>
        <p>可更改阿米巴类别。</p>
      </div>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="amoebaDialogVisible = false">关闭</a-button>
          <a-button type="primary" @click="handleAmoebaSubmit">开通</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 反悔模式弹窗 -->
    <a-modal
      v-model:open="regretDialogVisible"
      title="反悔模式"
      width="550px"
      :destroy-on-close="true"
      centered
    >
      <a-form :label-col="{ style: { width: '100px' } }">
        <a-form-item label="反悔类型：">
          <a-radio-group v-model:value="regretType">
            <a-radio value="code">修改核算对象编码</a-radio>
            <a-radio value="name">修改辅助类别名称</a-radio>
            <a-radio value="merge">合并核算对象</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item label="辅助类别：">
          <a-select v-model:value="regretCategory" placeholder="请选择" style="width: 100%;" :options="categories" />
        </a-form-item>
        <a-form-item label="核算对象：">
          <a-select v-model:value="regretObject" placeholder="请选择" style="width: 100%;"
            :options="currentCategoryItems.map((i: AuxiliaryItem) => ({ label: i.name, value: i.code }))" />
        </a-form-item>
        <a-form-item>
          <a-checkbox v-model:checked="isBatchRegret">批量修改</a-checkbox>
        </a-form-item>
      </a-form>

      <div class="regret-info-section">
        <div class="regret-info-title">该核算对象已使用的内容：</div>
      </div>

      <div class="info-tip dialog-tip">
        <p>将统一修改以上内容的核算对象编码</p>
      </div>

      <a-form :label-col="{ style: { width: '100px' } }">
        <a-form-item label="原编码：">
          <a-input v-model:value="originalCode" placeholder="请输入原编码" />
        </a-form-item>
        <a-form-item label="新编码：">
          <a-input v-model:value="newCode" placeholder="请输入新编码" disabled class="disabled-input" />
        </a-form-item>
      </a-form>

      <template #footer>
        <div class="dialog-footer">
          <a-button @click="regretDialogVisible = false">取消</a-button>
          <a-button type="primary" disabled>信息确认</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 新增自定义类别弹窗 -->
    <a-modal
      v-model:open="addCategoryDialogVisible"
      title="新增自定义类别"
      width="400px"
      :destroy-on-close="true"
      centered
    >
      <a-form :label-col="{ style: { width: '80px' } }">
        <a-form-item label="类别名称">
          <a-input v-model:value="newCategoryName" placeholder="请输入自定义类别名称" />
        </a-form-item>
      </a-form>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="addCategoryDialogVisible = false">取消</a-button>
          <a-button type="primary" @click="handleAddCategorySubmit">确定</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 外部数据源选择弹窗 -->
    <a-modal
      v-model:open="pickerVisible"
      :title="pickerTitle"
      width="700px"
      @ok="handlePickerConfirm"
      @cancel="pickerVisible = false"
      :confirmLoading="pickerLoading"
      centered
    >
      <div style="margin-bottom: 12px;">
        <a-input-search
          v-model:value="pickerSearchKeyword"
          placeholder="搜索"
          style="width: 250px"
          allow-clear
          @search="filterPickerData"
          @change="filterPickerData"
        />
      </div>
      <a-table
        :columns="pickerColumns"
        :data-source="filteredPickerData"
        :row-selection="{ selectedRowKeys: pickerSelectedKeys, onChange: onPickerSelectChange }"
        :pagination="{ pageSize: 10 }"
        :rowKey="pickerRowKey"
        size="small"
        :scroll="{ y: 400 }"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useDebouncedRef } from '@/utils/debounce'
import { message, Modal } from 'ant-design-vue'
import { SearchOutlined, DownOutlined, EditOutlined, UpOutlined } from '@ant-design/icons-vue'
import {
  getAccountTree, createAuxiliaryItem, getAuxiliaryItemsByAccountSet, getAccountsByAuxType,
  updateAccountAuxiliary, updateAuxiliaryItem, deleteAuxiliaryItem, checkAuxiliaryItemUsage,
  getAvailableCustomers, getAvailableSuppliers, getAvailableBrands,
  getAvailableEmployees, getAvailableDepartments, getAvailableNetworkPoints,
  addFromCustomer, addFromSupplier, addFromBrand, addFromUser, addFromOrg, addFromNetworkPoint,
} from '@/api/finance'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import { useAccountSetStore } from '@/stores/accountSet'

// 类型定义
interface Category {
  label: string
  value: string
}

interface AuxiliaryItem {
  id: number
  code: string
  name: string
  enabled: boolean
  category: string
  sourceType?: string | null
}

interface AccountItem {
  code: string
  name: string
  category?: string
}

interface AccountTreeNode {
  key?: string | number
  code: string
  name: string
  label: string
  children?: AccountTreeNode[]
}

// 账套 store
const accountSetStore = useAccountSetStore()

// 分类选项（value 使用英文编码）
const categories = ref<Category[]>([
  { label: '客户', value: 'customer' },
  { label: '供应商', value: 'supplier' },
  { label: '部门', value: 'department' },
  { label: '项目', value: 'project' },
  { label: '员工', value: 'employee' },
  { label: '经营单元', value: 'business_unit' },
  { label: '快递品牌', value: 'express_brand' },
  { label: '网点', value: 'outlet' },
  { label: '业务方向', value: 'business_direction' },
])

// 当前选中的分类
const currentCategory = ref('customer')

// 外部类型：从关联数据源选择添加
const isExternalType = computed(() => ['customer', 'supplier', 'employee', 'department', 'express_brand', 'outlet'].includes(currentCategory.value))

const externalAddLabel = computed(() => {
  const labelMap: Record<string, string> = {
    customer: '从客户添加',
    supplier: '从供应商添加',
    employee: '从员工添加',
    department: '从部门添加',
    express_brand: '从品牌库添加',
    outlet: '从网点添加',
  }
  return labelMap[currentCategory.value] || '添加'
})

// 搜索关键词
const searchKeyword = ref('')
// 防抖搜索关键词（300ms），避免每次按键都触发过滤计算
const debouncedSearchKeyword = useDebouncedRef(searchKeyword, 300)

// 自定义类别
const customCategories = ref<Category[]>([])
const customCategory = ref('')

const customCategoryLabel = computed(() => {
  if (!customCategory.value) return '自定义类别'
  const cat = customCategories.value.find(c => c.value === customCategory.value)
  return cat?.label || '自定义类别'
})

// 表格数据
const tableData = ref<AuxiliaryItem[]>([])
const loading = ref(false)
const selectedRows = ref<AuxiliaryItem[]>([])
const currentRow = ref<AuxiliaryItem | null>(null)

// 表格列配置
const tableColumns = [
  { title: '编码', dataIndex: 'code', width: 80, align: 'center' as const },
  { title: '名称', dataIndex: 'name' },
  { title: '启/禁用', dataIndex: 'enabled', width: 100, align: 'center' as const },
]

// 行选择配置
const rowSelection = computed(() => ({
  selectedRowKeys: selectedRows.value.map(r => r.id),
  onChange: (_keys: (string | number)[], rows: AuxiliaryItem[]) => {
    selectedRows.value = rows
  },
}))

// 分页
const currentPage = ref(1)
const pageSize = ref(15)
const total = ref(0)

// 新增弹窗相关
const addDialogVisible = ref(false)
const addDialogIsCustom = ref(false) // true 表示从"自定义类别"新增触发
const addDialogTitle = computed(() => addDialogIsCustom.value ? '新增辅助核算' : '新增辅助核算')
const addFormRef = ref()
const addFormData = reactive({
  code: '',
  name: '',
  category: '',
  categoryText: '',
  categoryReadonly: false,
})

const addFormRules: Record<string, any[]> = {
  categoryText: [{ required: true, message: '请输入类别', trigger: 'blur' }],
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
}

// 新增辅助核算弹窗相关（顶部新增按钮）
const addAuxDialogVisible = ref(false)
const addAuxFormRef = ref()
const addAuxFormData = reactive({
  code: '',
  name: '',
  selectedCategories: [] as Category[],
})
const addAuxFormRules: Record<string, any[]> = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
}
const addAuxCategorySelectVal = ref('')

// 所有可选类别（预置 + 自定义）
const availableCategoryOptions = computed(() => {
  const preset: Category[] = [
    { label: '客户', value: 'customer' },
    { label: '供应商', value: 'supplier' },
    { label: '部门', value: 'department' },
    { label: '项目', value: 'project' },
    { label: '员工', value: 'employee' },
    { label: '经营单元', value: 'business_unit' },
    { label: '快递品牌', value: 'express_brand' },
  ]
  const custom = customCategories.value
  const all = [...preset, ...custom]
  // Filter out already selected
  return all.filter(opt => !addAuxFormData.selectedCategories.some(s => s.value === opt.value))
})

// 编辑弹窗相关
const editDialogVisible = ref(false)
const editFormRef = ref()
const editFormData = reactive({
  id: 0,
  code: '',
  originalCode: '', // 记录原始编码用于检测是否被修改
  name: '',
  category: '',
  sourceType: '' as string | null,
  remark: '',
})

// 编辑弹窗字段控制
const isExternalSourceItem = computed(() => {
  return editFormData.sourceType != null && editFormData.sourceType !== ''
})
const isUsedByVoucher = ref(false)

// 外部数据源选择弹窗
const pickerVisible = ref(false)
const pickerLoading = ref(false)
const pickerTitle = ref('')
const pickerData = ref<any[]>([])
const filteredPickerData = ref<any[]>([])
const pickerSearchKeyword = ref('')
const pickerSelectedKeys = ref<any[]>([])
const pickerColumns = ref<any[]>([])
const pickerRowKey = ref('id')

// 关联科目弹窗相关
const accountDialogVisible = ref(false)
const activeAccountTab = ref('asset')
const isAccountListExpanded = ref(true)
const selectedAccountTags = ref<AccountItem[]>([])
const accountTreeData = ref<AccountTreeNode[]>([])
const checkedAccountKeys = ref<string[]>([])

// 阿米巴弹窗相关
const amoebaDialogVisible = ref(false)
const amoebaDefaultCategory = ref('department')

// 反悔模式弹窗相关
const regretDialogVisible = ref(false)
const regretType = ref('code')
const regretCategory = ref('')
const regretObject = ref('')
const isBatchRegret = ref(false)
const originalCode = ref('')
const newCode = ref('')

// 新增自定义类别弹窗
const addCategoryDialogVisible = ref(false)
const newCategoryName = ref('')

// 关联科目数据
const relatedAccounts = ref<AccountItem[]>([])
const relatedAccountsLoading = ref(false)

// 当前类别的项目（用于反悔模式，从已加载的 tableData 中过滤）
const currentCategoryItems = computed(() => {
  return tableData.value.filter(item => item.category === regretCategory.value)
})

// 外部类型提示信息
const externalTypeMessage = computed(() => {
  switch (currentCategory.value) {
    case 'customer': return '客户数据从【客户管理】同步，可点击“从客户添加”选择导入'
    case 'supplier': return '供应商数据从【供应商管理】同步，可点击“从供应商添加”选择导入'
    case 'employee': return '员工数据从【花名册】同步，可点击“从员工添加”选择导入'
    case 'department': return '部门数据从【组织架构】同步，可点击“从部门添加”选择导入'
    case 'express_brand': return '品牌数据从【品牌库】同步，可点击“从品牌库添加”选择导入'
    case 'outlet': return '网点数据从【快递网点管理】同步，可点击“从网点添加”选择导入'
    default: return ''
  }
})

// 过滤后的表格数据
const filteredTableData = computed(() => {
  let data = tableData.value
  if (debouncedSearchKeyword.value) {
    const keyword = debouncedSearchKeyword.value.toLowerCase()
    data = data.filter(item =>
      item.name.toLowerCase().includes(keyword) ||
      item.code.toLowerCase().includes(keyword)
    )
  }
  return data
})

// 过滤后的科目树（按分类）
const filteredAccountTree = computed(() => {
  const categoryMap: Record<string, string[]> = {
    asset: ['1001', '1002', '1012', '1101', '1121', '1122', '1123', '1131', '1132', '1221', '1401'],
    liability: ['2001', '2201', '2202', '2203', '2211', '2221', '2241'],
    equity: ['3001', '3101', '3103', '3104'],
    cost: ['4001', '4101', '4201', '4301'],
    profit: ['5001', '5051', '5111', '5301', '5401', '5402', '5601', '5602', '5603'],
  }
  const prefixes = categoryMap[activeAccountTab.value] || []

  const filterTree = (nodes: AccountTreeNode[]): AccountTreeNode[] => {
    const result: AccountTreeNode[] = []
    for (const node of nodes) {
      const match = prefixes.some(p => node.code.startsWith(p))
      if (node.children && node.children.length > 0) {
        const filteredChildren = filterTree(node.children)
        if (match || filteredChildren.length > 0) {
          result.push({ ...node, children: filteredChildren })
        }
      } else if (match) {
        result.push({ ...node })
      }
    }
    return result
  }

  return filterTree(accountTreeData.value)
})

// 获取类别标签
function getCategoryLabel(value: string): string {
  const cat = categories.value.find(c => c.value === value)
  return cat?.label || value
}

// 在树中查找节点
function findNodeInTree(nodes: AccountTreeNode[], code: string): AccountTreeNode | undefined {
  for (const n of nodes) {
    if (n.code === code) return n
    if (n.children) {
      const found = findNodeInTree(n.children, code)
      if (found) return found
    }
  }
  return undefined
}

// 加载数据
async function loadData() {
  loading.value = true
  try {
    // 所有类型统一从财务辅助核算表查询
    const accountSetId = accountSetStore.getCurrentAccountSetId()
    const items = await getAuxiliaryItemsByAccountSet({
      accountSetId,
      auxType: currentCategory.value,
      keyword: searchKeyword.value || undefined,
    })
    tableData.value = items.map((i: any) => ({
      id: i.id,
      code: i.code,
      name: i.name,
      enabled: i.enableStatus === 1,
      category: i.auxType || currentCategory.value,
      sourceType: i.sourceType || null,
    }))
    total.value = tableData.value.length
    if (tableData.value.length > 0) {
      currentRow.value = tableData.value[0]
    } else {
      currentRow.value = null
    }
  } catch {
    tableData.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

// 加载关联科目（根据当前辅助核算类别）
async function loadRelatedAccounts() {
  relatedAccountsLoading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId()
    const accounts = await getAccountsByAuxType({
      auxType: currentCategory.value,
      accountSetId,
    })
    relatedAccounts.value = accounts.map((a: any) => ({
      code: a.code,
      name: a.name,
    }))
  } catch {
    // 不影响主界面，保持空列表
    relatedAccounts.value = []
  } finally {
    relatedAccountsLoading.value = false
  }
}

// 加载科目树数据
async function loadAccountTree() {
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId()
    const data = await getAccountTree(undefined, accountSetId)
    accountTreeData.value = formatAccountTree(data)
  } catch {
    // 使用模拟数据
    accountTreeData.value = getMockAccountTree()
  }
}

// 格式化科目树
function formatAccountTree(data: any[]): AccountTreeNode[] {
  return data.map(item => ({
    code: item.code,
    name: item.name,
    label: `${item.code} ${item.name}`,
    children: item.children ? formatAccountTree(item.children) : undefined,
  }))
}

// 模拟科目树数据
function getMockAccountTree(): AccountTreeNode[] {
  return [
    { code: '1001', name: '库存现金', label: '1001 库存现金' },
    { code: '1002', name: '银行存款', label: '1002 银行存款', children: [
      { code: '100201', name: '基本户', label: '100201 基本户' },
      { code: '100202', name: '一般户', label: '100202 一般户' },
    ]},
    { code: '1012', name: '其他货币资金', label: '1012 其他货币资金' },
    { code: '1101', name: '短期投资', label: '1101 短期投资', children: [
      { code: '110101', name: '股票投资', label: '110101 股票投资' },
      { code: '110102', name: '债券投资', label: '110102 债券投资' },
    ]},
    { code: '1121', name: '应收票据', label: '1121 应收票据' },
    { code: '1122', name: '应收账款', label: '1122 应收账款', children: [
      { code: '112201', name: '应收国内账款', label: '112201 应收国内账款' },
      { code: '112202', name: '网点公司', label: '112202 网点公司' },
      { code: '112210', name: '其他账款', label: '112210 其他账款' },
    ]},
    { code: '1123', name: '预付账款', label: '1123 预付账款' },
    { code: '1131', name: '应收股利', label: '1131 应收股利' },
    { code: '1132', name: '应收利息', label: '1132 应收利息' },
    { code: '1221', name: '其他应收款', label: '1221 其他应收款', children: [
      { code: '122101', name: '员工借款', label: '122101 员工借款' },
      { code: '122102', name: '借款', label: '122102 借款' },
    ]},
    { code: '1401', name: '材料采购', label: '1401 材料采购' },
    { code: '2001', name: '短期借款', label: '2001 短期借款' },
    { code: '2201', name: '应付票据', label: '2201 应付票据' },
    { code: '2202', name: '应付账款', label: '2202 应付账款' },
    { code: '2203', name: '预收账款', label: '2203 预收账款' },
    { code: '220203', name: '其他应付账款', label: '220203 其他应付账款' },
    { code: '2211', name: '应付职工薪酬', label: '2211 应付职工薪酬' },
    { code: '2221', name: '应交税费', label: '2221 应交税费' },
    { code: '2241', name: '其他应付款', label: '2241 其他应付款', children: [
      { code: '22410401', name: '打印机押金', label: '22410401 打印机押金' },
      { code: '22410402', name: '客户押金', label: '22410402 客户押金' },
      { code: '224105', name: '客户返款', label: '224105 客户返款' },
    ]},
    { code: '5001', name: '主营业务收入', label: '5001 主营业务收入' },
    { code: '5401', name: '主营业务成本', label: '5401 主营业务成本' },
    { code: '5601', name: '销售费用', label: '5601 销售费用', children: [
      { code: '56012301', name: '客户返款', label: '56012301 客户返款' },
    ]},
  ]
}

// 切换分类
function handleCategoryChange(category: string) {
  currentCategory.value = category
  currentPage.value = 1
  searchKeyword.value = ''
  loadData()
  loadRelatedAccounts()
}

// 当前行变化
function handleCurrentChange(row: AuxiliaryItem) {
  currentRow.value = row
}

// 状态切换
function handleStatusChange(_row: any, val: any) {
  message.success(val ? '已启用' : '已禁用')
}

// 预置类别 value 集合
const _presetCategoryValues: string[] = ['customer', 'supplier', 'employee', 'project', 'department', 'business_unit', 'express_brand', 'outlet', 'business_direction']
void _presetCategoryValues

// 自动编码类型：这些类型的编码由后端自动生成或来自数据源
const autoCodeTypes = ['customer', 'supplier', 'employee', 'department', 'express_brand', 'outlet', 'project', 'business_unit', 'business_direction']

// 当前类型是否需要手动输入编码（不在预设类型中的就是自定义类别）
const isManualCodeType = computed(() => {
  return !autoCodeTypes.includes(currentCategory.value)
})

// 新增辅助核算（顶部新增按钮）
function handleAdd() {
  addAuxFormData.code = ''
  addAuxFormData.name = ''
  addAuxFormData.selectedCategories = []
  addAuxCategorySelectVal.value = ''
  // 预设当前类别
  const currentCat = [...categories.value, ...customCategories.value].find(c => c.value === currentCategory.value)
  if (currentCat) {
    addAuxFormData.selectedCategories = [currentCat]
  }
  addAuxDialogVisible.value = true
}

// 移除辅助核算新增弹窗中的类别Tag
function handleRemoveAuxCategory(value: string) {
  addAuxFormData.selectedCategories = addAuxFormData.selectedCategories.filter(c => c.value !== value)
}

// 选择类别后添加Tag
function handleAuxCategorySelect(value: any) {
  const opt = [...categories.value, ...customCategories.value].find(c => c.value === value)
  if (opt && !addAuxFormData.selectedCategories.some(s => s.value === value)) {
    addAuxFormData.selectedCategories.push(opt)
  }
  addAuxCategorySelectVal.value = ''
}

// 保存并关闭（新增辅助核算弹窗）
function handleAuxSaveAndClose() {
  addAuxFormRef.value?.validate().then(async () => {
    const selectedCats = addAuxFormData.selectedCategories.length > 0
      ? addAuxFormData.selectedCategories
      : [{ value: currentCategory.value, label: getCategoryLabel(currentCategory.value) }]
    try {
      const accountSetId = accountSetStore.getCurrentAccountSetId()
      for (const cat of selectedCats) {
        await createAuxiliaryItem({
          accountSetId,
          auxType: cat.value,
          name: addAuxFormData.name,
          code: isManualCodeType.value ? addAuxFormData.code : '',
        })
      }
      message.success('创建成功')
      addAuxDialogVisible.value = false
      loadData()
    } catch (err: any) {
      message.error(err?.message || '创建失败')
    }
  }).catch((err: any) => { console.warn('[AuxiliarySetting] 操作失败:', err?.message) })
}

// 保存并新增（新增辅助核算弹窗）—— 清空编码和名称，保留类别
function handleAuxSaveAndAdd() {
  addAuxFormRef.value?.validate().then(async () => {
    const selectedCats = addAuxFormData.selectedCategories.length > 0
      ? addAuxFormData.selectedCategories
      : [{ value: currentCategory.value, label: getCategoryLabel(currentCategory.value) }]
    try {
      const accountSetId = accountSetStore.getCurrentAccountSetId()
      for (const cat of selectedCats) {
        await createAuxiliaryItem({
          accountSetId,
          auxType: cat.value,
          name: addAuxFormData.name,
          code: isManualCodeType.value ? addAuxFormData.code : '',
        })
      }
      message.success('创建成功')
      addAuxFormData.name = ''
      addAuxCategorySelectVal.value = ''
      loadData()
    } catch (err: any) {
      message.error(err?.message || '创建失败')
    }
  }).catch((err: any) => { console.warn('[AuxiliarySetting] 操作失败:', err?.message) })
}

// 编辑
async function handleEdit(row: any) {
  editFormData.id = row.id
  editFormData.code = row.code
  editFormData.originalCode = row.code
  editFormData.name = row.name
  editFormData.category = row.category || currentCategory.value
  editFormData.sourceType = row.sourceType || null
  editFormData.remark = row.remark || ''
  // 检查是否被凭证使用
  try {
    const res = await checkAuxiliaryItemUsage(row.id)
    isUsedByVoucher.value = res?.data || res || false
  } catch {
    isUsedByVoucher.value = false
  }
  editDialogVisible.value = true
}

// 删除
async function handleDelete() {
  if (selectedRows.value.length === 0) {
    message.warning('请选择要删除的数据')
    return
  }
  // 检查所有选中行是否被使用
  for (const row of selectedRows.value) {
    try {
      const res = await checkAuxiliaryItemUsage(row.id)
      const used = res?.data || res || false
      if (used) {
        message.error(`“${row.name}” 已被凭证使用，无法删除`)
        return
      }
    } catch { /* ignore */ }
  }
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除选中的 ${selectedRows.value.length} 条数据吗？`,
    async onOk() {
      try {
        for (const row of selectedRows.value) {
          await deleteAuxiliaryItem(row.id)
        }
        message.success('删除成功')
        selectedRows.value = []
        loadData()
      } catch (err: any) {
        message.error(err?.message || '删除失败')
      }
    },
  })
}

// 保存并关闭
function handleSaveAndClose() {
  addFormRef.value?.validate().then(async () => {
    try {
      const accountSetId = accountSetStore.getCurrentAccountSetId()
      const category = addFormData.categoryReadonly ? addFormData.category : addFormData.categoryText
      await createAuxiliaryItem({
        accountSetId,
        auxType: category,
        name: addFormData.name,
        code: isManualCodeType.value ? addFormData.code : '',
      })
      message.success('创建成功')
      if (!addFormData.categoryReadonly) {
        const exists = customCategories.value.some(c => c.label === category || c.value === category)
        if (!exists) {
          const newValue = `custom_${Date.now()}`
          customCategories.value.push({ label: category, value: newValue })
        }
      }
      addDialogVisible.value = false
      loadData()
    } catch (err: any) {
      message.error(err?.message || '创建失败')
    }
  }).catch((err: any) => { console.warn('[AuxiliarySetting] 操作失败:', err?.message) })
}

// 保存并新增
function handleSaveAndAdd() {
  addFormRef.value?.validate().then(async () => {
    try {
      const accountSetId = accountSetStore.getCurrentAccountSetId()
      const category = addFormData.categoryReadonly ? addFormData.category : addFormData.categoryText
      await createAuxiliaryItem({
        accountSetId,
        auxType: category,
        name: addFormData.name,
        code: isManualCodeType.value ? addFormData.code : '',
      })
      message.success('创建成功')
      if (!addFormData.categoryReadonly) {
        const exists = customCategories.value.some(c => c.label === category || c.value === category)
        if (!exists) {
          const newValue = `custom_${Date.now()}`
          customCategories.value.push({ label: category, value: newValue })
        }
      }
      addFormData.name = ''
      loadData()
    } catch (err: any) {
      message.error(err?.message || '创建失败')
    }
  }).catch((err: any) => { console.warn('[AuxiliarySetting] 操作失败:', err?.message) })
}

// 编辑提交
function handleEditSubmit() {
  editFormRef.value?.validate().then(async () => {
    try {
      const accountSetId = accountSetStore.getCurrentAccountSetId()
      await updateAuxiliaryItem(editFormData.id, {
        accountSetId,
        auxType: editFormData.category,
        name: editFormData.name,
        remark: editFormData.remark,
      })
      message.success('更新成功')
      editDialogVisible.value = false
      loadData()
    } catch (err: any) {
      message.error(err?.message || '更新失败')
    }
  }).catch((err: any) => { console.warn('[AuxiliarySetting] 操作失败:', err?.message) })
}

// 移除类别标签
function _handleRemoveCategory() { // eslint-disable-line @typescript-eslint/no-unused-vars
  addFormData.category = ''
}
void _handleRemoveCategory

// 阿米巴模式
function handleAmoebaMode() {
  amoebaDefaultCategory.value = currentCategory.value
  amoebaDialogVisible.value = true
}

// 反悔模式
function handleRegretMode() {
  regretCategory.value = currentCategory.value
  regretDialogVisible.value = true
}

// 阿米巴提交
function handleAmoebaSubmit() {
  message.success('阿米巴功能开通成功')
  amoebaDialogVisible.value = false
}

// 更多操作
function handleMoreCommand(command: string | number) {
  switch (command) {
    case 'import':
      message.info('导入功能开发中')
      break
    case 'export':
      message.info('导出功能开发中')
      break
    case 'regret':
      regretCategory.value = currentCategory.value
      regretDialogVisible.value = true
      break
  }
}

// 更多菜单点击适配
function onMoreMenuClick(info: { key: string | number }) {
  handleMoreCommand(info.key)
}

// 自定义类别操作
function handleCustomCategoryCommand(command: string | number) {
  if (command === 'add') {
    addFormData.code = ''
    addFormData.name = ''
    addFormData.category = ''
    addFormData.categoryText = ''
    addFormData.categoryReadonly = false
    addDialogIsCustom.value = true
    addDialogVisible.value = true
  } else {
    customCategory.value = String(command)
    const cat = customCategories.value.find(c => c.value === command)
    if (cat) {
      message.info(`切换到类别: ${cat.label}`)
    }
  }
}

// 自定义类别菜单点击适配
function onCustomCategoryMenuClick(info: { key: string | number }) {
  handleCustomCategoryCommand(info.key)
}

// 新增自定义类别提交
function handleAddCategorySubmit() {
  if (!newCategoryName.value.trim()) {
    message.warning('请输入类别名称')
    return
  }
  const newValue = `custom${Date.now()}`
  customCategories.value.push({
    label: newCategoryName.value,
    value: newValue,
  })
  customCategory.value = newValue
  message.success('自定义类别添加成功')
  addCategoryDialogVisible.value = false
}

// 编辑关联科目
async function handleEditAccounts() {
  if (!currentRow.value) {
    message.warning('请先选择辅助项目')
    return
  }
  selectedAccountTags.value = [...relatedAccounts.value]
  checkedAccountKeys.value = relatedAccounts.value.map(a => a.code)
  await loadAccountTree()
  accountDialogVisible.value = true
}

// 科目选择变化
function handleAccountCheck(keys: any) {
  checkedAccountKeys.value = Array.isArray(keys) ? keys : keys.checked || []
  selectedAccountTags.value = keys.map((code: any) => {
    const existing = selectedAccountTags.value.find((a: any) => a.code === code)
    if (existing) return existing
    const node = findNodeInTree(accountTreeData.value, code)
    return { code, name: node?.name || '' }
  }).filter((a: any) => a.name !== '')
}

// 移除已选科目
function removeSelectedAccount(account: AccountItem) {
  selectedAccountTags.value = selectedAccountTags.value.filter(a => a.code !== account.code)
  checkedAccountKeys.value = checkedAccountKeys.value.filter(k => k !== account.code)
}

// 关联科目提交
async function handleAccountSubmit() {
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId()
    const accountCodes = selectedAccountTags.value.map(a => a.code)
    await updateAccountAuxiliary({
      accountSetId,
      auxType: currentCategory.value,
      accountCodes,
    })
    message.success('关联科目保存成功')
    accountDialogVisible.value = false
    loadRelatedAccounts()
  } catch (err: any) {
    message.error(err?.message || '关联科目保存失败')
  }
}

// 页码变化
function handlePageChange(page: number) {
  currentPage.value = page
}

// ========== 外部数据源选择弹窗 ==========

async function openExternalPicker() {
  pickerSearchKeyword.value = ''
  pickerSelectedKeys.value = []
  pickerLoading.value = false
  pickerRowKey.value = 'id'

  const accountSetId = accountSetStore.getCurrentAccountSetId()
  if (!accountSetId) {
    message.warning('请先选择账套')
    return
  }

  try {
    let data: any[] = []
    const type = currentCategory.value

    if (type === 'customer') {
      pickerTitle.value = '选择客户'
      pickerColumns.value = [
        { title: '客户编号', dataIndex: 'customerCode', width: 120 },
        { title: '名称', dataIndex: 'name' },
        { title: '联系人', dataIndex: 'contact', width: 100 },
        { title: '电话', dataIndex: 'phone', width: 130 },
      ]
      const res = await getAvailableCustomers(accountSetId)
      data = res?.data || res || []
    } else if (type === 'supplier') {
      pickerTitle.value = '选择供应商'
      pickerColumns.value = [
        { title: '编码', dataIndex: 'code', width: 120 },
        { title: '名称', dataIndex: 'name' },
        { title: '简称', dataIndex: 'shortName', width: 100 },
        { title: '联系人', dataIndex: 'contact', width: 100 },
      ]
      const res = await getAvailableSuppliers(accountSetId)
      data = res?.data || res || []
    } else if (type === 'employee') {
      pickerTitle.value = '选择员工'
      pickerColumns.value = [
        { title: '工号', dataIndex: 'account', width: 120 },
        { title: '姓名', dataIndex: 'name' },
        { title: '手机号', dataIndex: 'phone', width: 130 },
      ]
      const res = await getAvailableEmployees(accountSetId)
      data = res?.data || res || []
      // 员工数据用 userId 作为主键，统一映射为 id 以匹配 rowKey="id"
      data = data.map((item: any) => ({ ...item, id: item.userId }))
    } else if (type === 'department') {
      pickerTitle.value = '选择部门'
      pickerColumns.value = [
        { title: '部门编码', dataIndex: 'code', width: 120 },
        { title: '部门名称', dataIndex: 'name' },
        { title: '状态', dataIndex: 'status', width: 80 },
      ]
      const res = await getAvailableDepartments(accountSetId)
      data = res?.data || res || []
    } else if (type === 'express_brand') {
      pickerTitle.value = '选择快递品牌'
      pickerColumns.value = [
        { title: '品牌编码', dataIndex: 'code', width: 120 },
        { title: '品牌名称', dataIndex: 'name' },
      ]
      const res = await getAvailableBrands(accountSetId)
      data = res?.data || res || []
    } else if (type === 'outlet') {
      pickerTitle.value = '选择快递网点'
      pickerRowKey.value = 'code'
      pickerColumns.value = [
        { title: '网点编码', dataIndex: 'code', width: 120 },
        { title: '网点名称', dataIndex: 'name' },
        { title: '地址', dataIndex: 'address' },
        { title: '负责人', dataIndex: 'manager', width: 100 },
        { title: '电话', dataIndex: 'phone', width: 130 },
      ]
      const res = await getAvailableNetworkPoints(accountSetId)
      data = res?.data || res || []
    }

    if (!Array.isArray(data)) data = []
    pickerData.value = data
    filteredPickerData.value = data
    pickerVisible.value = true
  } catch (err: any) {
    const msg = err?.response?.data?.message || err?.message || '获取数据失败'
    message.error(msg)
  }
}

function filterPickerData() {
  const keyword = pickerSearchKeyword.value.toLowerCase()
  if (!keyword) {
    filteredPickerData.value = pickerData.value
    return
  }
  filteredPickerData.value = pickerData.value.filter((item: any) => {
    return Object.values(item).some(val =>
      val && String(val).toLowerCase().includes(keyword)
    )
  })
}

function onPickerSelectChange(keys: any[]) {
  pickerSelectedKeys.value = keys
}

async function handlePickerConfirm() {
  if (pickerSelectedKeys.value.length === 0) {
    message.warning('请至少选择一项')
    return
  }

  pickerLoading.value = true
  try {
    const accountSetId = accountSetStore.getCurrentAccountSetId()
    const type = currentCategory.value

    if (type === 'customer') {
      await addFromCustomer({ customerIds: pickerSelectedKeys.value.map(Number), accountSetId })
    } else if (type === 'supplier') {
      await addFromSupplier({ supplierIds: pickerSelectedKeys.value.map(Number), accountSetId })
    } else if (type === 'employee') {
      await addFromUser({ userIds: pickerSelectedKeys.value.map(Number), accountSetId })
    } else if (type === 'department') {
      await addFromOrg({ orgIds: pickerSelectedKeys.value.map(Number), accountSetId })
    } else if (type === 'express_brand') {
      await addFromBrand({ brandIds: pickerSelectedKeys.value.map(Number), accountSetId })
    } else if (type === 'outlet') {
      await addFromNetworkPoint({ networkPointCodes: pickerSelectedKeys.value, accountSetId })
    }

    message.success('添加成功')
    pickerVisible.value = false
    await loadData()
  } catch (err: any) {
    message.error(err?.response?.data?.message || err?.message || '添加失败')
  } finally {
    pickerLoading.value = false
  }
}

// 监听账套切换，重新加载数据
watch(
  () => accountSetStore.currentAccountSetId,
  () => {
    currentPage.value = 1
    searchKeyword.value = ''
    selectedRows.value = []
    loadData()
    loadRelatedAccounts()
  }
)

onMounted(() => {
  loadData()
  loadRelatedAccounts()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.toolbar-section {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  background: #fff;
  flex-shrink: 0;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

// 分类按钮组 - Ant Design radio-group，选中态中性化（中性底 + 深字）
.category-radio-group {
  :deep(.ant-radio-button-wrapper) {
    font-size: 14px;

    &:hover {
      color: var(--color-primary);
    }
  }

  :deep(.ant-radio-button-wrapper-checked) {
    background-color: var(--bg-muted) !important;
    border-color: var(--border-strong) !important;
    color: var(--text-1) !important;
    font-weight: 600;

    &:hover {
      background-color: var(--bg-muted) !important;
      border-color: var(--border-strong) !important;
      color: var(--text-1) !important;
    }

    &::before {
      background-color: var(--border-strong) !important;
    }
  }
}

.search-input {
  width: 180px;
}

// 覆盖全局 page-container，使其填满父容器高度并以列方向排列
.page-container {
  height: 100%;
  padding: 0; // 覆盖全局的 padding: 20px，避免高度计算错误
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.main-content {
  display: flex;
  flex: 1;
  overflow: hidden;
  gap: 0;
  min-height: 0;
}

// 左侧内容区
.left-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-width: 0;
  border-right: 1px solid #e4e7ed;
}
// 表格容器
.table-card {
  flex: 1;
  overflow: hidden;
  min-height: 0;
  display: flex;
  flex-direction: column;

  :deep(.ant-table-wrapper) {
    flex: 1;
    overflow: hidden;
  }
  :deep(.ant-table) {
    height: 100%;
  }
  :deep(.ant-table-container) {
    height: 100%;
    display: flex;
    flex-direction: column;
  }
  :deep(.ant-table-body) {
    flex: 1;
    overflow-y: auto !important;
  }
}

// 名称链接样式 - 对齐设计图：文字默认色 + 下划线
.name-link {
  color: #303133;
  cursor: pointer;
  text-decoration: underline;
  text-decoration-color: #909399;
  text-underline-offset: 2px;

  &:hover {
    color: var(--color-warning);
    text-decoration-color: var(--color-warning);
  }
}

// 分页容器
.pagination-container {
  padding: 6px $spacing-md;
  flex-shrink: 0;
  display: flex;
  justify-content: center;
  border-top: 1px solid #e4e7ed;
}

// 右侧关联科目面板
.right-panel {
  width: 300px;
  background-color: #fff;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  overflow: hidden;

  :deep(.ant-spin-nested-loading) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }
  :deep(.ant-spin-container) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: $spacing-sm;
  padding: 0 $spacing-md;
  height: 39px;
  border-bottom: 1px solid #e4e7ed;
  flex-shrink: 0;
  background-color: #fafafa;

  .panel-title {
    font-size: 15px;
    font-weight: 600;
    color: #303133;
  }

  .edit-icon {
    font-size: 15px;
    color: #909399;
    cursor: pointer;

    &:hover {
      color: var(--color-primary);
    }
  }
}

// 提示信息区（蓝色垂线样式）
.panel-tips {
  padding: 10px 12px;
  flex-shrink: 0;
  background-color: #fff;

  .tip-line {
    display: flex;
    align-items: flex-start;
    gap: 8px;
    margin-bottom: 6px;

    &:last-child {
      margin-bottom: 0;
    }

    .tip-bar {
      flex-shrink: 0;
      width: 3px;
      min-height: 16px;
      margin-top: 2px;
      background-color: var(--color-info);
      border-radius: 2px;
      align-self: stretch;
    }

    .tip-text {
      font-size: 12px;
      color: #909399;
      line-height: 1.6;
    }
  }
}

// 关联科目列表
.account-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;

  .no-related-accounts {
    color: #909399;
    font-size: 13px;
    text-align: center;
    padding: $spacing-md 0;
  }

  .account-item {
    display: flex;
    gap: 16px;
    padding: 7px 12px;
    font-size: 14px;

    &:hover {
      background-color: #f5f7fa;
    }

    .account-code {
      width: 80px;
      color: #606266;
      font-family: 'Courier New', monospace;
      flex-shrink: 0;
      font-size: 13px;
    }

    .account-name {
      color: #303133;
      flex: 1;
      font-size: 13px;
    }
  }
}

// 类别标签
.category-tag {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
}

// 关联科目弹窗样式
.account-dialog-content {
  .selected-accounts {
    display: flex;
    gap: $spacing-sm;
    margin-bottom: $spacing-md;

    .label {
      flex-shrink: 0;
      line-height: 32px;
    }

    .tags-container {
      display: flex;
      flex-wrap: wrap;
      gap: $spacing-sm;
    }

    .account-tag-item {
      margin-right: 0;
    }
  }

  .expand-toggle {
    text-align: center;
    color: #606266;
    cursor: pointer;
    padding: $spacing-sm 0;
    border-bottom: 1px solid #e4e7ed;
    margin-bottom: $spacing-md;

    &:hover {
      color: var(--color-primary);
    }
  }

  .account-tabs-container {
    .account-tabs {
      :deep(.ant-tabs-nav) {
        margin-bottom: $spacing-md;
      }
    }

    .account-tree-container {
      height: 300px;
      overflow: auto;
      border: 1px solid #e4e7ed;
      padding: $spacing-md;
      border-radius: $border-radius-md;
    }
  }
}

// 反悔模式样式
.regret-info-section {
  background-color: #f5f7fa;
  padding: $spacing-md $spacing-md;
  margin: $spacing-md 0;
  border-radius: $border-radius-md;

  .regret-info-title {
    color: #606266;
    font-size: 14px;
  }
}

.disabled-input {
  :deep(.ant-input) {
    background-color: #f5f7fa;
  }
}

// 弹窗底部
.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: $spacing-sm;
}

// 新增弹窗表单
.add-dialog-form {
  padding: 0 8px;
}

// 新增弹窗提示信息
.add-dialog-tips {
  margin: 12px 8px 0;

  .add-tip-item {
    display: flex;
    align-items: flex-start;
    background-color: #eef4ff;
    border-left: 3px solid var(--color-info);
    color: var(--color-info);
    font-size: 13px;
    line-height: 1.6;
    padding: 6px 12px;
    margin-bottom: 6px;

    &:last-child {
      margin-bottom: 0;
    }
  }
}

// 类别只读样式
.category-readonly {
  :deep(.ant-input) {
    background-color: #f5f7fa;
    cursor: not-allowed;
    color: #909399;
  }
}

// 橙色 switch
:deep(.ant-switch-checked) {
  background-color: var(--color-warning) !important;
}

// 选中行高亮
:deep(.ant-table-row.current-row > td) {
  background-color: var(--bg-muted) !important;
}

// 表格行高
:deep(.ant-table-row td) {
  height: 32px;
}

// 表头样式
:deep(.ant-table-thead > tr > th) {
  background-color: #f5f7fa;
  color: #606266;
  font-weight: 600;
  font-size: 13px;
  padding-top: 8px;
  padding-bottom: 8px;
  height: 39px;
  box-sizing: border-box;
}

// 类Tag选择器
.category-tag-selector {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  min-height: 32px;
  padding: 4px 8px;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  background-color: #fff;
  width: 100%;
  box-sizing: border-box;
  cursor: pointer;
  transition: border-color 0.2s;

  &:hover {
    border-color: #c0c4cc;
  }

  .selected-cat-tag {
    margin: 0;
  }

  .cat-inline-select {
    border: none;
    flex-shrink: 0;
    min-width: 90px;

    :deep(.ant-select-selector) {
      border: none;
      box-shadow: none !important;
      background: transparent;
      padding: 0 4px;
      min-height: 22px;
    }

    :deep(.ant-select-selection-placeholder) {
      color: #c0c4cc;
      font-size: 13px;
    }
  }
}

// 编码只读样式
.code-readonly {
  :deep(.ant-input) {
    background-color: #f5f7fa;
    cursor: not-allowed;
    color: #909399;
  }
}

// 提示信息（对话框内用）
.info-tip {
  background-color: #f4f7fe;
  border-left: 3px solid var(--color-info);
  padding: $spacing-md;
  margin-bottom: $spacing-md;
  font-size: 13px;
  color: #666;
  line-height: 1.8;

  &.dialog-tip {
    margin-top: $spacing-md;
    margin-bottom: 0;
  }

  p {
    margin: 0;

    &:not(:last-child) {
      margin-bottom: $spacing-sm;
    }
  }
}
</style>
