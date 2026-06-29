import fs from 'node:fs'
import assert from 'node:assert/strict'

const editorPath = new URL('../../web/src/components/cardflow/StageDefinitionEditor.vue', import.meta.url)
const flowPagePath = new URL('../../web/src/views/cardflow/FlowDefinitionEditPage.vue', import.meta.url)

const editor = fs.readFileSync(editorPath, 'utf8')
const flowPage = fs.readFileSync(flowPagePath, 'utf8')

assert.ok(
  /<StageDefinitionEditor[\s\S]*:schema-fields="state\.cardSchema"[\s\S]*:detail-schema-fields="state\.detailSchema"/.test(flowPage),
  'FlowDefinitionEditPage should pass detail schema fields into StageDefinitionEditor for per-node detail view profiles'
)

assert.ok(
  /detailSchemaFields\?:\s*SchemaFieldDefinition\[\]/.test(editor),
  'StageDefinitionEditor props should include detailSchemaFields'
)

assert.ok(
  /function\s+getDetailAccess/.test(editor) && /function\s+setDetailAccess/.test(editor),
  'StageDefinitionEditor should expose detail field access helpers'
)

assert.ok(
  /function\s+toDetailAccessKey[\s\S]*`default\.\$\{fieldKey\}`/.test(editor)
  && /stage\.viewProfile!\.detailAccess!\[key\]\s*=/.test(editor),
  'StageDefinitionEditor should bind detail field access rows to viewProfile.detailAccess using runtime-compatible keys'
)

assert.ok(
  /明细字段权限/.test(editor),
  'StageDefinitionEditor should render a visible detail field permission section'
)

assert.ok(
  /const\s+FALLBACK_OPTIONS[\s\S]*flowAdmin/.test(editor) && /editFallbackType/.test(editor),
  'StageDefinitionEditor should expose assignee fallback strategy configuration'
)

assert.ok(
  /const\s+fallback\s*=\s*\{\s*type:\s*editFallbackType\.value\s*\}/.test(editor)
  && /function\s+syncAssigneeConfig[\s\S]*JSON\.stringify\(config\)/.test(editor),
  'StageDefinitionEditor should serialize the selected assignee fallback strategy'
)

assert.ok(
  /approvalAdminUserIds:\s*number\[\]/.test(flowPage) && /approvalAdminUserIds:\s*\[\]/.test(flowPage),
  'FlowDefinitionEditPage settings should include approvalAdminUserIds'
)

assert.ok(
  /import\s+\{\s*getRoleList,\s*getUserList,\s*getUserDetail\s*\}\s+from\s+'@\/api\/system'/.test(flowPage)
  && /function\s+loadSelectedApprovalAdminUsers/.test(flowPage),
  'FlowDefinitionEditPage should import user APIs for approval admin selection and selected-user label hydration'
)

assert.ok(
  /v-model:value="state\.settings\.approvalAdminUserIds"/.test(flowPage),
  'FlowDefinitionEditPage should render an approval admin multi-user selector bound to flow settings'
)

assert.ok(
  /activeConfigTab/.test(editor)
  && /<a-tabs[\s\S]*v-model:active-key="activeConfigTab"/.test(editor)
  && /key="basic"[\s\S]*key="assignee"[\s\S]*key="view"[\s\S]*key="actions"[\s\S]*key="condition"/.test(editor),
  'StageDefinitionEditor should organize node configuration into CardFlow 2.0 tabs'
)

assert.ok(
  /function\s+getStageHealth/.test(editor)
  && /sde-node-health/.test(editor)
  && /节点健康/.test(editor),
  'StageDefinitionEditor should show per-node health status for designer-time feedback'
)

assert.ok(
  /function\s+validateCardFlow2Config/.test(flowPage)
  && /flowAdmin[\s\S]*approvalAdminUserIds/.test(flowPage)
  && /pluginRegistryId/.test(flowPage)
  && /字段权限/.test(flowPage),
  'FlowDefinitionEditPage should validate CardFlow 2.0 config before publish'
)

assert.ok(
  /selectedPreviewStageId/.test(flowPage)
  && /previewStageOptions/.test(flowPage)
  && /stagePreviewFields/.test(flowPage)
  && /<a-button[\s\S]*@click="openPreview"[\s\S]*<EyeOutlined/.test(flowPage)
  && /节点视图预览/.test(flowPage),
  'FlowDefinitionEditPage should preview the selected stage work view'
)

assert.ok(
  /审批规则/.test(flowPage) && /业务扩展/.test(flowPage),
  'FlowDefinitionEditPage should split global flow settings into approval rules and business extensions'
)

console.log('CardFlow stage config editor 2.0 contract is covered.')
