import { readFileSync } from 'node:fs'
import assert from 'node:assert/strict'

const read = (path) => readFileSync(path, 'utf8')

const crmEntity = read('src/STOTOP.Module.CRM/Entities/CrmCustomer.cs')
const crmConfig = read('src/STOTOP.Module.CRM/Configurations/CrmCustomerConfiguration.cs')
const crmDto = read('src/STOTOP.Module.CRM/Dtos/CustomerDto.cs')
const customerService = read('src/STOTOP.Module.CRM/Services/CustomerService.cs')
const crmApi = read('web/src/api/crm.ts')
const auxiliaryService = read('src/STOTOP.Module.Finance/Services/AuxiliaryService.cs')
const crmSeeder = read('src/STOTOP.WebAPI/Data/Seeders/CrmSeeder.cs')
const seederHelper = read('src/STOTOP.WebAPI/Data/SeederHelper.cs')
const databaseMigrator = read('src/STOTOP.WebAPI/Data/DatabaseSeederAdapter.cs')

for (const [label, source] of [
  ['CRM customer entity', crmEntity],
  ['CRM customer EF configuration', crmConfig],
  ['CRM customer DTOs', crmDto],
  ['CRM frontend API types', crmApi],
]) {
  assert.doesNotMatch(source, /FCustomerCode|CustomerCode|FSalesmanNameText|SalesmanNameText|F源UID|F客户编号|F业务员名称原值/, `${label} should not expose retired CRM customer fields`)
}
assert.doesNotMatch(customerService, /FCustomerCode|request\.CustomerCode|entity\.FCustomerCode|FSalesmanNameText|SalesmanNameText|F源UID|F业务员名称原值/, 'CRM customer service should not map retired CRM customer fields')

assert.doesNotMatch(auxiliaryService, /\[F客户编号\]/, 'Finance auxiliary customer SQL should not read the retired CRM F客户编号 column')
assert.match(auxiliaryService, /\[F编号\]\s+AS\s+\[CustomerCode\]/, 'Finance auxiliary customer SQL should use CRM F编号 as the customer auxiliary code')

for (const column of ['F客户编号', 'F业务员名称原值', 'F源UID']) {
  assert.match(crmSeeder, new RegExp(`DropColumnSafe\\(ctx, "CRM客户", "${column}"\\)`), `CRM seeder should drop ${column}`)
}
assert.match(crmSeeder, /DropIndexSafe\(ctx,\s*"CRM客户",\s*"IX_CRM客户_客户编号"\)/, 'CRM seeder should drop the retired customer-code index before dropping the column')
assert.match(seederHelper, /sys\.index_columns/, 'DropColumnSafe should inspect indexes that depend on the retired column')
assert.match(seederHelper, /DROP INDEX/, 'DropColumnSafe should drop non-constraint indexes before dropping a retired column')
assert.match(databaseMigrator, /\("CRM",\s*ctx2\s*=>\s*CrmSeeder\.Migrate\(ctx2\)\)/, 'Database migrator should execute CRM module migrations')

console.log('CRM customer retired-field contract passed')
