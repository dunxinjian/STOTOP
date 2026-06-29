import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')
const exists = (file) => fs.existsSync(path.join(root, file))

for (const file of [
  'web/src/components/cardflow/designer/FlowStateCanvas.vue',
  'web/src/components/cardflow/designer/RouteRuleCardEditor.vue',
  'web/src/components/cardflow/designer/DynamicApprovalPolicyEditor.vue',
  'web/src/components/cardflow/designer/PathPreviewPanel.vue',
  'web/src/components/cardflow/designer/RuleHealthPanel.vue',
  'web/src/components/cardflow/designer/CardComponentCatalog.vue',
  'web/src/components/cardflow/designer/CardComponentConfigDrawer.vue',
  'web/src/components/cardflow/designer/StageComponentViewEditor.vue',
]) {
  assert.equal(exists(file), true, `${file} should exist`)
}

const flowEdit = read('web/src/views/cardflow/FlowDefinitionEditPage.vue')
for (const token of [
  'FlowStateCanvas',
  'RouteRuleCardEditor',
  'DynamicApprovalPolicyEditor',
  'PathPreviewPanel',
  'RuleHealthPanel',
  'CardComponentCatalog',
  'CardComponentConfigDrawer',
  'routes: []',
  'dynamicPolicies: []',
  'dv.routes',
  'dv.dynamicPolicies',
  'routes: buildRouteRequests()',
  'dynamicPolicies: buildDynamicPolicyRequests()',
  'previewFlowDraftPath',
  "key: 'cardView'",
  "title: '卡片视图'",
  '@reorder-stages',
  '@connect-route',
  'reorderStagesByCanvas',
  'connectRouteFromCanvas',
]) {
  assert(flowEdit.includes(token), `FlowDefinitionEditPage should include ${token}`)
}
assert(
  flowEdit.includes('tb-version-context'),
  'FlowDefinitionEditPage should show draft/published version context in the compact editor toolbar',
)
assert(
  !flowEdit.includes('<!-- 草稿版本提示横幅 -->') && !flowEdit.includes('<a-alert'),
  'FlowDefinitionEditPage should not reserve a full-width draft version alert above the step content',
)
for (const token of [
  'tb-history-group',
  'tb-history-btn--undo',
  'tb-history-btn--redo',
  'RollbackOutlined',
  'ArrowRightOutlined',
  'aria-label="撤销"',
  'aria-label="重做"',
  '<span>撤销</span>',
  '<span>重做</span>',
]) {
  assert(flowEdit.includes(token), `FlowDefinitionEditPage should make undo/redo distinguishable with ${token}`)
}
assert(
  !flowEdit.includes('class="tb-icon"'),
  'FlowDefinitionEditPage should not render undo/redo as ambiguous icon-only toolbar buttons',
)
assert(
  !flowEdit.includes('UndoOutlined') && !flowEdit.includes('RedoOutlined'),
  'FlowDefinitionEditPage should avoid visually similar circular undo/redo icons in the editor toolbar',
)
const fieldStepIndex = flowEdit.indexOf('<!-- 步骤 2：字段设计 -->')
const cardViewStepIndex = flowEdit.indexOf('<!-- 步骤 3：卡片视图 -->')
const nodeChainStepIndex = flowEdit.indexOf('<!-- 步骤 4：节点链 -->')
const flowConfigStepIndex = flowEdit.indexOf('<!-- 步骤 5：流程配置 -->')
const previewStepIndex = flowEdit.indexOf('<!-- 步骤 6：预演与发布校验 -->')
assert(fieldStepIndex >= 0, 'FlowDefinitionEditPage should keep field design as step 2')
assert(cardViewStepIndex > fieldStepIndex, 'FlowDefinitionEditPage should place card view after field design')
assert(nodeChainStepIndex > cardViewStepIndex, 'FlowDefinitionEditPage should place node chain after card view')
assert(flowConfigStepIndex > nodeChainStepIndex, 'FlowDefinitionEditPage should place flow config after node chain')
assert(previewStepIndex > flowConfigStepIndex, 'FlowDefinitionEditPage should place preview as step 6')
const componentDesignerIndex = flowEdit.indexOf('fdef-card-view-workbench')
assert(
  componentDesignerIndex > cardViewStepIndex && componentDesignerIndex < nodeChainStepIndex,
  'Card view component designer should live inside the standalone card view step',
)
const cardViewStepMarkup = flowEdit.slice(cardViewStepIndex, nodeChainStepIndex)
for (const token of [
  'fdef-step__title',
  'fdef-step__mark',
  'fdef-step__hint-inline',
]) {
  assert(
    !flowEdit.includes(token),
    `FlowDefinitionEditPage should not render redundant in-step header token ${token}`,
  )
}
assert(
  !cardViewStepMarkup.includes('即时预览'),
  'Standalone card view step should not show a separate instant preview panel',
)
assert(
  !cardViewStepMarkup.includes('fdef-component-inspector__preview'),
  'Card view step should not reserve inspector space for a duplicate preview panel',
)
for (const token of [
  'fdef-card-canvas__stage',
  'fdef-card-canvas__surface',
  'fdef-card-canvas__surface-header',
  'fdef-card-canvas-item__runtime',
  'fdef-card-canvas-item__inline-actions',
  'fdef-card-canvas-item__icon-btn',
  'CopyOutlined',
  'DeleteOutlined',
  'aria-label="复制组件"',
  'aria-label="删除组件"',
  'aria-label="预览运行态卡片"',
  'openCardRuntimePreview',
  'cardRuntimePreviewOpen',
  'cardRuntimePreviewMode',
  'cardRuntimePreviewComponents',
  'runtimePreviewSampleData',
  'runtimePreviewDetailRows',
  'fdef-runtime-preview-modal',
  'fdef-runtime-preview__feature-list',
  '组件功能',
  ':mode="cardRuntimePreviewMode"',
  '@update:model-value="updateRuntimePreviewSampleData"',
  '@update:detail-rows="updateRuntimePreviewDetailRows"',
  'canvasRuntimeComponentsFor',
  'CardComponentRenderer',
]) {
  assert(cardViewStepMarkup.includes(token), `Card view canvas should render runtime editing preview token ${token}`)
}
assert(
  !cardViewStepMarkup.includes('编辑态预览'),
  'Card view canvas should use an explicit runtime preview action instead of a passive edit-preview tag',
)
for (const token of [
  'width: 375px',
  'max-width: 100%',
  'height: fit-content',
  'grid-template-columns: repeat(2, minmax(0, 1fr))',
  'fdef-card-canvas-item--full',
  'fdef-card-canvas-item--half',
  'fdef-card-canvas-item--compact',
  'width: fit-content',
  'min-width: 128px',
  'right: 0',
  'flex: 0 0 auto',
]) {
  assert(flowEdit.includes(token), `Card view canvas should keep runtime card proportion token ${token}`)
}
for (const token of [
  'grid-template-columns: 300px minmax(560px, 1fr) 320px',
  'gap: 0',
  'margin-top: 0',
  'padding: 0',
  'border-radius: 0',
  'min-height: calc(100vh - 210px)',
  'border-right: 1px solid #e6ebe8',
  'border-left: 1px solid #e6ebe8',
  'padding: 16px 12px 24px',
]) {
  assert(flowEdit.includes(token), `Card view workbench should use compact DingTalk-like three-panel layout token ${token}`)
}
const cardViewStepStyleBlock = flowEdit.match(/\.fdef-step--card-view\s*\{[\s\S]*?\}/)?.[0] || ''
for (const token of [
  'padding: 0',
  'border: 0',
  'min-height: 100%',
  'overflow: hidden',
]) {
  assert(cardViewStepStyleBlock.includes(token), `Card view step should remove outer page gap token ${token}`)
}
assert(
  !flowEdit.includes('fdef-card-canvas-item__chrome'),
  'Card view component actions should use inline icon buttons instead of the old floating toolbar chrome',
)
assert(
  !flowEdit.includes('left: calc(100% + 8px)') && !flowEdit.includes('top: -32px'),
  'Card view component actions should not float outside the selected component bounds',
)
const selectedCanvasItemBlock = flowEdit.match(/\.fdef-card-canvas-item--selected\s*\{[\s\S]*?\}/)?.[0] || ''
assert(
  selectedCanvasItemBlock && !/margin(?:-[a-z]+)?:/.test(selectedCanvasItemBlock),
  'Card view selected state should not change component margins or make the canvas drift on click',
)
const canvasEmptyBlock = flowEdit.match(/\.fdef-card-canvas__empty\s*\{[\s\S]*?\}/)?.[0] || ''
assert(
  canvasEmptyBlock.includes('grid-column: 1 / -1') && canvasEmptyBlock.includes('width: 100%'),
  'Card view empty drop zone should span the full runtime card width instead of occupying a half-row grid cell',
)
assert(
  !flowEdit.includes('min-height: 560px'),
  'Card view surface should grow with actual card content instead of forcing a tall blank phone canvas',
)
assert(
  !flowEdit.includes(`grid-template-columns: 1fr;
  gap: 0;`),
  'Card view canvas should not force all configured components into a single visual width',
)

