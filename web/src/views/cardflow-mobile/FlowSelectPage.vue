<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { AvailableFlowDto, FlowGroupDto } from '@/types/cardflow'
import {
  NavBar as VanNavBar,
  Search as VanSearch,
  CellGroup as VanCellGroup,
  Cell as VanCell,
  Empty as VanEmpty,
  Loading as VanLoading,
  PullRefresh as VanPullRefresh,
} from 'vant'
import 'vant/es/nav-bar/style'
import 'vant/es/search/style'
import 'vant/es/cell-group/style'
import 'vant/es/cell/style'
import 'vant/es/empty/style'
import 'vant/es/loading/style'
import 'vant/es/pull-refresh/style'
import { getAvailableFlows, getFlowGroups } from '@/api/cardflow'
import { useOrgContextStore } from '@/stores/orgContext'

const route = useRoute()
const router = useRouter()
const orgContextStore = useOrgContextStore()
const loading = ref(true)
const refreshing = ref(false)
const keyword = ref('')
const flows = ref<AvailableFlowDto[]>([])
const groups = ref<FlowGroupDto[]>([])
const currentOrgId = computed(() => orgContextStore.currentOrgId ?? 0)

// 按组分类的流程列表
const groupedFlows = computed(() => {
  const kw = keyword.value.toLowerCase()
  const filtered = kw
    ? flows.value.filter(f => f.flowName.toLowerCase().includes(kw) || (f.description || '').toLowerCase().includes(kw))
    : flows.value

  // 简单分组：无 groupId 信息时按首字母分组
  if (groups.value.length > 0) {
    const map = new Map<string, AvailableFlowDto[]>()
    const ungrouped: AvailableFlowDto[] = []
    for (const flow of filtered) {
      // 根据 groups 查找归属（这里简化处理）
      let placed = false
      for (const g of groups.value) {
        if (!placed) {
          if (!map.has(g.groupName)) map.set(g.groupName, [])
          // 实际中应判断 flow 是否属于 group
        }
      }
      if (!placed) ungrouped.push(flow)
    }
    // 如果无法确定归属，全部放 ungrouped
    if (ungrouped.length === filtered.length) {
      return [{ name: '全部流程', items: filtered }]
    }
    const result: { name: string; items: AvailableFlowDto[] }[] = []
    map.forEach((items, name) => { if (items.length) result.push({ name, items }) })
    if (ungrouped.length) result.push({ name: '其他', items: ungrouped })
    return result
  }

  return [{ name: '可发起流程', items: filtered }]
})

async function loadData() {
  loading.value = true
  try {
    if (!currentOrgId.value) {
      await orgContextStore.fetchCurrentContext()
    }
    if (!currentOrgId.value) {
      flows.value = []
      groups.value = []
      return
    }

    const [flowRes, groupRes] = await Promise.all([
      getAvailableFlows(currentOrgId.value),
      getFlowGroups(currentOrgId.value).catch(() => [] as FlowGroupDto[]),
    ])
    flows.value = flowRes || []
    groups.value = groupRes || []
  } catch {
    flows.value = []
  } finally {
    loading.value = false
  }
}

async function onRefresh() {
  await loadData()
  refreshing.value = false
}

function onSelectFlow(flow: AvailableFlowDto) {
  router.push({ path: `/m/cardflow/fill/${flow.id}`, query: route.query })
}

function onClickLeft() {
  router.back()
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="flow-select-page">
    <VanNavBar title="发起流程" left-arrow @click-left="onClickLeft" fixed placeholder />

    <VanSearch v-model="keyword" placeholder="搜索流程名称" />

    <div v-if="loading" class="loading-wrap">
      <VanLoading size="36px" vertical>加载中...</VanLoading>
    </div>

    <VanPullRefresh v-else v-model="refreshing" @refresh="onRefresh">
      <template v-if="groupedFlows.some(g => g.items.length > 0)">
        <VanCellGroup
          v-for="group in groupedFlows"
          :key="group.name"
          :title="group.name"
          inset
          class="flow-group"
        >
          <VanCell
            v-for="flow in group.items"
            :key="flow.id"
            :title="flow.flowName"
            :label="flow.description || undefined"
            is-link
            @click="onSelectFlow(flow)"
          >
            <template #icon>
              <div class="flow-icon">
                {{ flow.flowName.charAt(0) }}
              </div>
            </template>
          </VanCell>
        </VanCellGroup>
      </template>

      <VanEmpty v-else description="暂无可发起的流程" />
    </VanPullRefresh>
  </div>
</template>

<style scoped>
.flow-select-page {
  min-height: 100vh;
  background: #f7f8fa;
}
.loading-wrap {
  display: flex;
  justify-content: center;
  padding-top: 30vh;
}
.flow-group {
  margin-top: 12px !important;
}
.flow-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: #fff;
  font-size: 16px;
  font-weight: 600;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
}
</style>
