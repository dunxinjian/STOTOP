<script setup lang="ts">
/**
 * SchemaFieldEditor —— 卡片字段 / 明细行字段 可视化编辑器
 *
 * 设计原则：
 *  - 卡片化（非表格），强对比度的标识/标签视觉，monospace key、editorial label
 *  - vuedraggable 排序 + hover 显示删除按钮 + 必填星号醒目
 *  - 类型选择面板：8 种类型 2x4 网格，进入字段编辑 Drawer
 *  - Drawer 内根据 type 动态渲染专属配置（enum / file / cardRef）
 */
import { computed, ref, watch } from 'vue'
import {
  PlusOutlined,
  DeleteOutlined,
  HolderOutlined,
  CloseOutlined,
} from '@ant-design/icons-vue'
import draggable from 'vuedraggable'
import type { SchemaFieldDefinition, SchemaFieldType } from '@/types/cardflow'
import { getFlowDefinitions } from '@/api/cardflow'
import { message, Modal } from 'ant-design-vue'

const props = defineProps<{
  modelValue: SchemaFieldDefinition[]
  /** 区域标题，例如 "卡片字段" / "明细行字段" */
  title?: string
  /** 用于 cardRef 类型的"目标流程"下拉，可外部传入；不传则懒加载 */
  availableFlows?: { code: string; name: string }[]
  /**
   * 路由引用保护（E0-M2 拦截强度）：fieldKey → 引用分支名列表。
   * 被引用字段的删除被拦截（改类型/取消必填由发布校验兜底）。
   */
  routeRefs?: Map<string, string[]>
}>()

const emit = defineEmits<{
  'update:modelValue': [val: SchemaFieldDefinition[]]
  'change': [val: SchemaFieldDefinition[]]
}>()

// ==================== 类型元数据 ====================

interface TypeMeta {
  value: SchemaFieldType
  label: string
  icon: string
  hint: string
  tone: string
}

const TYPE_META: TypeMeta[] = [
  { value: 'text',        label: '文本',     icon: 'Aa', hint: '单行/多行文本',  tone: 'var(--cf-field-text)' },
  { value: 'money',       label: '金额',     icon: '¥',  hint: '人民币金额',     tone: 'var(--cf-field-money)' },
  { value: 'enum',        label: '枚举',     icon: '◎',  hint: '下拉选项',       tone: 'var(--cf-field-enum)' },
  { value: 'date',        label: '日期',     icon: '◈',  hint: '日期/时间',      tone: 'var(--cf-field-date)' },
  { value: 'file',        label: '附件',     icon: '◰',  hint: '上传文件',       tone: 'var(--cf-field-file)' },
  { value: 'user',        label: '人员',     icon: '⊙',  hint: '选择员工',       tone: 'var(--cf-field-user)' },
  { value: 'org',         label: '组织',     icon: '⊞',  hint: '选择组织',       tone: 'var(--cf-field-org)' },
  { value: 'cardRef',     label: '卡片引用', icon: '⤳',  hint: '关联其他流程',   tone: 'var(--cf-field-cardRef)' },
  { value: 'account',     label: '会计科目', icon: '科', hint: '选择财务科目',   tone: 'var(--cf-field-account)' },
  { value: 'auxiliary',   label: '辅助核算', icon: '辅', hint: '员工/部门/项目', tone: 'var(--cf-field-auxiliary)' },
  { value: 'bankAccount', label: '银行账户', icon: '银', hint: '组织银行账户',   tone: 'var(--cf-field-bankAccount)' },
  { value: 'voucherRef',  label: '凭证引用', icon: '凭', hint: '只读凭证链接',   tone: 'var(--cf-field-voucherRef)' },
]

const typeMetaOf = (t: SchemaFieldType): TypeMeta => TYPE_META.find(m => m.value === t) || TYPE_META[0]

// ==================== 内部状态 ====================

const fields = ref<SchemaFieldDefinition[]>(clone(props.modelValue || []))

watch(() => props.modelValue, (v) => {
  if (JSON.stringify(v) === JSON.stringify(fields.value)) return
  fields.value = clone(v || [])
}, { deep: true })

