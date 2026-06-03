<template>
  <form class="space-y-4 max-w-3xl" @submit.prevent="emit('submit')">
    <DsInput
      v-if="mode === 'create'"
      v-model="model.numeroVersao"
      label="Número da versão *"
      required
    />
    <DsTextarea v-model="model.descricaoAlteracoes" label="Descrição das alterações *" :rows="3" required />
    <DsTextarea v-model="model.alteracoesInterface" label="Alterações de interface" :rows="2" />
    <DsTextarea v-model="model.alteracoesCodigo" label="Alterações de código" :rows="2" />
    <DsTextarea v-model="model.observacoes" label="Observações" :rows="2" />

    <div v-if="sistema === 'Laudos UX'" class="rounded-xl border border-gray-200 p-4">
      <DsFileInput label="Arquivo JSON (opcional — copia do script se vazio)" accept=".json" icon="filetype-json" @change="onJson" />
    </div>
    <div v-if="sistema === 'Laudos Flex' && linguagem === 'C#'" class="rounded-xl border border-gray-200 p-4">
      <DsFileInput label="Arquivo DLL (opcional)" accept=".dll" icon="file-earmark-code" @change="onDll" />
    </div>

    <div class="rounded-xl border border-gray-200 p-4 space-y-3">
      <DsFileInput label="Arquivos MRD" multiple icon="files" @change="onMrd" />
      <p v-if="mode === 'create' && mrdPadraoIdx != null && mrdFilesCount > 0" class="text-xs text-gray-500">
        MRD padrão: arquivo #{{ mrdPadraoIdx + 1 }}
      </p>
      <div v-if="mode === 'edit' && existingMrd.length" class="space-y-2">
        <p class="text-sm font-medium text-gray-700">MRDs existentes</p>
        <label
          v-for="m in existingMrd"
          :key="m.codVersaoMrd"
          class="flex items-center gap-2 text-sm border rounded-lg px-3 py-2"
          :class="mrdExcluir.has(m.codVersaoMrd) ? 'opacity-50 line-through' : ''"
        >
          <input
            v-model="mrdPadraoCod"
            type="radio"
            :value="m.codVersaoMrd"
            :disabled="mrdExcluir.has(m.codVersaoMrd)"
          />
          <span class="flex-1 truncate">{{ m.nomeArquivo }}</span>
          <DsBadge v-if="m.padrao" variant="primary">Padrão</DsBadge>
          <DsButton
            type="button"
            variant="ghost"
            size="sm"
            icon="trash"
            @click="toggleMrdExcluir(m.codVersaoMrd)"
          />
        </label>
      </div>
    </div>

    <DsFileInput label="Imagens" accept="image/*" multiple icon="images" @change="onImg" />
    <DsFileInput label="PDFs" accept=".pdf" multiple icon="file-earmark-pdf" @change="onPdf" />

    <div v-if="mode === 'edit' && (existingImagens.length || existingPdfs.length)" class="rounded-xl border border-gray-200 p-4 space-y-3">
      <p class="text-sm font-medium">Anexos existentes</p>
      <div v-for="a in [...existingImagens, ...existingPdfs]" :key="a.codArquivo" class="flex items-center justify-between gap-2 text-sm">
        <span class="truncate">{{ a.nomeArquivo }}</span>
        <div class="flex gap-1 shrink-0">
          <DsButton type="button" variant="ghost" size="sm" icon="download" @click="emit('downloadAnexo', a)" />
          <DsButton type="button" variant="ghost" size="sm" icon="trash" @click="emit('deleteAnexo', a.codArquivo)" />
        </div>
      </div>
    </div>

    <div class="flex gap-2 pt-2">
      <DsButton type="submit" variant="success" icon="save" :loading="loading">{{ submitLabel }}</DsButton>
      <DsButton variant="secondary" @click="emit('cancel')">Cancelar</DsButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import type { ScriptVersionFileDto, ScriptVersionMrdDto, VersaoFormFiles } from '~/composables/useScriptsApi'

export interface VersaoFormModel {
  numeroVersao: string
  descricaoAlteracoes: string
  alteracoesInterface: string
  alteracoesCodigo: string
  observacoes: string
}

const props = withDefaults(
  defineProps<{
    modelValue: VersaoFormModel
    mode: 'create' | 'edit'
    sistema: string
    linguagem?: string
    existingMrd?: ScriptVersionMrdDto[]
    existingImagens?: ScriptVersionFileDto[]
    existingPdfs?: ScriptVersionFileDto[]
    loading?: boolean
    submitLabel?: string
  }>(),
  {
    existingMrd: () => [],
    existingImagens: () => [],
    existingPdfs: () => [],
    submitLabel: 'Salvar'
  }
)

const emit = defineEmits<{
  'update:modelValue': [VersaoFormModel]
  submit: []
  cancel: []
  files: [VersaoFormFiles]
  downloadAnexo: [ScriptVersionFileDto]
  deleteAnexo: [number]
}>()

const model = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v)
})

const files = ref<VersaoFormFiles>({})
const mrdPadraoIdx = ref<number | null>(null)
const mrdPadraoCod = ref<number | null>(null)
const mrdExcluir = ref(new Set<number>())
const mrdFilesCount = ref(0)

function emitFiles() {
  const payload: VersaoFormFiles = { ...files.value }
  if (props.mode === 'create' && mrdPadraoIdx.value != null) payload.mrd_padrao_versao_idx = mrdPadraoIdx.value
  if (props.mode === 'edit' && mrdPadraoCod.value) payload.mrd_padrao = mrdPadraoCod.value
  if (mrdExcluir.value.size) payload.mrd_excluir = [...mrdExcluir.value]
  emit('files', payload)
}

function onJson(fl: FileList | null) {
  files.value.arquivo_json = fl?.[0] ?? null
  emitFiles()
}
function onDll(fl: FileList | null) {
  files.value.arquivo_dll = fl?.[0] ?? null
  emitFiles()
}
function onMrd(fl: FileList | null) {
  files.value.arquivos_mrd = fl
  mrdFilesCount.value = fl?.length ?? 0
  if (fl?.length) mrdPadraoIdx.value = 0
  emitFiles()
}
function onImg(fl: FileList | null) {
  files.value.imagens = fl
  emitFiles()
}
function onPdf(fl: FileList | null) {
  files.value.pdfs = fl
  emitFiles()
}

function toggleMrdExcluir(cod: number) {
  if (mrdExcluir.value.has(cod)) mrdExcluir.value.delete(cod)
  else mrdExcluir.value.add(cod)
  emitFiles()
}

watch(mrdPadraoCod, () => emitFiles())

onMounted(() => {
  const padrao = props.existingMrd.find((m) => m.padrao)
  if (padrao) mrdPadraoCod.value = padrao.codVersaoMrd
})
</script>
