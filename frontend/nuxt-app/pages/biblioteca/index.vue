<template>
  <div>
    <DsPageHeader
      title="Biblioteca de Recursos"
      subtitle="Central de ferramentas e configurações do sistema."
      icon="bookshelf"
    />
    <DsTabs v-model="activeTab" :tabs="tabs" />
    <DsEmptyState v-if="!tabs.length" icon="lock" title="Nenhum módulo disponível" />

    <DsPageShell v-show="activeTab === 'referencias'">
      <div v-if="visibleQuickRef.length" class="rounded-2xl border border-gray-200 bg-white/80 p-4 mb-6">
        <h5 class="font-semibold text-ds-text mb-3 flex items-center gap-2">
          <i class="bi bi-lightning-charge-fill text-orange-500" />Acesso Rápido — Referências
        </h5>
        <div class="flex flex-wrap gap-2">
          <DsButton
            v-for="link in visibleQuickRef"
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
          v-for="mod in visibleReferenceModules"
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

    <DsPageShell v-if="activeTab === 'conteudos'">
      <div class="flex justify-end mb-4">
        <DsButton variant="secondary" size="sm" icon="collection" to="/conteudos">Ver todos os conteúdos</DsButton>
      </div>
      <DsSectionTitle title="Módulos de Conteúdos" />
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <DsModuleCard v-for="card in visibleContentModules" :key="card.type" :title="card.title" :desc="card.desc" :icon="card.icon" :icon-color="contentColors[card.themeName] || '#2563eb'">
          <DsButton variant="secondary" size="sm" @click="openContent(card)">Abrir</DsButton>
        </DsModuleCard>
      </div>
    </DsPageShell>

    <DsPageShell v-for="tab in visibleSections" :key="tab.id" v-show="activeTab === tab.id">
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
            @click="openCard(card)"
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
import { contentModules, contentRouteDomain, type ContentModule } from '~/utils/contentNavigation'
definePageMeta({ layout: 'default', compactWorkspace: true })

interface BibAction {
  label: string
  nuxtPath?: string
  flaskPath?: string
  icon?: string
  domain?: string
  action?: string
}

const activeTab = ref('referencias')
const auth = useAuthStore()
const { emMigracao, irOuMigracao } = useMigracao()

const tabDefinitions = [
  { id: 'referencias', label: 'Referências', icon: 'journal-bookmark' },
  { id: 'conteudos', label: 'Conteúdos', icon: 'collection' },
  { id: 'relatorios', label: 'Relatórios', icon: 'file-earmark-text' },
  { id: 'paineis', label: 'Painéis', icon: 'pie-chart' },
  { id: 'configuracoes', label: 'Configurações', icon: 'gear' }
]