const flowCanvas = read('web/src/components/cardflow/designer/FlowStateCanvas.vue')
for (const token of [
  '@vue-flow/core',
  '@vue-flow/background',
  '@vue-flow/controls',
  '@vue-flow/minimap',
  'VueFlow',
  'Handle',
  'Position',
  'flowNodes',
  'flowEdges',
  'node-drag-stop',
  'connect',
  '条件分支',
  '默认分支',
  '动态审批',
  '拖动节点调整主链顺序',
  '从节点连接点拖出条件分支',
  'syncFlowNodeLayout',
  'syncFlowNodeSelection',
  'preservedPosition',
]) {
  assert(flowCanvas.includes(token), `FlowStateCanvas should provide graph UX token ${token}`)
}
assert(
  !flowCanvas.includes('props.stages, props.dynamicPolicies, props.selectedType, props.selectedKey'),
  'FlowStateCanvas should not rebuild node layout when only selection changes',
)

const routeEditor = read('web/src/components/cardflow/designer/RouteRuleCardEditor.vue')
assert(routeEditor.includes('ConditionBuilder'), 'route rule editor should use the visual ConditionBuilder')
assert(!routeEditor.includes('conditionJson"') && !routeEditor.includes("conditionJson'"), 'route editor should not expose conditionJson as a raw input label')
for (const token of ['默认分支', '优先级', '目标节点', '流转条件']) {
  assert(routeEditor.includes(token), `route editor should expose ${token}`)
}

