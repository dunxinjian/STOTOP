<template>
  <div class="client-panel">
    <!-- 搜索和筛选区 -->
    <div class="filter-area">
      <a-input-search
        v-model:value="keyword"
        placeholder="搜索名称/编号"
        size="small"
        allow-clear
        @search="handleSearch"
      />
      <a-select
        v-model:value="filterType"
        size="small"
        class="type-filter"
        @change="handleSearch"
      >
        <a-select-option value="">全部</a-select-option>
        <a-select-option value="KH">客户</a-select-option>
        <a-select-option value="DL">代理</a-select-option>
        <a-select-option value="WD">网点</a-select-option>
        <a-select-option value="CB">承包区</a-select-option>
        <a-select-option value="YZ">驿站</a-select-option>
        <a-select-option value="YW">业务员</a-select-option>
      </a-select>
    </div>

    <!-- 业务对象列表 -->
    <div class="client-list" ref="listRef" @scroll="handleScroll">
      <div
        v-for="item in list"
        :key="item.id"
        class="client-item"
        :class="{ active: item.id === selectedId && item.type === selectedType }"
        @click="handleSelect(item)"
      >
        <div class="item-main">
          <div class="item-name">{{ item.name }}</div>
          <div class="item-meta">
            <span class="item-code">{{ item.code }}</span>
            <a-tag :color="typeColorMap[item.type]" size="small" class="item-type-tag">
              {{ typeLabelMap[item.type] || item.type }}
            </a-tag>
          </div>
        </div>
        <a-badge
          v-if="item.quotationCount > 0"
          :count="item.quotationCount"
          :number-style="{ backgroundColor: 'var(--color-info)' }"
        />
      </div>
      <div v-if="loading" class="list-loading">
        <a-spin size="small" />
      </div>
      <div v-if="!loading && list.length === 0" class="list-empty">
        暂无数据
      </div>
    </div>

    <!-- 新建业务对象按钮 -->
    <div class="panel-footer">
      <a-button type="dashed" block @click="showCreateModal = true">
        + 新建业务对象
      </a-button>
    </div>

    <!-- 新建业务对象 Modal -->
    <a-modal
      v-model:open="showCreateModal"
      title="新建业务对象"
      :confirm-loading="creating"
      @ok="handleCreate"
      @cancel="resetCreateForm"
      :width="520"
    >
      <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
        <a-form-item label="对象类型" required>
          <a-select v-model:value="createForm.type" placeholder="请选择类型">
            <a-select-option value="KH">客户</a-select-option>
            <a-select-option value="DL">代理</a-select-option>
            <a-select-option value="WD">网点</a-select-option>
            <a-select-option value="CB">承包区</a-select-option>
            <a-select-option value="YZ">驿站</a-select-option>
          </a-select>
        </a-form-item>

        <!-- KH 客户 -->
        <template v-if="createForm.type === 'KH'">
          <a-form-item label="编号">
            <a-input v-model:value="createForm.code" placeholder="留空自动生成" />
          </a-form-item>
          <a-form-item label="简称" required>
            <a-input v-model:value="createForm.shortName" placeholder="请输入客户简称" />
          </a-form-item>
          <a-form-item label="全称">
            <a-input v-model:value="createForm.fullName" placeholder="请输入客户全称" />
          </a-form-item>
        </template>

        <!-- DL 代理 -->
        <template v-if="createForm.type === 'DL'">
          <a-form-item label="编号" required>
            <a-input v-model:value="createForm.code" placeholder="请输入代理编号" />
          </a-form-item>
          <a-form-item label="名称" required>
            <a-input v-model:value="createForm.name" placeholder="请输入代理名称" />
          </a-form-item>
          <a-form-item label="代理级别">
            <a-select v-model:value="createForm.agentLevel" placeholder="请选择">
              <a-select-option :value="1">一级</a-select-option>
              <a-select-option :value="2">二级</a-select-option>
              <a-select-option :value="3">三级</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="代理区域">
            <a-input v-model:value="createForm.agentRegion" placeholder="请输入代理区域" />
          </a-form-item>
        </template>

        <!-- WD 网点 -->
        <template v-if="createForm.type === 'WD'">
          <a-form-item label="编号" required>
            <a-input v-model:value="createForm.code" placeholder="请输入网点编号" />
          </a-form-item>
          <a-form-item label="网点简称">
            <a-input v-model:value="createForm.shortName" placeholder="请输入网点简称" />
          </a-form-item>
          <a-form-item label="网点全称">
            <a-input v-model:value="createForm.fullName" placeholder="请输入网点全称" />
          </a-form-item>
          <a-form-item label="网点级别">
            <a-select v-model:value="createForm.pointLevel" placeholder="请选择">
              <a-select-option :value="1">一级</a-select-option>
              <a-select-option :value="2">二级</a-select-option>
              <a-select-option :value="3">三级</a-select-option>
            </a-select>
          </a-form-item>
        </template>

        <!-- CB 承包区 -->
        <template v-if="createForm.type === 'CB'">
          <a-form-item label="编号" required>
            <a-input v-model:value="createForm.code" placeholder="请输入承包区编号" />
          </a-form-item>
          <a-form-item label="承包人">
            <a-input v-model:value="createForm.contractor" placeholder="请输入承包人" />
          </a-form-item>
          <a-form-item label="开始日期">
            <a-date-picker v-model:value="createForm.contractStartDate" style="width: 100%" />
          </a-form-item>
          <a-form-item label="结束日期">
            <a-date-picker v-model:value="createForm.contractEndDate" style="width: 100%" />
          </a-form-item>
          <a-form-item label="覆盖片区">
            <a-input v-model:value="createForm.coverageDistrict" placeholder="请输入覆盖片区" />
          </a-form-item>
        </template>

        <!-- YZ 驿站 -->
        <template v-if="createForm.type === 'YZ'">
          <a-form-item label="编号">
            <a-input v-model:value="createForm.code" placeholder="留空自动生成" />
          </a-form-item>
          <a-form-item label="类型" required>
            <a-select v-model:value="createForm.stationType" placeholder="请选择">
              <a-select-option :value="1">直营</a-select-option>
              <a-select-option :value="2">合作</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="名称" required>
            <a-input v-model:value="createForm.name" placeholder="请输入驿站名称" />
          </a-form-item>
          <a-form-item label="地址">
            <a-input v-model:value="createForm.address" placeholder="请输入地址" />
          </a-form-item>
        </template>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  getClientQuotationSummary,
  createAgent,
  createNetworkPoint,
  createFranchiseArea,
  createLastMileStation,
  type ClientQuotationSummaryItem,
} from '@/api/express'
import { createCustomer } from '@/api/crm'

