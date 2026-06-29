<script setup lang="ts">
import { computed, ref } from 'vue'
import draggable from 'vuedraggable'
import { HolderOutlined } from '@ant-design/icons-vue'
import type { CardComponentDefinition, SchemaFieldDefinition } from '@/types/cardflow'
import {
  CARD_COMPONENT_CAPABILITIES,
  buildCapabilityProps,
  resolveComponentCapability,
} from './cardComponentCapabilities'

const props = defineProps<{
  schemaFields: SchemaFieldDefinition[]
  detailSchemaFields: SchemaFieldDefinition[]
}>()

const emit = defineEmits<{
  (e: 'add', component: CardComponentDefinition): void
}>()

type CatalogItem = {
  catalogKey: string
  type: string
  title: string
  source: string
  hint: string
  icon: string
  badge?: string
  capabilityKey?: string
  publishable?: boolean
  componentTier?: string
  componentStatus?: string
  templateOnly?: boolean
  experimental?: boolean
  requiresRuntimeIntegration?: boolean
  field?: SchemaFieldDefinition
  snapshotType?: string | null
  props?: Record<string, any>
}

type CatalogSeed = Omit<CatalogItem, 'catalogKey'>

type CatalogGroup = {
  key: string
  title: string
  subtitle?: string
  empty?: string
  items: CatalogItem[]
}

const dragHandle = '.cf-component-catalog__drag-handle'
const activeCatalogTab = ref<'components' | 'suites' | 'relations'>('components')
const relationSearch = ref('')
const relationFilter = ref<'all' | 'workflow' | 'data'>('all')
const relationHelpVisible = ref(true)

const catalogTabs = [
  { key: 'components', label: '组件', icon: '▦' },
  { key: 'suites', label: '组件套件', icon: '▱' },
  { key: 'relations', label: '关联', icon: '↔' },
] as const

const capabilityRegistry = CARD_COMPONENT_CAPABILITIES

const LAYOUT_COMPONENTS: CatalogSeed[] = [
  {
    type: 'placeholderControl',
    title: '分栏',
    source: 'static',
    icon: '▥',
    hint: '把卡片内容拆成左右两列',
    capabilityKey: 'columnLayout',
    props: { capabilityKey: 'columnLayout', controlKind: 'columnLayout', controlName: '分栏', description: '运行态按配置承载两列内容' },
  },
]

const BASIC_COMPONENTS: CatalogSeed[] = [
  { type: 'text', title: '单行输入框', source: 'cardField', icon: 'Aa', hint: '展示或录入单行文本' },
  { type: 'textarea', title: '多行输入框', source: 'cardField', icon: 'A≡', hint: '展示较长说明文本' },
  { type: 'number', title: '数字输入框', source: 'cardField', icon: '123', hint: '数字、数量、比例等字段' },
  { type: 'radio', title: '单选框', source: 'cardField', icon: '◉', hint: '展示单选结果' },
  { type: 'checkbox', title: '多选框', source: 'cardField', icon: '☑', hint: '展示多选结果' },
  { type: 'date', title: '日期', source: 'cardField', icon: '▣', hint: '日期字段' },
  { type: 'dateRange', title: '日期区间', source: 'cardField', icon: '▤', hint: '开始和结束日期' },
  { type: 'textBlock', title: '说明文字', source: 'static', icon: '!', hint: '静态说明，等同文本说明', props: { body: '这里填写说明文字' } },
  { type: 'idCard', title: '身份证', source: 'cardField', icon: 'ID', hint: '身份证号展示与脱敏' },
  { type: 'phone', title: '电话', source: 'cardField', icon: 'Tel', hint: '手机号、座机等联系方式' },
]

