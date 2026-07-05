export type CardComponentTier = 'P0' | 'P1' | 'deferred'

export type CardComponentCapability = {
  key: string
  type: string
  title: string
  tier: CardComponentTier
  publishable: boolean
  supportedBindings: string[]
  configSections: string[]
  defaultProps: Record<string, any>
  defaultLayout?: Record<string, any>
  templateOnly?: boolean
  experimental?: boolean
  requiresRuntimeIntegration?: boolean
  unsupportedReason?: string
}

const COMMON_FIELD_CONFIG = ['display', 'binding', 'fieldFormat', 'validation', 'permission', 'visibility']
const COMMON_BUSINESS_CONFIG = ['display', 'binding', 'businessStatus', 'permission', 'visibility']

export const CARD_COMPONENT_CAPABILITIES: Record<string, CardComponentCapability> = {
  text: {
    key: 'text',
    type: 'text',
    title: '单行输入框',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: COMMON_FIELD_CONFIG,
    defaultProps: {
      controlKind: 'text',
      placeholder: '请输入',
      emptyText: '-',
      maxLength: 100,
    },
  },
  textarea: {
    key: 'textarea',
    type: 'textarea',
    title: '多行输入框',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: COMMON_FIELD_CONFIG,
    defaultProps: {
      controlKind: 'textarea',
      placeholder: '请输入',
      emptyText: '-',
      rows: 3,
      maxLength: 500,
    },
    defaultLayout: { width: 'full' },
  },
  number: {
    key: 'number',
    type: 'number',
    title: '数字输入框',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: COMMON_FIELD_CONFIG,
    defaultProps: {
      controlKind: 'number',
      placeholder: '请输入数字',
      emptyText: '-',
      precision: 0,
    },
  },
  money: {
    key: 'money',
    type: 'money',
    title: '金额',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField', 'detailSummary'],
    configSections: ['display', 'binding', 'fieldFormat', 'businessStatus', 'validation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'money',
      placeholder: '请输入金额',
      emptyText: '-',
      currency: 'CNY',
      currencySymbol: '¥',
      precision: 2,
      thousandSeparator: true,
    },
  },
  date: {
    key: 'date',
    type: 'date',
    title: '日期',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: COMMON_FIELD_CONFIG,
    defaultProps: {
      controlKind: 'date',
      placeholder: '请选择日期',
      emptyText: '-',
      displayFormat: 'YYYY-MM-DD',
    },
  },
  dateRange: {
    key: 'dateRange',
    type: 'dateRange',
    title: '日期区间',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: COMMON_FIELD_CONFIG,
    defaultProps: {
      controlKind: 'dateRange',
      placeholder: '请选择日期区间',
      emptyText: '-',
      startPlaceholder: '开始日期',
      endPlaceholder: '结束日期',
      displayFormat: 'YYYY-MM-DD',
    },
    defaultLayout: { width: 'full' },
  },
  radio: {
    key: 'radio',
    type: 'radio',
    title: '单选框',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: ['display', 'binding', 'options', 'validation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'radio',
      placeholder: '请选择',
      emptyText: '-',
      displayMode: 'segmented',
      options: [
        { label: '选项一', value: 'option1' },
        { label: '选项二', value: 'option2' },
      ],
    },
  },
  checkbox: {
    key: 'checkbox',
    type: 'checkbox',
    title: '多选框',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: ['display', 'binding', 'options', 'validation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'checkbox',
      placeholder: '请选择',
      emptyText: '-',
      displayMode: 'tags',
      options: [
        { label: '选项一', value: 'option1' },
        { label: '选项二', value: 'option2' },
      ],
    },
  },
  idCard: {
    key: 'idCard',
    type: 'idCard',
    title: '身份证',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: ['display', 'binding', 'fieldFormat', 'validation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'idCard',
      placeholder: '请输入身份证号',
      emptyText: '-',
      maskPattern: 'idCard',
      maxLength: 18,
    },
  },
  phone: {
    key: 'phone',
    type: 'phone',
    title: '电话',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: ['display', 'binding', 'fieldFormat', 'validation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'phone',
      placeholder: '请输入手机号',
      emptyText: '-',
      maskPattern: 'phone',
      maxLength: 20,
    },
  },
  attachment: {
    key: 'attachment',
    type: 'attachment',
    title: '附件',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: ['display', 'binding', 'attachment', 'validation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'attachment',
      placeholder: '上传附件',
      emptyText: '暂无附件',
      maxCount: 9,
      accept: '.pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg',
      displayMode: 'list',
    },
    defaultLayout: { width: 'full' },
  },
  detailTable: {
    key: 'detailTable',
    type: 'detailTable',
    title: '明细/表格',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['detailTable'],
    configSections: ['display', 'binding', 'detailTable', 'aggregation', 'validation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'detailTable',
      emptyText: '暂无明细',
      showSummary: true,
    },
    defaultLayout: { width: 'full' },
  },
  amountSummary: {
    key: 'amountSummary',
    type: 'amountSummary',
    title: '金额摘要',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField', 'detailSummary'],
    configSections: COMMON_BUSINESS_CONFIG,
    defaultProps: {
      controlKind: 'amountSummary',
      amountField: 'amount',
      currencySymbol: '¥',
      precision: 2,
      showBadge: true,
    },
    defaultLayout: { width: 'full' },
  },
  budgetStatus: {
    key: 'budgetStatus',
    type: 'budgetStatus',
    title: '预算申请',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['detailSummary', 'cardField'],
    configSections: COMMON_BUSINESS_CONFIG,
    defaultProps: {
      controlKind: 'budgetStatus',
      statusField: 'budgetStatus',
      amountField: 'amount',
      showBadge: true,
    },
    defaultLayout: { width: 'full' },
  },
  invoiceStatus: {
    key: 'invoiceStatus',
    type: 'invoiceStatus',
    title: '发票',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField', 'detailSummary'],
    configSections: COMMON_BUSINESS_CONFIG,
    defaultProps: {
      controlKind: 'invoiceStatus',
      statusField: 'invoiceStatus',
      statusText: '待校验',
      severity: 'info',
      showBadge: true,
    },
  },
  loanOffset: {
    key: 'loanOffset',
    type: 'loanOffset',
    title: '借款冲抵',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['detailSummary', 'relation'],
    configSections: COMMON_BUSINESS_CONFIG,
    defaultProps: {
      controlKind: 'loanOffset',
      amountField: 'offsetAmount',
      balanceField: 'loanBalance',
      currencySymbol: '¥',
    },
    defaultLayout: { width: 'full' },
  },
  paymentInfo: {
    key: 'paymentInfo',
    type: 'paymentInfo',
    title: '收款账户',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField', 'relation'],
    configSections: COMMON_BUSINESS_CONFIG,
    defaultProps: {
      controlKind: 'paymentInfo',
      accountField: 'bankAccount',
      amountField: 'paidAmount',
      currencySymbol: '¥',
    },
    defaultLayout: { width: 'full' },
  },
  riskAlert: {
    key: 'riskAlert',
    type: 'riskAlert',
    title: '风险提示',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['cardField', 'detailSummary', 'snapshot'],
    configSections: COMMON_BUSINESS_CONFIG,
    defaultProps: {
      controlKind: 'riskAlert',
      statusField: 'riskLevel',
      statusText: '待检查',
      severity: 'warning',
      showBadge: true,
    },
    defaultLayout: { width: 'full' },
  },
  relationCards: {
    key: 'relationCards',
    type: 'relationCards',
    title: '关联审批单',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['relation'],
    configSections: ['display', 'binding', 'relation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'relationCards',
      relationKind: 'workflow',
      relationType: 'approval',
      displayFields: ['name', 'amount', 'status'],
      emptyText: '暂无关联单据',
    },
    defaultLayout: { width: 'full' },
  },
  relationLookup: {
    key: 'relationLookup',
    type: 'relationLookup',
    title: '关联表单',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['relation'],
    configSections: ['display', 'binding', 'relation', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'relationLookup',
      relationKind: 'workflow',
      relationType: 'approval',
      displayFields: ['name', 'status'],
      emptyText: '请选择关联数据',
    },
    defaultLayout: { width: 'full' },
  },
  routeDecision: {
    key: 'routeDecision',
    type: 'routeDecision',
    title: '流转说明',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['snapshot'],
    configSections: ['display', 'binding', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'routeDecision',
      snapshotType: 'routeDecision',
      emptyText: '暂无流转命中记录',
    },
    defaultLayout: { width: 'full' },
  },
  dynamicApprover: {
    key: 'dynamicApprover',
    type: 'dynamicApprover',
    title: '动态审批说明',
    tier: 'P0',
    publishable: true,
    supportedBindings: ['snapshot'],
    configSections: ['display', 'binding', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'dynamicApprover',
      snapshotType: 'dynamicApprover',
      emptyText: '暂无动态审批插入记录',
    },
    defaultLayout: { width: 'full' },
  },
  sectionTitle: {
    key: 'sectionTitle',
    type: 'sectionTitle',
    title: '分组标题',
    tier: 'P1',
    publishable: true,
    supportedBindings: ['static'],
    configSections: ['display', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'sectionTitle',
      description: '用于区分一组字段',
    },
    defaultLayout: { width: 'full' },
  },
  textBlock: {
    key: 'textBlock',
    type: 'textBlock',
    title: '说明文字',
    tier: 'P1',
    publishable: true,
    supportedBindings: ['static'],
    configSections: ['display', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'textBlock',
      body: '这里填写说明文字',
      tone: 'plain',
    },
    defaultLayout: { width: 'full' },
  },
  imageList: {
    key: 'imageList',
    type: 'imageList',
    title: '图片',
    tier: 'P1',
    publishable: true,
    supportedBindings: ['cardField', 'static'],
    configSections: ['display', 'binding', 'attachment', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'imageList',
      maxCount: 9,
      accept: '.png,.jpg,.jpeg,.webp',
      displayMode: 'grid',
      emptyText: '暂无图片',
    },
    defaultLayout: { width: 'full' },
  },
  signature: {
    key: 'signature',
    type: 'signature',
    title: '手写签名',
    tier: 'P1',
    publishable: true,
    supportedBindings: ['cardField', 'static'],
    configSections: ['display', 'binding', 'attachment', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'signature',
      placeholder: '签名后显示在这里',
      emptyText: '待签名',
    },
    defaultLayout: { width: 'full' },
  },
  rating: {
    key: 'rating',
    type: 'rating',
    title: '评分',
    tier: 'P1',
    publishable: true,
    supportedBindings: ['cardField', 'static'],
    configSections: ['display', 'binding', 'rating', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'rating',
      max: 5,
      allowHalf: false,
      emptyText: '未评分',
    },
  },
  placeholderControl: {
    key: 'placeholderControl',
    type: 'placeholderControl',
    title: '业务选择控件',
    tier: 'P1',
    publishable: true,
    supportedBindings: ['cardField'],
    configSections: ['display', 'binding', 'options', 'fieldFormat', 'permission', 'visibility'],
    defaultProps: {
      controlKind: 'select',
      placeholder: '请选择',
      emptyText: '-',
      options: [],
    },
  },
  aiAssist: {
    key: 'aiAssist',
    type: 'placeholderControl',
    title: 'AI 控件',
    tier: 'deferred',
    publishable: false,
    experimental: true,
    requiresRuntimeIntegration: true,
    supportedBindings: ['static'],
    configSections: ['display'],
    unsupportedReason: 'AI 控件需要提示词、调用权限、结果回写和人工确认链路，暂不作为可发布组件。',
    defaultProps: {
      controlKind: 'aiAssist',
      componentStatus: 'deferred',
      emptyText: 'AI 控件暂未启用',
    },
  },
  serialNumber: {
    key: 'serialNumber',
    type: 'placeholderControl',
    title: '流水号',
    tier: 'deferred',
    publishable: false,
    requiresRuntimeIntegration: true,
    supportedBindings: ['static'],
    configSections: ['display'],
    unsupportedReason: '流水号需要编号规则、生成时机和重复校验，暂不作为可发布组件。',
    defaultProps: {
      controlKind: 'serialNumber',
      componentStatus: 'deferred',
      emptyText: '保存后生成',
    },
  },
  ocrText: {
    key: 'ocrText',
    type: 'ocrText',
    title: 'OCR 识别',
    tier: 'deferred',
    publishable: false,
    experimental: true,
    requiresRuntimeIntegration: true,
    supportedBindings: ['static'],
    configSections: ['display'],
    unsupportedReason: 'OCR 识别需要文件识别服务、识别结果回写和失败兜底，暂不作为可发布组件。',
    defaultProps: {
      controlKind: 'ocrText',
      componentStatus: 'deferred',
      emptyText: 'OCR 识别暂未启用',
    },
  },
  componentSuite: {
    key: 'componentSuite',
    type: 'componentSuite',
    title: '组件套件',
    tier: 'deferred',
    publishable: false,
    templateOnly: true,
    requiresRuntimeIntegration: true,
    supportedBindings: ['static'],
    configSections: ['display'],
    unsupportedReason: '套件需要展开成一组明确组件后才能发布，当前仅作为快速建模模板。',
    defaultProps: {
      controlKind: 'componentSuite',
      componentStatus: 'template',
    },
  },
  formula: {
    key: 'formula',
    type: 'formula',
    title: '计算公式',
    tier: 'deferred',
    publishable: false,
    experimental: true,
    requiresRuntimeIntegration: true,
    supportedBindings: ['detailSummary'],
    configSections: ['display', 'binding'],
    unsupportedReason: '公式需要后端表达式引擎和审计快照，暂不开放发布。',
    defaultProps: {
      controlKind: 'formula',
      componentStatus: 'deferred',
    },
  },
  columnLayout: {
    key: 'columnLayout',
    type: 'columnLayout',
    title: '分栏',
    tier: 'deferred',
    publishable: false,
    templateOnly: true,
    requiresRuntimeIntegration: true,
    supportedBindings: ['static'],
    configSections: ['display'],
    unsupportedReason: '分栏需要嵌套组件容器和节点权限继承，当前先不进入发布能力。',
    defaultProps: {
      controlKind: 'columnLayout',
      componentStatus: 'template',
    },
  },
}

