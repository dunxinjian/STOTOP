<!--
  DataTable —— 列表页表格封装
  统一替代各页手写的 paginationConfig=computed + handleTableChange 回填 + 序号列计算 + #emptyText。
  - pagination：v-model:pagination 绑定 { pageIndex, pageSize, total }；翻页/改页长时 emit
    update:pagination（新对象，不变异 prop）+ emit('change') 供页面重新取数；传 false 关闭分页。
  - indexColumn：默认在最左插入「序号」列并按当前分页计算行号。
  - 其余列的单元格渲染由父组件 #bodyCell 作用域插槽透传（序号列由本组件内部渲染）。
  - 空态默认 <EmptyState size="small">，emptyText 作为主标题；可用 #emptyText 覆盖。
  - bordered 默认 false（克制收敛去边框）；size 不显式传，继承 ConfigProvider 全局 small。
-->
<template>
  <a-table
    :columns="mergedColumns"
    :data-source="dataSource"
    :loading="loading"
    :pagination="antPagination"
    :row-key="rowKey"
    :bordered="bordered"
    :scroll="scroll"
    @change="onChange"
  >
    <template #bodyCell="slotProps">
      <template v-if="slotProps.column.key === '__index__'">
        {{ indexText(slotProps.index) }}
      </template>
      <template v-else>
        <slot name="bodyCell" v-bind="slotProps" />
      </template>
    </template>
    <template #emptyText>
      <slot name="emptyText">
        <EmptyState size="small" :title="emptyText" />
      </slot>
    </template>
  </a-table>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import EmptyState from '@/components/EmptyState.vue'

interface PaginationModel {
  pageIndex: number
  pageSize: number
  total: number
}

const props = withDefaults(defineProps<{
  /** 列定义（不含序号列，序号列由 indexColumn 控制自动加） */
  columns: any[]
  /** 行数据 */
  dataSource: any[]
  /** 加载态 */
  loading?: boolean
  /** 分页模型（v-model:pagination）；传 false 关闭分页 */
  pagination?: PaginationModel | false
  /** 行 key */
  rowKey?: string
  /** 横向/纵向滚动 */
  scroll?: Record<string, any>
  /** 是否在最左加序号列 */
  indexColumn?: boolean
  /** 是否带边框（默认 false，克制收敛去边框） */
  bordered?: boolean
  /** 空态主标题文案 */
  emptyText?: string
}>(), {
  loading: false,
  pagination: () => ({ pageIndex: 1, pageSize: 20, total: 0 }),
  rowKey: 'id',
  scroll: undefined,
  indexColumn: true,
  bordered: false,
  emptyText: '暂无数据',
})

const emit = defineEmits<{
  /** v-model:pagination 更新（翻页/改页长） */
  (e: 'update:pagination', value: PaginationModel): void
  /** 翻页/改页长后触发，供父组件重新取数 */
  (e: 'change'): void
}>()

const indexColDef = {
  title: '序号',
  dataIndex: '__index__',
  key: '__index__',
  width: 60,
  align: 'center' as const,
}

const mergedColumns = computed(() =>
  props.indexColumn ? [indexColDef, ...props.columns] : props.columns,
)

const antPagination = computed(() => {
  if (props.pagination === false) return false
  return {
    current: props.pagination.pageIndex,
    pageSize: props.pagination.pageSize,
    total: props.pagination.total,
    showSizeChanger: true,
    pageSizeOptions: ['10', '20', '50', '100'],
    showTotal: (t: number) => `共 ${t} 条`,
  }
})

function indexText(index: number) {
  if (props.pagination === false) return index + 1
  return (props.pagination.pageIndex - 1) * props.pagination.pageSize + index + 1
}

// 翻页/改页长：emit 新分页对象（不变异 prop）+ 通知父组件取数
function onChange(pag: any) {
  if (props.pagination === false) return
  emit('update:pagination', {
    pageIndex: pag.current,
    pageSize: pag.pageSize,
    total: props.pagination.total,
  })
  emit('change')
}
</script>
<!-- 表格观感（列头/行线/斑马/hover/分页）统一收口在全局 styles/ant-override.scss；
     本封装只管行为（分页 v-model / 序号列 / 空态），不再重复 scoped 样式。 -->