// ==================== Props & Emits ====================

interface Props {
  selectedId?: string
  selectedType?: string
  quotationFilter?: 'all' | 'withQuotation' | 'withoutQuotation'
}

const props = withDefaults(defineProps<Props>(), {
  quotationFilter: 'all',
})

const emit = defineEmits<{
  select: [item: { id: string; name: string; code: string; type: string; quotationCount: number }]
}>()

// ==================== 常量 ====================

const typeColorMap: Record<string, string> = {
  KH: 'blue',
  DL: 'purple',
  WD: 'green',
  CB: 'orange',
  YZ: 'cyan',
  YW: 'volcano',
}

const typeLabelMap: Record<string, string> = {
  KH: '客户',
  DL: '代理',
  WD: '网点',
  CB: '承包区',
  YZ: '驿站',
  YW: '业务员',
}

// ==================== 列表状态 ====================

const keyword = ref('')
const filterType = ref('')
const rawList = ref<ClientQuotationSummaryItem[]>([])  // 原始数据
const loading = ref(false)
const pageIndex = ref(1)
const pageSize = 50
const hasMore = ref(true)
const listRef = ref<HTMLElement>()

// 后端已根据 quotationFilter 进行过滤，直接使用原始数据
const list = computed(() => rawList.value)

let debounceTimer: ReturnType<typeof setTimeout> | null = null

// ==================== 搜索加载 ====================

async function fetchList(reset = false) {
  if (reset) {
    pageIndex.value = 1
    hasMore.value = true
  }
  if (!hasMore.value && !reset) return

  loading.value = true
  try {
    const res = await getClientQuotationSummary({
      keyword: keyword.value || undefined,
      type: filterType.value || undefined,
      hasQuotation: props.quotationFilter === 'withQuotation' ? true
        : props.quotationFilter === 'withoutQuotation' ? false
        : undefined,
      pageIndex: pageIndex.value,
      pageSize,
    })
    const items = res.items || []
    if (reset) {
      rawList.value = items
    } else {
      rawList.value = [...rawList.value, ...items]
    }
    hasMore.value = items.length >= pageSize
  } catch (e) {
    console.error('获取业务对象列表失败:', e)
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  fetchList(true)
}

// 防抖搜索
watch(keyword, () => {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    fetchList(true)
  }, 300)
})

// 滚动加载更多
function handleScroll() {
  const el = listRef.value
  if (!el || loading.value || !hasMore.value) return
  if (el.scrollTop + el.clientHeight >= el.scrollHeight - 50) {
    pageIndex.value++
    fetchList(false)
  }
}

function handleSelect(item: ClientQuotationSummaryItem) {
  emit('select', item)
}

onMounted(() => {
  fetchList(true)
})

// quotationFilter 变化时刷新列表
watch(() => props.quotationFilter, () => {
  fetchList(true)
})

// ==================== 新建业务对象 ====================

