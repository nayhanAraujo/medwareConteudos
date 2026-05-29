<template>
  <nav aria-label="breadcrumb" class="mb-3 scripts-crumb">
    <ol class="breadcrumb mb-0">
      <li class="breadcrumb-item">
        <a href="#" @click.prevent="goSistema()">Seleção de Sistema</a>
      </li>
      <li v-if="sistema" class="breadcrumb-item">
        <a href="#" @click.prevent="goPacotes()">Seleção de Pacote</a>
      </li>
      <li v-if="sistema && pacoteNome" class="breadcrumb-item active">
        Scripts: <span class="fw-bold text-primary">{{ pacoteNome }}</span>
      </li>
    </ol>
  </nav>
</template>

<script setup lang="ts">
const props = defineProps<{
  sistema?: string
  pacote?: string | number
  pacoteNome?: string
}>()

const { goSistema, goPacotes: navPacotes } = useScriptsNav()

function goPacotes() {
  if (props.sistema) navPacotes(props.sistema)
}
</script>