const ENHANCED_COMPONENTS: CatalogSeed[] = [
  { type: 'sectionTitle', title: '分组标题', source: 'static', icon: 'H', hint: '在内容区分隔基本信息、费用明细等字段组', props: { description: '用于区分一组字段' } },
  { type: 'textBlock', title: '文本说明', source: 'static', icon: '文', hint: '展示审批说明、填写提示或制度说明', props: { body: '这里填写说明文字' } },
  { type: 'placeholderControl', title: '级联/分类', source: 'cardField', icon: '级', hint: '费用类型、组织分类、科目分类' },
  { type: 'placeholderControl', title: 'AI控件', source: 'static', icon: 'AI', hint: 'AI 提取、推荐或摘要', capabilityKey: 'aiAssist', props: { capabilityKey: 'aiAssist', controlKind: 'aiAssist' } },
  { type: 'imageList', title: '图片', source: 'static', icon: '图', hint: '图片凭证、现场照片' },
  { type: 'detailTable', title: '明细/表格', source: 'detailTable', icon: '表', hint: '费用/借款/付款明细行' },
  { type: 'money', title: '金额', source: 'cardField', icon: '¥', hint: '金额、币种和统计口径' },
  { type: 'attachment', title: '附件', source: 'cardField', icon: '附', badge: '支持电子签', hint: '文件、电子签附件和凭证' },
  { type: 'signature', title: '手写签名', source: 'static', icon: '签', hint: '审批确认签名' },
  { type: 'placeholderControl', title: '外部联系人', source: 'cardField', icon: '外', hint: '客户或外部协作人' },
  { type: 'placeholderControl', title: '联系人', source: 'cardField', icon: '人', hint: '内部联系人' },
  { type: 'placeholderControl', title: '部门', source: 'cardField', icon: '部', hint: '组织部门选择与展示' },
  { type: 'placeholderControl', title: '行业通讯录部门', source: 'cardField', icon: '组', hint: '外部通讯录组织' },
  { type: 'placeholderControl', title: '地点', source: 'cardField', icon: '地', hint: '地址、城市或坐标' },
  { type: 'placeholderControl', title: '计算公式', source: 'detailSummary', icon: '+×', hint: '由字段或明细汇总计算', capabilityKey: 'formula', props: { capabilityKey: 'formula', controlKind: 'formula' } },
  { type: 'relationCards', title: '关联审批单', source: 'relation', icon: '链', hint: '费用申请、借款、付款等引用关系' },
  { type: 'placeholderControl', title: '省市区', source: 'cardField', icon: '省', hint: '行政区划选择' },
  { type: 'rating', title: '评分', source: 'static', icon: '☆', hint: '满意度、风险等级、评价分' },
  { type: 'amountSummary', title: '金额摘要', source: 'cardField', icon: '¥', hint: '突出金额、币种和统计口径' },
  { type: 'loanOffset', title: '借款冲抵', source: 'detailSummary', icon: '抵', hint: '冲抵金额、未还余额' },
  { type: 'riskAlert', title: '风险提示', source: 'cardField', icon: '!', hint: '异常金额、重复报销、缺附件' },
  { type: 'routeDecision', title: '流转说明', source: 'snapshot', snapshotType: 'routeDecision', icon: '↗', hint: '展示命中的条件边' },
  { type: 'dynamicApprover', title: '动态审批说明', source: 'snapshot', snapshotType: 'dynamicApprover', icon: '人+', hint: '展示运行时插入审批人原因' },
]

const BUSINESS_COMPONENTS: CatalogSeed[] = [
  { type: 'invoiceStatus', title: '发票', source: 'cardField', icon: '票', hint: '发票完整性和税号校验' },
  { type: 'placeholderControl', title: '客户', source: 'cardField', icon: '客', hint: '客户档案或往来单位', props: { controlName: '客户' } },
  { type: 'paymentInfo', title: '收款账户', source: 'cardField', icon: '账', hint: '付款账户、收款账户、实付金额' },
  { type: 'budgetStatus', title: '预算申请', source: 'detailSummary', icon: '预', hint: '预算申请、占用和剩余额度' },
  { type: 'relationCards', title: '关联合同', source: 'relation', icon: '合', hint: '合同、申请单等引用关系', props: { relationType: 'contract' } },
  { type: 'placeholderControl', title: '工程项目', source: 'cardField', icon: '项', hint: '项目、工程或成本对象', props: { controlName: '工程项目' } },
]

