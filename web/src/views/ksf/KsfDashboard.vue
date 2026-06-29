<template>
  <div class="ksf-dashboard">
    <a-page-header title="KSF总览" sub-title="关键成功因子绩效概览" />
    <a-row :gutter="16">
      <a-col :span="6">
        <a-card title="本月核算">
          <a-statistic title="核算人数" :value="0" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="浮动薪酬">
          <a-statistic title="总额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="试运行方案">
          <a-statistic title="方案数" :value="0" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="正式方案">
          <a-statistic title="方案数" :value="0" />
        </a-card>
      </a-col>
    </a-row>
    <!-- 排行榜占位 -->
    <a-card title="KSF 绩效排行" style="margin-top: 16px">
      <a-empty description="暂无数据" />
    </a-card>
  </div>
</template>

<script setup lang="ts">
// KSF Dashboard - 数据加载逻辑待后续实现
</script>

<style scoped>
.ksf-dashboard {
  padding: 16px;
}
</style>
