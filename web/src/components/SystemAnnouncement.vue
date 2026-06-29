<template>
  <div class="system-announcement" @click="showDetail">
    <span class="announcement-icon">📢</span>
    <div
      ref="scrollWrapperRef"
      class="announcement-scroll-wrapper"
    >
      <span
        ref="scrollTextRef"
        class="announcement-text"
        :class="{ 'marquee-active': needsMarquee }"
        :style="marqueeStyle"
      >
        <span class="marquee-segment">{{ displayText }}</span>
        <span v-if="needsMarquee" class="marquee-segment">{{ displayText }}</span>
      </span>
    </div>

    <a-modal
      v-model:open="detailVisible"
      title="系统公告"
      :footer="null"
      width="500px"
      :destroy-on-close="true"
    >
      <div v-if="announcement" class="announcement-detail">
        <h3 class="announcement-title">{{ announcement.title }}</h3>
        <p class="announcement-summary">{{ announcement.summary }}</p>
        <div class="announcement-time">{{ formatDate(announcement.createdAt) }}</div>
      </div>
      <div v-else class="announcement-empty">
        <p>暂无系统公告</p>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick, watch } from 'vue'
import dayjs from 'dayjs'

interface Announcement {
  id: number
  title: string
  summary: string
  createdAt: string
  isRead: boolean
}

const announcement = ref<Announcement | null>(null)
const detailVisible = ref(false)
const needsMarquee = ref(false)
const scrollWrapperRef = ref<HTMLElement | null>(null)
const scrollTextRef = ref<HTMLElement | null>(null)
let resizeObserver: ResizeObserver | null = null
const marqueeStyle = ref<Record<string, string>>({})

// 降级欢迎语：API 未就绪或无公告时显示
const fallbackText = computed(() => {
  const today = dayjs().format('YYYY年M月D日')
  return `今天是 ${today} · 祥和的一天`
})

const displayText = computed(() => {
  return announcement.value?.summary || fallbackText.value
})

function showDetail() {
  // 仅在存在真实公告时才弹出详情；降级欢迎语点击不响应
  if (announcement.value) {
    detailVisible.value = true
  }
}

function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  return dayjs(dateStr).format('YYYY-MM-DD HH:mm')
}

async function loadAnnouncement() {
  try {
    // TODO: 后端 API 就绪后启用真实请求
    // const res = await fetch('/api/system/announcement/latest')
    // if (res.ok) {
    //   const data = await res.json()
    //   if (data && data.id) {
    //     announcement.value = data as Announcement
    //   }
    // }
  } catch {
    // 静默降级：显示欢迎语
    announcement.value = null
  }
}

// 检测文本是否溢出容器，需要 Marquee 滚动
function checkOverflow() {
  if (!scrollWrapperRef.value || !scrollTextRef.value) return
  const wrapperWidth = scrollWrapperRef.value.clientWidth
  // 优先测量第一个 marquee-segment 的宽度（避免双副本干扰）
  const segment = scrollTextRef.value.querySelector('.marquee-segment') as HTMLElement | null
  const textWidth = segment ? segment.offsetWidth : scrollTextRef.value.scrollWidth
  const wasMarquee = needsMarquee.value
  needsMarquee.value = textWidth > wrapperWidth

  // 根据溢出量动态计算滚动时长
  if (needsMarquee.value) {
    const overflowPx = textWidth - wrapperWidth
    const duration = Math.max(4, Math.min(20, overflowPx / 80))
    marqueeStyle.value = { '--marquee-duration': `${duration}s` }
  } else {
    marqueeStyle.value = {}
  }
}

watch(displayText, () => nextTick(checkOverflow))

onMounted(() => {
  loadAnnouncement()
  nextTick(checkOverflow)

  // 监听容器尺寸变化（窗口缩放等）
  if (scrollWrapperRef.value && typeof ResizeObserver !== 'undefined') {
    resizeObserver = new ResizeObserver(checkOverflow)
    resizeObserver.observe(scrollWrapperRef.value)
  }
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
})
</script>

<style scoped lang="scss">
.system-announcement {
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  font-size: 13px;
  transition: opacity 0.2s ease;

  &:hover {
    opacity: 0.8;
  }
}

.announcement-icon {
  flex-shrink: 0;
  font-size: 14px;
  color: rgba(255, 255, 255, 0.6);
}

.announcement-scroll-wrapper {
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.announcement-text {
  display: inline-block;
  white-space: nowrap;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.98);
  font-weight: 600;
  -webkit-font-smoothing: subpixel-antialiased;

  // 默认不溢出时：省略号
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;

  // 溢出时启用 Marquee 滚动
  &.marquee-active {
    overflow: visible;
    text-overflow: unset;
    max-width: none;
    animation: announcement-marquee var(--marquee-duration, 12s) linear infinite;

    .marquee-segment {
      padding-right: 60px; // 两段文本之间留出间距，避免首尾相贴
    }
  }
}

@keyframes announcement-marquee {
  0% {
    transform: translateX(0);
  }
  100% {
    transform: translateX(-50%);
  }
}

.announcement-detail {
  .announcement-title {
    margin: 0 0 12px;
    font-size: 16px;
    font-weight: 600;
    color: rgba(0, 0, 0, 0.88);
  }

  .announcement-summary {
    margin: 0;
    font-size: 13px;
    line-height: 1.6;
    color: rgba(0, 0, 0, 0.65);
    white-space: pre-wrap;
  }

  .announcement-time {
    margin-top: 12px;
    color: #999;
    font-size: 12px;
  }
}

.announcement-empty {
  text-align: center;
  padding: 24px 0;
  color: rgba(0, 0, 0, 0.45);
  font-size: 13px;
}
</style>
