<template>
  <DsBreadcrumb :items="items" @navigate="onNavigate" />
</template>

<script setup lang="ts">
import type { DsBreadcrumbItem } from '~/components/ds/DsBreadcrumb.vue'

const props = defineProps<{
  sistema?: string
  pacote?: string | number
  pacoteNome?: string
}>()

const { goSistema, goPacotes: navPacotes } = useScriptsNav()

const items = computed<DsBreadcrumbItem[]>(() => {
  const list: DsBreadcrumbItem[] = [{ label: 'Seleção de Sistema', href: true }]
  if (props.sistema) list.push({ label: 'Seleção de Pacote', href: true })
  if (props.sistema && props.pacoteNome) {
    list.push({ label: `Scripts: ${props.pacoteNome}`, active: true })
  }
  return list
})

function onNavigate(item: DsBreadcrumbItem) {
  if (item.label.startsWith('Seleção de Sistema')) goSistema()
  else if (item.label.startsWith('Seleção de Pacote') && props.sistema) navPacotes(props.sistema)
}
</script>
