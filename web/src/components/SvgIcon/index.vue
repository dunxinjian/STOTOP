<template>
  <svg
    :class="['svg-icon', extraClass]"
    :style="{ fontSize: size, color: color }"
    aria-hidden="true"
  >
    <use :xlink:href="`#icon-${name}`" />
  </svg>
</template>

<script setup lang="ts">
interface Props {
  name: string
  size?: string
  color?: string
  extraClass?: string
}

withDefaults(defineProps<Props>(), {
  size: '1em',
  color: 'inherit',
  extraClass: '',
})
</script>

<style scoped>
.svg-icon {
  display: inline-block;
  overflow: hidden;
  vertical-align: -0.15em;
  fill: currentColor;
}
</style>
