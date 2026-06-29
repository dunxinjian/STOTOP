import fs from 'node:fs'
import assert from 'node:assert/strict'

const read = (p) => fs.readFileSync(new URL(`../../${p}`, import.meta.url), 'utf8')
const tpl = read('web/src/views/finance/AmoebaPLTemplate.vue')
const report = read('web/src/views/finance/AmoebaPL.vue')

// 1) "+指标分区"手动新增入口已移除
assert.doesNotMatch(tpl, /@click="handleAddIndicatorSection"/, '"+指标分区"按钮应已移除')
assert.doesNotMatch(tpl, /function handleAddIndicatorSection/, 'handleAddIndicatorSection 应被 ensureIndicatorSection 取代')

// 2) treeData 不再把指标分区全局置顶到每个 Tab（"出现在所有Tab"根因）
assert.doesNotMatch(tpl, /指标分区始终放在树最顶部/, 'treeData 不应再全局置顶指标分区')

// 3) 固定"指标分区"特别 Tab：哨兵常量 + 计算节点 + 特别样式类
assert.match(tpl, /const\s+INDICATOR_TAB_ID\s*=\s*-1/, '应定义固定Tab哨兵 id')
assert.match(tpl, /const\s+indicatorTabNode\s*=\s*computed/, '应有 indicatorTabNode 计算属性')
assert.match(tpl, /class="dir-tab-label indicator-tab"/, '固定指标Tab应有 indicator-tab 特别样式类')

// 4) 懒创建：首次在指标Tab新增指标项时创建根级指标分区
assert.match(tpl, /async\s+function\s+ensureIndicatorSection/, '应有 ensureIndicatorSection 懒创建函数')
assert.match(tpl, /isIndicatorSection:\s*true/, 'ensureIndicatorSection 应以 isIndicatorSection=true 建根级分区')

// 5) tabNodes 仍排除指标分区根节点，避免与固定Tab重复
assert.match(tpl, /!i\.isIndicatorSection/, 'tabNodes 应继续排除指标分区根节点')

// 6) 报表左栏不回归：仍读取全局 indicatorSections 并保留左栏面板
assert.match(report, /indicatorSections\.value\s*=\s*res\?\.indicatorSections/, '报表应继续读取全局 indicatorSections')
assert.match(report, /class="indicator-panel"/, '报表应保留左栏指标面板')

console.log('Amoeba indicator fixed-tab contracts are aligned.')
