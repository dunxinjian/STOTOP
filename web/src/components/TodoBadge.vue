<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { BellOutlined } from '@ant-design/icons-vue'
import { useTodoStore } from '@/stores/todo'

const router = useRouter()
const todoStore = useTodoStore()

onMounted(() => {
  todoStore.startPolling()
})

onUnmounted(() => {
  todoStore.stopPolling()
})

function handleClick() {
  router.push('/workhub')
}
</script>

<template>
  <a-badge :count="todoStore.todoCount" :overflow-count="99" @click="handleClick" class="todo-badge">
    <BellOutlined style="font-size: 18px; cursor: pointer;" />
  </a-badge>
</template>

<style scoped>
.todo-badge {
  cursor: pointer;
}
</style>
