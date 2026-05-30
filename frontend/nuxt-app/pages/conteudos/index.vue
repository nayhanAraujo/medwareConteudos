<template>
  <div>
    <DsPageHeader
      title="Visualizar Conteúdos"
      subtitle="Escolha o tipo de conteúdo que deseja gerenciar"
      icon="box-seam"
    />

    <DsPageShell>
      <DsSectionTitle
        title="Escolha o Tipo de Conteúdo"
        :subtitle="`${visibleCards.length} módulo${visibleCards.length === 1 ? '' : 's'} disponível${visibleCards.length === 1 ? '' : 'is'}`"
      />

      <DsSearchInput
        v-model="searchQuery"
        wrapper-class="max-w-xl mx-auto"
        placeholder="Buscar tipo de conteúdo..."
        hint="Digite para filtrar os módulos disponíveis"
      />

      <DsEmptyState
        v-if="visibleCards.length === 0"
        title="Nenhum módulo encontrado"
        message="Tente outro termo de busca"
        action-label="Limpar busca"
        @action="searchQuery = ''"
      />

      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
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
</template>

<script setup lang="ts">
import type { DsThemeName } from '~/composables/useDsTheme'

definePageMeta({ layout: 'default' })

interface ContentCard {
  type: string
  title: string
  desc: string
  icon: string
  themeName: DsThemeName
  badge?: string
  hidden?: boolean
  nuxtPath?: string
  flaskPath?: string
}

const { irOuMigracao } = useMigracao()
const searchQuery = ref('')

const cards: ContentCard[] = [
  {
    type: 'modelos_laudos',
    title: 'Modelos de Laudos',
    desc: 'Scripts e modelos para laudos médicos',
    icon: 'file-earmark-medical',
    themeName: 'blue',
    badge: 'Mais usado',
    nuxtPath: '/scripts/sistema'
  },
  {
    type: 'mensagens_personalizadas',
    title: 'Banco de frases',
    desc: 'Gerência de frases',
    icon: 'quote',
    themeName: 'green',
    flaskPath: '/conteudos/banco_de_frases'
  },
  {
    type: 'painel_api',
    title: 'Painéis',
    desc: 'Dashboards e relatórios via API',
    icon: 'bar-chart-line',
    themeName: 'orange',
    flaskPath: '/paineis/selecionar_tipo_listagem'
  },
  {
    type: 'relatorios',
    title: 'Relatórios',
    desc: 'Relatórios personalizados e documentos',
    icon: 'file-earmark-bar-graph',
    themeName: 'gray',
    flaskPath: '/relatorios'
  },
  {
    type: 'banco_referencias',
    title: 'Banco de referências',
    desc: 'Banco de referências e variáveis',
    icon: 'database',
    themeName: 'purple',
    nuxtPath: '/variaveis'
  },
  {
    type: 'modelos',
    title: 'Modo Texto',
    desc: 'Modo texto e templates personalizados',
    icon: 'file-earmark-text',
    themeName: 'rose',
    flaskPath: '/modelos'
  },
  {
    type: 'impressos',
    title: 'Impressos',
    desc: 'Impressos e documentos MRD com scripts VBS',
    icon: 'printer',
    themeName: 'slate',
    flaskPath: '/impressos'
  },
  {
    type: 'oraculo',
    title: 'Oráculo IA',
    desc: 'Assistente IA para dúvidas sobre o sistema',
    icon: 'robot',
    themeName: 'dark',
    badge: 'IA',
    flaskPath: '/conteudos/oraculo'
  }
]

const visibleCards = computed(() =>
  cards.filter((card) => {
    if (card.hidden) return false
    const q = searchQuery.value.trim().toLowerCase()
    if (!q) return true
    return card.title.toLowerCase().includes(q) || card.desc.toLowerCase().includes(q)
  })
)

function onSelect(card: ContentCard) {
  irOuMigracao(card.nuxtPath, card.flaskPath)
}
</script>
