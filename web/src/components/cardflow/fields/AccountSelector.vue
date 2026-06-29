<script setup lang="ts">
/**
 * AccountSelector.vue — 财务科目选择器
 *
 * 基于账套查询科目（树形），存储为 {id, code, name}
 */
import { ref, computed, watch, onMounted } from 'vue'
import type { AccountFieldValue } from '@/types/cardflow'
import { getAccounts } from '@/api/cardflow'

interface Props {
  modelValue?: AccountFieldValue | null
  accountSetId?: number | null
  disabled?: boolean
  placeholder?: string
  /** 仅可选叶子科目 */
  onlyLeaf?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  accountSetId: null,
  disabled: false,
  placeholder: '请选择科目',
  onlyLeaf: true,
})

const emit = defineEmits<{
  (e: 'update:modelValue', val: AccountFieldValue | null): void
}>()

const loading = ref(false)
const accounts = ref<Array<{ id: number; code: string; name: string; parentId?: number; isLeaf?: boolean }>>([])
const selectedId = ref<number | null>(props.modelValue?.id ?? null)

watch(
  () => props.modelValue,
  (v) => { selectedId.value = v?.id ?? null },
)

watch(
  () => props.accountSetId,
  (id) => {
    if (id) load()
  },
)

onMounted(() => {
  if (props.accountSetId) load()
})

async function load() {
  if (!props.accountSetId) return
  loading.value = true
  try {
    accounts.value = await getAccounts({
      accountSetId: props.accountSetId,
      onlyLeaf: props.onlyLeaf,
    })
  } catch {
    accounts.value = []
  } finally {
    loading.value = false
  }
}

const treeData = computed(() => {
  // 构造 tree-select 数据；当后端返回扁平列表时按 parentId 组装
  const map = new Map<number, any>()
  accounts.value.forEach((a) => {
    map.set(a.id, {
      value: a.id,
      title: `${a.code} ${a.name}`,
      __raw: a,
      children: [],
      selectable: !props.onlyLeaf || a.isLeaf !== false,
    })
  })
  const roots: any[] = []
  accounts.value.forEach((a) => {
    const node = map.get(a.id)
    if (a.parentId && map.has(a.parentId)) {
      map.get(a.parentId).children.push(node)
    } else {
      roots.push(node)
    }
  })
  return roots
})

function onChange(id: number | null) {
  if (id == null) {
    emit('update:modelValue', null)
    return
  }
  const acc = accounts.value.find((a) => a.id === id)
  if (acc) {
    emit('update:modelValue', { id: acc.id, code: acc.code, name: acc.name })
  }
}
</script>

<template>
  <a-tree-select
    :value="selectedId"
    :tree-data="treeData"
    :disabled="disabled"
    :placeholder="placeholder"
    :loading="loading"
    show-search
    allow-clear
    tree-default-expand-all
    :tree-node-filter-prop="'title'"
    style="width: 100%"
    @change="onChange"
  />
</template>
