<template>
  <div class="biblioteca-page">
    <LayoutAppPageHeader
      title="Biblioteca de Recursos"
      subtitle="Central de ferramentas e configurações do sistema."
      icon="bookshelf"
    />

    <ul class="nav nav-tabs nav-fill mb-4" role="tablist">
      <li v-for="tab in tabs" :key="tab.id" class="nav-item" role="presentation">
        <button
          class="nav-link"
          :class="{ active: activeTab === tab.id }"
          type="button"
          role="tab"
          @click="activeTab = tab.id"
        >
          <i :class="`bi bi-${tab.icon} me-2`" />{{ tab.label }}
        </button>
      </li>
    </ul>

    <!-- Referências -->
    <div v-show="activeTab === 'referencias'">
      <div class="quick-access mb-4">
        <h5 class="quick-access-title">
          <i class="bi bi-lightning-charge-fill" />Acesso Rápido - Referências
        </h5>
        <div class="row g-3">
          <div v-for="link in quickRef" :key="link.label" class="col-xl-2 col-lg-3 col-md-4 col-6">
            <a href="#" class="module-action d-block text-center py-2" @click.prevent="onAction(link)">
              <i :class="`${link.icon} me-1`" />{{ link.label }}
            </a>
          </div>
        </div>
      </div>
      <h4 class="section-title">
        <i class="bi bi-journal-bookmark me-2" />Módulos do Sistema de Referências
      </h4>
      <div class="row g-4">
        <div v-for="mod in modulosRef" :key="mod.title" class="col-md-6 col-lg-4">
          <div class="module-card">
            <div :class="`module-icon ${mod.iconBg}`">
              <i :class="mod.icon" />
            </div>
            <h5 class="module-title">{{ mod.title }}</h5>
            <p class="module-desc">{{ mod.desc }}</p>
            <div class="d-flex flex-wrap gap-2 mt-auto">
              <a
                v-for="act in mod.actions"
                :key="act.label"
                href="#"
                class="module-action"
                @click.prevent="onAction(act)"
              >
                {{ act.label }}
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Conteúdos -->
    <div v-show="activeTab === 'conteudos'">
      <div class="quick-access mb-4">
        <h5 class="quick-access-title">
          <i class="bi bi-lightning-charge-fill" />Acesso Rápido - Conteúdos
        </h5>
        <a href="#" class="module-action d-inline-block py-2 px-3" @click.prevent="navigateTo('/conteudos')">
          <i class="bi bi-collection me-1" />Ver Conteúdos
        </a>
      </div>
      <h4 class="section-title"><i class="bi bi-collection me-2" />Módulos de Conteúdos</h4>
      <div class="row g-4">
        <div class="col-md-6 col-lg-4">
          <div class="module-card">
            <div class="module-icon" style="background-color: #fd7e14">
              <i class="bi bi-collection" />
            </div>
            <h5 class="module-title">Gerenciar Conteúdos</h5>
            <p class="module-desc">Acesse todos os tipos de conteúdo organizados por categorias.</p>
            <a href="#" class="module-action" @click.prevent="navigateTo('/conteudos')">Ver Todos os Conteúdos</a>
          </div>
        </div>
        <div class="col-md-6 col-lg-4">
          <div class="module-card">
            <div class="module-icon bg-secondary">
              <i class="bi bi-plus-circle" />
            </div>
            <h5 class="module-title">Novo Módulo</h5>
            <p class="module-desc">Módulo em desenvolvimento. Em breve novos recursos estarão disponíveis.</p>
            <span class="module-action opacity-50">Em breve</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Relatórios, Painéis, Configurações (placeholders) -->
    <div v-for="tab in placeholderTabs" :key="tab.id" v-show="activeTab === tab.id">
      <h4 class="section-title">
        <i :class="`bi bi-${tab.icon} me-2`" />{{ tab.sectionTitle }}
      </h4>
      <div class="row g-4">
        <div v-for="card in tab.cards" :key="card.title" class="col-md-6 col-lg-4">
          <div class="module-card">
            <div class="module-icon" :style="{ backgroundColor: card.color }">
              <i :class="card.icon" />
            </div>
            <h5 class="module-title">{{ card.title }}</h5>
            <p class="module-desc">{{ card.desc }}</p>
            <a href="#" class="module-action" @click.prevent="emMigracao()">Em migração</a>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import '~/assets/css/biblioteca.css'

definePageMeta({ layout: 'default', middleware: ['admin'] })

interface BibAction {
  label: string
  nuxtPath?: string
  flaskPath?: string
  icon?: string
}

const activeTab = ref('referencias')
const { emMigracao, irOuMigracao } = useMigracao()

const tabs = [
  { id: 'referencias', label: 'Referências', icon: 'journal-bookmark' },
  { id: 'conteudos', label: 'Conteúdos', icon: 'collection' },
  { id: 'relatorios', label: 'Relatórios', icon: 'file-earmark-text' },
  { id: 'paineis', label: 'Painéis', icon: 'pie-chart' },
  { id: 'configuracoes', label: 'Configurações', icon: 'gear' }
]

