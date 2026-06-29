<template>
  <div class="page-container">
    <PageHeader title="组织架构图" description="可视化展示公司组织架构">
      <template #actions>
        <a-space>
          <a-button @click="handleZoomIn"><ZoomInOutlined /></a-button>
          <a-button @click="handleZoomOut"><ZoomOutOutlined /></a-button>
          <a-button @click="handleFitView" title="适配画布"><FullscreenOutlined /></a-button>
        </a-space>
        <a-button @click="toggleFullscreen">
          <template #icon><component :is="fullscreenActive ? FullscreenExitOutlined : FullscreenOutlined" /></template>
          {{ fullscreenActive ? '退出全屏' : '全屏' }}
        </a-button>
      </template>
    </PageHeader>

    <!-- 图例 -->
    <div class="legend-bar">
      <span class="legend-item" v-for="item in legendItems" :key="item.label">
        <span class="legend-dot" :style="{ background: item.color }" />
        {{ item.label }}
      </span>
    </div>

    <!-- G6 画布容器 -->
    <a-card class="chart-card" :bordered="false" ref="chartCardRef">
      <a-spin :spinning="loading" tip="加载组织架构数据...">
        <div
          ref="containerRef"
          class="chart-container"
        />
      </a-spin>
    </a-card>

    <!-- 详情抽屉 -->
    <a-drawer v-model:open="drawerVisible" title="组织详情" :width="360" :destroyOnClose="true">
      <a-descriptions :column="1" bordered v-if="selectedNode">
        <a-descriptions-item label="组织名称">{{ selectedNode.name }}</a-descriptions-item>
        <a-descriptions-item label="组织编码">{{ selectedNode.code }}</a-descriptions-item>
        <a-descriptions-item label="组织类型">
          <a-tag :color="typeColorMap[selectedNode.type]">
            {{ selectedNode.type }}
          </a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="负责人">{{ selectedNode.managerName || '-' }}</a-descriptions-item>
        <a-descriptions-item label="编制人数">{{ selectedNode.headcount ?? '-' }}</a-descriptions-item>
        <a-descriptions-item label="实际人数">{{ selectedNode.actualCount ?? '-' }}</a-descriptions-item>
        <a-descriptions-item label="描述">{{ selectedNode.description || '-' }}</a-descriptions-item>
      </a-descriptions>
      <template #footer>
        <a-button type="primary" @click="goToOrganization">前往组织管理</a-button>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { ZoomInOutlined, ZoomOutOutlined, FullscreenOutlined, FullscreenExitOutlined } from '@ant-design/icons-vue'
import { Graph, treeToGraphData, register, ExtensionCategory, Rect } from '@antv/g6'
import type { NodeData } from '@antv/g6'
import PageHeader from '@/components/PageHeader.vue'
import { getOrgChart } from '@/api/system'
import type { OrganizationDto } from '@/types/organization'

// ==================== 常量 ====================

// G6 canvas 节点分类色板（喂 @antv/g6 的 stroke/fill，canvas 无法解析 CSS 变量），
// 按"图表分类调色板"排除项跳过去蓝，遗留至数据可视化专项；下方 #1890ff 为色板成员非UI色
const typeColorMap: Record<string, string> = {
  '集团': '#1a3a5c',
  '子公司': '#1890ff',
  '中心': '#52c41a',
  '部门': '#fa8c16',
  '团组': '#8c8c8c',
}

const legendItems = Object.entries(typeColorMap).map(([label, color]) => ({ label, color }))

// ==================== 状态 ====================

const router = useRouter()
const containerRef = ref<HTMLDivElement>()
const chartCardRef = ref<any>()
const loading = ref(false)
const drawerVisible = ref(false)
const fullscreenActive = ref(false)
const selectedNode = ref<any>(null)

let graph: Graph | null = null

// ==================== 自定义节点注册 ====================

class OrgCardNode extends Rect {
  get defaultStyleProps() {
    return {
      // @ts-expect-error G6 internal property
      ...super.defaultStyleProps,
      radius: 6,
      fill: '#fff',
      stroke: '#d9d9d9',
      lineWidth: 1,
      shadowColor: 'rgba(0,0,0,0.08)',
      shadowBlur: 8,
      shadowOffsetY: 2,
      width: 220,
      height: 90,
    }
  }

  drawKeyShape(attributes: any, container: any) {
    const data = this.context.model.getNodeLikeDatum(this.id) as NodeData
    const orgType = (data?.style as any)?.orgType || ''
    const color = typeColorMap[orgType] || '#d9d9d9'

    return super.drawKeyShape(
      {
        ...attributes,
        stroke: color,
        lineWidth: 2,
      },
      container,
    )
  }

