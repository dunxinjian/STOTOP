<template>
  <div class="nav-chain">
    <a-dropdown
      v-for="(tab, index) in navChainStore.chain"
      :key="tab.path"
      :trigger="['contextmenu']"
    >
      <div
        class="nav-tab"
        :class="{ active: index === navChainStore.activeIndex }"
        @click="onTabClick(index)"
        @mouseenter="hoverIndex = index"
        @mouseleave="hoverIndex = -1"
      >
        <span class="nav-tab__label">{{ tab.label }}</span>
        <span
          v-if="navChainStore.chain.length > 1 && (index === navChainStore.activeIndex || hoverIndex === index)"
          class="nav-tab__close"
          @click.stop="onClose(index)"
        >
          <CloseOutlined />
        </span>
      </div>
      <template #overlay>
        <a-menu @click="(info: any) => onContextMenu(info.key as string, index)">
          <a-menu-item key="close" :disabled="navChainStore.chain.length <= 1">关闭当前</a-menu-item>
          <a-menu-item key="closeOther" :disabled="navChainStore.chain.length <= 1">关闭其他</a-menu-item>
          <a-menu-item key="closeRight" :disabled="index >= navChainStore.chain.length - 1">关闭右侧</a-menu-item>
        </a-menu>
      </template>
    </a-dropdown>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { CloseOutlined } from '@ant-design/icons-vue'
import { useNavChainStore, markNavSource } from '@/stores/navChain'

const router = useRouter()
const navChainStore = useNavChainStore()
const hoverIndex = ref<number>(-1)

function onTabClick(index: number) {
  if (index === navChainStore.activeIndex) return
  markNavSource('internal')
  navChainStore.switchTo(index)
  const tab = navChainStore.chain[index]
  if (tab) {
    router.push(tab.path)
  }
}

function onClose(index: number) {
  if (navChainStore.chain.length <= 1) return
  const redirectPath = navChainStore.removeTab(index)
  if (redirectPath) {
    router.push(redirectPath)
  }
}

function onContextMenu(key: string, index: number) {
  switch (key) {
    case 'close':
      onClose(index)
      break
    case 'closeOther': {
      const keep = navChainStore.chain[index]
      if (!keep) return
      navChainStore.resetChain(keep)
      if (navChainStore.activeIndex !== 0) {
        navChainStore.switchTo(0)
      }
      router.push(keep.path)
      break
    }
    case 'closeRight': {
      const removeCount = navChainStore.chain.length - 1 - index
      for (let i = 0; i < removeCount; i++) {
        navChainStore.chain.splice(index + 1, 1)
      }
      if (navChainStore.activeIndex > index) {
        navChainStore.switchTo(index)
        const tab = navChainStore.chain[index]
        if (tab) router.push(tab.path)
      }
      break
    }
  }
}
</script>

<style scoped lang="scss">
.nav-chain {
  display: flex;
  align-items: stretch;
  gap: 4px;
  min-width: 0;
  overflow: hidden;
  height: 100%;
}

.nav-tab {
  display: flex;
  align-items: center;
  height: 100%;
  border-radius: 0;
  padding: 0 12px;
  max-width: 220px;
  cursor: pointer;
  background: transparent;
  border-bottom: 2px solid transparent;
  transition: background 0.2s, color 0.2s, border-color 0.2s;
  user-select: none;

  &:hover {
    background: var(--bg-muted);

    .nav-tab__label {
      color: var(--text-1);
    }
  }

  &.active {
    background: transparent;
    border-bottom-color: var(--color-primary);

    .nav-tab__label {
      color: var(--text-1);
      font-weight: 500;
    }

    .nav-tab__close {
      color: var(--text-2);
    }
  }

  &__label {
    font-size: 13px;
    color: var(--text-2);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    line-height: 1;
  }

  &__close {
    display: flex;
    align-items: center;
    justify-content: center;
    margin-left: 6px;
    font-size: 10px;
    color: var(--text-3);
    flex-shrink: 0;
    width: 15px;
    height: 15px;
    border-radius: var(--radius-sm);
    transition: background 0.15s, color 0.15s;

    &:hover {
      background: var(--bg-muted);
      color: var(--text-1);
    }
  }
}
</style>