function clone<T>(v: T): T { return JSON.parse(JSON.stringify(v)) }

function emitUpdate() {
  emit('update:modelValue', clone(fields.value))
  emit('change', clone(fields.value))
}

function genKey(prefix = 'field'): string {
  let i = 1
  while (fields.value.some(f => f.key === `${prefix}_${i}`)) i++
  return `${prefix}_${i}`
}

function defaultFieldOf(type: SchemaFieldType): SchemaFieldDefinition {
  const base: SchemaFieldDefinition = {
    key: genKey(type),
    label: '新字段',
    type,
    required: false,
    readonly: false,
    dataSource: 'none',
  }
  if (type === 'enum') base.options = ['选项A', '选项B']
  if (type === 'file') { base.accept = '*'; base.maxSize = 10 }
  if (type === 'auxiliary') base.auxType = 'employee'
  if (type === 'voucherRef') base.readonly = true
  return base
}

// ==================== 类型面板 ====================

const typePanelOpen = ref(false)
function openTypePanel() { typePanelOpen.value = true }
function pickType(type: SchemaFieldType) {
  const f = defaultFieldOf(type)
  fields.value.push(f)
  emitUpdate()
  typePanelOpen.value = false
  // 直接进入编辑
  openEditor(fields.value.length - 1, true)
}

// ==================== Drawer 编辑 ====================

const drawerOpen = ref(false)
const editingIndex = ref(-1)
const editingIsNew = ref(false)
const draft = ref<SchemaFieldDefinition | null>(null)

const maskPatternModel = computed<string | undefined>({
  get: () => draft.value?.maskPattern ?? undefined,
  set: (value) => {
    if (draft.value) draft.value.maskPattern = value ?? null
  },
})

function openEditor(index: number, isNew = false) {
  editingIndex.value = index
  editingIsNew.value = isNew
  draft.value = clone(fields.value[index])
  drawerOpen.value = true
}

function cancelEditor() {
  // 新建字段在 pickType 时已先入列（供实时预览），取消编辑必须把它移除，否则残留"新字段"垃圾数据
  if (editingIsNew.value && editingIndex.value >= 0) {
    fields.value.splice(editingIndex.value, 1)
    emitUpdate()
  }
  drawerOpen.value = false
  draft.value = null
  editingIndex.value = -1
  editingIsNew.value = false
}

function commitEditor() {
  if (editingIndex.value < 0 || !draft.value) return   // 内部不变量，静默
  // 校验 key 唯一
  const key = (draft.value.key || '').trim()
  if (!key) { message.warning('请填写字段标识（key）'); return }
  const dup = fields.value.some((f, i) => i !== editingIndex.value && f.key === key)
  if (dup) { message.warning(`字段标识「${key}」已存在`); return }
  fields.value[editingIndex.value] = clone(draft.value)
  emitUpdate()
  // 提交成功后清除"新建"标记再关闭，避免 cancelEditor 误删已提交字段
  editingIsNew.value = false
  cancelEditor()
}

function removeField(index: number) {
  const field = fields.value[index]
  // 路由依赖锁定（设计屏2/E0-M2）：被条件分支引用的字段删除=拦截而非确认
  const refs = field?.key ? props.routeRefs?.get(field.key) : undefined
  if (refs?.length) {
    Modal.warning({
      title: `字段「${field?.label || field?.key}」不可删除`,
      content: `该字段被条件分支引用（${refs.slice(0, 3).join('、')}${refs.length > 3 ? ' 等' : ''}），删除会使分支永不命中。请先在流程设计中解除引用。`,
      okText: '知道了',
    })
    return
  }
  Modal.confirm({
    title: `删除字段「${field?.label || field?.key || ''}」？`,
    content: '若该字段被节点权限、摘要、进入条件或流转条件引用，引用会失效并在发布校验中报错。',
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk() {
      fields.value.splice(index, 1)
      emitUpdate()
    },
  })
}

function onDragEnd() {
  emitUpdate()
}

// ==================== enum 选项编辑 ====================

