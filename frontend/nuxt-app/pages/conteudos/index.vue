<template>
  <div>
    <LayoutAppPageHeader
      title="Visualizar Conteúdos"
      subtitle="Escolha o tipo de conteúdo que deseja gerenciar"
      icon="box-seam"
    />
    <div class="card shadow-sm mb-4">
      <div class="card-body text-center">
        <h5 class="card-title fw-bold mb-4">Escolha o Tipo de Conteúdo</h5>
        <div class="row justify-content-center g-4">
          <div
            v-for="card in cards"
            :key="card.type"
            class="col-12 col-sm-6 col-md-4 col-lg-4"
            :class="{ 'd-none': card.hidden }"
          >
            <div
              class="content-type-card card h-100 p-4"
              :class="card.bgClass"
              role="button"
              tabindex="0"
              @click="onSelect(card)"
              @keydown.enter="onSelect(card)"
            >
              <div class="card-body d-flex flex-column align-items-center position-relative">
                <span v-if="card.badge" class="badge position-absolute top-0 end-0 m-2" :class="card.badgeClass">
                  {{ card.badge }}
                </span>
                <i :class="`${card.icon} fs-1 mb-3`" />
                <h5 class="mt-3 module-title">{{ card.title }}</h5>
                <p class="text-muted small module-desc mb-0">{{ card.desc }}</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

interface ContentCard {
  type: string
  title: string
  desc: string
  icon: string
  bgClass: string
  badge?: string
  badgeClass?: string
  hidden?: boolean
  nuxtPath?: string
  flaskPath?: string
}

const { irOuMigracao } = useMigracao()

const cards: ContentCard[] = [
  {
    type: 'modelos_laudos',
    title: 'Modelos de Laudos',
    desc: 'Scripts e modelos para laudos médicos',
    icon: 'bi bi-file-earmark-medical text-primary',
    bgClass: 'bg-primary-subtle',
    badge: 'Mais usado',
    badgeClass: 'bg-primary',
    nuxtPath: '/scripts/sistema'
  },
  {
    type: 'mensagens_personalizadas',
    title: 'Banco de frases',
    desc: 'Gerência de frases',
    icon: 'bi bi-quote text-success',
    bgClass: 'bg-success-subtle',
    flaskPath: '/conteudos/banco_de_frases'
  },
  {
    type: 'painel_api',
    title: 'Painéis',
    desc: 'Dashboards e relatórios via API',
    icon: 'bi bi-bar-chart-line text-warning',
    bgClass: 'bg-warning-subtle',
    flaskPath: '/paineis/selecionar_tipo_listagem'
  },
  {
    type: 'relatorios',
    title: 'Relatórios',
    desc: 'Relatórios personalizados e documentos',
    icon: 'bi bi-file-earmark-bar-graph text-dark',
    bgClass: 'bg-light-subtle',
    flaskPath: '/relatorios'
  },
  {
    type: 'banco_referencias',
    title: 'Banco de referências',
    desc: 'Banco de referências e variáveis',
    icon: 'bi bi-database text-secondary',
    bgClass: 'bg-secondary-subtle',
    nuxtPath: '/variaveis'
  },
  {
    type: 'modelos',
    title: 'Modo Texto',
    desc: 'Modo texto e templates personalizados',
    icon: 'bi bi-file-earmark-text text-danger',
    bgClass: 'bg-danger-subtle',
    flaskPath: '/modelos'
  },
  {
    type: 'impressos',
    title: 'Impressos',
    desc: 'Impressos e documentos MRD com scripts VBS',
    icon: 'bi bi-printer text-dark',
    bgClass: 'bg-dark-subtle',
    flaskPath: '/impressos'
  },
  {
    type: 'oraculo',
    title: 'Oráculo IA',
    desc: 'Assistente IA para dúvidas sobre o sistema',
    icon: 'bi bi-robot',
    bgClass: '',
    badge: 'IA',
    badgeClass: 'bg-success',
    flaskPath: '/conteudos/oraculo'
  }
]

function onSelect(card: ContentCard) {
  irOuMigracao(card.nuxtPath, card.flaskPath)
}
</script>

<style scoped>
.module-title {
  font-weight: 600;
}
</style>
