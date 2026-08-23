<template>
  <DsPageHeader v-bind="$props" />
</template>

<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
  icon?: string
  showUserInfo?: boolean
  showClock?: boolean
}>()
</script>