const ADVANCED_COMPONENTS: CatalogSeed[] = [
  { type: 'ocrText', title: '通用文字识别', source: 'static', icon: 'OCR', hint: '从图片或附件中识别文本' },
  { type: 'ocrText', title: '身份证识别', source: 'static', icon: 'ID', hint: '识别身份证姓名和证号', props: { ocrType: 'idCard' } },
  { type: 'placeholderControl', title: '流水号', source: 'static', icon: 'No', hint: '展示卡片编号或业务流水号', capabilityKey: 'serialNumber', props: { capabilityKey: 'serialNumber', controlKind: 'serialNumber', controlName: '流水号' } },
]

const SUITE_GROUPS: Array<{ key: string; title: string; subtitle: string; items: CatalogSeed[] }> = [
  {
    key: 'attendance',
    title: '假勤管理',
    subtitle: '来自考勤打卡',
    items: [
      suite('请假/调休', '时', 'leave'),
      suite('补卡', '卡', 'attendanceFix'),
      suite('加班', '加', 'overtime'),
      suite('外出', '外', 'out'),
      suite('出差', '差', 'trip'),
      suite('换班', '换', 'shiftChange'),
      suite('商旅出行套件', '旅', 'travelPackage'),
    ],
  },
  {
    key: 'hr',
    title: '人事管理',
    subtitle: '来自智能人事',
    items: [
      suite('转正套件', '转', 'regularization'),
      suite('离职套件', '离', 'resignation'),
      suite('离职和交接套件', '交', 'resignHandover'),
      suite('离职交接套件', '接', 'handover'),
      suite('调岗套件', '岗', 'transfer'),
      suite('入职套件', '入', 'onboarding'),
      suite('晋升套件', '升', 'promotion'),
      suite('人事综合套件', '人', 'hrBundle'),
      suite('人事合同审批', '合', 'hrContract'),
      suite('工资发放', '资', 'payroll'),
    ],
  },
  {
    key: 'finance',
    title: '财税管理',
    subtitle: '来自智能财务',
    items: [
      suite('批量付款', '付', 'batchPayment'),
      suite('申请套件', '申', 'financeApply'),
      suite('报销套件', '报', 'reimbursement'),
      suite('付款套件', '付', 'payment'),
      suite('收款套件', '收', 'receipt'),
      suite('备用金套件', '备', 'pettyCash'),
      suite('备用金报销套件', '报', 'pettyCashReimburse'),
      suite('备用金还款套件', '还', 'pettyCashRepay'),
      suite('应收套件', '应', 'ar'),
      suite('应收回款套件', '回', 'arReceipt'),
      suite('应收坏账套件', '坏', 'badDebt'),
      suite('应付套件', '应', 'ap'),
      suite('应付实付套件', '实', 'apPaid'),
      suite('应付免付套件', '免', 'apWaive'),
      suite('开票申请套件', '票', 'invoiceApply'),
      suite('转账套件', '转', 'transferPayment'),
      suite('立项套件', '立', 'projectSetup'),
      suite('红冲套件', '红', 'invoiceRed'),
      suite('收票套件', '票', 'invoiceReceive'),
      suite('采购-beta', '采', 'purchaseBeta'),
      suite('机票超标', '机', 'flightOverLimit'),
      suite('机票改签', '改', 'flightChange'),
      suite('机票退票', '退', 'flightRefund'),
      suite('火车票改签', '改', 'trainChange'),
      suite('火车票退票', '退', 'trainRefund'),
    ],
  },
  {
    key: 'legal',
    title: '法务管理',
    subtitle: '来自智能合同',
    items: [
      suite('合同审批套件', '合', 'contractApproval'),
      suite('用印申请', '印', 'sealApply'),
      suite('档案借阅申请', '档', 'archiveBorrow'),
      suite('合同归档申请', '归', 'contractArchive'),
    ],
  },
  {
    key: 'customer',
    title: '客户管理',
    subtitle: '来自客户管理',
    items: [
      suite('客户拜访签到', '访', 'customerVisit'),
    ],
  },
]

