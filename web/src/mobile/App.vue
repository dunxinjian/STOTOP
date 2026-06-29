<template>
  <div class="mobile-app">
    <router-view v-slot="{ Component }">
      <keep-alive :include="keepAlivePages">
        <component :is="Component" />
      </keep-alive>
    </router-view>
    <van-tabbar v-model="activeTab" v-if="showTabbar" route>
      <van-tabbar-item icon="wap-home-o" to="/m/home">工作台</van-tabbar-item>
      <van-tabbar-item icon="chart-trending-o" to="/m/dashboard">数据</van-tabbar-item>
      <van-tabbar-item icon="contact-o" to="/m/mine">我的</van-tabbar-item>
    </van-tabbar>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { Tabbar as VanTabbar, TabbarItem as VanTabbarItem } from 'vant'

const route = useRoute()
const activeTab = ref(0)

const keepAlivePages = ['MobileHome', 'MobileDashboard', 'MobileMine']

// 详情/提交等子页面隐藏 Tabbar
const showTabbar = computed(() => {
  const hiddenPaths = ['/m/card/', '/m/submit/', '/m/scan']
  return !hiddenPaths.some(p => route.path.startsWith(p))
})
</script>

<style>
.mobile-app {
  min-height: 100vh;
  background: #f5f5f5;
  padding-bottom: env(safe-area-inset-bottom);
}
</style>
