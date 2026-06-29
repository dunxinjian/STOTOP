import { ref, watch, type Ref } from 'vue'

/**
 * 创建一个防抖 ref —— 当 source 变化时，延迟 delay 毫秒后才更新返回的 ref。
 * 适用于搜索输入等场景，避免每次按键都触发过滤/请求。
 */
export function useDebouncedRef<T>(source: Ref<T>, delay = 300): Ref<T> {
  const debounced = ref(source.value) as Ref<T>
  let timer: ReturnType<typeof setTimeout> | null = null

  watch(source, (val) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => {
      debounced.value = val
    }, delay)
  })

  return debounced
}

/**
 * 创建一个防抖函数 —— 连续调用时只在最后一次调用后 delay 毫秒执行。
 */
export function useDebounceFn<T extends (...args: any[]) => any>(fn: T, delay = 300): T {
  let timer: ReturnType<typeof setTimeout> | null = null
  return ((...args: any[]) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => fn(...args), delay)
  }) as unknown as T
}
