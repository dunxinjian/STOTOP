<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'

// 流程分组的编辑能力已合并到分组管理主页（/cardflow/groups）的左右分栏中。
// 此页面保留为兼容旧链接，统一重定向。
const router = useRouter()
onMounted(() => {
  router.replace('/cardflow/groups')
})
</script>

<template>
  <div class="redirect-stub">
    <a-spin tip="正在跳转到流程分组管理…" />
  </div>
</template>

<style scoped lang="scss">
.redirect-stub {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 240px;
}
</style>
