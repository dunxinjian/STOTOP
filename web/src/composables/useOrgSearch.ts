import { ref } from 'vue'
import { getOrganizationTree, type OrgTreeNode } from '@/api/system'

export interface OrgSearchOption {
  label: string
  value: number
  name: string
  raw?: OrgTreeNode
}

// 组织树一次拉取扁平化后进程内缓存（无专用搜索端点，客户端按关键字过滤）
let cachedFlat: OrgSearchOption[] | null = null
let loadPromise: Promise<OrgSearchOption[]> | null = null

function flatten(nodes: OrgTreeNode[] | undefined, acc: OrgSearchOption[] = []): OrgSearchOption[] {
  for (const n of nodes || []) {
    const id = Number(n.id)
    if (Number.isFinite(id) && id > 0) {
      acc.push({ label: n.name || `#${id}`, value: id, name: n.name || `#${id}`, raw: n })
    }
    if (n.children?.length) flatten(n.children, acc)
  }
  return acc
}

async function loadFlatOrgs(): Promise<OrgSearchOption[]> {
  if (cachedFlat) return cachedFlat
  if (!loadPromise) {
    loadPromise = getOrganizationTree()
      .then((tree) => {
        cachedFlat = flatten(Array.isArray(tree) ? tree : [])
        return cachedFlat
      })
      .catch(() => {
        loadPromise = null
        return []
      })
  }
  return loadPromise
}

/**
 * 组织搜索（客户端过滤扁平化组织树 + 已选项固定回显）。
 * 与 useUserSearch 对称，供卡片组织字段、条件值等复用。
 */
export function useOrgSearch(options?: { debounceMs?: number; limit?: number }) {
  const debounceMs = options?.debounceMs ?? 300
  const limit = options?.limit ?? 50

  const orgOptions = ref<OrgSearchOption[]>([])
  const loading = ref(false)
  let timer: ReturnType<typeof setTimeout> | null = null
  const pinned = ref<OrgSearchOption[]>([])

  function dedupe(list: OrgSearchOption[]) {
    return list.filter((o, i, arr) => arr.findIndex((x) => x.value === o.value) === i)
  }

  async function load(keyword = '') {
    loading.value = true
    try {
      const all = await loadFlatOrgs()
      const kw = keyword.trim().toLowerCase()
      const filtered = kw
        ? all.filter((o) => o.label.toLowerCase().includes(kw))
        : all.slice(0, limit)
      orgOptions.value = dedupe([...pinned.value, ...filtered])
    } finally {
      loading.value = false
    }
  }

  function search(keyword: string) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => load(keyword), debounceMs)
  }

  function pin(opt: OrgSearchOption | null | undefined) {
    if (!opt || !Number.isFinite(opt.value)) return
    if (!pinned.value.some((o) => o.value === opt.value)) pinned.value.push(opt)
    if (!orgOptions.value.some((o) => o.value === opt.value)) {
      orgOptions.value = [opt, ...orgOptions.value]
    }
  }

  return { orgOptions, loading, load, search, pin }
}