function addEnumOption() {
  if (!draft.value) return
  if (!draft.value.options) draft.value.options = []
  draft.value.options.push(`选项${draft.value.options.length + 1}`)
}
function removeEnumOption(i: number) {
  if (!draft.value?.options) return
  draft.value.options.splice(i, 1)
}
function moveEnumOption(i: number, dir: -1 | 1) {
  if (!draft.value?.options) return
  const arr = draft.value.options
  const j = i + dir
  if (j < 0 || j >= arr.length) return
  ;[arr[i], arr[j]] = [arr[j], arr[i]]
}

const isIdentityDraft = computed(() => draft.value?.type === 'user' || draft.value?.type === 'org')

type IdentitySourceMode = 'none' | 'currentUser' | 'currentUserDepartment' | 'currentOrg'

const identitySourceMode = computed<IdentitySourceMode>({
  get() {
    if (!draft.value || draft.value.dataSource !== 'auto') return 'none'
    if (draft.value.type === 'user') return 'currentUser'
    if (draft.value.type === 'org') return draft.value.autoSource === 'currentOrg' ? 'currentOrg' : 'currentUserDepartment'
    return 'none'
  },
  set(value) {
    if (!draft.value) return
    if (value === 'none') {
      draft.value.dataSource = 'none'
      draft.value.autoSource = undefined
      draft.value.readonly = false
      return
    }
    draft.value.dataSource = 'auto'
    draft.value.autoSource = value === 'currentUser' ? 'currentUser' : value
    draft.value.readonly = true
  },
})

function identitySourceLabel(source: IdentitySourceMode) {
  if (draft.value?.type === 'user') {
    return source === 'currentUser' ? '当前用户' : '用户选择'
  }
  if (source === 'currentUserDepartment') return '当前用户所在部门'
  if (source === 'currentOrg') return '当前切换组织'
  return '部门选择'
}

watch(
  () => [draft.value?.type, draft.value?.dataSource] as const,
  ([type, dataSource]) => {
    if ((type === 'user' || type === 'org') && dataSource === 'auto' && draft.value) {
      draft.value.readonly = true
      if (type === 'user' && !draft.value.autoSource) draft.value.autoSource = 'currentUser'
      if (type === 'org' && !draft.value.autoSource) draft.value.autoSource = 'currentUserDepartment'
    }
  },
)

// ==================== cardRef 目标流程懒加载 ====================

const flowOptions = ref<{ code: string; name: string }[]>([])
const flowOptionsLoaded = ref(false)
async function ensureFlowOptions() {
  if (props.availableFlows) {
    flowOptions.value = props.availableFlows
    flowOptionsLoaded.value = true
    return
  }
  if (flowOptionsLoaded.value) return
  try {
    const r: any = await getFlowDefinitions({ page: 1, pageSize: 200, status: 'published' })
    flowOptions.value = (r?.items || []).map((d: any) => ({ code: d.flowCode, name: d.flowName }))
  } catch { /* ignore */ }
  flowOptionsLoaded.value = true
}

watch(() => draft.value?.type, (t) => {
  if (t === 'cardRef') void ensureFlowOptions()
})

// ==================== 暴露 ====================

const fieldCount = computed(() => fields.value.length)

defineExpose({ fields })
</script>

