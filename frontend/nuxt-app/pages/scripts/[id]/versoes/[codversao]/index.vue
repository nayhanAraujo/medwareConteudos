<template>
  <div>
    <DsPageHeader :title="pageTitle" icon="tag" />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-6">
        <DsButton variant="secondary" size="sm" icon="arrow-left" :to="`/scripts/${id}/versoes`">Voltar</DsButton>
        <DsButton size="sm" icon="pencil" @click="editarNoFlask">Editar</DsButton>
        <DsButton
          v-if="versao && !isActive"
          variant="success"
          size="sm"
          icon="play-circle"
          :loading="saving"
          @click="ativar"
        >
          Ativar
        </DsButton>
      </div>

      <div v-if="loading" class="text-gray-500 py-8 text-center">Carregando...</div>
      <DsAlert v-else-if="!versao" variant="warning" title="Versão não encontrada." />

      <div v-else class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-6">
          <div class="rounded-2xl border border-gray-200 bg-white p-6">
            <div class="flex flex-wrap justify-between gap-3 mb-4">
              <div>
                <h3 class="font-semibold font-manrope text-ds-text">{{ versao.nomeScript }}</h3>
                <p class="text-sm text-gray-600">{{ versao.sistema }} — {{ versao.linguagem || 'Não especificado' }}</p>
              </div>
              <div class="flex gap-2">
                <DsBadge :variant="isActive ? 'success' : 'default'">{{ isActive ? 'Ativa' : 'Inativa' }}</DsBadge>
                <DsBadge :variant="isApproved ? 'primary' : 'warning'">{{ isApproved ? 'Aprovada' : 'Pendente' }}</DsBadge>
              </div>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 text-sm">
              <div><span class="text-gray-500 block">Versão</span><strong>{{ versao.numeroVersao }}</strong></div>
              <div><span class="text-gray-500 block">Criada em</span>{{ formatDate(versao.dataCriacao) }}</div>
              <div><span class="text-gray-500 block">Responsável</span>{{ versao.usuarioResponsavel || '—' }}</div>
            </div>
          </div>

          <div class="rounded-2xl border border-gray-200 bg-white p-6">
            <h4 class="font-semibold mb-4 flex items-center gap-2"><i class="bi bi-list-ul" />Alterações</h4>
            <div class="space-y-4 text-sm">
              <div>
                <span class="text-gray-500 block mb-1">Descrição geral</span>
                <p class="preserve-lines m-0">{{ versao.descricaoAlteracoes || '—' }}</p>
              </div>
              <div class="grid sm:grid-cols-2 gap-4">
                <div>
                  <span class="text-gray-500 block mb-1">Interface</span>
                  <p class="preserve-lines m-0">{{ versao.alteracoesInterface || '—' }}</p>
                </div>
                <div>
                  <span class="text-gray-500 block mb-1">Código</span>
                  <p class="preserve-lines m-0">{{ versao.alteracoesCodigo || '—' }}</p>
                </div>
              </div>
            </div>
          </div>

          <div class="rounded-2xl border border-gray-200 bg-white p-6">
            <h4 class="font-semibold mb-4 flex items-center gap-2"><i class="bi bi-clock-history" />Histórico</h4>
            <p v-if="!versao.historico.length" class="text-gray-500 text-sm">Nenhum registro.</p>
            <div v-else class="space-y-3">
              <div v-for="item in versao.historico" :key="`${item.tipoAlteracao}-${item.dataAlteracao}`" class="border-b border-gray-100 pb-3 last:border-0">
                <div class="flex justify-between gap-2 text-sm">
                  <strong>{{ item.tipoAlteracao }}</strong>
                  <span class="text-gray-500">{{ formatDate(item.dataAlteracao) }}</span>
                </div>
                <p class="text-sm mb-0">{{ item.descricao || '—' }}</p>
                <span class="text-xs text-gray-500">{{ item.usuario || 'Sistema' }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="space-y-6">
          <div class="rounded-2xl border border-gray-200 bg-white p-6">
            <h4 class="font-semibold mb-4 flex items-center gap-2"><i class="bi bi-paperclip" />Arquivos</h4>
            <div class="grid gap-2 mb-4">
              <DsButton variant="secondary" size="sm" icon="file-earmark-code" :disabled="!versao.temArquivoJson" @click="abrirDownload('json')">JSON</DsButton>
              <DsButton variant="secondary" size="sm" icon="file-earmark-text" :disabled="!hasMrd" @click="abrirDownload('mrd')">MRD</DsButton>
              <DsButton variant="secondary" size="sm" icon="file-earmark-binary" :disabled="!versao.temArquivoDll" @click="abrirDownload('dll')">DLL</DsButton>
            </div>
            <p class="text-xs text-gray-500 uppercase mb-2">MRDs da versão</p>
            <ul v-if="versao.mrdList.length" class="space-y-2 text-sm mb-4">
              <li v-for="mrd in versao.mrdList" :key="mrd.codVersaoMrd" class="flex justify-between gap-2">
                <span class="truncate">{{ mrd.nomeArquivo }}</span>
                <DsBadge v-if="mrd.padrao" variant="primary">Padrão</DsBadge>
              </li>
            </ul>
            <p v-else class="text-sm text-gray-500">Nenhum MRD.</p>
          </div>

          <div class="rounded-2xl border border-gray-200 bg-white p-6">
            <h4 class="font-semibold mb-4 flex items-center gap-2"><i class="bi bi-images" />Imagens e PDFs</h4>
            <p v-if="!anexos.length" class="text-sm text-gray-500">Nenhum anexo.</p>
            <div v-else class="space-y-2">
              <a
                v-for="arquivo in anexos"
                :key="arquivo.codArquivo"
                :href="arquivo.caminho"
                target="_blank"
                class="flex items-center gap-2 text-sm text-blue-600 hover:underline"
              >
                <i :class="arquivo.tipo === 'PDF' ? 'bi bi-file-pdf text-rose-600' : 'bi bi-image text-blue-600'" />
                <span class="truncate">{{ arquivo.nomeArquivo }}</span>
              </a>
            </div>
          </div>
        </div>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ScriptVersionDetailDto, ScriptVersionFileDto } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const id = Number(route.params.id)