const RELATION_GROUPS: Array<{ key: string; title: string; items: CatalogSeed[] }> = [
  {
    key: 'approval',
    title: '来自审批',
    items: [
      relation('事务申请', 'workflow', '事', 'affairApply'),
      relation('设备维修登记', 'workflow', '修', 'maintenance'),
      relation('热敏客户申请', 'workflow', '客', 'thermalCustomer'),
      relation('用章申请(石申通)', 'workflow', '印', 'sealStone'),
      relation('运输部服务单', 'workflow', '运', 'transportService'),
      relation('司机绩效申报', 'workflow', '司', 'driverPerformance'),
      relation('事故申报', 'workflow', '故', 'accidentReport'),
      relation('商旅报销', 'workflow', '旅', 'travelReimburse'),
    ],
  },
  {
    key: 'data',
    title: '来自数据表',
    items: [
      relation('客户', 'data', '客', 'customer'),
      relation('合同', 'data', '合', 'contract'),
      relation('发票', 'data', '票', 'invoice'),
      relation('预算申请', 'data', '预', 'budgetApply'),
      relation('收款账户', 'data', '账', 'bankAccount'),
    ],
  },
]

function suite(title: string, icon: string, suiteType: string): CatalogSeed {
  return {
    type: 'componentSuite',
    title,
    source: 'static',
    icon,
    hint: `${title}的审批卡片组件组合`,
    props: {
      suiteType,
      description: `${title}会以套件卡片展示关键字段、状态和处理结果`,
    },
  }
}

function relation(title: string, kind: 'workflow' | 'data', icon: string, relationType: string): CatalogSeed {
  return {
    type: 'relationLookup',
    title,
    source: 'relation',
    icon,
    hint: kind === 'workflow' ? '流程表单' : '数据表单',
    props: {
      relationKind: kind,
      relationType,
      description: `关联${title}产生的数据`,
    },
  }
}

function genId(type: string) {
  return `${type}_${Math.random().toString(36).slice(2, 8)}`
}

function withCatalogKey(section: string, item: CatalogSeed, index: number): CatalogItem {
  const propsWithCapabilityKey = {
    ...(item.props || {}),
    ...(item.capabilityKey ? { capabilityKey: item.capabilityKey } : {}),
  }
  const capability = resolveComponentCapability(item.type, propsWithCapabilityKey)
  const statusBadge = item.badge || (!capability.publishable ? (capability.templateOnly ? '模板' : '暂缓') : undefined)
  return {
    ...item,
    props: propsWithCapabilityKey,
    capabilityKey: capability.key,
    publishable: capability.publishable,
    componentTier: capability.tier,
    componentStatus: capability.publishable ? 'ready' : capability.templateOnly ? 'template' : 'deferred',
    templateOnly: !!capability.templateOnly,
    experimental: !!capability.experimental,
    requiresRuntimeIntegration: !!capability.requiresRuntimeIntegration,
    badge: statusBadge,
    catalogKey: `${section}:${item.type}:${item.title}:${index}`,
  }
}

const componentGroups = computed<CatalogGroup[]>(() => [
  {
    key: 'layout',
    title: '布局控件',
    items: LAYOUT_COMPONENTS.map((item, index) => withCatalogKey('layout', item, index)),
  },
  {
    key: 'basic',
    title: '基础控件',
    items: [
      ...BASIC_COMPONENTS.map((item, index) => withCatalogKey('basic', item, index)),
      ...props.schemaFields.map((field): CatalogItem => ({
        catalogKey: `field:${field.key}`,
        type: field.type,
        title: field.label || field.key,
        source: 'cardField',
        icon: field.type === 'money' ? '¥' : (field.type as string) === 'number' ? '123' : 'Aa',
        hint: `${field.type} / ${field.key} · 添加到审批卡片视图`,
        field,
        capabilityKey: resolveComponentCapability(field.type, { capabilityKey: capabilityRegistry[field.type] ? field.type : 'placeholderControl' }).key,
        publishable: true,
        componentTier: capabilityRegistry[field.type]?.tier || 'P1',
        props: buildCapabilityProps(field.type, {
          capabilityKey: capabilityRegistry[field.type] ? field.type : 'placeholderControl',
          controlKind: field.type,
          fieldKey: field.key,
        }),
      })),
    ],
  },
  {
    key: 'enhanced',
    title: '增强控件',
    items: ENHANCED_COMPONENTS.map((item, index) => withCatalogKey('enhanced', item, index)),
  },
  {
    key: 'business',
    title: '业务控件',
    items: BUSINESS_COMPONENTS.map((item, index) => withCatalogKey('business', item, index)),
  },
  {
    key: 'advanced',
    title: '高级控件',
    items: ADVANCED_COMPONENTS.map((item, index) => withCatalogKey('advanced', item, index)),
  },
])

