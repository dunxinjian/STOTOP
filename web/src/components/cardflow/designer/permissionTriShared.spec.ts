import { describe, it, expect } from 'vitest'
import { buildTriStates, PERMISSION_TRI_LABELS } from '@/components/cardflow/designer/permissionTriShared'

describe('buildTriStates 权限胶囊状态推导', () => {
  it('无锁定：四项均可点，当前值 active', () => {
    const states = buildTriStates('readonly', [])
    expect(states).toEqual([
      { value: 'editable', label: '可编辑', active: false, locked: false },
      { value: 'readonly', label: '只读', active: true, locked: false },
      { value: 'masked', label: '脱敏', active: false, locked: false },
      { value: 'hidden', label: '隐藏', active: false, locked: false },
    ])
  })

  it('锁定 editable+hidden（路由字段场景）：锁定项 locked=true', () => {
    const states = buildTriStates('readonly', ['editable', 'hidden'])
    expect(states.find((s) => s.value === 'editable')?.locked).toBe(true)
    expect(states.find((s) => s.value === 'hidden')?.locked).toBe(true)
    expect(states.find((s) => s.value === 'readonly')?.locked).toBe(false)
  })

  it('锁定项即使等于当前值也保持 active（显示态不受锁影响）', () => {
    const states = buildTriStates('hidden', ['editable'])
    expect(states.find((s) => s.value === 'hidden')?.active).toBe(true)
  })

  it('values 参数可裁剪展示子集（如矩阵紧凑列只出编/读/隐）', () => {
    const states = buildTriStates('readonly', [], ['editable', 'readonly', 'hidden'])
    expect(states.map((s) => s.value)).toEqual(['editable', 'readonly', 'hidden'])
  })

  it('标签表固定四值', () => {
    expect(PERMISSION_TRI_LABELS).toEqual({ editable: '可编辑', readonly: '只读', masked: '脱敏', hidden: '隐藏' })
  })
})
