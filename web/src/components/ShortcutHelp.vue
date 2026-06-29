<template>
  <a-modal
    v-model:open="visible"
    title="键盘快捷键"
    :footer="null"
    width="500px"
    class="shortcut-help-modal"
  >
    <div v-for="group in shortcutGroups" :key="group.title" class="shortcut-group">
      <div class="group-title">{{ group.title }}</div>
      <div
        v-for="item in group.items"
        :key="item.key"
        class="shortcut-item"
      >
        <span class="shortcut-desc">{{ item.description || item.label }}</span>
        <span class="shortcut-keys">
          <kbd v-for="(part, i) in formatKey(item.key)" :key="`${part}-${i}`">{{ part }}</kbd>
        </span>
      </div>
    </div>
    <div v-if="shortcutGroups.every(g => g.items.length === 0)" class="shortcut-empty">
      暂无已注册的快捷键
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { keyboardManager } from '@/utils/keyboardManager'
import { useShortcut } from '@/composables/useShortcut'

const visible = ref(false)

function open() {
  visible.value = true
}

const shortcutGroups = computed(() => [
  {
    title: '全局快捷键',
    items: keyboardManager.getByScope('global'),
  },
  {
    title: '当前页面快捷键',
    items: keyboardManager.getByScope('page'),
  },
])

/** 将 'ctrl+k' 格式化为 ['Ctrl', 'K'] 便于 kbd 显示 */
function formatKey(key: string): string[] {
  const nameMap: Record<string, string> = {
    ctrl: 'Ctrl',
    alt: 'Alt',
    shift: 'Shift',
    meta: '⌘',
    esc: 'Esc',
    space: 'Space',
    delete: 'Del',
    backspace: '⌫',
    enter: 'Enter',
    tab: 'Tab',
    up: '↑',
    down: '↓',
    left: '←',
    right: '→',
  }
  return key.split('+').map(k => nameMap[k] ?? k.toUpperCase())
}

// 注册 Shift+? 打开帮助面板
useShortcut('shift+?', () => {
  visible.value = !visible.value
}, { label: '快捷键帮助', description: '显示快捷键列表', scope: 'global' })

defineExpose({ open })
</script>

<style lang="scss">
.shortcut-help-modal {
  .ant-modal-content {
    border-radius: 8px;
  }

  .ant-modal-header {
    border-bottom: 1px solid #f0f0f0;
  }
}
</style>

<style scoped lang="scss">
.shortcut-group {
  &:not(:last-child) {
    margin-bottom: 20px;
  }

  .group-title {
    font-size: 13px;
    font-weight: 600;
    color: rgba(0, 0, 0, 0.45);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    margin-bottom: 8px;
    padding-bottom: 6px;
    border-bottom: 1px solid #f5f5f5;
  }
}

.shortcut-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 4px;
  border-radius: 4px;
  transition: background 0.15s;

  &:hover {
    background: #fafafa;
  }

  .shortcut-desc {
    font-size: 14px;
    color: rgba(0, 0, 0, 0.85);
  }

  .shortcut-keys {
    display: flex;
    gap: 4px;
    flex-shrink: 0;
    margin-left: 12px;

    kbd {
      display: inline-block;
      padding: 2px 8px;
      font-size: 12px;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', monospace;
      color: rgba(0, 0, 0, 0.65);
      background: #fafafa;
      border: 1px solid #d9d9d9;
      border-radius: 4px;
      box-shadow: 0 1px 0 rgba(0, 0, 0, 0.08);
      line-height: 20px;
      min-width: 24px;
      text-align: center;
    }
  }
}

.shortcut-empty {
  padding: 32px 16px;
  text-align: center;
  color: rgba(0, 0, 0, 0.45);
  font-size: 14px;
}
</style>