const suiteGroups = computed<CatalogGroup[]>(() =>
  SUITE_GROUPS.map(group => ({
    key: group.key,
    title: group.title,
    subtitle: group.subtitle,
    items: group.items.map((item, index) => withCatalogKey(`suite:${group.key}`, {
      ...item,
      props: {
        ...(item.props || {}),
        suiteDomain: group.title,
        suiteSource: group.subtitle,
      },
    }, index)),
  })),
)

const relationGroups = computed<CatalogGroup[]>(() => {
  const keyword = relationSearch.value.trim().toLowerCase()
  return RELATION_GROUPS.map(group => ({
    key: group.key,
    title: group.title,
    items: group.items
      .filter(item => relationFilter.value === 'all' || item.props?.relationKind === relationFilter.value)
      .filter(item => !keyword || `${item.title}${item.hint}`.toLowerCase().includes(keyword))
      .map((item, index) => withCatalogKey(`relation:${group.key}`, item, index)),
    empty: '没有匹配的关联表单',
  })).filter(group => group.items.length > 0 || keyword)
})

function buildComponent(item: CatalogItem): CardComponentDefinition {
  const capabilityProps = buildCapabilityProps(item.type, {
    ...(item.props || {}),
    ...(item.capabilityKey ? { capabilityKey: item.capabilityKey } : {}),
  })
  const capability = resolveComponentCapability(item.type, capabilityProps)
  if (item.field) {
    return {
      id: `field_${item.field.key}_${Math.random().toString(36).slice(2, 6)}`,
      type: item.field.type,
      title: item.field.label || item.field.key,
      binding: { source: 'cardField', fieldKey: item.field.key },
      props: {
        ...capabilityProps,
        catalogTitle: item.field.label || item.field.key,
        controlName: item.field.label || item.field.key,
        description: `${item.field.type} / ${item.field.key}`,
      },
      layout: capability.defaultLayout || {},
    }
  }
  const firstField = props.schemaFields[0]
  const firstDetail = props.detailSchemaFields[0]
  return {
    id: genId(item.type),
    type: item.type,
    title: item.title,
    binding: {
      source: item.source,
      fieldKey: item.source === 'cardField' ? firstField?.key || undefined : undefined,
      detailTableKey: item.source === 'detailTable' ? 'default' : undefined,
      summaryKey: item.source === 'detailSummary' ? 'detailSum.amount' : undefined,
      relationType: item.source === 'relation' ? item.props?.relationType || item.title : undefined,
      snapshotType: item.snapshotType || undefined,
    },
    props: {
      ...capabilityProps,
      catalogTitle: item.title,
      controlName: capabilityProps.controlName || item.title,
      description: capabilityProps.description || item.hint,
    },
    validation: item.type === 'detailTable' ? { minRows: 1, requiredColumns: firstDetail ? [firstDetail.key] : [] } : null,
    aggregation: item.type === 'detailTable' ? { sum: [{ fieldKey: 'amount', targetKey: 'detailSum.amount' }] } : null,
    layout: capability.defaultLayout || (item.type === 'componentSuite' || item.type === 'relationLookup' ? { width: 'full' } : {}),
  }
}

function cloneCatalogComponent(item: CatalogItem) {
  return buildComponent(item)
}

function addCatalogItem(item: CatalogItem) {
  emit('add', buildComponent(item))
}
</script>