// 运行时 type 同为 placeholderControl、仅靠 controlKind 区分的暂缓能力项（capability key 与 controlKind 同名）。
const DEFERRED_PLACEHOLDER_CONTROL_KINDS = ['formula', 'columnLayout', 'aiAssist', 'serialNumber', 'ocrText', 'componentSuite']

export function resolveComponentCapability(type?: string | null, props?: Record<string, any> | null): CardComponentCapability {
  const capabilityKey = typeof props?.capabilityKey === 'string' ? props.capabilityKey : null
  if (capabilityKey && CARD_COMPONENT_CAPABILITIES[capabilityKey]) {
    return CARD_COMPONENT_CAPABILITIES[capabilityKey]
  }
  // 无 capabilityKey 的存量占位组件（历史数据/手改）按 controlKind 回退到对应能力项，
  // 封死 aiAssist/serialNumber/ocrText 等落到 publishable 的 placeholderControl 兜底绕过发布门禁。
  if (
    type === 'placeholderControl'
    && typeof props?.controlKind === 'string'
    && DEFERRED_PLACEHOLDER_CONTROL_KINDS.includes(props.controlKind)
    && CARD_COMPONENT_CAPABILITIES[props.controlKind]
  ) {
    return CARD_COMPONENT_CAPABILITIES[props.controlKind]
  }
  return CARD_COMPONENT_CAPABILITIES[type || ''] || CARD_COMPONENT_CAPABILITIES.placeholderControl
}

export function buildCapabilityProps(type: string, props: Record<string, any> = {}): Record<string, any> {
  const capability = resolveComponentCapability(type, props)
  const componentStatus = capability.publishable ? 'ready' : capability.templateOnly ? 'template' : 'deferred'
  return {
    ...capability.defaultProps,
    ...props,
    capabilityKey: capability.key,
    publishable: capability.publishable,
    componentTier: capability.tier,
    componentStatus,
    templateOnly: !!capability.templateOnly,
    experimental: !!capability.experimental,
    requiresRuntimeIntegration: !!capability.requiresRuntimeIntegration,
    unsupportedReason: capability.unsupportedReason || props.unsupportedReason || null,
  }
}

export function isComponentPublishable(type?: string | null, props?: Record<string, any> | null) {
  return resolveComponentCapability(type, props).publishable
}
