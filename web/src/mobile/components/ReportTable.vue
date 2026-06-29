<template>
  <div class="report-table-wrapper">
    <van-loading v-if="loading" class="table-loading" />
    <div v-else class="table-scroll">
      <table class="report-table">
        <thead>
          <tr>
            <th
              v-for="col in columns"
              :key="col.key"
              :class="{ 'col-fixed': col.fixed }"
              :style="col.width ? { minWidth: col.width } : {}"
            >
              {{ col.title }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, idx) in data" :key="idx" @click="$emit('rowClick', row, idx)">
            <td
              v-for="col in columns"
              :key="col.key"
              :class="{ 'col-fixed': col.fixed }"
            >
              <slot :name="col.key" :row="row" :value="row[col.key]">
                {{ row[col.key] ?? '-' }}
              </slot>
            </td>
          </tr>
          <tr v-if="!data.length">
            <td :colspan="columns.length" class="empty-cell">暂无数据</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Loading as VanLoading } from 'vant'

defineOptions({ name: 'ReportTable' })

export interface Column {
  key: string
  title: string
  fixed?: boolean
  width?: string
}

withDefaults(defineProps<{
  columns: Column[]
  data: any[]
  loading?: boolean
}>(), {
  loading: false,
})

defineEmits<{
  rowClick: [row: any, index: number]
}>()
</script>

<style scoped>
.report-table-wrapper {
  background: #fff;
  border-radius: 10px;
  overflow: hidden;
}
.table-loading {
  display: flex;
  justify-content: center;
  padding: 40px 0;
}
.table-scroll {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}
.report-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  white-space: nowrap;
}
.report-table th,
.report-table td {
  padding: 10px 12px;
  text-align: left;
  border-bottom: 1px solid #f0f0f0;
}
.report-table th {
  background: #fafafa;
  font-weight: 500;
  color: #595959;
  position: sticky;
  top: 0;
  z-index: 1;
}
.col-fixed {
  position: sticky;
  left: 0;
  background: #fff;
  z-index: 2;
}
thead .col-fixed {
  background: #fafafa;
  z-index: 3;
}
.empty-cell {
  text-align: center;
  color: #bfbfbf;
  padding: 30px 0;
}
</style>
