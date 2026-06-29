import fs from 'node:fs'
import assert from 'node:assert/strict'

const editorPath = new URL('../../web/src/components/cardflow/StageDefinitionEditor.vue', import.meta.url)
const editor = fs.readFileSync(editorPath, 'utf8')

const userServicePath = new URL('../../src/STOTOP.Module.System/Services/UserService.cs', import.meta.url)
const userService = fs.readFileSync(userServicePath, 'utf8')

assert.ok(
  /interface\s+UserOption[\s\S]*userName:\s*string[\s\S]*orgName\?:\s*string/.test(editor),
  'StageDefinitionEditor should keep raw userName and orgName separately from the display label'
)

assert.ok(
  /function\s+formatUserOptionLabel[\s\S]*\$\{name\}\s*\/\s*\$\{orgName\}/.test(editor),
  'StageDefinitionEditor should format fixed assignee options as "{姓名} / {部门}"'
)

assert.ok(
  /label:\s*formatUserOptionLabel\(u\)/.test(editor),
  'StageDefinitionEditor should use the formatted label for searched users'
)

assert.ok(
  /userName:\s*opt\?\.userName\s*\|\|/.test(editor),
  'StageDefinitionEditor should save the raw user name instead of the formatted dropdown label'
)

assert.ok(
  /show-search[\s\S]*@search="onUserSearch"[\s\S]*option-filter-prop="label"/.test(editor),
  'The fixed assignee select should enable fuzzy search against option labels'
)

assert.ok(
  /UserOrganizations[\s\S]*Organization\.FName\.Contains\(keyword\)/.test(userService),
  'UserService keyword search should include the user organization name so department searches work'
)

console.log('CardFlow stage fixed-assignee user search behavior is covered.')
