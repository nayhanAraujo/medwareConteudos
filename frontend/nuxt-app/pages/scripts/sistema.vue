<template>
  <div>
    <DsPageHeader title="Seleção de Sistema" icon="gear-fill" />
    <div class="max-w-[90rem] mx-auto">
      <DsPageShell>
        <DsSectionTitle title="Escolha o Sistema" subtitle="Selecione o sistema para visualizar os scripts disponíveis" />
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <DsHubCard
            title="Laudos UX"
            desc="Sistema moderno de laudos"
            icon="pc-display-horizontal"
            theme-name="blue"
            @click="select('Laudos UX')"
          />
          <DsHubCard
            title="Laudos Flex"
            desc="Sistema tradicional de laudos"
            icon="laptop"
            theme-name="purple"
            @click="select('Laudos Flex')"
          />
        </div>
      </DsPageShell>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
const { goPacotes } = useScriptsNav()
function select(sistema: string) {
  goPacotes(sistema)
}
</script>
