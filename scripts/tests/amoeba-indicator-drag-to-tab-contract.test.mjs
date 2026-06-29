import fs from 'node:fs'
import assert from 'node:assert/strict'

const read = (p) => fs.readFileSync(new URL(`../../${p}`, import.meta.url), 'utf8')
const tpl = read('web/src/views/finance/AmoebaPLTemplate.vue')

// 1) a-tree 记录被拖项
assert.match(tpl, /@dragstart="onTreeDragStart"/, 'a-tree 应监听 dragstart 记录被拖项')
assert.match(tpl, /@dragend="onTreeDragEnd"/, 'a-tree 应监听 dragend 清状态')
assert.match(tpl, /const\s+draggingItemId\s*=\s*ref/, '应有 draggingItemId ref')

// 2) 指标 Tab 标签作为放置目标
assert.match(tpl, /@dragover="onIndicatorTabDragOver"/, '指标Tab标签应监听 dragover')
assert.match(tpl, /@drop\.prevent="onIndicatorTabDrop"/, '指标Tab标签应监听 drop')
assert.match(tpl, /indicator-tab--drop-active/, '应有放置高亮类')

// 3) 落下处理：校验指标项 + 懒创建 + 改父
assert.match(tpl, /async\s+function\s+onIndicatorTabDrop/, '应有 onIndicatorTabDrop 处理函数')
assert.match(tpl, /只有指标项可移入指标分区/, '非指标项应被拒收并提示')
assert.match(tpl, /onIndicatorTabDrop[\s\S]*ensureIndicatorSection\(\)/, '落下处理应懒创建指标分区')
assert.match(tpl, /onIndicatorTabDrop[\s\S]*updateAmoebaPLItem\(/, '落下处理应调用 updateAmoebaPLItem 改父')

console.log('Amoeba indicator drag-to-tab contracts are aligned.')