const conditionBuilder = read('web/src/components/cardflow/ConditionBuilder.vue')
for (const operator of ['empty', 'notEmpty', 'inOrgChain']) {
  assert(conditionBuilder.includes(operator), `condition builder should expose ${operator}`)
}
for (const label of ['为空', '不为空', '属于组织链']) {
  assert(conditionBuilder.includes(label), `condition builder should expose ${label}`)
}

const dynamicEditor = read('web/src/components/cardflow/designer/DynamicApprovalPolicyEditor.vue')
for (const label of ['按金额矩阵加签', '按组织链逐级审批', '按发起人角色决定审批人', '按费用类型指定财务 BP', '按字段人员作为审批人', '指定人员']) {
  assert(dynamicEditor.includes(label), `dynamic policy editor should expose ${label}`)
}
for (const token of ['amountField', 'ranges', 'roleCode', 'fieldKey', 'mapping', 'fallbackJson']) {
  assert(dynamicEditor.includes(token), `dynamic policy editor should serialize ${token}`)
}
assert(dynamicEditor.includes('ConditionBuilder'), 'dynamic policy editor should use visual conditions')

const pathPreview = read('web/src/components/cardflow/designer/PathPreviewPanel.vue')
for (const token of ['previewFlowDraftPath', '发起人', '组织', '金额', '费用类型', '引用请款', '引用借款']) {
  assert(pathPreview.includes(token), `path preview panel should include ${token}`)
}
for (const token of ['disabled?: boolean', '预演条件未就绪', ':disabled="disabled"']) {
  assert(pathPreview.includes(token), `path preview panel should support disabled preview state ${token}`)
}

const healthPanel = read('web/src/components/cardflow/designer/RuleHealthPanel.vue')
for (const token of ['默认分支', '规则重叠', '死路节点', '循环路径', '无法到达', '处理人']) {
  assert(healthPanel.includes(token), `rule health panel should check ${token}`)
}