const quickRef: BibAction[] = [
  { label: 'Nova Variável', icon: 'bi bi-plus-circle', nuxtPath: '/variaveis/nova', action: 'criar' },
  { label: 'Nova Fórmula', icon: 'bi bi-plus-square', nuxtPath: '/formulas', action: 'criar' },
  { label: 'Nova Referência', icon: 'bi bi-journal-plus', nuxtPath: '/referencias/nova', action: 'criar' },
  { label: 'Importar JSON / C#', icon: 'bi bi-upload', nuxtPath: '/variaveis/importar', action: 'importar' },
  { label: 'Novo Script', icon: 'bi bi-code-slash', nuxtPath: '/scripts/sistema', action: 'criar' },
  { label: 'Listar Pacotes', icon: 'bi bi-box-seam', nuxtPath: '/pacotes' },
  { label: 'Novo Pacote', icon: 'bi bi-box-fill', nuxtPath: '/pacotes', action: 'criar' },
  { label: 'Agente de Extração', icon: 'bi bi-robot', flaskPath: '/agente/processar_documento' },
  { label: 'Grupos de Variáveis', icon: 'bi bi-collection', nuxtPath: '/grupos' },
  { label: 'Vincular Autores', icon: 'bi bi-person-lines-fill', nuxtPath: '/referencias', action: 'vincular' },
  { label: 'Tipos de Autores', icon: 'bi bi-tags', nuxtPath: '/autores' },
  { label: 'Especialidades', icon: 'bi bi-heart-pulse', nuxtPath: '/especialidades', domain: 'configuracoes' },
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
      { label: 'Nova Variável', nuxtPath: '/variaveis/nova', action: 'criar' },
      { label: 'Nova Fórmula', nuxtPath: '/formulas', action: 'criar' },
      { label: 'Listar Fórmulas', nuxtPath: '/formulas' }
    ] as BibAction[]
  },
  {
    title: 'Autores e Referências',
    desc: 'Armazene e gerencie referências bibliográficas e documentos de apoio.',
    icon: 'bi bi-journal-bookmark-fill',
    iconColor: '#198754',
    actions: [
      { label: 'Nova Referência', nuxtPath: '/referencias/nova', action: 'criar' },
      { label: 'Listar Referências', nuxtPath: '/referencias' },
      { label: 'Especialidades', nuxtPath: '/especialidades', domain: 'configuracoes' }
    ]
  },
  {
    title: 'Pacotes e Scripts',
    desc: 'Crie pacotes, gerencie scripts de laudo e suas configurações.',
    icon: 'bi bi-collection-play-fill',
    iconColor: '#6c757d',
    actions: [
      { label: 'Listar Pacotes', nuxtPath: '/pacotes' },
      { label: 'Listar Scripts', nuxtPath: '/scripts/sistema' },
      { label: 'Novo Script', nuxtPath: '/scripts/sistema', action: 'criar' },
      { label: 'E-mails de Notificação', nuxtPath: '/scripts/emails-notificacao', action: 'editar' }
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

interface LibraryCard { title: string; desc: string; icon: string; color: string; nuxtPath?: string; domain?: string; adminOnly?: boolean }
const moduleSections: { id: string; sectionTitle: string; cards: LibraryCard[] }[] = [
  {
    id: 'relatorios',
    sectionTitle: 'Módulos de Relatórios',
    cards: [
      { title: 'Relatórios', desc: 'Gerencie relatórios personalizados.', icon: 'bi bi-file-earmark-text', color: '#0d6efd', nuxtPath: '/relatorios', domain: 'relatorios' },
    ]
  },
  {
    id: 'paineis',
    sectionTitle: 'Módulos de Painéis',
    cards: [
      { title: 'Painéis API', desc: 'Dashboards via API.', icon: 'bi bi-bar-chart', color: '#fd7e14', nuxtPath: '/paineis?tipo=api&view=lista', domain: 'paineis' },
      { title: 'Power BI', desc: 'Integração Power BI.', icon: 'bi bi-graph-up', color: '#198754', nuxtPath: '/paineis?tipo=powerbi&view=lista', domain: 'paineis' }
    ]
  },
  {
    id: 'configuracoes',
    sectionTitle: 'Configurações do Sistema',
    cards: [
      { title: 'Central de Orçamentos', desc: 'Solicitações, revisões e acompanhamento comercial.', icon: 'bi bi-receipt', color: '#2563eb', nuxtPath: '/biblioteca/orcamentos', domain: 'orcamentos' },
      { title: 'Usuários', desc: 'Gerencie usuários do sistema.', icon: 'bi bi-people', color: '#0d6efd', nuxtPath: '/usuarios', domain: 'usuarios' },
      { title: 'Clientes', desc: 'Gerencie clientes cadastrados no sistema.', icon: 'bi bi-building', color: '#198754', nuxtPath: '/configuracoes/clientes' },
      { title: 'Permissões', desc: 'Controle de acesso.', icon: 'bi bi-shield-lock', color: '#dc3545', nuxtPath: '/biblioteca/configuracoes/permissoes', domain: 'configuracoes' },
      { title: 'Central de Auditoria', desc: 'Consulte ações dos usuários e alterações nos registros.', icon: 'bi bi-clock-history', color: '#2563eb', nuxtPath: '/biblioteca/configuracoes/auditoria', adminOnly: true },
      { title: 'Unidades', desc: 'Unidades de medida.', icon: 'bi bi-rulers', color: '#6c757d', nuxtPath: '/configuracoes', domain: 'configuracoes' },
      { title: 'Especialidades', desc: 'Cadastro de especialidades do banco de referências.', icon: 'bi bi-heart-pulse', color: '#e83e8c', nuxtPath: '/especialidades', domain: 'configuracoes' }
    ]
  }
]

const contentColors: Record<string, string> = { blue: '#2563eb', green: '#198754', purple: '#7c3aed', orange: '#d97706', gray: '#667085', slate: '#475569', dark: '#1f2937', rose: '#db4772' }
const visibleContentModules = computed(() => contentModules.filter(card => auth.can(card.domain, 'visualizar')))
function canOpenCard(card: LibraryCard) {
  return (!card.adminOnly || auth.isAdmin) && (!card.domain || auth.can(card.domain, 'visualizar'))
}
const visibleSections = computed(() => moduleSections.map(tab => ({ ...tab, cards: tab.cards.filter(canOpenCard) })).filter(tab => tab.cards.length))
function canOpenAction(action: BibAction) {
  const domain = action.domain || contentRouteDomain(action.nuxtPath || '') ||
    (action.nuxtPath === '/grupos' ? 'variaveis' : action.nuxtPath === '/autores' || action.flaskPath ? 'referencias' : undefined)
  if (!domain || !auth.can(domain, 'visualizar')) return false
  return auth.can(domain, action.action || 'visualizar')
}
const visibleQuickRef = computed(() => quickRef.filter(canOpenAction))
const visibleReferenceModules = computed(() => modulosRef.map(module => ({ ...module, actions: module.actions.filter(canOpenAction) })).filter(module => module.actions.length))
const tabs = computed(() => tabDefinitions.filter(tab => tab.id === 'referencias' ? visibleReferenceModules.value.length || visibleQuickRef.value.length : tab.id === 'conteudos' ? visibleContentModules.value.length : visibleSections.value.some(section => section.id === tab.id)))
watch(tabs, available => { if (!available.some(tab => tab.id === activeTab.value)) activeTab.value = available[0]?.id || '' }, { immediate: true })
function openContent(card: ContentModule) { if (auth.can(card.domain, 'visualizar')) irOuMigracao(card.nuxtPath) }
function openCard(card: LibraryCard) { if (canOpenCard(card)) irOuMigracao(card.nuxtPath) }
function onAction(action: BibAction) {
  if (canOpenAction(action)) irOuMigracao(action.nuxtPath, action.flaskPath)
}
</script>
