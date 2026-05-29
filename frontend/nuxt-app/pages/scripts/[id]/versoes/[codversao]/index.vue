<template>
  <div>
    <LayoutAppPageHeader :title="pageTitle" icon="tag" />

    <div class="d-flex flex-wrap gap-2 mb-3">
      <NuxtLink class="btn btn-outline-secondary btn-sm" :to="`/scripts/${id}/versoes`">
        <i class="bi bi-arrow-left" /> Voltar
      </NuxtLink>
      <button type="button" class="btn btn-primary btn-sm" @click="editarNoFlask">
        <i class="bi bi-pencil" /> Editar
      </button>
      <button
        v-if="versao && !isActive"
        type="button"
        class="btn btn-success btn-sm"
        :disabled="saving"
        @click="ativar"
      >
        <i class="bi bi-play-circle" /> Ativar
      </button>
    </div>

    <div v-if="loading" class="card shadow-sm">
      <div class="card-body text-muted">Carregando...</div>
    </div>

    <div v-else-if="!versao" class="alert alert-warning">Versao nao encontrada.</div>

    <div v-else class="row g-4">
      <div class="col-lg-8">
        <div class="card shadow-sm mb-4">
          <div class="card-header d-flex flex-wrap align-items-center justify-content-between gap-2">
            <div>
              <h5 class="mb-0">{{ versao.nomeScript }}</h5>
              <small class="text-muted">{{ versao.sistema }} - {{ versao.linguagem || 'Nao especificado' }}</small>
            </div>
            <div class="d-flex gap-2">
              <span class="badge" :class="isActive ? 'bg-success' : 'bg-secondary'">
                {{ isActive ? 'Ativa' : 'Inativa' }}
              </span>
              <span class="badge" :class="isApproved ? 'bg-primary' : 'bg-warning text-dark'">
                {{ isApproved ? 'Aprovada' : 'Pendente' }}
              </span>
            </div>
          </div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-4">
                <div class="text-muted small">Versao</div>
                <div class="fw-semibold">{{ versao.numeroVersao }}</div>
              </div>
              <div class="col-md-4">
                <div class="text-muted small">Criada em</div>
                <div>{{ formatDate(versao.dataCriacao) }}</div>
              </div>
              <div class="col-md-4">
                <div class="text-muted small">Responsavel</div>
                <div>{{ versao.usuarioResponsavel || '-' }}</div>
              </div>
              <div v-if="versao.nomePacote" class="col-md-4">
                <div class="text-muted small">Pacote</div>
                <div>{{ versao.nomePacote }}</div>
              </div>
              <div v-if="versao.aprovadoPor" class="col-md-4">
                <div class="text-muted small">Aprovada por</div>
                <div>{{ versao.aprovadoPor }}</div>
              </div>
              <div v-if="versao.dataAprovacao" class="col-md-4">
                <div class="text-muted small">Data de aprovacao</div>
                <div>{{ formatDate(versao.dataAprovacao) }}</div>
              </div>
            </div>
          </div>
        </div>

        <div class="card shadow-sm mb-4">
          <div class="card-header">
            <h5 class="mb-0"><i class="bi bi-list-ul me-2" />Alteracoes</h5>
          </div>
          <div class="card-body">
            <div class="mb-3">
              <div class="text-muted small">Descricao geral</div>
              <p class="mb-0 preserve-lines">{{ versao.descricaoAlteracoes || '-' }}</p>
            </div>
            <div class="row g-3">
              <div class="col-md-6">
                <div class="text-muted small">Interface</div>
                <p class="mb-0 preserve-lines">{{ versao.alteracoesInterface || '-' }}</p>
              </div>
              <div class="col-md-6">
                <div class="text-muted small">Codigo</div>
                <p class="mb-0 preserve-lines">{{ versao.alteracoesCodigo || '-' }}</p>
              </div>
            </div>
            <div v-if="versao.observacoes" class="mt-3">
              <div class="text-muted small">Observacoes</div>
              <p class="mb-0 preserve-lines">{{ versao.observacoes }}</p>
            </div>
          </div>
        </div>

        <div class="card shadow-sm">
          <div class="card-header">
            <h5 class="mb-0"><i class="bi bi-clock-history me-2" />Historico</h5>
          </div>
          <div class="card-body">
            <div v-if="versao.historico.length === 0" class="text-muted">Nenhum registro.</div>
            <div v-else class="list-group list-group-flush">
              <div v-for="item in versao.historico" :key="`${item.tipoAlteracao}-${item.dataAlteracao}`" class="list-group-item px-0">
                <div class="d-flex justify-content-between gap-3">
                  <strong>{{ item.tipoAlteracao }}</strong>
                  <small class="text-muted">{{ formatDate(item.dataAlteracao) }}</small>
                </div>
                <div>{{ item.descricao || '-' }}</div>
                <small class="text-muted">{{ item.usuario || 'Sistema' }}</small>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="col-lg-4">
        <div class="card shadow-sm mb-4">
          <div class="card-header">
            <h6 class="mb-0"><i class="bi bi-paperclip me-2" />Arquivos</h6>
          </div>
          <div class="card-body">
            <div class="d-grid gap-2 mb-3">
              <button type="button" class="btn btn-outline-primary btn-sm" :disabled="!versao.temArquivoJson" @click="abrirDownload('json')">
                <i class="bi bi-file-earmark-code" /> JSON
              </button>
              <button type="button" class="btn btn-outline-primary btn-sm" :disabled="!hasMrd" @click="abrirDownload('mrd')">
                <i class="bi bi-file-earmark-text" /> MRD
              </button>
              <button type="button" class="btn btn-outline-primary btn-sm" :disabled="!versao.temArquivoDll" @click="abrirDownload('dll')">
                <i class="bi bi-file-earmark-binary" /> DLL
              </button>
            </div>

            <h6 class="small text-muted text-uppercase">MRDs da versao</h6>
            <div v-if="versao.mrdList.length === 0" class="text-muted small mb-3">Nenhum MRD.</div>
            <ul v-else class="list-group list-group-flush mb-3">
              <li v-for="mrd in versao.mrdList" :key="mrd.codVersaoMrd" class="list-group-item px-0 d-flex justify-content-between">
                <span class="text-truncate">{{ mrd.nomeArquivo }}</span>
                <span v-if="mrd.padrao" class="badge bg-primary">Padrao</span>
              </li>
            </ul>

            <h6 class="small text-muted text-uppercase">Anexos</h6>
            <div class="d-flex gap-2">
              <span class="badge bg-light text-dark border">
                <i class="bi bi-image" /> {{ versao.imagens.length }} imagem(ns)
              </span>
              <span class="badge bg-light text-dark border">
                <i class="bi bi-file-pdf" /> {{ versao.pdfs.length }} PDF(s)
              </span>
            </div>
          </div>
        </div>

        <div class="card shadow-sm">
          <div class="card-header">
            <h6 class="mb-0"><i class="bi bi-images me-2" />Imagens e PDFs</h6>
          </div>
          <div class="card-body">
            <div v-if="anexos.length === 0" class="text-muted">Nenhum anexo.</div>
            <div v-else class="list-group list-group-flush">
              <a
                v-for="arquivo in anexos"
                :key="arquivo.codArquivo"
                class="list-group-item list-group-item-action px-0"
                :href="arquivo.caminho"
                target="_blank"
              >
                <div class="d-flex align-items-center gap-2">
                  <i :class="arquivo.tipo === 'PDF' ? 'bi bi-file-pdf text-danger' : 'bi bi-image text-info'" />
                  <span class="text-truncate">{{ arquivo.nomeArquivo }}</span>
                </div>
                <small class="text-muted">{{ formatDate(arquivo.dataUpload) }} - {{ arquivo.usuarioUpload || '-' }}</small>
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>
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

const pageTitle = computed(() => (versao.value ? `Versao ${versao.value.numeroVersao}` : `Versao ${codversao}`))
const isActive = computed(() => (versao.value?.ativo || '').toUpperCase() === 'T')
const isApproved = computed(() => ['T', '1', 'TRUE'].includes((versao.value?.aprovado || '').toUpperCase()))
const hasMrd = computed(() => (versao.value?.mrdList.length || 0) > 0)
const anexos = computed<ScriptVersionFileDto[]>(() => [...(versao.value?.imagens || []), ...(versao.value?.pdfs || [])])

function formatDate(value?: string) {
  if (!value) return '-'
  return new Date(value).toLocaleString('pt-BR')
}

async function carregar() {
  loading.value = true
  try {
    const res = await scriptsApi.getVersao(id, codversao)
    versao.value = res.data
  } catch (e: unknown) {
    versao.value = null
    await swal.toast(e instanceof Error ? e.message : 'Erro ao carregar versao', 'error')
  } finally {
    loading.value = false
  }
}

async function ativar() {
  saving.value = true
  try {
    await scriptsApi.ativarVersao(codversao)
    await swal.toast('Versao ativada!')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao ativar versao', 'error')
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
