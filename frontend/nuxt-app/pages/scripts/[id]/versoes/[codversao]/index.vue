<template>
  <div>
    <DsPageHeader :title="pageTitle" icon="tag" />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-6">
        <DsButton variant="secondary" size="sm" icon="arrow-left" :to="`/scripts/${id}/versoes`">Voltar</DsButton>
        <DsButton size="sm" icon="pencil" :to="`/scripts/${id}/versoes/${codversao}/editar`">Editar</DsButton>
        <DsButton
          v-if="versao && !isApproved"
          variant="primary"
          size="sm"
          icon="check-circle"
          :loading="saving"
          @click="aprovar"
        >
          Aprovar
        </DsButton>
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
        <DsButton
          v-if="versao && !isActive && podeExcluir"
          variant="danger"
          size="sm"
          icon="trash"
          :loading="saving"
          @click="excluir"
        >
          Excluir
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
              <div v-if="versao.aprovadoPor">
                <span class="text-gray-500 block">Aprovado por</span>{{ versao.aprovadoPor }}
              </div>
              <div v-if="versao.dataAprovacao">
                <span class="text-gray-500 block">Data aprovação</span>{{ formatDate(versao.dataAprovacao) }}
              </div>
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
              <div v-if="versao.observacoes">
                <span class="text-gray-500 block mb-1">Observações</span>
                <p class="preserve-lines m-0">{{ versao.observacoes }}</p>
              </div>
            </div>
          </div>

          <div class="rounded-2xl border border-gray-200 bg-white p-6">
            <h4 class="font-semibold mb-4 flex items-center gap-2"><i class="bi bi-clock-history" />Histórico</h4>
            <p v-if="!versao.historico.length" class="text-gray-500 text-sm">Nenhum registro.</p>
            <div v-else class="space-y-3">
              <div
                v-for="(item, idx) in versao.historico"
                :key="`${item.tipoAlteracao}-${item.dataAlteracao}-${idx}`"
                class="border-b border-gray-100 pb-3 last:border-0"
              >
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
              <DsButton
                variant="secondary"
                size="sm"
                icon="file-earmark-code"
                :disabled="!versao.temArquivoJson"
                @click="download('json')"
              >
                JSON
              </DsButton>
              <DsButton variant="secondary" size="sm" icon="file-earmark-text" :disabled="!hasMrd" @click="download('mrd')">
                MRD padrão
              </DsButton>
              <DsButton
                variant="secondary"
                size="sm"
                icon="file-zip"
                :disabled="!hasMrd"
                @click="downloadMrdZip"
              >
                ZIP MRDs
              </DsButton>
              <DsButton
                variant="secondary"
                size="sm"
                icon="file-earmark-binary"
                :disabled="!versao.temArquivoDll"
                @click="download('dll')"
              >
                DLL
              </DsButton>
            </div>
            <p class="text-xs text-gray-500 uppercase mb-2">MRDs da versão</p>
            <ul v-if="versao.mrdList.length" class="space-y-2 text-sm mb-4">
              <li v-for="mrd in versao.mrdList" :key="mrd.codVersaoMrd" class="flex justify-between gap-2 items-center">
                <button
                  type="button"
                  class="truncate text-left text-blue-600 hover:underline"
                  @click="downloadMrd(mrd)"
                >
                  {{ mrd.nomeArquivo }}
                </button>
                <DsBadge v-if="mrd.padrao" variant="primary">Padrão</DsBadge>
              </li>
            </ul>
            <p v-else class="text-sm text-gray-500">Nenhum MRD.</p>
          </div>

          <div class="rounded-2xl border border-gray-200 bg-white p-6">
            <h4 class="font-semibold mb-4 flex items-center gap-2"><i class="bi bi-images" />Imagens e PDFs</h4>
            <p v-if="!anexos.length" class="text-sm text-gray-500">Nenhum anexo.</p>
            <div v-else class="space-y-2">
              <button
                v-for="arquivo in anexos"
                :key="arquivo.codArquivo"
                type="button"
                class="flex items-center gap-2 text-sm text-blue-600 hover:underline w-full text-left"
                @click="baixarAnexo(arquivo)"
              >
                <i :class="arquivo.tipo === 'PDF' ? 'bi bi-file-pdf text-rose-600' : 'bi bi-image text-blue-600'" />
                <span class="truncate">{{ arquivo.nomeArquivo }}</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { ScriptVersionDetailDto, ScriptVersionFileDto, ScriptVersionMrdDto } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default', middleware: ['admin'] })

