<template>
  <div v-if="item" class="my-3 flex flex-wrap items-center gap-2 text-sm">
    <NuxtLink to="/scripts/publicacao" class="underline">Assistente: {{ item.ESTADO ?? item.estado }}</NuxtLink>
    <span v-if="item.ERRO ?? item.erro" class="break-words text-red-700">{{ item.ERRO ?? item.erro }}</span>
  </div>
</template>
<script setup lang="ts">
const props = defineProps<{ scriptId: number }>()
const api = useApi()
const auth = useAuthStore()
const item = ref<Record<string, unknown> | null>(null)
onMounted(async () => {
  if (!auth.isAdmin) return
  try {
    const result = await api.get<{ items: Record<string, unknown>[] }>(`/api/web/assistente/publicacao/scripts?scriptId=${props.scriptId}`)
    item.value = result.items[0] ?? null
  } catch { /* Publication state must not block the source editor. */ }
})
</script>