  render(attributes: any, container: any) {
    super.render(attributes, container)

    const data = this.context.model.getNodeLikeDatum(this.id) as NodeData
    const style = data?.style as any || {}
    const orgType = style.orgType || ''
    const managerName = style.managerName || ''
    const headcount = style.headcount
    const actualCount = style.actualCount
    const color = typeColorMap[orgType] || '#8c8c8c'
    const label = (data?.style as any)?.labelText || ''

    const w = 220
    const h = 90

    // 顶部色带
    this.upsert('topBar', 'rect', {
      x: -w / 2,
      y: -h / 2,
      width: w,
      height: 6,
      fill: color,
      radius: [6, 6, 0, 0],
    }, container)

    // 组织名称
    this.upsert('orgName', 'text', {
      x: 0,
      y: -h / 2 + 26,
      text: label,
      fontSize: 13,
      fontWeight: 600,
      fill: '#303133',
      textAlign: 'center',
      textBaseline: 'middle',
      wordWrap: true,
      wordWrapWidth: w - 20,
      maxLines: 1,
      textOverflow: '...',
    }, container)

    // 类型标签
    const tagW = orgType.length * 14 + 16
    this.upsert('typeBg', 'rect', {
      x: -tagW / 2,
      y: -h / 2 + 38,
      width: tagW,
      height: 20,
      fill: color,
      radius: 3,
      opacity: 0.15,
    }, container)
    this.upsert('typeText', 'text', {
      x: 0,
      y: -h / 2 + 48,
      text: orgType,
      fontSize: 11,
      fill: color,
      textAlign: 'center',
      textBaseline: 'middle',
    }, container)

    // 底部信息行
    const infoItems: string[] = []
    if (managerName) infoItems.push(`👤 ${managerName}`)
    if (headcount != null || actualCount != null) {
      infoItems.push(`📊 ${headcount ?? '-'} / ${actualCount ?? '-'}`)
    }
    if (infoItems.length > 0) {
      this.upsert('infoText', 'text', {
        x: 0,
        y: -h / 2 + 72,
        text: infoItems.join('   '),
        fontSize: 11,
        fill: '#909399',
        textAlign: 'center',
        textBaseline: 'middle',
      }, container)
    }
  }
}

register(ExtensionCategory.NODE, 'org-card', OrgCardNode)

// ==================== 数据转换 ====================

function transformTreeData(orgs: OrganizationDto[]): any {
  // API 返回的已经是树形结构 (带 children)
  function convert(node: OrganizationDto): any {
    return {
      id: String(node.id),
      children: node.children?.map(convert) || [],
      style: {
        labelText: node.name,
        orgType: node.type || '',
        managerName: node.managerName || '',
        headcount: node.headcount,
        actualCount: node.actualCount,
      },
      data: {
        name: node.name,
        code: node.code,
        type: node.type,
        managerName: node.managerName,
        headcount: node.headcount,
        actualCount: node.actualCount,
        description: node.description,
        parentId: node.parentId,
        id: node.id,
      },
    }
  }

  if (orgs.length === 1) {
    return convert(orgs[0])
  }
  // 多个根节点 -> 虚拟根
  return {
    id: 'virtual-root',
    style: { labelText: '组织架构', orgType: '集团' },
    data: { name: '组织架构' },
    children: orgs.map(convert),
  }
}

/** 默认只展开前 N 层，其余折叠 */
function collectCollapseIds(treeNode: any, depth: number, maxDepth: number): string[] {
  const ids: string[] = []
  if (!treeNode.children) return ids
  for (const child of treeNode.children) {
    if (depth >= maxDepth && child.children && child.children.length > 0) {
      ids.push(child.id)
    }
    ids.push(...collectCollapseIds(child, depth + 1, maxDepth))
  }
  return ids
}

// ==================== 图初始化 ====================

