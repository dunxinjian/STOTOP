import { describe, it, expect } from 'vitest'
import { buildTriStates, PERMISSION_TRI_LABELS } from '@/components/cardflow/designer/permissionTriShared'

describe('buildTriStates 三态胶囊状态推导', () => {
  it('无锁定：三项均可点，当前值 active', () => {
    const states = buildTriStates('read', [])
    expect(states).toEqual([
      { value: 'edit', label: '可编辑', active: false, locked: false },
      { value: 'read', label: '只读', active: true, locked: false },
      { value: 'hidden', label: '隐藏', active: false, locked: false },
    ])
  })

  it('锁定 edit+hidden（路由字段场景）：锁定项 locked=true', () => {
    const states = buildTriStates('read', ['edit', 'hidden'])
    expect(states.find((s) => s.value === 'edit')?.locked).toBe(true)
    expect(states.find((s) => s.value === 'hidden')?.locked).toBe(true)
    expect(states.find((s) => s.value === 'read')?.locked).toBe(false)
  })

  it('锁定项即使等于当前值也保持 active（显示态不受锁影响）', () => {
    const states = buildTriStates('hidden', ['edit'])
    expect(states.find((s) => s.value === 'hidden')?.active).toBe(true)
  })

  it('canSelect：锁定或已激活均不可再选', () => {
    const states = buildTriStates('read', ['edit'])
    const edit = states.find((s) => s.value === 'edit')!
    const read = states.find((s) => s.value === 'read')!
    const hidden = states.find((s) => s.value === 'hidden')!
    expect(edit.locked || edit.active).toBe(true)   // 锁定 → 不可选
    expect(read.active).toBe(true)                  // 当前值 → 无需再选
    expect(hidden.locked || hidden.active).toBe(false) // 唯一可选
  })

  it('标签表固定三值', () => {
    expect(PERMISSION_TRI_LABELS).toEqual({ edit: '可编辑', read: '只读', hidden: '隐藏' })
  })
})
