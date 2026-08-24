<template>
  <div>
    <DsPageHeader
      title="Biblioteca de Recursos"
      subtitle="Central de ferramentas e configurações do sistema."
      icon="bookshelf"
    />
    <DsTabs v-model="activeTab" :tabs="tabs" />

    <DsPageShell v-show="activeTab === 'referencias'">
      <div class="rounded-2xl border border-gray-200 bg-white/80 p-4 mb-6">
        <h5 class="font-semibold text-ds-text mb-3 flex items-center gap-2">
          <i class="bi bi-lightning-charge-fill text-orange-500" />Acesso Rápido — Referências
        </h5>
        <div class="flex flex-wrap gap-2">
          <DsButton
            v-for="link in quickRef"
            :key="link.label"
            variant="secondary"
            size="sm"
            @click="onAction(link)"
          >
            <i v-if="link.icon" :class="`${link.icon} me-1`" />{{ link.label }}
          </DsButton>
        </div>
      </div>
      <DsSectionTitle title="Módulos do Sistema de Referências" />
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <DsModuleCard
          v-for="mod in modulosRef"
          :key="mod.title"
          :title="mod.title"
          :desc="mod.desc"
          :icon="mod.icon"
          :icon-color="mod.iconColor"
        >
          <DsButton
            v-for="act in mod.actions"
            :key="act.label"
            variant="secondary"
            size="sm"
            @click="onAction(act)"
          >
            {{ act.label }}
          </DsButton>
        </DsModuleCard>
      </div>
    </DsPageShell>

    <DsPageShell v-show="activeTab === 'conteudos'">
      <div class="rounded-2xl border border-gray-200 bg-white/80 p-4 mb-6">
        <h5 class="font-semibold text-ds-text mb-3 flex items-center gap-2">
          <i class="bi bi-lightning-charge-fill text-orange-500" />Acesso Rápido — Conteúdos
        </h5>
        <DsButton variant="secondary" size="sm" icon="collection" @click="navigateTo('/conteudos')">Ver Conteúdos</DsButton>
      </div>
      <DsSectionTitle title="Módulos de Conteúdos" />
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <DsModuleCard
          title="Gerenciar Conteúdos"
          desc="Acesse todos os tipos de conteúdo organizados por categorias."
          icon="bi bi-collection"
          icon-color="#fd7e14"
        >
          <DsButton variant="secondary" size="sm" @click="navigateTo('/conteudos')">Ver Todos os Conteúdos</DsButton>
        </DsModuleCard>
        <DsModuleCard
          title="Novo Módulo"
          desc="Módulo em desenvolvimento. Em breve novos recursos estarão disponíveis."
          icon="bi bi-plus-circle"
          icon-color="#6c757d"
        >
          <span class="text-xs text-gray-400 px-3 py-1.5 rounded-full bg-ds-surface">Em breve</span>
        </DsModuleCard>
      </div>
    </DsPageShell>

    <DsPageShell v-for="tab in placeholderTabs" :key="tab.id" v-show="activeTab === tab.id">
      <DsSectionTitle :title="tab.sectionTitle" />
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <DsModuleCard
          v-for="card in tab.cards"
          :key="card.title"
          :title="card.title"
          :desc="card.desc"
          :icon="card.icon"
          :icon-color="card.color"
        >
          <DsButton
            v-if="card.nuxtPath"
            variant="secondary"
            size="sm"
            @click="irOuMigracao(card.nuxtPath)"
          >
            Abrir
          </DsButton>
          <DsButton v-else variant="secondary" size="sm" @click="emMigracao()">Em migração</DsButton>
        </DsModuleCard>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
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
  { label: 'Nova Variável', icon: 'bi bi-plus-circle', nuxtPath: '/variaveis/nova' },
  { label: 'Nova Fórmula', icon: 'bi bi-plus-square', nuxtPath: '/formulas' },
  { label: 'Nova Referência', icon: 'bi bi-journal-plus', nuxtPath: '/referencias/nova' },
  { label: 'Importar JSON', icon: 'bi bi-upload', flaskPath: '/uploads/uploaddll' },
  { label: 'Novo Script', icon: 'bi bi-code-slash', nuxtPath: '/scripts/sistema' },
  { label: 'Novo Pacote', icon: 'bi bi-box-fill', nuxtPath: '/pacotes' },
  { label: 'Agente de Extração', icon: 'bi bi-robot', flaskPath: '/agente/processar_documento' },
  { label: 'Grupos de Variáveis', icon: 'bi bi-collection', nuxtPath: '/grupos' },
  { label: 'Vincular Autores', icon: 'bi bi-person-lines-fill', nuxtPath: '/referencias' },
  { label: 'Tipos de Autores', icon: 'bi bi-tags', nuxtPath: '/autores' },
  { label: 'Gerenciar Classificações', icon: 'bi bi-tags-fill', nuxtPath: '/variaveis/classificacoes' }
]

