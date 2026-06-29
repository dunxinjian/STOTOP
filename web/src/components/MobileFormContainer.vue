<template>
  <div class="mobile-form-container">
    <div class="mobile-form-inner">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
defineOptions({ name: 'MobileFormContainer' });
</script>

<style scoped>
.mobile-form-container {
  display: flex;
  justify-content: center;
  padding: 24px 0;
}
.mobile-form-inner {
  width: 100%;
  max-width: 480px;       /* 手机端等效宽度 */
  background: #fff;
  border-radius: 12px;
  padding: 20px 16px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
</style>