<template>
  <section class="cf-component-catalog cf-component-catalog--compact">
    <nav class="cf-component-catalog__rail" aria-label="卡片组件分类">
      <button
        v-for="tab in catalogTabs"
        :key="tab.key"
        type="button"
        class="cf-component-catalog__tab"
        :class="{ 'is-active': activeCatalogTab === tab.key }"
        @click="activeCatalogTab = tab.key"
      >
        <b aria-hidden="true">{{ tab.icon }}</b>
        <span>{{ tab.label }}</span>
      </button>
    </nav>

    <div class="cf-component-catalog__content">
      <template v-if="activeCatalogTab === 'components'">
        <header class="cf-component-catalog__header">
          <strong>组件</strong>
          <span>可添加的卡片视图组件 · 不会新增字段，拖拽到中间卡片画布或点击添加</span>
        </header>

        <div v-for="group in componentGroups" :key="group.key" class="cf-component-catalog__group">
          <h4>
            <span>{{ group.title }}</span>
            <i aria-hidden="true">⌃</i>
          </h4>
          <draggable
            :list="group.items"
            item-key="catalogKey"
            :group="{ name: 'card-components', pull: 'clone', put: false }"
            :sort="false"
            :clone="cloneCatalogComponent"
            :handle="dragHandle"
            class="cf-component-catalog__items cf-component-catalog__grid"
            ghost-class="cf-component-catalog__drag-clone"
          >
            <template #item="{ element }">
              <button type="button" class="cf-component-catalog__tile" :title="element.hint" @click="addCatalogItem(element)">
                <i class="cf-component-catalog__drag-handle" aria-hidden="true"><HolderOutlined /></i>
                <b class="cf-component-catalog__icon" aria-hidden="true">{{ element.icon }}</b>
                <span>
                  <strong :title="element.title">{{ element.title }}</strong>
                  <em>{{ element.hint }}</em>
                </span>
                <small v-if="element.badge">{{ element.badge }}</small>
              </button>
            </template>
          </draggable>
          <p v-if="group.items.length === 0">{{ group.empty }}</p>
        </div>
      </template>

      <template v-else-if="activeCatalogTab === 'suites'">
        <header class="cf-component-catalog__header">
          <strong>组件套件</strong>
          <span>按业务场景提供成组卡片组件，适合快速搭建常见审批视图</span>
        </header>

        <div v-for="group in suiteGroups" :key="group.key" class="cf-component-catalog__group">
          <h4>
            <span>{{ group.title }}</span>
            <i aria-hidden="true">⌃</i>
          </h4>
          <p class="cf-component-catalog__subtitle">{{ group.subtitle }}</p>
          <draggable
            :list="group.items"
            item-key="catalogKey"
            :group="{ name: 'card-components', pull: 'clone', put: false }"
            :sort="false"
            :clone="cloneCatalogComponent"
            :handle="dragHandle"
            class="cf-component-catalog__items cf-component-catalog__grid"
            ghost-class="cf-component-catalog__drag-clone"
          >
            <template #item="{ element }">
              <button type="button" class="cf-component-catalog__tile cf-component-catalog__tile--suite" :title="element.hint" @click="addCatalogItem(element)">
                <i class="cf-component-catalog__drag-handle" aria-hidden="true"><HolderOutlined /></i>
                <b class="cf-component-catalog__icon" aria-hidden="true">{{ element.icon }}</b>
                <span>
                  <strong :title="element.title">{{ element.title }}</strong>
                </span>
              </button>
            </template>
          </draggable>
        </div>
      </template>

      <template v-else>
        <header class="cf-component-catalog__header">
          <strong>关联表单</strong>
          <span>把其他流程或数据表产生的数据作为当前卡片的引用信息</span>
        </header>

        <section v-if="relationHelpVisible" class="cf-component-catalog__relation-help">
          <button type="button" aria-label="关闭关联表单说明" @click="relationHelpVisible = false">×</button>
          <strong>什么是关联表单？</strong>
          <p>可以在当前表单中关联其他表单产生的数据。例如在订单中关联合同编号和合同金额，新建订单时选择已有合同，卡片上自动展示对应信息。</p>
        </section>

        <label class="cf-component-catalog__search">
          <span aria-hidden="true">⌕</span>
          <input v-model="relationSearch" type="search" placeholder="搜索" />
        </label>

        <div class="cf-component-catalog__filters" role="tablist" aria-label="关联表单类型">
          <button
            type="button"
            :class="{ 'is-active': relationFilter === 'all' }"
            @click="relationFilter = 'all'"
          >
            全部
          </button>
          <button
            type="button"
            :class="{ 'is-active': relationFilter === 'workflow' }"
            @click="relationFilter = 'workflow'"
          >
            流程表单
          </button>
          <button
            type="button"
            :class="{ 'is-active': relationFilter === 'data' }"
            @click="relationFilter = 'data'"
          >
            数据表单
          </button>
        </div>

        <div v-for="group in relationGroups" :key="group.key" class="cf-component-catalog__group">
          <h4>
            <span>{{ group.title }}</span>
            <i aria-hidden="true">⌃</i>
          </h4>
          <draggable
            :list="group.items"
            item-key="catalogKey"
            :group="{ name: 'card-components', pull: 'clone', put: false }"
            :sort="false"
            :clone="cloneCatalogComponent"
            :handle="dragHandle"
            class="cf-component-catalog__items"
            ghost-class="cf-component-catalog__drag-clone"
          >
            <template #item="{ element }">
              <button type="button" class="cf-component-catalog__relation-card" :title="element.hint" @click="addCatalogItem(element)">
                <b class="cf-component-catalog__icon" aria-hidden="true">{{ element.icon }}</b>
                <span :title="element.title">{{ element.title }}</span>
                <em>{{ element.hint }}</em>
                <i class="cf-component-catalog__drag-handle" aria-hidden="true"><HolderOutlined /></i>
              </button>
            </template>
          </draggable>
          <p v-if="group.items.length === 0">{{ group.empty }}</p>
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped lang="scss">
.cf-component-catalog {
  display: grid;
  grid-template-columns: 46px minmax(0, 1fr);
  min-height: 100%;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);
  overflow: hidden;
}

