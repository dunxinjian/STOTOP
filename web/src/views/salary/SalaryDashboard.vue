<template>
  <div class="salary-dashboard">
    <a-page-header title="薪酬总览" sub-title="薪酬管理统计概览" />
    <a-row :gutter="16">
      <a-col :span="6">
        <a-card title="本月待审工资单">
          <a-statistic title="数量" :value="0" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="本月已发放总额">
          <a-statistic title="金额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="待评审晋升">
          <a-statistic title="数量" :value="0" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card title="本月B分兑换">
          <a-statistic title="总额(元)" :value="0" :precision="2" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="16" style="margin-top: 16px">
      <a-col :span="12">
        <a-card title="档位分布">
          <a-empty description="暂无档位分布数据" />
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card title="近期工资单">
          <a-empty description="暂无最近工资单" />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
// Salary Dashboard - 数据加载逻辑待后续实现
// TODO: 调用 /api/salary/dashboard 获取统计数据
// TODO: 调用 /api/salary/payrolls?recent=10 获取最近工资单
</script>

<style scoped>
.salary-dashboard {
  padding: 16px;
}
</style>
