<template>
  <div class="ksf-my-progress">
    <a-page-header title="我的KSF" sub-title="员工自助查看 KSF 进度" />

    <!-- 当前期间卡片 -->
    <a-card title="当前期间" style="margin-bottom: 16px">
      <a-row :gutter="16">
        <a-col :span="6">
          <a-statistic title="期间" value="--" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="固定部分(元)" :value="0" :precision="2" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="浮动部分(元)" :value="0" :precision="2" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="预计实发(元)" :value="0" :precision="2" />
        </a-col>
      </a-row>
      <a-divider />
      <a-empty description="本期暂无指标数据" />
    </a-card>

    <!-- 历史期间列表 -->
    <a-card title="历史期间">
      <a-list :data-source="historyList" :pagination="false">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta :title="item.period" :description="item.summary" />
            <template #actions>
              <a-button type="link" size="small">查看明细</a-button>
            </template>
          </a-list-item>
        </template>
        <template #header>
          <a-empty v-if="historyList.length === 0" description="暂无历史数据" />
        </template>
      </a-list>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

interface HistoryItem {
  period: string
  summary: string
}

const historyList = ref<HistoryItem[]>([])
</script>

<style scoped>
.ksf-my-progress {
  padding: 16px;
}
</style>
