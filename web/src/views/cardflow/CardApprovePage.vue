<template>
  <div class="redirect-loading">
    <a-spin tip="正在跳转..." />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

onMounted(() => {
  const id = route.params.id as string
  const isMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent)
    || window.innerWidth < 768

  if (isMobile) {
    router.replace(`/m/cardflow/approval/${id}`)
  } else {
    // PC端跳转WorkHub并通过query参数打开浮窗
    router.replace({ path: '/workhub', query: { openPanel: id } })
  }
})
</script>

<style scoped>
.redirect-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  min-height: 100vh;
}
</style>
