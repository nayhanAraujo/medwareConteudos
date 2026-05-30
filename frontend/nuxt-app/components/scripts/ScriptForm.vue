<template>
  <form class="grid grid-cols-1 md:grid-cols-2 gap-4" @submit.prevent="emit('submit')">
    <DsInput v-model="model.nome" label="Nome do Script *" required />
    <DsInput v-model="model.criado_por" label="Criado por" :required="!editMode" :disabled="editMode" />
    <DsSelect v-model="model.codpacote" label="Pacote *" required>
      <option :value="0" disabled>Selecione...</option>
      <option v-for="p in pacotes" :key="p.codPacote" :value="p.codPacote">{{ p.nome }}</option>
    </DsSelect>
    <DsSelect v-model="model.sistema" label="Sistema *" required>
      <option value="" disabled>Selecione...</option>
      <option v-for="s in SISTEMAS" :key="s" :value="s">{{ s }}</option>
    </DsSelect>
    <DsSelect v-model="model.linguagem" label="Linguagem" :disabled="!model.sistema">
      <option value="">—</option>
      <option v-if="model.sistema === 'Laudos Flex'" value="C#">C#</option>
      <option v-if="model.sistema === 'Laudos UX'" value="HTML">HTML</option>
    </DsSelect>
    <div class="md:col-span-2">
      <DsTextarea v-model="model.descricao" label="Descrição" :rows="2" />
    </div>
    <DsInput v-model="model.link_teste" label="Link de teste" type="url" />
    <DsInput v-if="model.sistema === 'Laudos Flex'" v-model="model.caminho_azure" label="Caminho Azure" />
    <div v-if="model.sistema === 'Laudos UX' && !editMode" class="md:col-span-2">
      <DsFileInput
        label="Arquivo JSON *"
        accept=".json"
        icon="filetype-json"
        required
        @change="onJson"
      />
    </div>
    <div v-if="model.sistema === 'Laudos Flex' && model.linguagem === 'C#'" class="md:col-span-2">
      <DsFileInput
        label="Arquivo DLL"
        accept=".dll"
        icon="file-earmark-code"
        @change="onDll"
      />
    </div>
    <div class="md:col-span-2">
      <DsFileInput
        label="Arquivos MRD"
        multiple
        icon="files"
        @change="onMrd"
      />
    </div>
    <DsFileInput
      label="Imagens"
      accept="image/*"
      multiple
      icon="images"
      @change="onImg"
    />
    <DsFileInput
      label="PDFs"
      accept=".pdf"
      multiple
      icon="file-earmark-pdf"
      @change="onPdf"
    />
    <div v-if="imagePreviewUrls.length || (existingImagens?.length || 0)" class="md:col-span-2 rounded-2xl border border-gray-200 bg-white p-4">
      <h6 class="font-semibold mb-3 text-sm">Pré-visualização de imagens</h6>
      <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
        <div v-for="img in imagePreviewUrls" :key="img.url">
          <img :src="img.url" :alt="img.nome" class="rounded-xl border object-cover w-full max-h-36" />
          <small class="text-gray-500 block truncate text-xs mt-1">{{ img.nome }}</small>
        </div>
        <div v-for="img in existingImagens || []" :key="img.caminho">
          <img :src="mediaUrl(img.caminho)" :alt="img.nomeArquivo" class="rounded-xl border object-cover w-full max-h-36" />
          <small class="text-gray-500 block truncate text-xs mt-1">{{ img.nomeArquivo }}</small>
        </div>
      </div>
    </div>
    <div class="md:col-span-2 flex flex-wrap gap-4">
      <label class="flex items-center gap-2 text-sm">
        <input v-model="model.ativo" type="checkbox" class="rounded border-gray-300" /> Ativo
      </label>
      <label class="flex items-center gap-2 text-sm">
        <input v-model="model.aprovado" type="checkbox" class="rounded border-gray-300" /> Aprovado
      </label>
    </div>
    <DsInput v-if="model.aprovado" v-model="model.aprovado_por" label="Aprovado por" />
    <div class="md:col-span-2 flex gap-2 pt-2">
      <DsButton type="submit" variant="success" icon="save" :loading="loading">
        {{ loading ? 'Salvando...' : 'Salvar' }}
      </DsButton>
      <DsButton variant="secondary" @click="emit('cancel')">Cancelar</DsButton>
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

watch(() => model.value.sistema, onSistemaChange)

function onJson(fileList: FileList | null) {
  files.value.arquivo_json = fileList?.[0] ?? null
  emit('files', files.value)
}
function onDll(fileList: FileList | null) {
  files.value.arquivo_dll = fileList?.[0] ?? null
  emit('files', files.value)
}
function onMrd(fileList: FileList | null) {
  files.value.arquivos_mrd = fileList
  emit('files', files.value)
}
function onImg(fileList: FileList | null) {
  cleanupPreviewUrls()
  files.value.imagens = fileList
  imagePreviewUrls.value = fileList ? Array.from(fileList).map((f) => ({ url: URL.createObjectURL(f), nome: f.name })) : []
  emit('files', files.value)
}
function onPdf(fileList: FileList | null) {
  files.value.pdfs = fileList
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
