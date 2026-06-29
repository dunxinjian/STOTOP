<template>
  <a-modal
    :open="open"
    title="暂估数据管理"
    :width="860"
    :footer="null"
    :destroy-on-close="true"
    @update:open="(v) => emit('update:open', v)"
  >
    <div class="estimate-dialog">
      <!-- 上下文提示：模板 / 组织 / 期间 -->
      <a-alert type="info" show-icon class="ctx-alert">
        <template #message>
          <span>模板：<b>{{ templateName || '—' }}</b></span>
          <a-divider type="vertical" />
          <span>期间：<b>{{ periodLabel || period }}</b></span>
          <a-divider type="vertical" />
          <span class="ctx-hint">暂估行按「科目编码 + 辅助核算」与凭证共享独占匹配，参与报表计算</span>
        </template>
      </a-alert>

      <!-- 列表 -->
      <div class="list-toolbar">
        <span class="list-title">暂估明细（{{ rows.length }} 条）</span>
        <a-button type="primary" size="small" :disabled="!canOperate" @click="openForm()">
          <template #icon><PlusOutlined /></template>
          新增暂估
        </a-button>
      </div>

      <a-table
        :data-source="rows"
        :columns="columns"
        :loading="listLoading"
        :pagination="false"
        size="small"
        row-key="id"
        class="estimate-table"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'account'">
            <div class="account-cell">
              <span class="acc-code">{{ record.accountCode || '—' }}</span>
              <span class="acc-name">{{ accountName(record.accountCode) }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'aux'">
            <template v-if="parseAuxTags(record.auxiliaryJson).length">
              <a-tag v-for="(tag, i) in parseAuxTags(record.auxiliaryJson)" :key="i" color="blue">
                {{ tag }}
              </a-tag>
            </template>
            <span v-else class="muted">—</span>
          </template>
          <template v-else-if="column.key === 'amount'">
            <span :class="['amount-cell', { negative: record.amount < 0 }]">
              {{ formatAmount(record.amount) }}
            </span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" @click="openForm(record as EstimateDataDto)">编辑</a-button>
            <a-popconfirm title="确认删除该暂估行？" ok-text="删除" cancel-text="取消" @confirm="handleDelete(record as EstimateDataDto)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
        <template #emptyText>
          <a-empty :image="simpleImage" description="暂无暂估数据" />
        </template>
      </a-table>
    </div>

    <!-- 新增 / 编辑 表单 -->
    <a-modal
      :open="formOpen"
      :title="form.id ? '编辑暂估行' : '新增暂估行'"
      :width="600"
      :confirm-loading="saving"
      :ok-text="'保存'"
      @ok="handleSave"
      @cancel="formOpen = false"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }" class="estimate-form">
        <a-form-item label="科目" required>
          <a-tree-select
            v-model:value="form.accountCode"
            :tree-data="accountTreeData"
            placeholder="选择末级科目"
            show-search
            tree-node-filter-prop="title"
            :field-names="{ label: 'title', value: 'code', children: 'children' }"
            allow-clear
            style="width: 100%"
            :dropdown-style="{ maxHeight: '360px', overflow: 'auto' }"
          />
        </a-form-item>

        <a-form-item label="金额" required>
          <a-input-number
            v-model:value="form.amount"
            :precision="2"
            :step="100"
            style="width: 100%"
            placeholder="暂估金额（可为负）"
          />
        </a-form-item>

        <a-form-item label="辅助核算">
          <div v-for="(aux, idx) in form.auxList" :key="idx" class="aux-row">
            <a-select
              v-model:value="aux.type"
              placeholder="类型"
              style="width: 130px"
              :options="auxTypeOptions"
              @change="() => onAuxTypeChange(aux)"
            />
            <a-select
              v-model:value="aux.code"
              placeholder="选择项"
              show-search
              option-filter-prop="label"
              style="flex: 1"
              :options="auxItemOptions[aux.type] || []"
              :loading="auxItemLoading[aux.type]"
              @change="(val: SelectValue) => onAuxItemChange(aux, val as string)"
            />
            <a-button type="text" danger @click="form.auxList.splice(idx, 1)">
              <template #icon><DeleteOutlined /></template>
            </a-button>
          </div>
          <a-button type="dashed" size="small" block @click="addAux">
            <template #icon><PlusOutlined /></template>
            添加辅助核算
          </a-button>
          <div class="aux-tip">辅助核算用于与凭证分录精确匹配，可留空（仅按科目编码匹配）</div>
        </a-form-item>
      </a-form>
    </a-modal>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { message, Empty } from 'ant-design-vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import {
  getAccountTree,
  getAuxiliaryTypes,
  getAuxiliaryItemsByAccountSet,
  getEstimateData,
  saveEstimateData,
  deleteEstimateData,
  type EstimateDataDto,
} from '@/api/finance'