const catalog = read('web/src/components/cardflow/designer/CardComponentCatalog.vue')
for (const token of [
  'catalogTabs',
  "key: 'components'",
  "key: 'suites'",
  "key: 'relations'",
  '组件',
  '组件套件',
  '关联',
  'cf-component-catalog--compact',
  'cf-component-catalog__rail',
  'cf-component-catalog__tab',
  'cf-component-catalog__content',
  ':title="element.hint"',
]) {
  assert(catalog.includes(token), `component catalog should provide DingTalk-like three-lane navigation token ${token}`)
}
for (const token of [
  'grid-template-columns: 46px minmax(0, 1fr)',
  'max-height: 640px',
  'min-height: 40px',
  'grid-template-columns: 22px minmax(0, 1fr)',
  'position: absolute',
  'text-overflow: ellipsis',
  'white-space: nowrap',
  'line-height: 16px',
  'display: none',
]) {
  assert(catalog.includes(token), `component catalog should keep dense sidebar token ${token}`)
}
for (const group of ['布局控件', '基础控件', '增强控件', '业务控件', '高级控件']) {
  assert(catalog.includes(group), `component catalog should expose ${group}`)
}
for (const token of ['可添加的卡片视图组件', '不会新增字段', '添加到审批卡片视图']) {
  assert(catalog.includes(token), `component catalog should explain ${token}`)
}
for (const token of ['限时免费', '免费']) {
  assert(!catalog.includes(token), `component catalog should not expose commercialization label ${token}`)
}
for (const token of [
  '分栏',
  '单行输入框',
  '多行输入框',
  '数字输入框',
  '单选框',
  '多选框',
  '日期',
  '日期区间',
  '说明文字',
  '身份证',
  '电话',
  '级联/分类',
  'AI控件',
  '图片',
  '明细/表格',
  '金额',
  '附件',
  '手写签名',
  '外部联系人',
  '联系人',
  '部门',
  '行业通讯录部门',
  '地点',
  '计算公式',
  '关联审批单',
  '省市区',
  '评分',
  '发票',
  '客户',
  '收款账户',
  '预算申请',
  '关联合同',
  '工程项目',
  '通用文字识别',
  '身份证识别',
  '流水号',
]) {
  assert(catalog.includes(token), `component catalog should include DingTalk-like control ${token}`)
}
for (const token of [
  '假勤管理',
  '来自考勤打卡',
  '请假/调休',
  '补卡',
  '加班',
  '外出',
  '出差',
  '换班',
  '商旅出行套件',
  '人事管理',
  '来自智能人事',
  '转正套件',
  '离职套件',
  '离职和交接套件',
  '入职套件',
  '工资发放',
  '财税管理',
  '来自智能财务',
  '批量付款',
  '报销套件',
  '付款套件',
  '收款套件',
  '开票申请套件',
  '采购-beta',
  '机票超标',
  '火车票退票',
  '法务管理',
  '来自智能合同',
  '合同审批套件',
  '用印申请',
  '合同归档申请',
  '客户管理',
  '来自客户管理',
  '客户拜访签到',
  'componentSuite',
]) {
  assert(catalog.includes(token), `component catalog should expose DingTalk-like component suite token ${token}`)
}
for (const token of [
  '关联表单',
  '什么是关联表单？',
  'relationSearch',
  'relationFilter',
  '流程表单',
  '数据表单',
  '来自审批',
  '事务申请',
  '设备维修登记',
  '热敏客户申请',
  '用章申请(石申通)',
  '运输部服务单',
  '司机绩效申报',
  '事故申报',
  '商旅报销',
  'cf-component-catalog__relation-help',
  'cf-component-catalog__relation-card',
]) {
  assert(catalog.includes(token), `component catalog should expose relation panel token ${token}`)
}