const modulosRef = [
  {
    title: 'Variáveis e Fórmulas',
    desc: 'Gerencie todas as variáveis e fórmulas utilizadas nos cálculos do sistema.',
    icon: 'bi bi-calculator-fill',
    iconColor: '#0d6efd',
    actions: [
      { label: 'Listar Variáveis', nuxtPath: '/variaveis' },
      { label: 'Nova Variável', nuxtPath: '/variaveis/nova' },
      { label: 'Nova Fórmula', nuxtPath: '/formulas' },
      { label: 'Listar Fórmulas', nuxtPath: '/formulas' }
    ] as BibAction[]
  },
  {
    title: 'Autores e Referências',
    desc: 'Armazene e gerencie referências bibliográficas e documentos de apoio.',
    icon: 'bi bi-journal-bookmark-fill',
    iconColor: '#198754',
    actions: [
      { label: 'Nova Referência', nuxtPath: '/referencias/nova' },
      { label: 'Listar Referências', nuxtPath: '/referencias' }
    ]
  },
  {
    title: 'Pacotes e Scripts',
    desc: 'Crie pacotes, gerencie scripts de laudo e suas configurações.',
    icon: 'bi bi-collection-play-fill',
    iconColor: '#6c757d',
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
    iconColor: '#22c2b4',
    actions: [{ label: 'Processar Documento', flaskPath: '/agente/processar_documento' }]
  }
]

const placeholderTabs = [
  {
    id: 'relatorios',
    sectionTitle: 'Módulos de Relatórios',
    cards: [
      { title: 'Relatórios', desc: 'Gerencie relatórios personalizados.', icon: 'bi bi-file-earmark-text', color: '#0d6efd', nuxtPath: '/relatorios' },
      { title: 'Templates', desc: 'Modelos de relatório.', icon: 'bi bi-layout-text-window', color: '#6f42c1' }
    ]
  },
  {
    id: 'paineis',
    sectionTitle: 'Módulos de Painéis',
    cards: [
      { title: 'Painéis API', desc: 'Dashboards via API.', icon: 'bi bi-bar-chart', color: '#fd7e14', nuxtPath: '/paineis?tipo=api&view=lista' },
      { title: 'Power BI', desc: 'Integração Power BI.', icon: 'bi bi-graph-up', color: '#198754', nuxtPath: '/paineis?tipo=powerbi&view=lista' }
    ]
  },
  {
    id: 'configuracoes',
    sectionTitle: 'Configurações do Sistema',
    cards: [
      { title: 'Usuários', desc: 'Gerencie usuários do sistema.', icon: 'bi bi-people', color: '#0d6efd', nuxtPath: '/usuarios' },
      { title: 'Clientes', desc: 'Gerencie clientes cadastrados no sistema.', icon: 'bi bi-building', color: '#198754', nuxtPath: '/configuracoes/clientes' },
      { title: 'Permissões', desc: 'Controle de acesso.', icon: 'bi bi-shield-lock', color: '#dc3545' },
      { title: 'Unidades', desc: 'Unidades de medida.', icon: 'bi bi-rulers', color: '#6c757d', nuxtPath: '/configuracoes' }
    ]
  }
]

function onAction(action: BibAction) {
  irOuMigracao(action.nuxtPath, action.flaskPath)
}
</script>