.cf-component-catalog__rail {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 3px;
  padding: 8px 5px;
  border-right: 1px solid var(--border);
  background: var(--bg-muted);
}

.cf-component-catalog__tab {
  display: grid;
  place-items: center;
  gap: 3px;
  min-height: 48px;
  padding: 5px 3px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: var(--text-2);
  cursor: pointer;
  transition: background .15s ease, color .15s ease;

  b {
    display: grid;
    place-items: center;
    width: 20px;
    height: 20px;
    color: inherit;
    font-size: 14px;
    line-height: 1;
  }

  span {
    font-size: 11px;
    font-weight: 700;
    line-height: 14px;
  }

  &:hover,
  &.is-active {
    background: var(--color-primary-light);
    color: var(--color-primary);
  }
}

.cf-component-catalog__content {
  min-width: 0;
  max-height: 640px;
  overflow-y: auto;
  padding: 10px 8px 12px;
}

.cf-component-catalog__header {
  display: grid;
  gap: 2px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border);

  strong {
    color: var(--text-1);
    font-size: 16px;
    line-height: 22px;
    font-weight: 800;
  }

  span {
    color: var(--text-2);
    font-size: 12px;
    line-height: 16px;
  }
}

.cf-component-catalog__group {
  display: grid;
  gap: 6px;
  padding: 10px 0;
  border-bottom: 1px solid var(--border);

  &:last-child {
    border-bottom: 0;
  }

  h4 {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 8px;
    margin: 0;
    color: var(--text-1);
    font-size: 13px;
    font-weight: 800;
    line-height: 18px;
  }

  h4 i {
    color: var(--text-2);
    font-style: normal;
    font-size: 14px;
  }

  p {
    margin: 0;
    color: var(--text-2);
    font-size: 12px;
    line-height: 16px;
  }
}

.cf-component-catalog__subtitle {
  margin-top: -3px !important;
}

.cf-component-catalog__items {
  display: grid;
  gap: 6px;
}

.cf-component-catalog__grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.cf-component-catalog__tile,
.cf-component-catalog__relation-card {
  position: relative;
  width: 100%;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);
  color: var(--text-1);
  text-align: left;
  cursor: pointer;
  transition: border-color .15s ease, background .15s ease, box-shadow .15s ease, transform .15s ease;

  &:hover {
    border-color: var(--color-primary);
    background: var(--color-primary-light);
    box-shadow: var(--shadow-sm);
    transform: translateY(-1px);
  }
}

