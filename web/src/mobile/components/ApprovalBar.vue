<template>
  <div class="approval-bar">
    <div class="approval-bar__inner">
      <van-button
        type="danger"
        size="small"
        :loading="loading"
        :disabled="loading"
        class="approval-bar__btn approval-bar__btn--reject"
        @click="emit('reject')"
      >
        退回
      </van-button>

      <van-button
        v-if="canSign"
        type="default"
        size="small"
        :loading="loading"
        :disabled="loading"
        class="approval-bar__btn approval-bar__btn--sign"
        @click="emit('sign')"
      >
        加签
      </van-button>

      <van-button
        type="primary"
        size="small"
        :loading="loading"
        :disabled="loading"
        class="approval-bar__btn approval-bar__btn--approve"
        @click="emit('approve')"
      >
        通过
      </van-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Button as VanButton } from 'vant'

defineOptions({ name: 'ApprovalBar' })

defineProps<{
  loading: boolean
  canSign?: boolean
}>()

const emit = defineEmits<{
  approve: []
  reject: []
  sign: []
}>()
</script>

<style scoped lang="scss">
.approval-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 100;
  background: #fff;
  padding: 8px 16px;
  padding-bottom: calc(8px + env(safe-area-inset-bottom));
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.06);

  &__inner {
    display: flex;
    gap: 12px;
  }

  &__btn {
    flex: 1;
    height: 40px;
    border-radius: 20px;
    font-size: 15px;
    font-weight: 500;

    &--approve {
      background: var(--color-success);
      border-color: var(--color-success);
    }
  }
}
</style>