const codversao = Number(route.params.codversao)
const scriptsApi = useScriptsApi()
const swal = useSwal()
const migracao = useMigracao()

const loading = ref(true)
const saving = ref(false)
const versao = ref<ScriptVersionDetailDto | null>(null)

const pageTitle = computed(() => (versao.value ? `Versão ${versao.value.numeroVersao}` : `Versão ${codversao}`))
const isActive = computed(() => (versao.value?.ativo || '').toUpperCase() === 'T')
const isApproved = computed(() => ['T', '1', 'TRUE'].includes((versao.value?.aprovado || '').toUpperCase()))
const hasMrd = computed(() => (versao.value?.mrdList.length || 0) > 0)
const anexos = computed<ScriptVersionFileDto[]>(() => [...(versao.value?.imagens || []), ...(versao.value?.pdfs || [])])

function formatDate(value?: string) {
  if (!value) return '—'
  return new Date(value).toLocaleString('pt-BR')
}

async function carregar() {
  loading.value = true
  try {
    const res = await scriptsApi.getVersao(id, codversao)
    versao.value = res.data
  } catch (e: unknown) {
    versao.value = null
    await swal.toast(e instanceof Error ? e.message : 'Erro ao carregar versão', 'error')
  } finally {
    loading.value = false
  }
}

async function ativar() {
  saving.value = true
  try {
    await scriptsApi.ativarVersao(codversao)
    await swal.toast('Versão ativada!')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao ativar versão', 'error')
  } finally {
    saving.value = false
  }
}

function editarNoFlask() {
  migracao.abrirFlask(`/scripts/editar_versao/${codversao}`)
}

function abrirDownload(tipo: 'json' | 'mrd' | 'dll') {
  migracao.abrirFlask(`/scripts/download_versao_arquivo/${codversao}/${tipo}`)
}

onMounted(carregar)
</script>

<style scoped>
.preserve-lines {
  white-space: pre-line;
}
</style>
