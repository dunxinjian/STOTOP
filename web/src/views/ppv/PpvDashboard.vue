<template>
  <div class="ppv-dashboard">
    <a-page-header title="PPV总览" sub-title="个人产值绩效概览" />
    <a-row :gutter="16">
      <a-col :span="6">
        <a-card title="本月产值">
          <a-statistic title="总金额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="本月记录">
          <a-statistic title="录入条数" :value="0" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="待审核">
          <a-statistic title="条数" :value="0" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="跨岗占比">
          <a-statistic title="比例(%)" :value="0" :precision="2" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="16" style="margin-top: 16px">
      <a-col :span="10">
        <a-card title="质量分布">
          <a-empty description="暂无质量分布数据" />
        </a-card>
      </a-col>
      <a-col :span="14">
        <a-card title="最近记录">
          <a-empty description="暂无最近记录" />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
// PPV Dashboard - 数据加载逻辑待后续实现
// TODO: 调用 /api/ppv/dashboard 获取统计数据
// TODO: 调用 /api/ppv/records?recent=10 获取最近记录
</script>

<style scoped>
.ppv-dashboard {
  padding: 16px;
}
</style>