<template>
  <section class="sfe">
    <!-- 标题区 -->
    <header class="sfe__head">
      <div class="sfe__head-left">
        <span class="sfe__title">{{ title || 'Schema 字段' }}</span>
        <span class="sfe__count">{{ fieldCount }}</span>
      </div>
      <a-button type="primary" size="small" ghost @click="openTypePanel">
        <template #icon><PlusOutlined /></template>
        添加字段
      </a-button>
    </header>

    <!-- 字段列表 -->
    <div class="sfe__body">
      <div v-if="fieldCount === 0" class="sfe__empty">
        <span class="sfe__empty-mark">∅</span>
        <p>尚未配置字段</p>
        <span class="sfe__empty-hint">点击右上 添加字段 开始构建表单 schema</span>
      </div>

      <draggable
        v-else
        v-model="fields"
        :animation="180"
        item-key="key"
        handle=".sfe-card__handle"
        ghost-class="sfe-card--ghost"
        @end="onDragEnd"
      >
        <template #item="{ element, index }">
          <article
            class="sfe-card"
            :class="{ 'sfe-card--required': element.required }"
            @click="openEditor(index)"
          >
            <span class="sfe-card__handle" @click.stop><HolderOutlined /></span>
            <span class="sfe-card__icon" :style="{ color: typeMetaOf(element.type).tone }">
              {{ typeMetaOf(element.type).icon }}
            </span>
            <div class="sfe-card__body">
              <div class="sfe-card__top">
                <code class="sfe-card__key">{{ element.key }}</code>
                <span class="sfe-card__sep">·</span>
                <span class="sfe-card__label">{{ element.label || '未命名' }}</span>
              </div>
              <div class="sfe-card__meta">
                <span class="sfe-card__tag" :style="{ borderColor: typeMetaOf(element.type).tone, color: typeMetaOf(element.type).tone }">
                  {{ typeMetaOf(element.type).label }}
                </span>
                <span v-if="element.readonly" class="sfe-card__chip">只读</span>
                <span v-if="element.dataSource && element.dataSource !== 'none'" class="sfe-card__chip">
                  {{ element.dataSource === 'auto' ? '自动' : '计算' }}
                </span>
              </div>
            </div>
            <div class="sfe-card__right">
              <span v-if="element.required" class="sfe-card__star" title="必填">✦</span>
              <button class="sfe-card__del" title="删除" @click.stop="removeField(index)">
                <DeleteOutlined />
              </button>
            </div>
          </article>
        </template>
      </draggable>
    </div>

    <!-- 类型选择面板（弹层 2x4 网格） -->
    <a-modal
      v-model:open="typePanelOpen"
      :footer="null"
      :width="520"
      :closable="true"
      title="选择字段类型"
      class="sfe-type-modal"
    >
      <div class="sfe-type-grid">
        <button
          v-for="m in TYPE_META"
          :key="m.value"
          class="sfe-type-cell"
          @click="pickType(m.value)"
        >
          <span class="sfe-type-cell__icon" :style="{ color: m.tone }">{{ m.icon }}</span>
          <span class="sfe-type-cell__label">{{ m.label }}</span>
          <span class="sfe-type-cell__hint">{{ m.hint }}</span>
        </button>
      </div>
    </a-modal>

    <!-- 字段编辑 Drawer -->
    <a-drawer
      :open="drawerOpen"
      placement="left"
      :width="400"
      :closable="false"
      :mask-closable="true"
      class="sfe-drawer"
      @close="cancelEditor"
    >
      <template #title>
        <div class="sfe-drawer__title">
          <span class="sfe-drawer__title-mark"></span>
          <span>{{ editingIsNew ? '新建字段' : '编辑字段' }}</span>
          <button class="sfe-drawer__close" @click="cancelEditor"><CloseOutlined /></button>
        </div>
      </template>

      <div v-if="draft" class="sfe-drawer__body">
        <!-- 标识 -->
        <div class="sfe-fld">
          <label class="sfe-fld__label">字段标识 <span class="sfe-fld__req">*</span></label>
          <a-input
            v-model:value="draft.key"
            :disabled="!editingIsNew"
            placeholder="snake_case，仅新建时可修改"
          />
          <p v-if="!editingIsNew" class="sfe-fld__hint">已存在字段标识不可修改，避免破坏历史卡片数据</p>
        </div>

        <!-- 显示名称 -->
        <div class="sfe-fld">
          <label class="sfe-fld__label">显示名称 <span class="sfe-fld__req">*</span></label>
          <a-input v-model:value="draft.label" placeholder="例如：申请金额" />
        </div>

        <!-- 类型 -->
        <div class="sfe-fld">
          <label class="sfe-fld__label">字段类型</label>
          <a-select v-model:value="draft.type" style="width: 100%">
            <a-select-option v-for="m in TYPE_META" :key="m.value" :value="m.value">
              <span :style="{ color: m.tone, fontWeight: 600, marginRight: 6 }">{{ m.icon }}</span>
              {{ m.label }}
            </a-select-option>
          </a-select>
        </div>

        <!-- 类型专属 -->
        <div class="sfe-fld sfe-fld--block" v-if="draft.type === 'enum'">
          <label class="sfe-fld__label">枚举选项</label>
          <div class="sfe-enum">
            <div v-for="(opt, i) in (draft.options || [])" :key="i" class="sfe-enum__row">
              <a-input v-model:value="(draft.options as string[])[i]" size="small" />
              <button class="sfe-enum__btn" :disabled="i === 0" @click="moveEnumOption(i, -1)">↑</button>
              <button class="sfe-enum__btn" :disabled="i === (draft.options?.length || 0) - 1" @click="moveEnumOption(i, 1)">↓</button>
              <button class="sfe-enum__btn sfe-enum__btn--del" @click="removeEnumOption(i)"><DeleteOutlined /></button>
            </div>
            <a-button type="dashed" block size="small" @click="addEnumOption">
              <template #icon><PlusOutlined /></template>
              新增选项
            </a-button>
          </div>
        </div>

        <div class="sfe-fld sfe-fld--block" v-if="draft.type === 'file'">
          <label class="sfe-fld__label">附件配置</label>
          <a-row :gutter="8">
            <a-col :span="14">
              <a-input v-model:value="draft.accept" placeholder="格式 (.pdf,.docx 或 image/*)" />
            </a-col>
            <a-col :span="10">
              <a-input-number
                v-model:value="draft.maxSize"
                :min="1"
                :max="1024"
                addon-after="MB"
                style="width:100%"
              />
            </a-col>
          </a-row>
        </div>

        <div class="sfe-fld sfe-fld--block" v-if="draft.type === 'auxiliary'">
          <label class="sfe-fld__label">辅助核算类型</label>
          <a-select v-model:value="draft.auxType" style="width: 100%">
            <a-select-option value="employee">员工</a-select-option>
            <a-select-option value="department">部门</a-select-option>
            <a-select-option value="project">项目</a-select-option>
            <a-select-option value="supplier">供应商</a-select-option>
            <a-select-option value="customer">客户</a-select-option>
          </a-select>
        </div>

        <div class="sfe-fld sfe-fld--block" v-if="draft.type === 'cardRef'">
          <label class="sfe-fld__label">关联流程</label>
          <a-select
            v-model:value="draft.targetFlowCode"
            style="width: 100%"
            placeholder="选择目标流程"
            show-search
            :options="flowOptions.map(f => ({ value: f.code, label: f.name }))"
            option-filter-prop="label"
          />
          <label class="sfe-fld__label" style="margin-top:10px">展示字段</label>
          <a-select
            v-model:value="draft.displayFields"
            mode="tags"
            style="width: 100%"
            placeholder="例如：cardNumber,title"
          />
        </div>

        <!-- 通用配置 -->
        <div class="sfe-fld sfe-fld--row">
          <span class="sfe-fld__label sfe-fld__label--inline">必填</span>
          <a-switch v-model:checked="draft.required" />
        </div>
        <div class="sfe-fld sfe-fld--row">
          <span class="sfe-fld__label sfe-fld__label--inline">只读</span>
          <a-switch v-model:checked="draft.readonly" />
        </div>
        <div class="sfe-fld sfe-fld--row">
          <span class="sfe-fld__label sfe-fld__label--inline">敏感字段</span>
          <a-switch v-model:checked="draft.sensitive" />
        </div>
        <div v-if="draft.sensitive" class="sfe-fld">
          <label class="sfe-fld__label">脱敏样式</label>
          <a-select v-model:value="maskPatternModel" allow-clear placeholder="通用（前2后2）" style="width: 100%">
            <a-select-option value="phone">手机号（138****8000）</a-select-option>
            <a-select-option value="idCard">身份证（1101**********5678）</a-select-option>
            <a-select-option value="bankCard">银行卡（**** **** **** 0123）</a-select-option>
            <a-select-option value="email">邮箱（z***@corp.com）</a-select-option>
            <a-select-option value="name">姓名（张**）</a-select-option>
          </a-select>
        </div>

        <div class="sfe-fld">
          <label class="sfe-fld__label">默认值</label>
          <a-input v-model:value="draft.defaultValue" placeholder="可选" />
        </div>

        <div class="sfe-fld">
          <label class="sfe-fld__label">数据来源</label>
          <a-radio-group v-if="isIdentityDraft" v-model:value="identitySourceMode" button-style="solid">
            <a-radio-button value="none">{{ identitySourceLabel('none') }}</a-radio-button>
            <a-radio-button v-if="draft.type === 'user'" value="currentUser">
              {{ identitySourceLabel('currentUser') }}
            </a-radio-button>
            <template v-else>
              <a-radio-button value="currentUserDepartment">
                {{ identitySourceLabel('currentUserDepartment') }}
              </a-radio-button>
              <a-radio-button value="currentOrg">
                {{ identitySourceLabel('currentOrg') }}
              </a-radio-button>
            </template>
          </a-radio-group>
          <a-radio-group v-else v-model:value="draft.dataSource" button-style="solid">
            <a-radio-button value="none">手动</a-radio-button>
            <a-radio-button value="auto">自动</a-radio-button>
            <a-radio-button value="computed">计算</a-radio-button>
          </a-radio-group>
        </div>

        <div class="sfe-fld" v-if="draft.dataSource === 'computed'">
          <label class="sfe-fld__label">计算表达式</label>
          <a-textarea
            v-model:value="draft.computeExpr"
            :rows="3"
            placeholder="例：{amount} * 0.13"
          />
        </div>
      </div>

      <template #footer>
        <div class="sfe-drawer__footer">
          <a-button @click="cancelEditor">取消</a-button>
          <a-button type="primary" @click="commitEditor">确定</a-button>
        </div>
      </template>
    </a-drawer>
  </section>
