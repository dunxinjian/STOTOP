import fs from 'node:fs'
import assert from 'node:assert/strict'

const programPath = new URL('../../src/STOTOP.WebAPI/Program.cs', import.meta.url)
const dynamicFactoryPath = new URL('../../src/STOTOP.Infrastructure/Data/DynamicDbContextFactory.cs', import.meta.url)
const oaSeederPath = new URL('../../src/STOTOP.WebAPI/Data/Seeders/OASeeder.cs', import.meta.url)
const appStorePath = new URL('../../web/src/stores/app.ts', import.meta.url)
const moduleConfigPath = new URL('../../web/src/config/modules.ts', import.meta.url)
const routesPath = new URL('../../web/src/router/routes.ts', import.meta.url)

const program = fs.readFileSync(programPath, 'utf8')
const dynamicFactory = fs.readFileSync(dynamicFactoryPath, 'utf8')
const oaSeeder = fs.readFileSync(oaSeederPath, 'utf8')
const appStore = fs.readFileSync(appStorePath, 'utf8')
const moduleConfig = fs.readFileSync(moduleConfigPath, 'utf8')
const routes = fs.readFileSync(routesPath, 'utf8')

assert.ok(
  !/using\s+STOTOP\.Module\.OA\s*;/.test(program)
    && !/AddOAModule\s*\(/.test(program)
    && !/RegisterModuleAssembly\(Assembly\.Load\("STOTOP\.Module\.OA"\)\)/.test(program),
  'WebAPI should not register OA services or OA EF configurations after OA single-doc retirement'
)

assert.ok(
  /ConfigureApplicationPartManager[\s\S]*STOTOP\.Module\.OA[\s\S]*ApplicationParts\.Remove/.test(program),
  'WebAPI controller discovery should remove the OA application part so /api/oa controllers are not exposed'
)

assert.ok(
  !/"STOTOP\.Module\.OA"/.test(dynamicFactory),
  'DynamicDbContextFactory should not load OA entity configurations'
)

assert.ok(
  /new\(3,\s*"下线OA单据体系",\s*MigrateV3\)/.test(oaSeeder)
    && /private\s+static\s+void\s+DropOaTables/.test(oaSeeder),
  'OASeeder should include a V3 retirement migration that drops the old OA table family'
)

for (const table of [
  'OA费用请款单',
  'OA费用报销单',
  'OA费用报销单_明细',
  'OA借款申请单',
  'OA对外付款单',
  'OA对外付款单_明细',
  'OA备用金申请单',
  'OA备用金报销单',
  'OA备用金报销单_明细',
  'OA备用金还款单',
  'OA备用金冲销单',
  'OA预支工资单',
  'OA费用类型',
  'OA费用类型科目映射',
  'OA审批委托',
  'OA日程事件',
  'OA日程参与者',
  'OA对话会话',
  'OA对话消息',
  'OA流程表单字段',
  'OA凭证生成记录'
]) {
  assert.ok(oaSeeder.includes(`DROP TABLE IF EXISTS [${table}]`), `OASeeder should drop ${table}`)
}

assert.ok(
  !/code:\s*'workflow'/.test(appStore)
    && !/route:\s*'\/oa\/home'/.test(appStore),
  'The top-level frontend module tabs should not expose the retired OA workflow module'
)

assert.ok(
  !/workflow:\s*\{[\s\S]*routePrefix:\s*'\/oa'/.test(moduleConfig),
  'Module config should not map the workflow module to /oa after retirement'
)

assert.ok(
  !/path:\s*'oa\/dashboard'/.test(routes),
  'Static routes should not retain the retired OA dashboard route'
)

console.log('OA module retirement contract is covered.')
