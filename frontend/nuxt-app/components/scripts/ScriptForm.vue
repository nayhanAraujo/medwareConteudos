<template>
  <form class="row g-3" @submit.prevent="emit('submit')">
    <div class="col-md-6">
      <label class="form-label">Nome do Script <span class="text-danger">*</span></label>
      <input v-model="model.nome" type="text" class="form-control" required />
    </div>
    <div class="col-md-6">
      <label class="form-label">Criado por <span v-if="!editMode" class="text-danger">*</span></label>
      <input v-model="model.criado_por" type="text" class="form-control" :required="!editMode" :disabled="editMode" />
    </div>
    <div class="col-md-6">
      <label class="form-label">Pacote <span class="text-danger">*</span></label>
      <select v-model.number="model.codpacote" class="form-select" required>
        <option :value="0" disabled>Selecione...</option>
        <option v-for="p in pacotes" :key="p.codPacote" :value="p.codPacote">{{ p.nome }}</option>
      </select>
    </div>
    <div class="col-md-6">
      <label class="form-label">Sistema <span class="text-danger">*</span></label>
      <select v-model="model.sistema" class="form-select" required @change="onSistemaChange">
        <option value="" disabled>Selecione...</option>
        <option v-for="s in SISTEMAS" :key="s" :value="s">{{ s }}</option>
      </select>
    </div>
    <div class="col-md-6">
      <label class="form-label">Linguagem</label>
      <select v-model="model.linguagem" class="form-select" :disabled="!model.sistema">
        <option value="">—</option>
        <option v-if="model.sistema === 'Laudos Flex'" value="C#">C#</option>
        <option v-if="model.sistema === 'Laudos UX'" value="HTML">HTML</option>
      </select>
    </div>
    <div class="col-md-12">
      <label class="form-label">Descrição</label>
      <textarea v-model="model.descricao" class="form-control" rows="2" />
    </div>
    <div class="col-md-6">
      <label class="form-label">Link de teste</label>
      <input v-model="model.link_teste" type="url" class="form-control" />
    </div>
    <div v-if="model.sistema === 'Laudos Flex'" class="col-md-6">
      <label class="form-label">Caminho Azure</label>
      <input v-model="model.caminho_azure" type="text" class="form-control" />
    </div>
    <div v-if="model.sistema === 'Laudos UX' && !editMode" class="col-md-12">
      <label class="form-label">Arquivo JSON <span class="text-danger">*</span></label>
      <input type="file" accept=".json" class="form-control" @change="onJson" />
    </div>
    <div v-if="model.sistema === 'Laudos Flex' && model.linguagem === 'C#'" class="col-md-12">
      <label class="form-label">Arquivo DLL</label>
      <input type="file" accept=".dll" class="form-control" @change="onDll" />
    </div>
    <div class="col-md-12">
      <label class="form-label">Arquivos MRD</label>
      <input type="file" class="form-control" multiple @change="onMrd" />
    </div>
    <div class="col-md-6">
      <label class="form-label">Imagens</label>
      <input type="file" accept="image/*" class="form-control" multiple @change="onImg" />
    </div>
    <div class="col-md-6">
      <label class="form-label">PDFs</label>
      <input type="file" accept=".pdf" class="form-control" multiple @change="onPdf" />
    </div>
    <div v-if="imagePreviewUrls.length || (existingImagens?.length || 0)" class="col-12">
      <div class="card border-light">
        <div class="card-body">
          <h6 class="fw-semibold mb-2">Pré-visualização de imagens</h6>
          <div class="row g-2">
            <div v-for="img in imagePreviewUrls" :key="img.url" class="col-md-3 col-6">
              <img :src="img.url" :alt="img.nome" class="img-fluid rounded border" style="max-height: 140px; object-fit: cover; width: 100%" />
              <small class="text-muted d-block text-truncate">{{ img.nome }}</small>
            </div>
            <div v-for="img in existingImagens || []" :key="img.caminho" class="col-md-3 col-6">
              <img :src="mediaUrl(img.caminho)" :alt="img.nomeArquivo" class="img-fluid rounded border" style="max-height: 140px; object-fit: cover; width: 100%" />
              <small class="text-muted d-block text-truncate">{{ img.nomeArquivo }}</small>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class="col-md-4">
      <div class="form-check">
        <input id="ativo" v-model="model.ativo" class="form-check-input" type="checkbox" />
        <label class="form-check-label" for="ativo">Ativo</label>
      </div>
    </div>
    <div class="col-md-4">
      <div class="form-check">
        <input id="aprovado" v-model="model.aprovado" class="form-check-input" type="checkbox" />
        <label class="form-check-label" for="aprovado">Aprovado</label>
      </div>
    </div>
    <div v-if="model.aprovado" class="col-md-4">
      <label class="form-label">Aprovado por</label>
      <input v-model="model.aprovado_por" type="text" class="form-control" />
    </div>
    <div class="col-12">
      <button type="submit" class="btn btn-success" :disabled="loading">
        <i class="bi bi-save me-1" />{{ loading ? 'Salvando...' : 'Salvar' }}
      </button>
      <button type="button" class="btn btn-outline-secondary ms-2" @click="emit('cancel')">Cancelar</button>
    </div>
  </form>
</template>

<script setup lang="ts">
import type { PacoteDto } from '~/composables/useScriptsApi'

export interface ScriptFormModel {
  nome: string
  criado_por: string
  codpacote: number
  sistema: string
  linguagem: string
  descricao: string
  link_teste: string
  caminho_azure: string
  caminho_projeto: string
  ativo: boolean
  aprovado: boolean
  aprovado_por: string
}

export interface ScriptImagePreview {
  caminho: string
  nomeArquivo: string
}

const props = defineProps<{
  modelValue: ScriptFormModel
  pacotes: PacoteDto[]
  existingImagens?: ScriptImagePreview[]
  editMode?: boolean
  loading?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [ScriptFormModel]
  submit: []
  cancel: []
  files: [Record<string, File | FileList | null>]
}>()

const { SISTEMAS } = useScriptsNav()
const model = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v)
})

const files = ref<Record<string, File | FileList | null>>({})
const imagePreviewUrls = ref<{ url: string; nome: string }[]>([])

function onSistemaChange() {
  model.value.linguagem = ''
}

function onJson(e: Event) {
  files.value.arquivo_json = (e.target as HTMLInputElement).files?.[0] ?? null
  emit('files', files.value)
}
function onDll(e: Event) {
  files.value.arquivo_dll = (e.target as HTMLInputElement).files?.[0] ?? null
  emit('files', files.value)
}
function onMrd(e: Event) {
  files.value.arquivos_mrd = (e.target as HTMLInputElement).files
  emit('files', files.value)
}
function onImg(e: Event) {
  cleanupPreviewUrls()
  const list = (e.target as HTMLInputElement).files
  files.value.imagens = list
  imagePreviewUrls.value = list ? Array.from(list).map((f) => ({ url: URL.createObjectURL(f), nome: f.name })) : []
  emit('files', files.value)
}
function onPdf(e: Event) {
  files.value.pdfs = (e.target as HTMLInputElement).files
  emit('files', files.value)
}

function cleanupPreviewUrls() {
  imagePreviewUrls.value.forEach((p) => URL.revokeObjectURL(p.url))
  imagePreviewUrls.value = []
}

onBeforeUnmount(() => cleanupPreviewUrls())

function mediaUrl(path?: string) {
  if (!path) return ''
  if (/^https?:\/\//i.test(path)) return path
  return `http://localhost:5080${path.startsWith('/') ? '' : '/'}${path}`
}
</script>