</template>

<style scoped lang="scss">
.sfe {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 10px;
  overflow: hidden;
}

.sfe__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: linear-gradient(180deg, var(--bg-muted), var(--bg-card));
  border-bottom: 1px solid var(--border);
}
.sfe__head-left { display: flex; align-items: center; gap: 10px; }
.sfe__title {
  font-size: 14px;
  font-weight: 600;
  letter-spacing: 0.4px;
  color: var(--text-1);
}
.sfe__count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 20px;
  padding: 0 6px;
  border-radius: 10px;
  background: var(--text-1);
  color: var(--text-on-accent);
  font-size: 11px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.sfe__body {
  flex: 1;
  padding: 12px;
  overflow-y: auto;
  min-height: 360px;
}

.sfe__empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 56px 16px;
  color: var(--text-3);
  text-align: center;

  .sfe__empty-mark {
    font-size: 36px;
    color: var(--text-3);
    line-height: 1;
    margin-bottom: 10px;
  }
  p { margin: 0; font-size: 13px; color: var(--text-2); }
  .sfe__empty-hint { font-size: 12px; margin-top: 4px; color: var(--text-3); }
}

/* ============== 字段卡片 ============== */
.sfe-card {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  margin-bottom: 8px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  cursor: pointer;
  transition: border-color .18s ease, box-shadow .18s ease, transform .15s ease;

  &:hover {
    border-color: var(--text-1);
    box-shadow: 0 4px 12px var(--shadow-sm);

    .sfe-card__del { opacity: 1; transform: translateX(0); }
  }

  &--required::before {
    content: '';
    position: absolute;
    left: 0; top: 10px; bottom: 10px;
    width: 3px;
    background: var(--color-danger);
    border-radius: 0 2px 2px 0;
  }

  &--ghost {
    opacity: 0.4;
    background: var(--bg-muted) !important;
  }
}