.cf-component-catalog__tile {
  display: grid;
  grid-template-columns: 22px minmax(0, 1fr);
  align-items: center;
  gap: 6px;
  min-height: 40px;
  padding: 6px 8px;

  strong,
  span {
    display: block;
    min-width: 0;
  }

  strong {
    overflow: hidden;
    color: var(--text-1);
    font-size: 12px;
    line-height: 16px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  em {
    display: none;
    overflow: hidden;
    color: var(--text-2);
    font-size: 12px;
    font-style: normal;
    line-height: 17px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  small {
    position: absolute;
    top: -7px;
    right: 8px;
    padding: 1px 5px;
    border: 1px solid var(--color-warning);
    border-radius: 999px;
    background: var(--color-warning-light);
    color: var(--color-warning-text);
    font-size: 10px;
    line-height: 14px;
    white-space: nowrap;
  }
}

.cf-component-catalog__tile--suite {
  grid-template-columns: 22px minmax(0, 1fr);
  min-height: 40px;

  strong {
    font-size: 12px;
  }
}

.cf-component-catalog__drag-handle {
  position: absolute;
  top: 50%;
  left: 3px;
  transform: translateY(-50%);
  color: var(--text-3);
  cursor: grab;
  font-style: normal;
  font-size: 10px;
  opacity: 0;
  transition: opacity .15s ease;
}

.cf-component-catalog__tile:hover .cf-component-catalog__drag-handle,
.cf-component-catalog__relation-card:hover .cf-component-catalog__drag-handle {
  opacity: 1;
}

.cf-component-catalog__icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 22px;
  border-radius: 6px;
  background: var(--bg-muted);
  color: var(--text-1);
  font-size: 11px;
  font-weight: 800;
  line-height: 1;
}

.cf-component-catalog__drag-clone {
  opacity: .64;
  background: var(--color-primary-light) !important;
}

.cf-component-catalog__relation-help {
  position: relative;
  display: grid;
  gap: 5px;
  margin-top: 10px;
  padding: 10px 34px 10px 10px;
  border-radius: 8px;
  background: var(--color-info-light);

  button {
    position: absolute;
    top: 10px;
    right: 10px;
    width: 20px;
    height: 20px;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: var(--text-2);
    font-size: 18px;
    line-height: 18px;
    cursor: pointer;
  }

  strong {
    color: var(--text-1);
    font-size: 13px;
    line-height: 18px;
    font-weight: 800;
  }

  p {
    margin: 0;
    color: var(--text-2);
    font-size: 12px;
    line-height: 18px;
  }
}

.cf-component-catalog__search {
  display: grid;
  grid-template-columns: 24px minmax(0, 1fr);
  align-items: center;
  margin-top: 10px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--bg-card);

  span {
    color: var(--text-3);
    text-align: center;
    font-size: 15px;
  }

  input {
    width: 100%;
    min-width: 0;
    border: 0;
    padding: 7px 8px 7px 0;
    color: var(--text-1);
    font-size: 12px;
    outline: none;
  }
}

.cf-component-catalog__filters {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 9px;

  button {
    border: 0;
    border-radius: 7px;
    padding: 5px 9px;
    background: var(--bg-muted);
    color: var(--text-1);
    font-size: 12px;
    font-weight: 700;
    cursor: pointer;
    transition: background .15s ease, color .15s ease;

    &.is-active {
      background: var(--bg-card);
      color: var(--text-1);
      box-shadow: var(--shadow-sm);
    }
  }
}

.cf-component-catalog__relation-card {
  display: grid;
  grid-template-columns: 22px minmax(0, 1fr) auto;
  align-items: center;
  gap: 7px;
  min-height: 40px;
  padding: 6px 8px;

  span {
    overflow: hidden;
    color: var(--text-1);
    font-size: 12px;
    font-weight: 700;
    line-height: 16px;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  em {
    color: var(--text-3);
    font-size: 11px;
    font-style: normal;
    white-space: nowrap;
  }
}
</style>
