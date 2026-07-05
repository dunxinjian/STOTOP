import { ref } from 'vue'
import { getUserList } from '@/api/system'

export interface UserSearchOption {
  label: string
  value: number
  name: string
  orgName?: string
  raw?: any
}

/**
 * 人员远端搜索（防抖 + 已选项固定回显）。
 * 抽自 CardFlowPanel 转交/加签弹窗已验证的模式，供卡片人员字段、条件值、发起人预演等复用。
 */
export function useUserSearch(options?: { pageSize?: number; debounceMs?: number }) {
  const pageSize = options?.pageSize ?? 50
  const debounceMs = options?.debounceMs ?? 300

  const userOptions = ref<UserSearchOption[]>([])
  const loading = ref(false)
  let timer: ReturnType<typeof setTimeout> | null = null
  // 已选项池：远端换页搜索后仍能回显已选值
  const pinned = ref<UserSearchOption[]>([])

  function dedupe(list: UserSearchOption[]) {
    return list.filter((o, i, arr) => arr.findIndex((x) => x.value === o.value) === i)
  }

  async function load(keyword = '') {
    loading.value = true
    try {
      const res: any = await getUserList({ keyword, pageIndex: 1, pageSize })
      const items = res?.items || res?.data?.items || (Array.isArray(res) ? res : [])
      const next: UserSearchOption[] = items
        .map((u: any) => {
          const name = u.realName || u.userName || u.username || `#${u.id}`
          return {
            label: [name, u.orgName].filter(Boolean).join(' / '),
            value: Number(u.id),
            name,
            orgName: u.orgName,
            raw: u,
          }
        })
        .filter((o: UserSearchOption) => Number.isFinite(o.value) && o.value > 0)
      userOptions.value = dedupe([...pinned.value, ...next])
    } catch {
      // 搜索失败不阻断上层操作
    } finally {
      loading.value = false
    }
  }

  function search(keyword: string) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => load(keyword), debounceMs)
  }

  /** 固定一个已选项，确保后续搜索换页不丢回显 */
  function pin(opt: UserSearchOption | null | undefined) {
    if (!opt || !Number.isFinite(opt.value)) return
    if (!pinned.value.some((o) => o.value === opt.value)) pinned.value.push(opt)
    if (!userOptions.value.some((o) => o.value === opt.value)) {
      userOptions.value = [opt, ...userOptions.value]
    }
  }

  return { userOptions, loading, load, search, pin }
}