.sfe-card__handle {
  color: var(--text-3);
  cursor: grab;
  font-size: 14px;
  &:active { cursor: grabbing; }
}

.sfe-card__icon {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  font-weight: 700;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 6px;
  flex-shrink: 0;
}

.sfe-card__body { flex: 1; min-width: 0; }
.sfe-card__top {
  display: flex;
  align-items: baseline;
  gap: 6px;
  flex-wrap: wrap;
}
.sfe-card__key {
  font-family: 'JetBrains Mono', 'SF Mono', Consolas, monospace;
  font-size: 12px;
  color: var(--text-2);
  background: var(--bg-muted);
  padding: 1px 6px;
  border-radius: 4px;
}
.sfe-card__sep { color: var(--text-3); font-size: 12px; }
.sfe-card__label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-1);
}
.sfe-card__meta {
  display: flex;
  gap: 6px;
  margin-top: 4px;
  flex-wrap: wrap;
}
.sfe-card__tag {
  display: inline-flex;
  font-size: 11px;
  font-weight: 600;
  padding: 0 6px;
  height: 18px;
  line-height: 16px;
  border: 1px solid;
  border-radius: 3px;
  letter-spacing: 0.3px;
}
.sfe-card__chip {
  font-size: 11px;
  height: 18px;
  line-height: 16px;
  padding: 0 6px;
  border-radius: 3px;
  background: var(--bg-muted);
  color: var(--text-2);
}