const props = defineProps<{
  open: boolean
  templateId: number
  orgId: number
  accountSetId: number
  period: string
  templateName?: string
  periodLabel?: string
}>()

const emit = defineEmits<{
  (e: 'update:open', v: boolean): void
}>()

const simpleImage = Empty.PRESENTED_IMAGE_SIMPLE

const canOperate = computed(
  () => !!props.templateId && !!props.orgId && !!props.period && !!props.accountSetId
)

const columns = [
  { title: '科目', key: 'account', width: 240 },
  { title: '辅助核算', key: 'aux' },
  { title: '金额', key: 'amount', width: 130, align: 'right' as const },
  { title: '操作', key: 'action', width: 120, align: 'center' as const },
]

// ==================== 列表 ====================
const rows = ref<EstimateDataDto[]>([])
const listLoading = ref(false)

async function loadList() {
  if (!canOperate.value) {
    rows.value = []
    return
  }
  listLoading.value = true
  try {
    const res = await getEstimateData({
      templateId: props.templateId,
      orgId: props.orgId,
      period: props.period,
    })
    rows.value = Array.isArray(res) ? res : []
  } catch (e: any) {
    console.error('加载暂估数据失败', e)
    message.error(e?.message || '加载暂估数据失败')
    rows.value = []
  } finally {
    listLoading.value = false
  }
}

// ==================== 科目树 ====================
const accountTreeData = ref<any[]>([])
const accountNameMap = ref<Record<string, string>>({})

async function loadAccountTree() {
  if (!props.accountSetId) return
  try {
    const res: any = await getAccountTree(undefined, props.accountSetId)
    const list = Array.isArray(res) ? res : res?.data ?? []
    const map: Record<string, string> = {}
    accountTreeData.value = buildTreeData(list, map)
    accountNameMap.value = map
  } catch (e) {
    console.error('加载科目树失败', e)
    accountTreeData.value = []
  }
}

function buildTreeData(items: any[], map: Record<string, string>): any[] {
  return items.map((item) => {
    const code = item.code ?? item.fCode ?? ''
    const name = item.name ?? item.fName ?? ''
    if (code) map[code] = name
    const children = item.children ? buildTreeData(item.children, map) : undefined
    const isLeaf = !children || children.length === 0
    return {
      // field-names 将 value 映射到 code，无需额外 value 字段
      code,
      // 仅末级科目可选：暂估须落到具体科目，非末级仅作分组展开
      title: `${code} ${name}`.trim(),
      selectable: isLeaf,
      disabled: !isLeaf,
      children,
    }
  })
}

function accountName(code?: string): string {
  if (!code) return ''
  return accountNameMap.value[code] || ''
}

// ==================== 辅助核算 ====================
const auxTypeOptions = ref<{ label: string; value: string }[]>([])
const auxItemOptions = reactive<Record<string, { label: string; value: string; name: string }[]>>({})
const auxItemLoading = reactive<Record<string, boolean>>({})

async function loadAuxTypes() {
  try {
    const res: any = await getAuxiliaryTypes()
    const list = Array.isArray(res) ? res : res?.data ?? []
    // 辅助核算类型的 name 即类型编码（如 express_brand / outlet / customer）
    auxTypeOptions.value = list.map((t: any) => ({ label: t.name, value: t.name }))
  } catch (e) {
    console.error('加载辅助核算类型失败', e)
    auxTypeOptions.value = []
  }
}

async function loadAuxItems(auxType: string) {
  if (!auxType || !props.accountSetId) return
  if (auxItemOptions[auxType]) return
  auxItemLoading[auxType] = true
  try {
    const res: any = await getAuxiliaryItemsByAccountSet({
      accountSetId: props.accountSetId,
      auxType,
    })
    const list = Array.isArray(res) ? res : res?.data ?? []
    auxItemOptions[auxType] = list.map((it: any) => ({
      value: it.code,
      label: `${it.code} ${it.name}`.trim(),
      name: it.name,
    }))
  } catch (e) {
    console.error('加载辅助核算项失败', e)
    auxItemOptions[auxType] = []
  } finally {
    auxItemLoading[auxType] = false
  }
}

function onAuxTypeChange(aux: AuxFormItem) {
  aux.code = ''
  aux.name = ''
  if (aux.type) loadAuxItems(aux.type)
}

