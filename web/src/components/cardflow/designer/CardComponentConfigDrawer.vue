<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import type { CardComponentDefinition, SchemaFieldDefinition } from '@/types/cardflow'
import {
  buildCapabilityProps,
  resolveComponentCapability,
} from './cardComponentCapabilities'
import StatusTag from '@/components/StatusTag.vue'

const props = defineProps<{
  open: boolean
  modelValue: CardComponentDefinition | null
  schemaFields: SchemaFieldDefinition[]
  detailSchemaFields: SchemaFieldDefinition[]
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'update:modelValue', value: CardComponentDefinition): void
  (e: 'delete', componentId: string): void
}>()

const draft = ref<CardComponentDefinition | null>(null)

const componentCapability = computed(() =>
  draft.value ? resolveComponentCapability(draft.value.type, draft.value.props || {}) : null,
)

const configWarnings = computed(() => {
  if (!draft.value || !componentCapability.value) return []
  const warnings: string[] = []
  if (!componentCapability.value.publishable) {
    warnings.push(componentCapability.value.unsupportedReason || '该组件暂未支持发布，请先作为模板参考。')
  }
  if (draft.value.binding?.source && !componentCapability.value.supportedBindings.includes(draft.value.binding.source)) {
    warnings.push(`当前绑定来源「${draft.value.binding.source}」不在该组件推荐范围内。`)
  }
  return warnings
})

const fieldOptions = computed(() =>
  props.schemaFields.map(field => ({ value: field.key, label: field.label || field.key })),
)

const detailColumnOptions = computed(() =>
  props.detailSchemaFields.map(field => ({ value: field.key, label: field.label || field.key })),
)

watch(
  () => props.modelValue,
  value => {
    draft.value = value ? JSON.parse(JSON.stringify(value)) : null
    if (draft.value) {
      draft.value.props = buildCapabilityProps(draft.value.type, draft.value.props || {})
      draft.value.layout ||= {}
      draft.value.binding ||= { source: 'cardField' }
    }
  },
  { immediate: true, deep: true },
)

function close() {
  emit('update:open', false)
}

function commit() {
  if (!draft.value) return
  emit('update:modelValue', JSON.parse(JSON.stringify(draft.value)))
  close()
}

function setDraftAccess(editable: boolean | string | number) {
  if (!draft.value) return
  draft.value.access = editable ? 'editable' : 'readonly'
}

function ensureBinding() {
  if (!draft.value) return
  draft.value.binding ||= { source: 'cardField' }
}

function ensureValidation() {
  if (!draft.value) return
  draft.value.validation ||= { minRows: undefined, requiredColumns: [] }
}

function ensureAggregation() {
  if (!draft.value) return
  draft.value.aggregation ||= { sum: [] }
}

function addSumRule() {
  if (!draft.value) return
  ensureAggregation()
  draft.value.aggregation!.sum ||= []
  draft.value.aggregation!.sum!.push({ fieldKey: 'amount', targetKey: 'detailSum.amount' })
}

function removeSumRule(index: number) {
  if (!draft.value?.aggregation?.sum) return
  draft.value.aggregation.sum.splice(index, 1)
}

function setProp(key: string, value: any) {
  if (!draft.value) return
  draft.value.props ||= {}
  draft.value.props[key] = value
}

function setBindingProp(key: string, value: any) {
  if (!draft.value) return
  ensureBinding()
  ;(draft.value.binding as Record<string, any>)[key] = value
}

function hasConfigSection(section: string) {
  return !!componentCapability.value?.configSections.includes(section)
}

const showOptionConfig = computed(() => hasConfigSection('options'))
const showFieldFormatConfig = computed(() => hasConfigSection('fieldFormat'))
const showAttachmentConfig = computed(() => hasConfigSection('attachment'))
const showRatingConfig = computed(() => hasConfigSection('rating'))
const showBusinessStatusConfig = computed(() => hasConfigSection('businessStatus'))
const showRelationConfig = computed(() => hasConfigSection('relation'))

const optionList = computed<Array<{ label: string; value: string }>>(() => {
  const options = draft.value?.props?.options
  return Array.isArray(options) ? options : []
})

function setNestedProp(path: string, value: any) {
  if (!draft.value) return
  draft.value.props ||= {}
  const keys = path.split('.').filter(Boolean)
  if (!keys.length) return
  let cursor: Record<string, any> = draft.value.props
  keys.slice(0, -1).forEach((key) => {
    if (!cursor[key] || typeof cursor[key] !== 'object') cursor[key] = {}
    cursor = cursor[key]
  })
  cursor[keys[keys.length - 1]] = value
}

function ensureOptions() {
  if (!draft.value) return
  draft.value.props ||= {}
  if (!Array.isArray(draft.value.props.options)) {
    draft.value.props.options = []
  }
}

