import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import vm from 'node:vm'
import { fileURLToPath } from 'node:url'
import ts from 'typescript'

const scriptDir = path.dirname(fileURLToPath(import.meta.url))
const webRoot = path.resolve(scriptDir, '..')

function read(relativePath) {
  return fs.readFileSync(path.join(webRoot, relativePath), 'utf8')
}

function loadTsModule(relativePath, requireMap = {}) {
  const filename = path.join(webRoot, relativePath)
  const source = read(relativePath)
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
    fileName: filename,
  })

  const module = { exports: {} }
  const context = {
    console,
    exports: module.exports,
    module,
    require: (specifier) => {
      if (specifier in requireMap) return requireMap[specifier]
      throw new Error(`Unexpected import ${specifier} while loading ${relativePath}`)
    },
  }

  vm.runInNewContext(outputText, context, { filename })
  return module.exports
}

const priceCellParser = loadTsModule('src/views/express/shared/priceCellParser.ts')
const costPriceCell = loadTsModule('src/views/express/cost-plan/utils/costPriceCell.ts', {
  '../../shared/priceCellParser': priceCellParser,
})
const plain = (value) => value == null ? value : JSON.parse(JSON.stringify(value))

assert.deepEqual(plain(costPriceCell.parseCostCellInput('0')), {
  basePrice: 0,
  firstWeight: 0,
  continuePrice: 0,
  continueStep: 1,
})

assert.deepEqual(plain(costPriceCell.parseCostCellInput('0+0.8')), {
  basePrice: 0,
  firstWeight: 0,
  continuePrice: 0.8,
  continueStep: 1,
})

assert.deepEqual(plain(costPriceCell.parseCostCellInput('0+(w-10)*0.5')), {
  basePrice: 0,
  firstWeight: 10,
  continuePrice: 0.5,
  continueStep: 1,
})

assert.equal(costPriceCell.formatCostCell({ basePrice: 0, firstWeight: 0, continuePrice: 0, continueStep: 1 }), '0')
assert.equal(costPriceCell.formatCostCell({ basePrice: 0, firstWeight: 0, continuePrice: 0.8, continueStep: 1 }), '0+0.8')
assert.equal(costPriceCell.formatCostCell({ basePrice: 0, firstWeight: 10, continuePrice: 0.5, continueStep: 1 }), '0+(w-10)*0.5')

assert.deepEqual(
  plain(costPriceCell.mergeCostCellInput(
    {
      basePrice: 9,
      firstWeight: 1,
      continuePrice: 2,
      continueStep: 1,
      roundingMethodOverride: 5,
      truncParamOverride: 0.1,
      ceilParamOverride: null,
    },
    '0+(w-10)*0.5',
  )),
  {
    basePrice: 0,
    firstWeight: 10,
    continuePrice: 0.5,
    continueStep: 1,
    roundingMethodOverride: 5,
    truncParamOverride: 0.1,
    ceilParamOverride: null,
  },
)

const costMatrixTable = read('src/views/express/cost-plan/components/CostMatrixTable.vue')
assert.ok(costMatrixTable.includes('mergeCostCellInput'), 'CostMatrixTable should parse full cost price formulas and preserve overrides')
assert.ok(costMatrixTable.includes('formatCostCell'), 'CostMatrixTable should render firstWeight/continueStep instead of dropping them')

const costItemDetail = read('src/views/express/cost-plan/CostItemDetail.vue')
assert.ok(costItemDetail.includes('FixedPriceCostMatrix'), 'Cost item province matrix should use the formula-aware matrix editor')
assert.ok(costItemDetail.includes('firstWeight: cell.firstWeight,'), 'Cost item matrix save should preserve firstWeight')
assert.ok(costItemDetail.includes('continueStep: cell.continueStep,'), 'Cost item matrix save should preserve continueStep')

console.log('cost plan price cell parser tests passed')