function onAuxItemChange(aux: AuxFormItem, code: string) {
  const opt = (auxItemOptions[aux.type] || []).find((o) => o.value === code)
  aux.name = opt?.name || ''
}

// 解析存量 auxiliaryJson 为列表展示的标签
function parseAuxTags(json?: string): string[] {
  const items = parseAuxJson(json)
  return items.map((a) => `${a.type}: ${a.name || a.code}`)
}

function parseAuxJson(json?: string): { type: string; code: string; name: string }[] {
  if (!json) return []
  try {
    const parsed = JSON.parse(json)
    const arr = Array.isArray(parsed) ? parsed : [parsed]
    return arr
      .filter((a) => a && (a.type || a.code))
      .map((a) => ({ type: a.type || '', code: a.code || '', name: a.name || '' }))
  } catch {
    return []
  }
}

// ==================== 表单 ====================
interface AuxFormItem {
  type: string
  code: string
  name: string
}
const formOpen = ref(false)
const saving = ref(false)
const form = reactive<{
  id: number
  accountCode: string | undefined
  amount: number | undefined
  auxList: AuxFormItem[]
}>({
  id: 0,
  accountCode: undefined,
  amount: undefined,
  auxList: [],
})

function openForm(record?: EstimateDataDto) {
  if (record) {
    form.id = record.id
    form.accountCode = record.accountCode || undefined
    form.amount = record.amount
    form.auxList = parseAuxJson(record.auxiliaryJson)
    // 预加载已选类型的辅助核算项，确保下拉能回显
    for (const aux of form.auxList) {
      if (aux.type) loadAuxItems(aux.type)
    }
  } else {
    form.id = 0
    form.accountCode = undefined
    form.amount = undefined
    form.auxList = []
  }
  formOpen.value = true
}

function addAux() {
  form.auxList.push({ type: '', code: '', name: '' })
}

function buildAuxJson(): string {
  const valid = form.auxList.filter((a) => a.type && a.code)
  if (valid.length === 0) return ''
  return JSON.stringify(valid.map((a) => ({ type: a.type, code: a.code, name: a.name })))
}

async function handleSave() {
  if (!form.accountCode) {
    message.warning('请选择科目')
    return
  }
  if (form.amount == null || Number.isNaN(form.amount)) {
    message.warning('请输入金额')
    return
  }
  // 校验辅助核算行完整性：选了类型必须选项
  const incomplete = form.auxList.some((a) => (a.type && !a.code) || (!a.type && a.code))
  if (incomplete) {
    message.warning('辅助核算行需同时选择类型与项，或整行删除')
    return
  }

  const dto: EstimateDataDto = {
    id: form.id,
    amount: Number(form.amount),
    templateId: props.templateId,
    orgId: props.orgId,
    period: props.period,
    dataType: 'estimate',
    accountCode: form.accountCode,
    auxiliaryJson: buildAuxJson(),
  }
  saving.value = true
  try {
    await saveEstimateData(dto)
    message.success('保存成功')
    formOpen.value = false
    await loadList()
  } catch (e: any) {
    console.error('保存暂估数据失败', e)
    message.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

async function handleDelete(record: EstimateDataDto) {
  try {
    await deleteEstimateData(record.id)
    message.success('删除成功')
    await loadList()
  } catch (e: any) {
    console.error('删除暂估数据失败', e)
    message.error(e?.message || '删除失败')
  }
}

// ==================== 格式化 ====================
function formatAmount(val: number | null | undefined): string {
  if (val == null) return '0.00'
  return Number(val).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// ==================== 打开时加载 ====================
watch(
  () => props.open,
  (v) => {
    if (v) {
      loadList()
      loadAccountTree()
      loadAuxTypes()
    }
  }
)
</script>

<style scoped lang="scss">
.estimate-dialog {
  .ctx-alert {
    margin-bottom: 12px;
    .ctx-hint {
      color: #888;
      font-size: 12px;
    }
  }
  .list-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 8px;
    .list-title {
      font-weight: 600;
    }
  }
  .account-cell {
    display: flex;
    flex-direction: column;
    line-height: 1.3;
    .acc-code {
      font-family: 'SFMono-Regular', Consolas, monospace;
    }
    .acc-name {
      color: #888;
      font-size: 12px;
    }
  }
  .amount-cell {
    font-variant-numeric: tabular-nums;
    &.negative {
      color: var(--color-danger-text);
    }
  }
  .muted {
    color: #bbb;
  }
}
.estimate-form {
  .aux-row {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 8px;
  }
  .aux-tip {
    color: #999;
    font-size: 12px;
    margin-top: 4px;
  }
}
</style>