.sfe-card__right {
  display: flex;
  align-items: center;
  gap: 6px;
}
.sfe-card__star {
  color: var(--color-danger);
  font-size: 14px;
  font-weight: 700;
}
.sfe-card__del {
  border: none;
  background: transparent;
  color: var(--color-danger);
  width: 26px;
  height: 26px;
  border-radius: 6px;
  cursor: pointer;
  opacity: 0;
  transform: translateX(4px);
  transition: opacity .18s, transform .18s, background .18s;
  &:hover { background: var(--color-danger-light); }
}

/* ============== 类型选择面板 ============== */
.sfe-type-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
  padding: 6px 0;
}
.sfe-type-cell {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 16px 8px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 8px;
  cursor: pointer;
  transition: all .18s ease;

  &:hover {
    border-color: var(--text-1);
    transform: translateY(-2px);
    box-shadow: 0 6px 14px var(--shadow-md);
  }

  &__icon {
    font-size: 22px;
    font-weight: 800;
    line-height: 1;
  }
  &__label {
    font-size: 13px;
    font-weight: 600;
    color: var(--text-1);
    margin-top: 4px;
  }
  &__hint {
    font-size: 11px;
    color: var(--text-3);
  }
}

/* ============== Drawer ============== */
.sfe-drawer__title {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 600;
}
.sfe-drawer__title-mark {
  width: 4px; height: 16px;
  background: var(--text-1);
  border-radius: 2px;
}
.sfe-drawer__close {
  margin-left: auto;
  border: none;
  background: transparent;
  width: 24px; height: 24px;
  border-radius: 4px;
  cursor: pointer;
  color: var(--text-2);
  &:hover { background: var(--bg-muted); }
}

.sfe-drawer__body {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.sfe-fld {
  display: flex;
  flex-direction: column;
  gap: 6px;

  &--row {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    padding: 6px 0;
    border-top: 1px dashed var(--border);
  }
  &--block { padding: 12px; background: var(--bg-muted); border-radius: 8px; }

  &__label {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-2);
    letter-spacing: 0.3px;

    &--inline { margin: 0; }
  }
  &__req { color: var(--color-danger); }
  &__hint { margin: 0; font-size: 11px; color: var(--text-3); }
}

.sfe-enum {
  display: flex;
  flex-direction: column;
  gap: 6px;

  &__row {
    display: flex;
    align-items: center;
    gap: 4px;
  }
  &__btn {
    width: 24px; height: 24px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border: 1px solid var(--border);
    background: var(--bg-card);
    border-radius: 4px;
    cursor: pointer;
    color: var(--text-2);
    font-size: 12px;

    &:hover { border-color: var(--text-1); color: var(--text-1); }
    &:disabled { opacity: 0.4; cursor: not-allowed; }

    &--del:hover { background: var(--color-danger-light); color: var(--color-danger); border-color: var(--color-danger-border); }
  }
}

.sfe-drawer__footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
