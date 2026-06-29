<script setup lang="ts">
import { computed } from 'vue'
import { BgColorsOutlined, CheckOutlined } from '@ant-design/icons-vue'
import { useThemeStore, THEME_PRESETS, type ThemePreset } from '@/stores/theme'

const themeStore = useThemeStore()

const presetKeys = Object.keys(THEME_PRESETS) as ThemePreset[]

const currentPreset = computed(() => themeStore.themeConfig.themePreset)

function shades(p: ThemePreset) {
  return THEME_PRESETS[p].shades
}
</script>

<template>
  <a-popover trigger="click" placement="bottomRight" :arrow="false" overlay-class-name="theme-switcher-pop">
    <button class="theme-switcher-trigger" title="主题色" aria-label="切换主题色">
      <BgColorsOutlined />
    </button>
    <template #content>
      <div class="ts-panel">
        <div class="ts-title">主题色</div>
        <div class="ts-presets">
          <button
            v-for="key in presetKeys"
            :key="key"
            class="ts-preset"
            :class="{ active: currentPreset === key }"
            @click="themeStore.setPreset(key)"
          >
            <span class="ts-preset-bars">
              <span class="b" :style="{ height: '58%', background: shades(key).primary }" />
              <span class="b" style="height: 100%" />
              <span class="b" style="height: 44%" />
              <span class="b" style="height: 76%" />
            </span>
            <span class="ts-preset-foot">
              <span class="ts-dot" :style="{ background: shades(key).primary }" />
              <span class="ts-name">{{ THEME_PRESETS[key].label }}</span>
              <CheckOutlined v-if="currentPreset === key" class="ts-check" />
            </span>
          </button>
        </div>
      </div>
    </template>
  </a-popover>
</template>

<style scoped lang="scss">
.theme-switcher-trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border: none;
  background: transparent;
  border-radius: var(--radius-md);
  color: var(--text-2);
  font-size: 17px;
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.theme-switcher-trigger:hover {
  background: var(--bg-muted);
  color: var(--text-1);
}

.ts-panel {
  width: 264px;
  padding: 2px;
}

.ts-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-1);
  margin-bottom: 12px;
}

.ts-presets {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 9px;
}

.ts-preset {
  text-align: left;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-lg);
  padding: 9px;
  background: var(--bg-card);
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;

  &:hover {
    border-color: var(--color-primary);
  }

  &.active {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 1px var(--color-primary);
  }
}

.ts-preset-bars {
  display: flex;
  align-items: flex-end;
  gap: 4px;
  height: 26px;
  margin-bottom: 8px;

  .b {
    flex: 1;
    border-radius: 2px;
    background: var(--bg-muted);
  }
}

.ts-preset-foot {
  display: flex;
  align-items: center;
  gap: 6px;
}

.ts-dot {
  width: 12px;
  height: 12px;
  border-radius: 3px;
  flex-shrink: 0;
}

.ts-name {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-1);
}

.ts-check {
  margin-left: auto;
  font-size: 13px;
  color: var(--color-primary);
}
</style>