for (const token of [
  '字段 = 数据结构',
  '拖拽组件',
  '编辑组件',
  '所见即所得',
  'cardHeader',
  'patchCardHeader',
  'selectCardHeader',
  'cardHeaderTitle',
  'cardHeaderSubtitle',
  '卡片头部属性',
  '主标题来源',
  '副标题来源',
  '头部不是组件',
  'header: state.cardHeader',
  'fdef-card-canvas__surface-header--selected',
  'fdef-layout-toggle',
  'fdef-layout-toggle__btn',
  "width: 'full'",
  "width: 'half'",
  "width: 'compact'",
  'fdef-card-canvas__surface',
  'fdef-card-view-workbench',
  'fdef-card-canvas',
  'fdef-component-inspector',
  'draggable',
  'selectedCardComponent',
  'patchCardComponent',
  '预演与发布校验',
  'CardComponentRenderer',
  'previewRuntimeComponents',
]) {
  assert(flowEdit.includes(token), `FlowDefinitionEditPage should include ${token}`)
}
assert(
  !flowEdit.includes('button-style="solid"'),
  'Card component layout width should use an inline segmented control instead of a dropdown-like form control',
)
for (const token of [
  'previewReady',
  'previewReadinessItems',
  'previewBlockingItems',
  'previewToolbarPlaceholder',
  'previewCoverageStats',
  'previewConfigWarnings',
  'goPreviewReadinessStep',
  'reloadFlowDefinition',
  'fdef-preview-controlbar',
  'fdef-preview-workbench',
  'fdef-preview-card-pane',
  'fdef-preview-path-pane',
  'fdef-preview-check-pane',
  'fdef-preview-not-ready',
  '流程定义还没有加载成功',
  '预演还未就绪',
  '去字段设计',
  '去卡片视图',
  '去节点链',
  '当前节点无可见组件',
  ':disabled="!previewReady"',
]) {
  assert(flowEdit.includes(token), `FlowDefinitionEditPage preview workbench should include ${token}`)
}
for (const token of [
  'fdef-preview__left',
  'fdef-preview__right',
  'fdef-preview-section--path',
]) {
  assert(!flowEdit.includes(token), `Preview workbench should remove old stacked preview layout token ${token}`)
}
assert(
  !flowEdit.includes('fdef-card-view-guide'),
  'Card view step should not reserve vertical space for guide cards',
)

for (const token of [
  'vuedraggable',
  'drag-clone',
  'cloneCatalogComponent',
  'dragHandle',
  '拖拽到中间卡片画布',
  'cf-component-catalog__icon',
  'cf-component-catalog__grid',
  '分组标题',
  '文本说明',
  'sectionTitle',
]) {
  assert(catalog.includes(token), `component catalog should support drag design token ${token}`)
}
assert(
  !catalog.includes('卡片主标题') && !catalog.includes('主标题组件'),
  'component catalog should not treat the card container title as a draggable content component',
)

const renderer = read('web/src/components/cardflow/runtime/CardComponentRenderer.vue')
for (const token of [
  "component.type === 'sectionTitle'",
  "component.type === 'placeholderControl'",
  "component.type === 'imageList'",
  "component.type === 'signature'",
  "component.type === 'rating'",
  "component.type === 'ocrText'",
  "component.type === 'componentSuite'",
  "component.type === 'relationLookup'",
  'formatBusinessValue',
  'cf-runtime-placeholder',
  'cf-runtime-media-placeholder',
  'cf-runtime-rating',
  'cf-runtime-suite',
  'cf-runtime-relation-lookup',
  'cf-runtime-section-title',
  'component.props?.description',
]) {
  assert(renderer.includes(token), `runtime component renderer should support section title token ${token}`)
}

const schemaUtils = read('web/src/utils/cardflowSchema.ts')
for (const token of [
  'CardHeaderConfig',
  'defaultCardHeaderConfig',
  'parseCardSchemaHeader',
  'header?: CardHeaderConfig',
]) {
  assert(schemaUtils.includes(token), `cardflow schema utils should preserve header config token ${token}`)
}

const cardFlowPanel = read('web/src/components/cardflow/CardFlowPanel.vue')
for (const token of [
  'parseCardSchemaHeader',
  'cardHeaderConfig',
  'cardHeaderTitle',
  'cardHeaderSubtitle',
  'cardHeaderShowStatus',
]) {
  assert(cardFlowPanel.includes(token), `runtime CardFlow panel should apply card header config token ${token}`)
}

const configDrawer = read('web/src/components/cardflow/designer/CardComponentConfigDrawer.vue')
for (const section of ['数据绑定', '显示', '输入', '校验', '权限', '条件可见', '联动', '汇总', '统计']) {
  assert(configDrawer.includes(section), `component config drawer should expose ${section}`)
}

const stageComponentView = read('web/src/components/cardflow/designer/StageComponentViewEditor.vue')
for (const access of ['可见', '隐藏', '只读', '可编辑', '必填', '脱敏']) {
  assert(stageComponentView.includes(access), `stage component view editor should expose ${access}`)
}

const types = read('web/src/types/cardflow.ts')
for (const token of ['CardComponentDefinition', 'CardHeaderConfig', 'CardComponentAccessRule', 'StageRouteRuleRequest', 'DynamicStagePolicyRequest']) {
  assert(types.includes(token), `cardflow types should include ${token}`)
}

console.log('CardFlow flow editor P3 UI contract is covered.')
