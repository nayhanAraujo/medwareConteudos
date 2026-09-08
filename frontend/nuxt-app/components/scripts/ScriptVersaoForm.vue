<template>
  <form class="space-y-6" @submit.prevent="emit('submit')">
    <div class="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div class="space-y-6">
        <section class="rounded-xl border border-gray-200 bg-white p-5 space-y-4">
          <div>
            <h3 class="text-base font-semibold text-ds-text">Dados da versão</h3>
            <p class="text-sm text-gray-500">{{ sistema }}<template v-if="linguagem"> - {{ linguagem }}</template></p>
          </div>
          <DsInput
            v-if="mode === 'create'"
            v-model="model.numeroVersao"
            label="Número da versão *"
            required
          />
          <DsTextarea v-model="model.descricaoAlteracoes" label="Descrição das alterações *" :rows="4" required />
        </section>

        <section class="rounded-xl border border-gray-200 bg-white p-5 space-y-4">
          <div>
            <h3 class="text-base font-semibold text-ds-text">Detalhamento das alterações</h3>
            <p class="text-sm text-gray-500">Separe o que mudou na interface, no código e observações gerais.</p>
          </div>
          <div class="grid gap-4 lg:grid-cols-2">
            <DsTextarea v-model="model.alteracoesInterface" label="Alterações de interface" :rows="5" />
            <DsTextarea v-model="model.alteracoesCodigo" label="Alterações de código" :rows="5" />
          </div>
          <DsTextarea v-model="model.observacoes" label="Observações" :rows="3" />
        </section>
      </div>

      <aside class="space-y-6">
        <section class="rounded-xl border border-gray-200 bg-white p-5 space-y-4">
          <div>
            <h3 class="text-base font-semibold text-ds-text">Arquivos técnicos</h3>
            <p class="text-sm text-gray-500">Substitua apenas os arquivos que precisam ser atualizados.</p>
          </div>

          <DsFileInput
            v-if="sistema === 'Laudos UX'"
            label="Arquivo JSON"
            accept=".json"
            icon="filetype-json"
            @change="onJson"
          />
          <DsFileInput
            v-if="sistema === 'Laudos Flex' && linguagem === 'C#'"
            label="Arquivo DLL"
            accept=".dll"
            icon="file-earmark-code"
            @change="onDll"
          />

          <div class="space-y-3">
            <DsFileInput label="Arquivos MRD" multiple icon="files" @change="onMrd" />
            <p v-if="mode === 'create' && mrdPadraoIdx != null && mrdFilesCount > 0" class="text-xs text-gray-500">
              MRD padrão: arquivo #{{ mrdPadraoIdx + 1 }}
            </p>
          </div>
        </section>

        <section v-if="mode === 'edit' && existingMrd.length" class="rounded-xl border border-gray-200 bg-white p-5 space-y-3">
          <div>
            <h3 class="text-base font-semibold text-ds-text">MRDs existentes</h3>
            <p class="text-sm text-gray-500">Escolha o padrão ou marque arquivos para remoção.</p>
          </div>
          <label
            v-for="m in existingMrd"
            :key="m.codVersaoMrd"
            class="flex items-center gap-2 text-sm border border-gray-200 rounded-lg px-3 py-2"
            :class="mrdExcluir.has(m.codVersaoMrd) ? 'opacity-50 line-through' : ''"
          >
            <input
              v-model="mrdPadraoCod"
              type="radio"
              :value="m.codVersaoMrd"
              :disabled="mrdExcluir.has(m.codVersaoMrd)"
            />
            <span class="min-w-0 flex-1 truncate">{{ m.nomeArquivo }}</span>
            <DsBadge v-if="m.padrao" variant="primary">Padrão</DsBadge>
            <DsButton
              type="button"
              variant="ghost"
              size="sm"
              icon="trash"
              @click="toggleMrdExcluir(m.codVersaoMrd)"
            />
          </label>
        </section>

        <section class="rounded-xl border border-gray-200 bg-white p-5 space-y-4">
          <div>
            <h3 class="text-base font-semibold text-ds-text">Anexos</h3>
            <p class="text-sm text-gray-500">Imagens e PDFs de apoio da versão.</p>
          </div>
          <DsFileInput label="Imagens" accept="image/*" multiple icon="images" @change="onImg" />
          <DsFileInput label="PDFs" accept=".pdf" multiple icon="file-earmark-pdf" @change="onPdf" />
        </section>

        <section v-if="mode === 'edit' && (existingImagens.length || existingPdfs.length)" class="rounded-xl border border-gray-200 bg-white p-5 space-y-3">
          <div>
            <h3 class="text-base font-semibold text-ds-text">Anexos existentes</h3>
            <p class="text-sm text-gray-500">{{ existingImagens.length + existingPdfs.length }} arquivo(s)</p>
          </div>
          <div v-for="a in [...existingImagens, ...existingPdfs]" :key="a.codArquivo" class="flex items-center justify-between gap-2 text-sm">
            <span class="min-w-0 truncate">{{ a.nomeArquivo }}</span>
            <div class="flex gap-1 shrink-0">
              <DsButton type="button" variant="ghost" size="sm" icon="download" @click="emit('downloadAnexo', a)" />
              <DsButton type="button" variant="ghost" size="sm" icon="trash" @click="emit('deleteAnexo', a.codArquivo)" />
            </div>
          </div>
        </section>
      </aside>
    </div>

    <div class="sticky bottom-0 z-10 -mx-6 flex gap-2 border-t border-gray-200 bg-white/95 px-6 py-4 backdrop-blur md:-mx-8 md:px-8">
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
