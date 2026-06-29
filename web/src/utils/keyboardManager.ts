export interface ShortcutConfig {
  key: string           // 如 'ctrl+k', 'ctrl+n', 'delete', 'esc'
  label: string         // 显示名称，如 '保存'
  description: string   // 描述，如 '保存当前表单'
  scope: 'global' | 'page'  // 全局或页面级
  handler: (e: KeyboardEvent) => void
}

class KeyboardManager {
  private shortcuts: Map<string, ShortcutConfig> = new Map()
  private bound = false

  /**
   * 注册快捷键，返回注销函数
   */
  register(config: ShortcutConfig): () => void {
    const normalizedKey = this.normalizeKey(config.key)
    if (this.shortcuts.has(normalizedKey)) {
      console.warn(`[KeyboardManager] 快捷键冲突: "${config.key}" 已被 "${this.shortcuts.get(normalizedKey)!.label}" 占用，将被覆盖`)
    }
    this.shortcuts.set(normalizedKey, { ...config, key: normalizedKey })
    return () => this.unregister(normalizedKey)
  }

  /**
   * 注销快捷键
   */
  unregister(key: string): void {
    const normalizedKey = this.normalizeKey(key)
    this.shortcuts.delete(normalizedKey)
  }

  /**
   * 获取所有快捷键配置
   */
  getAll(): ShortcutConfig[] {
    return Array.from(this.shortcuts.values())
  }

  /**
   * 按作用域获取快捷键
   */
  getByScope(scope: 'global' | 'page'): ShortcutConfig[] {
    return this.getAll().filter(s => s.scope === scope)
  }

  /**
   * 主动触发已注册的快捷键 handler（用于按钮点击等场景）
   * 返回是否成功触发
   */
  trigger(key: string): boolean {
    const normalizedKey = this.normalizeKey(key)
    const config = this.shortcuts.get(normalizedKey)
    if (!config) return false
    try {
      // 提供一个合成事件，handler 内若不使用则忽略
      config.handler(new KeyboardEvent('keydown', { key: normalizedKey }))
    } catch (err) {
      console.warn(`[KeyboardManager] 触发快捷键 "${key}" 失败:`, err)
      return false
    }
    return true
  }

  /**
   * 初始化：绑定全局 keydown 监听
   */
  init(): void {
    if (this.bound) return
    document.addEventListener('keydown', this.handleKeydown)
    this.bound = true
  }

  /**
   * 销毁：移除全局监听
   */
  destroy(): void {
    document.removeEventListener('keydown', this.handleKeydown)
    this.bound = false
    this.shortcuts.clear()
  }

  /**
   * 将按键字符串标准化为统一格式
   */
  private normalizeKey(key: string): string {
    return key
      .toLowerCase()
      .split('+')
      .map(k => k.trim())
      .sort((a, b) => {
        const order = ['ctrl', 'meta', 'alt', 'shift']
        const ai = order.indexOf(a)
        const bi = order.indexOf(b)
        if (ai !== -1 && bi !== -1) return ai - bi
        if (ai !== -1) return -1
        if (bi !== -1) return 1
        return a.localeCompare(b)
      })
      .join('+')
  }

  /**
   * 从 KeyboardEvent 解析出按键组合字符串
   */
  private parseKey(e: KeyboardEvent): string {
    const parts: string[] = []
    if (e.ctrlKey || e.metaKey) parts.push('ctrl')
    if (e.altKey) parts.push('alt')
    if (e.shiftKey) parts.push('shift')

    const key = e.key.toLowerCase()
    // 避免把修饰键本身加到组合里
    if (!['control', 'meta', 'alt', 'shift'].includes(key)) {
      // 特殊键映射
      const keyMap: Record<string, string> = {
        escape: 'esc',
        ' ': 'space',
        arrowup: 'up',
        arrowdown: 'down',
        arrowleft: 'left',
        arrowright: 'right',
        '/': '/',
        '?': '?',
      }
      parts.push(keyMap[key] ?? key)
    }

    return this.normalizeKey(parts.join('+'))
  }

  /**
   * 全局 keydown 事件处理器
   */
  private handleKeydown = (e: KeyboardEvent): void => {
    // 在输入框中不拦截（除非是带 ctrl/meta 的组合键）
    const target = e.target as HTMLElement
    const isInputElement = target.tagName === 'INPUT'
      || target.tagName === 'TEXTAREA'
      || target.tagName === 'SELECT'
      || target.isContentEditable
    if (isInputElement && !e.ctrlKey && !e.metaKey && !e.altKey) {
      return
    }

    const parsed = this.parseKey(e)
    const config = this.shortcuts.get(parsed)
    if (config) {
      e.preventDefault()
      config.handler(e)
    }
  }
}

export const keyboardManager = new KeyboardManager()
