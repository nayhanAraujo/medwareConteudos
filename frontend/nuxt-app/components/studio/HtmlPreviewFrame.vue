<script setup lang="ts">
const props = defineProps<{
  html: string
  executeScripts?: boolean
}>()

const iframeAttrs = computed(() => {
  const { buildPreviewDocument } = useHtmlPreview()
  return buildPreviewDocument(props.html, props.executeScripts ?? false)
})

const sandboxValue = computed(() => iframeAttrs.value.sandbox)
</script>

<template>
  <iframe
    v-if="html"
    :srcdoc="iframeAttrs.srcdoc"
    :sandbox="sandboxValue"
    class="h-full min-h-[400px] w-full rounded-ds-sm border border-ds-border bg-white"
    title="Preview HTML LaudosUX"
  />
  <div
    v-else
    class="flex h-64 items-center justify-center rounded-ds-sm border border-dashed border-ds-border bg-ds-media text-ds-muted"
  >
    Nenhum HTML para visualizar
  </div>
</template>