const showCreateModal = ref(false)
const creating = ref(false)

interface CreateFormState {
  type: string
  code: string
  name: string
  shortName: string
  fullName: string
  agentLevel: number | undefined
  agentRegion: string
  pointLevel: number | undefined
  contractor: string
  contractStartDate: any
  contractEndDate: any
  coverageDistrict: string
  stationType: number | undefined
  address: string
}

const createForm = ref<CreateFormState>(getDefaultCreateForm())

function getDefaultCreateForm(): CreateFormState {
  return {
    type: '',
    code: '',
    name: '',
    shortName: '',
    fullName: '',
    agentLevel: undefined,
    agentRegion: '',
    pointLevel: undefined,
    contractor: '',
    contractStartDate: null,
    contractEndDate: null,
    coverageDistrict: '',
    stationType: undefined,
    address: '',
  }
}

function resetCreateForm() {
  createForm.value = getDefaultCreateForm()
}

async function handleCreate() {
  const form = createForm.value
  if (!form.type) {
    message.warning('请选择对象类型')
    return
  }

  creating.value = true
  try {
    let newId = ''
    let newName = ''
    let newCode = ''

    switch (form.type) {
      case 'KH': {
        if (!form.shortName) { message.warning('请输入客户简称'); return }
        const res: any = await createCustomer({
          shortName: form.shortName,
          fullName: form.fullName || undefined,
          code: form.code || undefined,
        })
        newId = String(res.id || res.fId)
        newName = form.shortName
        newCode = res.code || res.customerCode || form.code || ''
        break
      }
      case 'DL': {
        if (!form.code || !form.name) { message.warning('请填写编号和名称'); return }
        const res = await createAgent({
          code: form.code,
          name: form.name,
          agentLevel: form.agentLevel || 1,
          agentRegion: form.agentRegion || undefined,
        })
        newId = String(res.id)
        newName = form.name
        newCode = form.code
        break
      }
      case 'WD': {
        if (!form.code) { message.warning('请填写网点编号'); return }
        const res = await createNetworkPoint({
          code: form.code,
          shortName: form.shortName || undefined,
          orgId: 0, // TODO: 需要组织ID选择
          pointLevel: form.pointLevel || 1,
        })
        newId = String(res.id)
        newName = form.shortName || form.fullName || form.code
        newCode = form.code
        break
      }
      case 'CB': {
        if (!form.code) { message.warning('请填写承包区编号'); return }
        const res = await createFranchiseArea({
          code: form.code,
          orgId: 0, // TODO: 需要组织ID选择
          contractor: form.contractor || undefined,
          contractStartDate: form.contractStartDate?.format?.('YYYY-MM-DD') || undefined,
          contractEndDate: form.contractEndDate?.format?.('YYYY-MM-DD') || undefined,
          coverageDistrict: form.coverageDistrict || undefined,
        })
        newId = String(res.id)
        newName = form.contractor || form.code
        newCode = form.code
        break
      }
      case 'YZ': {
        if (!form.name) { message.warning('请输入驿站名称'); return }
        const res = await createLastMileStation({
          stationType: form.stationType || 1,
          code: form.code || undefined,
          name: form.name,
          address: form.address || undefined,
        })
        newId = String(res.id)
        newName = form.name
        newCode = res.code || form.code || ''
        break
      }
    }

    message.success('创建成功')
    showCreateModal.value = false
    resetCreateForm()

    // 刷新列表并选中新创建的对象
    await fetchList(true)
    emit('select', {
      id: newId,
      name: newName,
      code: newCode,
      type: form.type,
      quotationCount: 0,
    })
  } catch (e: any) {
    message.error(e?.message || '创建失败')
  } finally {
    creating.value = false
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.client-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  border-right: 1px solid $border-color-lighter;
}

.filter-area {
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  border-bottom: 1px solid $border-color-lighter;

  .type-filter {
    width: 100%;
  }
}

.client-list {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.client-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  cursor: pointer;
  border-left: 3px solid transparent;
  transition: $transition-base;

  &:hover {
    background: $bg-page;
  }

  &.active {
    background: $color-primary-light;
    border-left-color: $color-primary;
  }

  .item-main {
    flex: 1;
    min-width: 0;
    overflow: hidden;
  }

  .item-name {
    font-size: $font-size-base;
    color: $text-primary;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .item-meta {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-top: 4px;
  }

  .item-code {
    font-size: $font-size-sm;
    color: $text-secondary;
  }

  .item-type-tag {
    font-size: 11px;
    line-height: 18px;
    padding: 0 4px;
    border-radius: 2px;
  }
}

.list-loading,
.list-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  color: $text-secondary;
  font-size: $font-size-sm;
}

.panel-footer {
  padding: 12px;
  border-top: 1px solid $border-color-lighter;
}
</style>
