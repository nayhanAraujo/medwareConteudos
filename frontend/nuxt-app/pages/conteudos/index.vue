<template>
  <div>
    <DsPageHeader
      title="Gerenciamento de conteudos e banco do assistente"
      subtitle="Escolha o tipo de conteúdo que deseja gerenciar"
      icon="box-seam"
    />

    <div class="max-w-[90rem] mx-auto">
      <DsPageShell>
        <DsEmptyState v-if="!visibleCards.length" icon="lock" title="Nenhum módulo disponível" />
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <DsHubCard
            v-for="(card, index) in visibleCards"
            :key="card.type"
            :title="card.title"
            :desc="card.desc"
            :icon="card.icon"
            :theme-name="card.themeName"
            :badge="card.badge"
            :delay-index="index"
            @click="onSelect(card)"
          />
        </div>
      </DsPageShell>
    </div>
  </div>
</template>

<script setup lang="ts">
import { contentModules, type ContentModule } from '~/utils/contentNavigation'

definePageMeta({ layout: 'default' })

const { irOuMigracao } = useMigracao()

const auth = useAuthStore()
const visibleCards = computed(() => contentModules.filter(card => auth.can(card.domain, 'visualizar')))

function onSelect(card: ContentModule) {
  if (auth.can(card.domain, 'visualizar')) irOuMigracao(card.nuxtPath)
}
</script>