const quickRef: BibAction[] = [
  { label: 'Nova Variável', icon: 'bi bi-plus-circle', flaskPath: '/variaveis/nova' },
  { label: 'Nova Fórmula', icon: 'bi bi-plus-square', flaskPath: '/formulas/nova' },
  { label: 'Nova Referência', icon: 'bi bi-journal-plus', flaskPath: '/referencias/nova' },
  { label: 'Novo Modelo', icon: 'bi bi-file-earmark-plus', flaskPath: '/modelos/novo' },
  { label: 'Importar JSON', icon: 'bi bi-upload', flaskPath: '/uploads/uploaddll' },
  { label: 'Novo Script', icon: 'bi bi-code-slash', nuxtPath: '/scripts/sistema' },
  { label: 'Novo Pacote', icon: 'bi bi-box-fill', flaskPath: '/pacotes/novo' },
  { label: 'Agente de Extração', icon: 'bi bi-robot', flaskPath: '/agente/processar_documento' },
  { label: 'Grupos de Variáveis', icon: 'bi bi-collection', flaskPath: '/grupos' },
  { label: 'Vincular Autores', icon: 'bi bi-person-lines-fill', flaskPath: '/autores' },
  { label: 'Tipos de Autores', icon: 'bi bi-tags', flaskPath: '/autores/tipos' },
  { label: 'Gerenciar Classificações', icon: 'bi bi-tags-fill', flaskPath: '/variaveis/classificacoes' }
]

const modulosRef = [
  {
    title: 'Variáveis e Fórmulas',
    desc: 'Gerencie todas as variáveis e fórmulas utilizadas nos cálculos do sistema.',
    icon: 'bi bi-calculator-fill',
    iconBg: 'bg-primary',
    actions: [
      { label: 'Listar Variáveis', nuxtPath: '/variaveis' },
      { label: 'Nova Variável', flaskPath: '/variaveis/nova' },
      { label: 'Nova Fórmula', flaskPath: '/formulas/nova' },
      { label: 'Listar Fórmulas', flaskPath: '/formulas' }
    ] as BibAction[]
  },
  {
    title: 'Autores e Referências',
    desc: 'Armazene e gerencie referências bibliográficas e documentos de apoio.',
    icon: 'bi bi-journal-bookmark-fill',
    iconBg: 'bg-success',
    actions: [
      { label: 'Nova Referência', flaskPath: '/referencias/nova' },
      { label: 'Listar Referências', flaskPath: '/referencias' }
    ]
  },
  {
    title: 'Pacotes e Scripts',
    desc: 'Crie pacotes, gerencie scripts de laudo e suas configurações.',
    icon: 'bi bi-collection-play-fill',
    iconBg: 'text-bg-secondary',
    actions: [
      { label: 'Listar Scripts', nuxtPath: '/scripts/sistema' },
      { label: 'Novo Script', nuxtPath: '/scripts/sistema' },
      { label: 'E-mails de Notificação', nuxtPath: '/scripts/emails-notificacao' }
    ]
  },
  {
    title: 'Agentes',
    desc: 'Utilize IA para extrair faixas de normalidade de documentos acadêmicos.',
    icon: 'bi bi-robot',
    iconBg: 'agentes-icon',
    actions: [{ label: 'Processar Documento', flaskPath: '/agente/processar_documento' }]
  }
]

const placeholderTabs = [
  {
    id: 'relatorios',
    icon: 'file-earmark-text',
    sectionTitle: 'Módulos de Relatórios',
    cards: [
      { title: 'Relatórios', desc: 'Gerencie relatórios personalizados.', icon: 'bi bi-file-earmark-text', color: '#0d6efd' },
      { title: 'Templates', desc: 'Modelos de relatório.', icon: 'bi bi-layout-text-window', color: '#6f42c1' }
    ]
  },
  {
    id: 'paineis',
    icon: 'pie-chart',
    sectionTitle: 'Módulos de Painéis',
    cards: [
      { title: 'Painéis API', desc: 'Dashboards via API.', icon: 'bi bi-bar-chart', color: '#fd7e14' },
      { title: 'Power BI', desc: 'Integração Power BI.', icon: 'bi bi-graph-up', color: '#198754' }
    ]
  },
  {
    id: 'configuracoes',
    icon: 'gear',
    sectionTitle: 'Configurações do Sistema',
    cards: [
      { title: 'Usuários', desc: 'Gerencie usuários do sistema.', icon: 'bi bi-people', color: '#0d6efd' },
      { title: 'Permissões', desc: 'Controle de acesso.', icon: 'bi bi-shield-lock', color: '#dc3545' },
      { title: 'Unidades', desc: 'Unidades de medida.', icon: 'bi bi-rulers', color: '#6c757d' }
    ]
  }
]

function onAction(action: BibAction) {
  irOuMigracao(action.nuxtPath, action.flaskPath)
}
</script>

<style scoped>
.biblioteca-page .module-icon.agentes-icon {
  background-color: #22c2b4;
}
</style>
