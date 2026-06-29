import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import vm from 'node:vm'
import { fileURLToPath } from 'node:url'
import ts from 'typescript'

const scriptDir = path.dirname(fileURLToPath(import.meta.url))
const webRoot = path.resolve(scriptDir, '..')

function loadTsModule(relativePath, requireMap = {}) {
  const filename = path.join(webRoot, relativePath)
  const source = fs.readFileSync(filename, 'utf8')
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
const pasteMatrixParser = loadTsModule('src/views/express/shared/pasteMatrixParser.ts', {
  './priceCellParser': priceCellParser,
})

const {
  normalizePastedMatrixLines,
  parsePastedPriceCellValue,
  parsePastedSegmentHeader,
} = pasteMatrixParser
const plain = (value) => value == null ? value : JSON.parse(JSON.stringify(value))

assert.deepEqual(plain(parsePastedPriceCellValue('3.15', 0, 31)), {
  segmentIndex: 0,
  provinceId: 31,
  basePrice: 3.15,
  firstWeight: 0,
  continuePrice: 0,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})

assert.deepEqual(plain(parsePastedPriceCellValue('3.15+0.3', 10, 31)), {
  segmentIndex: 10,
  provinceId: 31,
  basePrice: 3.15,
  firstWeight: 0,
  continuePrice: 0.3,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})

assert.deepEqual(plain(parsePastedPriceCellValue('3.15+(w-10)*0.5', 11, 31)), {
  segmentIndex: 11,
  provinceId: 31,
  basePrice: 3.15,
  firstWeight: 10,
  continuePrice: 0.5,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})

assert.deepEqual(plain(parsePastedPriceCellValue('3.15+(w-10)*0.5/0.5', 12, 31)), {
  segmentIndex: 12,
  provinceId: 31,
  basePrice: 3.15,
  firstWeight: 10,
  continuePrice: 0.5,
  continueStep: 0.5,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})

assert.deepEqual(
  plain(parsePastedPriceCellValue('4.15+0.8', 12, 31, {
    segmentIndex: 9,
    provinceId: 20,
    basePrice: 1,
    firstWeight: 0,
    continuePrice: 0,
    continueStep: 1,
    roundingMethodOverride: 5,
    truncParamOverride: 0.1,
    ceilParamOverride: null,
  })),
  {
    segmentIndex: 12,
    provinceId: 31,
    basePrice: 4.15,
    firstWeight: 0,
    continuePrice: 0.8,
    continueStep: 1,
    roundingMethodOverride: 5,
    truncParamOverride: 0.1,
    ceilParamOverride: null,
  },
)

assert.equal(parsePastedPriceCellValue('3.15+', 0, 31), null)
assert.equal(parsePastedPriceCellValue('', 0, 31), null)

const samplePasteText = `
	0-0.5KG	0.5-1KG	1-2KG	2-3KG	3-4kg	4-5kg	5-6kg	6-7kg	7-8kg	8-9kg	9-10kg	10-15kg	15-30kg	30-999kg
上海	1.45	2.1	2.15	2.6	4.05	4.75	5.25	5.75	5.95	6.15	6.45	3.65+0.3	3.65+0.35	3.65+0.65
江苏	0.95	1.6	1.65	2.1	3.55	4.25	4.75	5.25	5.45	5.65	5.95	3.15+0.3	3.15+0.35	3.15+0.65
浙江	0.95	1.6	1.65	2.1	3.55	4.25	4.75	5.25	5.45	5.65	5.95	3.15+0.3	3.15+0.35	3.15+0.65
安徽	0.95	1.6	1.65	2.1	3.55	4.25	4.75	5.25	5.45	5.65	5.95	3.15+0.3	3.15+0.35	3.15+0.65
湖北	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.6
湖南	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
江西	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
广东	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
山东	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
福建	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
河南	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
河北	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
北京	1.95	2.6	2.65	3.1	5.25	5.55	6.45	7.05	7.95	9.05	10.05	4.15+0.8	4.15+1.2	4.15+2
天津	0.95	1.6	1.65	2.1	4.25	4.55	5.45	6.05	6.95	8.05	9.05	3.15+0.8	3.15+1.2	3.15+1.9
陕西	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+2.8
山西	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+2.8
重庆	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+2.8
贵州	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+2.8
辽宁	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+2.8
广西	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+3.5
四川	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+3.5
吉林	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+3.5
黑龙	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+3.5
云南	0.95	1.6	1.65	2.1	4.55	4.85	5.75	6.35	7.25	8.35	9.35	3.15+1.2	3.15+1.5	3.15+3.5
甘肃	2.05	2.45	2.65	3.15	3.15	3.15	3.15+2	3.15+2	3.15+2	3.15+2	3.15+2	3.15+3.3	3.15+3.3	3.15+3.3
海南	2.05	2.45	2.65	3.15	3.15	3.15	3.15+2	3.15+2	3.15+2	3.15+2	3.15+2	3.15+3.3	3.15+3.3	3.15+3.3
内蒙	2.05	2.45	2.65	3.15	3.15	3.15	3.15+2	3.15+2	3.15+2	3.15+2	3.15+2	3.15+3.3	3.15+3.3	3.15+3.3
宁夏	2.05	2.45	2.65	3.15	3.15	3.15	3.15+2	3.15+2	3.15+2	3.15+2	3.15+2	3.15+3.3	3.15+3.3	3.15+3.3
青海	2.05	2.45	2.65	3.15	3.15	3.15	3.15+2	3.15+2	3.15+2	3.15+2	3.15+2	3.15+3.3	3.15+3.3	3.15+3.3
新疆	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10
西藏	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10	3.15+10
`

const provinceNames = [
  '上海', '江苏', '浙江', '安徽', '湖北', '湖南', '江西', '广东',
  '山东', '福建', '河南', '河北', '北京', '天津', '陕西', '山西',
  '重庆', '贵州', '辽宁', '广西', '四川', '吉林', '黑龙江', '云南',
  '甘肃', '海南', '内蒙古', '宁夏', '青海', '新疆', '西藏',
]
const matrix = provinceNames.map((provinceName, index) => ({
  provinceId: index + 1,
  provinceName,
  prices: {},
}))
const lines = normalizePastedMatrixLines(samplePasteText)
const headerCells = lines[0].split('\t')
const parsedSegments = headerCells.slice(1).map(parsePastedSegmentHeader).filter(Boolean)
const unmatched = []
const invalidCells = []
let matched = 0
let filled = 0

for (let i = 1; i < lines.length; i++) {
  const cells = lines[i].split('\t')
  const provinceName = cells[0].trim()
  const row = matrix.find(r =>
    r.provinceName === provinceName ||
    r.provinceName.startsWith(provinceName) ||
    provinceName.startsWith(r.provinceName)
  )
  if (!row) {
    unmatched.push(provinceName)
    continue
  }

  matched++

  for (let col = 0; col < parsedSegments.length; col++) {
    const parsed = parsePastedPriceCellValue(cells[col + 1]?.trim(), col, row.provinceId, row.prices[col])
    if (!parsed) {
      invalidCells.push(`${provinceName}/${headerCells[col + 1]}=${cells[col + 1] ?? ''}`)
      continue
    }
    row.prices[col] = plain(parsed)
    filled++
  }
}

const byName = new Map(matrix.map(row => [row.provinceName, row]))
assert.equal(parsedSegments.length, 14)
assert.equal(lines.length - 1, 31)
assert.equal(matched, 31)
assert.deepEqual(unmatched, [])
assert.deepEqual(invalidCells, [])
assert.equal(filled, 31 * 14)
assert.deepEqual(byName.get('上海').prices[11], {
  segmentIndex: 11,
  provinceId: 1,
  basePrice: 3.65,
  firstWeight: 0,
  continuePrice: 0.3,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})
assert.deepEqual(byName.get('北京').prices[13], {
  segmentIndex: 13,
  provinceId: 13,
  basePrice: 4.15,
  firstWeight: 0,
  continuePrice: 2,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})
assert.deepEqual(byName.get('黑龙江').prices[13], {
  segmentIndex: 13,
  provinceId: 23,
  basePrice: 3.15,
  firstWeight: 0,
  continuePrice: 3.5,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})
assert.deepEqual(byName.get('内蒙古').prices[6], {
  segmentIndex: 6,
  provinceId: 27,
  basePrice: 3.15,
  firstWeight: 0,
  continuePrice: 2,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})
assert.deepEqual(byName.get('新疆').prices[0], {
  segmentIndex: 0,
  provinceId: 30,
  basePrice: 3.15,
  firstWeight: 0,
  continuePrice: 10,
  continueStep: 1,
  roundingMethodOverride: null,
  truncParamOverride: null,
  ceilParamOverride: null,
})

console.log('paste price cell parser tests passed')