function addOption() {
  if (!draft.value) return
  ensureOptions()
  const nextIndex = optionList.value.length + 1
  draft.value.props!.options.push({ label: `选项${nextIndex}`, value: `option${nextIndex}` })
}

function removeOption(index: number) {
  if (!draft.value || !Array.isArray(draft.value.props?.options)) return
  draft.value.props.options.splice(index, 1)
}

function setOption(index: number, key: 'label' | 'value', value: string) {
  if (!draft.value || !Array.isArray(draft.value.props?.options)) return
  draft.value.props.options[index] = {
    ...draft.value.props.options[index],
    [key]: value,
  }
}
</script>

<template>
  <a-drawer
    :open="open"
    :width="460"
    placement="right"
    :destroy-on-close="false"
    @close="close"
  >
    <template #title>
      <span>{{ draft ? '组件配置' : '未选择组件' }}</span>
    </template>

    <div v-if="draft" class="cf-component-config">
      <section v-if="componentCapability" class="cf-component-config__status" :class="{ 'is-blocking': !componentCapability.publishable }">
        <h3>发布状态</h3>
        <div class="cf-component-config__row">
          <span>能力分层</span>
          <StatusTag :type="componentCapability.publishable ? 'success' : 'warning'">
            {{ componentCapability.publishable ? componentCapability.tier : '暂缓' }}
          </StatusTag>
        </div>
        <p v-for="warning in configWarnings" :key="warning">{{ warning }}</p>
      </section>

      <section>
        <h3>显示</h3>
        <label>
          <span>组件标题</span>
          <a-input v-model:value="draft.title" />
        </label>
        <label>
          <span>组件类型</span>
          <a-input v-model:value="draft.type" />
        </label>
        <label>
          <span>占位提示</span>
          <a-input
            :value="draft.props?.placeholder"
            placeholder="运行态编辑时的提示"
            @update:value="(value: string) => setProp('placeholder', value)"
          />
        </label>
        <label>
          <span>默认值</span>
          <a-input
            :value="draft.props?.defaultValue"
            placeholder="可选，预览和新卡片默认展示"
            @update:value="(value: string) => setProp('defaultValue', value)"
          />
        </label>
        <label>
          <span>空值文案</span>
          <a-input
            :value="draft.props?.emptyText"
            placeholder="没有数据时展示"
            @update:value="(value: string) => setProp('emptyText', value)"
          />
        </label>
      </section>

      <section>
        <h3>数据绑定</h3>
        <label>
          <span>绑定来源</span>
          <a-select v-model:value="draft.binding.source" style="width: 100%" @change="ensureBinding">
            <a-select-option value="static">静态内容</a-select-option>
            <a-select-option value="cardField">卡片字段</a-select-option>
            <a-select-option value="detailTable">明细表</a-select-option>
            <a-select-option value="detailSummary">明细汇总</a-select-option>
            <a-select-option value="relation">关联卡片</a-select-option>
            <a-select-option value="snapshot">运行快照</a-select-option>
          </a-select>
        </label>
        <label v-if="draft.binding.source === 'cardField'">
          <span>字段</span>
          <a-select v-model:value="draft.binding.fieldKey" :options="fieldOptions" style="width: 100%" allow-clear />
        </label>
        <label v-if="draft.binding.source === 'detailTable'">
          <span>明细表 Key</span>
          <a-input v-model:value="draft.binding.detailTableKey" placeholder="default" />
        </label>
        <label v-if="draft.binding.source === 'detailSummary'">
          <span>汇总字段</span>
          <a-input v-model:value="draft.binding.summaryKey" placeholder="detailSum.amount" />
        </label>
        <label v-if="draft.binding.source === 'relation'">
          <span>关系类型</span>
          <a-input v-model:value="draft.binding.relationType" placeholder="loanOffset" />
        </label>
        <label v-if="draft.binding.source === 'snapshot'">
          <span>快照类型</span>
          <a-select v-model:value="draft.binding.snapshotType" style="width: 100%">
            <a-select-option value="routeDecision">routeDecision</a-select-option>
            <a-select-option value="dynamicApprover">dynamicApprover</a-select-option>
          </a-select>
        </label>
      </section>

      <section v-if="showOptionConfig">
        <h3>选项配置</h3>
        <div v-for="(option, index) in optionList" :key="index" class="cf-component-config__option">
          <a-input
            :value="option.label"
            placeholder="选项名称"
            @update:value="(value: string) => setOption(index, 'label', value)"
          />
          <a-input
            :value="option.value"
            placeholder="选项值"
            @update:value="(value: string) => setOption(index, 'value', value)"
          />
          <a-button size="small" danger type="text" @click="removeOption(index)">删除</a-button>
        </div>
        <a-button size="small" type="dashed" @click="addOption">添加选项</a-button>
      </section>

      <section v-if="showFieldFormatConfig">
        <h3>字段格式</h3>
        <label>
          <span>前缀</span>
          <a-input :value="draft.props?.prefix" placeholder="如 ¥" @update:value="(value: string) => setProp('prefix', value)" />
        </label>
        <label>
          <span>后缀</span>
          <a-input :value="draft.props?.suffix" placeholder="如 %" @update:value="(value: string) => setProp('suffix', value)" />
        </label>
        <label>
          <span>小数位</span>
          <a-input-number :value="draft.props?.precision" :min="0" :max="8" style="width: 100%" @update:value="(value: string | number) => setProp('precision', value)" />
        </label>
        <label>
          <span>最大长度</span>
          <a-input-number :value="draft.props?.maxLength" :min="0" style="width: 100%" @update:value="(value: string | number) => setProp('maxLength', value)" />
        </label>
        <label>
          <span>脱敏规则</span>
          <a-select :value="draft.props?.maskPattern" style="width: 100%" allow-clear @update:value="(value: SelectValue) => setProp('maskPattern', value)">
            <a-select-option value="phone">手机号</a-select-option>
            <a-select-option value="idCard">身份证</a-select-option>
            <a-select-option value="custom">自定义</a-select-option>
          </a-select>
        </label>
      </section>

      <section v-if="showAttachmentConfig">
        <h3>附件/图片</h3>
        <label>
          <span>文件数量</span>
          <a-input-number :value="draft.props?.maxCount" :min="1" :max="50" style="width: 100%" @update:value="(value: string | number) => setProp('maxCount', value)" />
        </label>
        <label>
          <span>允许文件类型</span>
          <a-input :value="draft.props?.accept" placeholder=".pdf,.png,.jpg" @update:value="(value: string) => setProp('accept', value)" />
        </label>
        <label>
          <span>展示方式</span>
          <a-select :value="draft.props?.displayMode" style="width: 100%" @update:value="(value: SelectValue) => setProp('displayMode', value)">
            <a-select-option value="list">列表</a-select-option>
            <a-select-option value="grid">宫格</a-select-option>
            <a-select-option value="signature">签名板</a-select-option>
          </a-select>
        </label>
      </section>

      <section v-if="showRatingConfig">
        <h3>评分</h3>
        <label>
          <span>评分上限</span>
          <a-input-number :value="draft.props?.max" :min="1" :max="10" style="width: 100%" @update:value="(value: string | number) => setProp('max', value)" />
        </label>
        <div class="cf-component-config__row">
          <span>允许半星</span>
          <a-switch :checked="!!draft.props?.allowHalf" @update:checked="(value: boolean | string | number) => setProp('allowHalf', value)" />
        </div>
      </section>

      <section v-if="showBusinessStatusConfig">
        <h3>业务状态</h3>
        <label>
          <span>状态字段</span>
          <a-input :value="draft.props?.statusField" placeholder="如 invoiceStatus" @update:value="(value: string) => setProp('statusField', value)" />
        </label>
        <label>
          <span>状态文案</span>
          <a-input :value="draft.props?.statusText" placeholder="如 待校验" @update:value="(value: string) => setProp('statusText', value)" />
        </label>
        <label>
          <span>严重程度</span>
          <a-select :value="draft.props?.severity" style="width: 100%" @update:value="(value: SelectValue) => setProp('severity', value)">
            <a-select-option value="info">信息</a-select-option>
            <a-select-option value="success">正常</a-select-option>
            <a-select-option value="warning">预警</a-select-option>
            <a-select-option value="danger">风险</a-select-option>
          </a-select>
        </label>
        <label>
          <span>金额字段</span>
          <a-input :value="draft.props?.amountField" placeholder="如 amount" @update:value="(value: string) => setProp('amountField', value)" />
        </label>
        <label>
          <span>汇总字段</span>
          <a-input :value="draft.binding?.summaryKey" placeholder="detailSum.amount" @update:value="(value: string) => setBindingProp('summaryKey', value)" />
        </label>
        <div class="cf-component-config__row">
          <span>显示状态标记</span>
          <a-switch :checked="draft.props?.showBadge !== false" @update:checked="(value: boolean | string | number) => setProp('showBadge', value)" />
        </div>
      </section>

      <section v-if="showRelationConfig">
        <h3>关联配置</h3>
        <label>
          <span>数据源类型</span>
          <a-select :value="draft.props?.relationKind" style="width: 100%" @update:value="(value: SelectValue) => setProp('relationKind', value)">
            <a-select-option value="workflow">流程表单</a-select-option>
            <a-select-option value="data">数据表单</a-select-option>
          </a-select>
        </label>
        <label>
          <span>关联类型</span>
          <a-input :value="draft.props?.relationType || draft.binding?.relationType" placeholder="如 loan / contract" @update:value="(value: string) => { setProp('relationType', value); setBindingProp('relationType', value) }" />
        </label>
        <label>
          <span>展示字段</span>
          <a-select
            :value="draft.props?.displayFields"
            mode="tags"
            style="width: 100%"
            placeholder="name、amount、status"
            @update:value="(value: SelectValue) => setNestedProp('displayFields', value)"
          />
        </label>
      </section>

      <section>
        <h3>输入</h3>
        <div class="cf-component-config__row">
          <span>默认可编辑</span>
          <a-switch
            :checked="draft.access === 'editable' || draft.access === 'required'"
            @update:checked="setDraftAccess"
          />
        </div>
      </section>

      <section>
        <h3>校验</h3>
        <a-button size="small" type="dashed" @click="ensureValidation">启用校验</a-button>
        <template v-if="draft.validation">
          <label>
            <span>最少明细行数</span>
            <a-input-number v-model:value="draft.validation.minRows" :min="0" style="width: 100%" />
          </label>
          <label>
            <span>必填列</span>
            <a-select v-model:value="draft.validation.requiredColumns" mode="multiple" :options="detailColumnOptions" style="width: 100%" />
          </label>
        </template>
      </section>

      <section>
        <h3>权限</h3>
        <label>
          <span>默认权限</span>
          <a-select v-model:value="draft.access" style="width: 100%">
            <a-select-option value="readonly">只读</a-select-option>
            <a-select-option value="editable">可编辑</a-select-option>
            <a-select-option value="required">必填</a-select-option>
            <a-select-option value="masked">脱敏</a-select-option>
            <a-select-option value="hidden">隐藏</a-select-option>
          </a-select>
        </label>
      </section>

      <section>
        <h3>条件可见</h3>
        <label>
          <span>可见条件标识</span>
          <a-input v-model:value="draft.visibilityCondition" placeholder="可选：保存结构化条件引用键" />
        </label>
      </section>

      <section>
        <h3>联动</h3>
        <label>
          <span>联动分组</span>
          <a-input
            :value="draft.props?.linkageGroup"
            placeholder="如 invoice-check"
            @update:value="(value: string) => setProp('linkageGroup', value)"
          />
        </label>
      </section>

      <section>
        <h3>汇总</h3>
        <a-button size="small" type="dashed" @click="addSumRule">添加求和规则</a-button>
        <div v-for="(sum, index) in (draft.aggregation?.sum || [])" :key="index" class="cf-component-config__sum">
          <a-select v-model:value="sum.fieldKey" :options="detailColumnOptions" placeholder="明细字段" />
          <a-input v-model:value="sum.targetKey" placeholder="detailSum.amount" />
          <a-button size="small" danger type="text" @click="removeSumRule(index)">移除</a-button>
        </div>
      </section>

      <section>
        <h3>统计</h3>
        <label>
          <span>统计 Key</span>
          <a-input v-model:value="draft.statisticKey" placeholder="expenseAmount" />
        </label>
      </section>
    </div>

    <template #footer>
      <div class="cf-component-config__footer">
        <a-button v-if="draft" danger @click="emit('delete', draft.id)">删除组件</a-button>
        <span />
        <a-button @click="close">取消</a-button>
        <a-button type="primary" :disabled="!draft" @click="commit">保存</a-button>
      </div>
    </template>
  </a-drawer>
