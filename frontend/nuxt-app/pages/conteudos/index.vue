<template>
  <div>
    <DsPageHeader
      title="Visualizar Conteúdos"
      subtitle="Escolha o tipo de conteúdo que deseja gerenciar"
      icon="box-seam"
    />

    <div class="max-w-[90rem] mx-auto">
      <DsPageShell>
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
  externalUrl?: string
}

const { irOuMigracao } = useMigracao()

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
    nuxtPath: '/conteudos/banco-de-frases',
    flaskPath: '/conteudos/banco_de_frases'
  },
  {
    type: 'painel_api',
    title: 'Painéis',
    desc: 'Dashboards e relatórios via API',
    icon: 'bar-chart-line',
    themeName: 'orange',
    nuxtPath: '/paineis',
    flaskPath: '/paineis/selecionar_tipo_listagem'
  },
  {
    type: 'relatorios',
    title: 'Relatórios',
    desc: 'Relatórios personalizados e documentos',
    icon: 'file-earmark-bar-graph',
    themeName: 'gray',
    nuxtPath: '/relatorios',
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
    type: 'impressos',
    title: 'Impressos',
    desc: 'Impressos e documentos MRD com scripts VBS',
    icon: 'printer',
    themeName: 'slate',
    nuxtPath: '/impressos',
    flaskPath: '/impressos'
  },
  {
    type: 'modelos_mensagens',
    title: 'Modelos de mensagens',
    desc: 'Grupos e modelos de mensagens reutilizáveis',
    icon: 'chat-square-text',
    themeName: 'purple',
    nuxtPath: '/conteudos/modelos-mensagens'
  },
  {
    type: 'assistente',
    title: 'Assistente',
    desc: 'Gerencie procedimentos, scripts e modelos do banco Assistente',
    icon: 'database-gear',
    themeName: 'dark',
    nuxtPath: '/assistente'
  },
  {
    type: 'studio',
    title: 'Studio',
    desc: 'Converta imagens de laudos em HTML compatível com LaudosUX',
    icon: 'magic',
    themeName: 'rose',
    externalUrl: 'http://localhost:3000/studio'
  }
]

const visibleCards = computed(() => cards.filter((card) => !card.hidden))

function onSelect(card: ContentCard) {
  if (card.externalUrl) {
    if (import.meta.client) {
      window.open(card.externalUrl, '_blank', 'noopener,noreferrer')
    }
    return
  }
  irOuMigracao(card.nuxtPath, card.flaskPath)
}
</script>