const route = useRoute()
const router = useRouter()
const id = Number(route.params.id)
const codversao = Number(route.params.codversao)
const scriptsApi = useScriptsApi()
const swal = useSwal()

const loading = ref(true)
const saving = ref(false)
const versao = ref<ScriptVersionDetailDto | null>(null)
const totalVersoes = ref(0)

const pageTitle = computed(() => (versao.value ? `Versão ${versao.value.numeroVersao}` : `Versão ${codversao}`))
const isActive = computed(() => (versao.value?.ativo || '').toUpperCase() === 'T')
const isApproved = computed(() => versao.value?.aprovado === 'T')
const hasMrd = computed(() => (versao.value?.mrdList.length || 0) > 0)
const anexos = computed<ScriptVersionFileDto[]>(() => [...(versao.value?.imagens || []), ...(versao.value?.pdfs || [])])
const podeExcluir = computed(() => totalVersoes.value > 1)

function formatDate(value?: string) {
  if (!value) return '—'
  return new Date(value).toLocaleString('pt-BR')
}

async function carregar() {
  loading.value = true
  try {
    const [det, list] = await Promise.all([
      scriptsApi.getVersao(id, codversao),
      scriptsApi.listVersoes(id)
    ])
    versao.value = det.data
    totalVersoes.value = list.data.length
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

async function aprovar() {
  const { isConfirmed } = (await swal.confirm('Aprovar versão?', 'Confirma a aprovação desta versão?')) || {}
  if (!isConfirmed) return
  saving.value = true
  try {
    await scriptsApi.aprovarVersao(codversao)
    await swal.toast('Versão aprovada!')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally {
    saving.value = false
  }
}

async function excluir() {
  const { isConfirmed } = (await swal.confirm('Excluir versão?', 'Esta ação não pode ser desfeita.')) || {}
  if (!isConfirmed) return
  saving.value = true
  try {
    await scriptsApi.excluirVersao(codversao)
    await swal.toast('Versão excluída')
    await router.push(`/scripts/${id}/versoes`)
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally {
    saving.value = false
  }
}

function download(tipo: 'json' | 'dll' | 'mrd') {
  if (!versao.value) return
  const nome = `${versao.value.nomeScript}_${versao.value.numeroVersao}`
  const ext = tipo === 'json' ? '.json' : tipo === 'dll' ? '.dll' : ''
  scriptsApi.downloadVersaoArquivo(codversao, tipo, `${nome}${ext}`).catch((e: unknown) =>
    swal.toast(e instanceof Error ? e.message : 'Download indisponível', 'error')
  )
}

function downloadMrd(mrd: ScriptVersionMrdDto) {
  scriptsApi.downloadVersaoMrd(codversao, mrd.codVersaoMrd, mrd.nomeArquivo).catch((e: unknown) =>
    swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  )
}

function downloadMrdZip() {
  if (!versao.value) return
  const safe = versao.value.nomeScript.replace(/[^\w\s-]/g, '_').replace(/\s+/g, '_')
  scriptsApi.downloadVersaoMrdZip(codversao, `${safe}_v${versao.value.numeroVersao}_mrd.zip`).catch((e: unknown) =>
    swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  )
}

function baixarAnexo(arquivo: ScriptVersionFileDto) {
  scriptsApi.downloadVersaoAnexo(arquivo.codArquivo, arquivo.nomeArquivo).catch((e: unknown) =>
    swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  )
}

onMounted(carregar)
</script>

<style scoped>
.preserve-lines {
  white-space: pre-line;
}
</style>