async function initGraph() {
  if (!containerRef.value) return

  loading.value = true
  try {
    const res = await getOrgChart() as OrganizationDto[] | OrganizationDto | null
    if (!res) {
      message.warning('暂无组织架构数据')
      return
    }

    const orgArray = Array.isArray(res) ? res : [res]
    if (orgArray.length === 0) {
      message.warning('暂无组织架构数据')
      return
    }

    const treeData = transformTreeData(orgArray)
    const graphData = treeToGraphData(treeData)

    // 计算默认折叠的节点（只展开前2层）
    const collapseIds = collectCollapseIds(treeData, 0, 2)

    const container = containerRef.value
    const width = container.clientWidth || 800
    const height = container.clientHeight || 600

    if (graph) {
      graph.destroy()
      graph = null
    }

    graph = new Graph({
      container,
      width,
      height,
      data: graphData,
      autoFit: 'view',
      padding: [40, 40, 40, 40],
      node: {
        type: 'org-card',
        style: {
          size: [220, 90],
          ports: [
            { key: 'top', placement: [0.5, 0] },
            { key: 'bottom', placement: [0.5, 1] },
          ],
        },
      },
      edge: {
        type: 'cubic-vertical',
        style: {
          stroke: '#C0C4CC',
          lineWidth: 1.5,
          endArrow: false,
        },
      },
      layout: {
        type: 'compact-box',
        direction: 'TB',
        getHeight: () => 90,
        getWidth: () => 220,
        getVGap: () => 30,
        getHGap: () => 40,
      },
      behaviors: [
        'drag-canvas',
        'zoom-canvas',
        {
          type: 'collapse-expand',
          trigger: 'click',
        },
      ],
      plugins: [
        {
          type: 'tooltip',
          getContent: (_event: any, items: any[]) => {
            if (!items?.length) return ''
            const item = items[0]
            const d = item.data || {}
            return `<div style="padding:6px 10px;font-size:13px">
              <div style="font-weight:600;margin-bottom:4px">${d.name || ''}</div>
              <div>类型: ${d.type || '-'}</div>
              <div>负责人: ${d.managerName || '-'}</div>
              <div>编制/实际: ${d.headcount ?? '-'} / ${d.actualCount ?? '-'}</div>
            </div>`
          },
        },
      ],
      animation: false,
    })

    // 折叠深层节点
    if (collapseIds.length > 0) {
      for (const id of collapseIds) {
        try {
          // @ts-expect-error G6 internal method
          graph.setElementCollapsibility(id, true)
        } catch { /* ignore */ }
      }
    }

    await graph.render()

    // 点击节点查看详情
    graph.on('node:click', (evt: any) => {
      const id = evt.target?.id
      if (!id) return
      const nodeData = graph?.getNodeData(id)
      if (nodeData?.data) {
        selectedNode.value = nodeData.data
        drawerVisible.value = true
      }
    })
  } catch (error: any) {
    console.error('加载组织架构图失败:', error)
    message.error('加载组织架构图失败')
  } finally {
    loading.value = false
  }
}

// ==================== 操作 ====================

async function handleRefresh() {
  await initGraph()
}

function handleZoomIn() {
  if (!graph) return
  const currentZoom = graph.getZoom()
  graph.zoomTo(currentZoom * 1.2, true)
}

function handleZoomOut() {
  if (!graph) return
  const currentZoom = graph.getZoom()
  graph.zoomTo(currentZoom / 1.2, true)
}

function handleFitView() {
  if (!graph) return
  graph.fitView({ padding: [40, 40, 40, 40] } as any)
}

function toggleFullscreen() {
  const card = chartCardRef.value?.$el as HTMLElement | undefined
  if (!card) return

  if (!fullscreenActive.value) {
    card.requestFullscreen?.()
    fullscreenActive.value = true
  } else {
    document.exitFullscreen?.()
    fullscreenActive.value = false
  }
}

function goToOrganization() {
  drawerVisible.value = false
  router.push('/system/organizations')
}

// ==================== 响应式 ====================

let resizeObserver: ResizeObserver | null = null

function handleResize() {
  if (!graph || !containerRef.value) return
  const w = containerRef.value.clientWidth
  const h = containerRef.value.clientHeight
  if (w > 0 && h > 0) {
    graph.setSize(w, h)
  }
}

function onFullscreenChange() {
  fullscreenActive.value = !!document.fullscreenElement
  nextTick(() => handleResize())
}

onMounted(() => {
  nextTick(() => initGraph())

  if (containerRef.value) {
    resizeObserver = new ResizeObserver(() => handleResize())
    resizeObserver.observe(containerRef.value)
  }

  document.addEventListener('fullscreenchange', onFullscreenChange)
})

onUnmounted(() => {
  if (graph) {
    graph.destroy()
    graph = null
  }
  if (resizeObserver) {
    resizeObserver.disconnect()
    resizeObserver = null
  }
  document.removeEventListener('fullscreenchange', onFullscreenChange)
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.legend-bar {
  display: flex;
  gap: $spacing-lg;
  margin-bottom: $spacing-md;
  padding: $spacing-sm $spacing-md;
  background: $bg-card;
  border-radius: $border-radius-sm;
  border: 1px solid $border-color-lighter;

  .legend-item {
    display: flex;
    align-items: center;
    gap: $spacing-xs;
    font-size: $font-size-sm;
    color: $text-regular;

    .legend-dot {
      display: inline-block;
      width: 12px;
      height: 12px;
      border-radius: 3px;
    }
  }
}

.chart-card {
  :deep(.ant-card-body) {
    padding: 0;
    height: calc(100vh - 240px);
    min-height: 500px;
  }

  .chart-container {
    width: 100%;
    height: 100%;
    cursor: grab;

    &:active {
      cursor: grabbing;
    }
  }
}
</style>
