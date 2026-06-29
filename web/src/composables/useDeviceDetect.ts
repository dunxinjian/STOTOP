import { computed, ref, onMounted, onUnmounted } from 'vue'

export function useDeviceDetect() {
  const windowWidth = ref(window.innerWidth)

  function onResize() {
    windowWidth.value = window.innerWidth
  }

  onMounted(() => window.addEventListener('resize', onResize))
  onUnmounted(() => window.removeEventListener('resize', onResize))

  const isMobile = computed(() => {
    const ua = navigator.userAgent
    const mobileUA = /Android|iPhone|iPad|iPod|Mobile/i.test(ua)
    const narrowScreen = windowWidth.value < 768
    return mobileUA || narrowScreen
  })

  return { isMobile, windowWidth }
}