</template>

<style scoped lang="scss">
.cf-component-config {
  display: flex;
  flex-direction: column;
  gap: 12px;

  section {
    display: flex;
    flex-direction: column;
    gap: 9px;
    padding: 11px 12px;
    border: 1px solid var(--border);
    border-radius: 6px;
    background: var(--bg-muted);
  }

  h3 {
    margin: 0;
    color: var(--text-1);
    font-size: 13px;
    font-weight: 700;
  }

  label {
    display: flex;
    flex-direction: column;
    gap: 5px;

    span {
      color: var(--text-2);
      font-size: 12px;
    }
  }
}

.cf-component-config__row,
.cf-component-config__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.cf-component-config__row span {
  color: var(--text-2);
  font-size: 12px;
}

.cf-component-config__sum {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 8px;
  align-items: center;
}

.cf-component-config__option {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 8px;
  align-items: center;
}

.cf-component-config__status {
  p {
    margin: 0;
    border-radius: 5px;
    padding: 6px 8px;
    background: var(--color-warning-light);
    color: var(--color-warning-text);
    font-size: 12px;
    line-height: 18px;
  }

  &.is-blocking {
    border-color: var(--color-warning);
    background: var(--color-warning-light);
  }
}

.cf-component-config__footer span {
  flex: 1;
}
</style>
