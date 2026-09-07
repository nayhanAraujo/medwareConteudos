<template>
  <div>
    <DsPageHeader
      title="Cadastrar Referência"
      subtitle="Dados básicos, anexos e autores na mesma tela"
      icon="journal-plus"
    />
    <DsPageShell>
      <DsAlert v-if="metaError" variant="error" class="mb-4">{{ metaError }}</DsAlert>

      <ReferenciasReferenciaForm
        :form="form"
        :especialidades="especialidades"
        :tipos="tipos"
        :show-validation="attemptedSubmit"
        @submit="onSubmit"
      >
        <template #actions>
          <DsButton variant="secondary" icon="arrow-left" to="/biblioteca">Voltar para Biblioteca</DsButton>
          <DsButton type="submit" icon="check-circle" :loading="saving" :disabled="saving">
            Salvar referência
          </DsButton>
        </template>
      </ReferenciasReferenciaForm>

      <div class="mt-8 space-y-8 border-t border-gray-200 pt-6">
        <section>
          <DsSectionTitle title="Autores" />
          <p class="text-sm text-gray-600 mb-3">
            Selecione os autores que serão vinculados ao salvar a referência.
          </p>
          <DsAlert v-if="!autores.length" variant="info" class="mb-3">
            Nenhum autor cadastrado. Cadastre autores em Biblioteca → Autores.
          </DsAlert>
          <div v-else class="max-h-[40vh] overflow-y-auto space-y-2 rounded-2xl border border-gray-200 bg-white p-3">
            <label
              v-for="autor in autores"
              :key="autor.codAutor"
              class="flex items-center gap-3 p-3 rounded-xl border border-gray-100 hover:bg-gray-50 cursor-pointer"
            >
              <input
                type="checkbox"
                class="rounded"
                :checked="autorIds.has(autor.codAutor)"
                @change="toggleAutor(autor.codAutor)"
              />
              <div>
                <p class="font-medium text-sm mb-0">{{ autor.nome }}</p>
                <small class="text-gray-500">{{ autor.abreviacao || 'Sem abreviação' }}</small>
              </div>
            </label>
          </div>
          <p class="text-xs text-gray-500 mt-2">{{ autorIds.size }} autor(es) selecionado(s)</p>
        </section>

        <section>
          <DsSectionTitle title="Anexos" />
          <p class="text-sm text-gray-600 mb-3">
            Adicione PDFs, links ou textos. Eles serão gravados após criar a referência.
          </p>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
            <DsInput v-model="anexoDraft.descricao" label="Descrição" placeholder="Ex.: PDF do guideline" />
            <DsInput v-model="anexoDraft.nome" label="Nome" placeholder="Ex.: ASE 2015 Chamber" />
            <DsInput v-model="anexoDraft.link" label="Link" placeholder="https://..." />
            <DsFileInput
              label="Arquivo (PDF)"
              accept=".pdf"
              @change="onAnexoArquivo"
            />
          </div>
          <div class="flex justify-end mb-4">
            <DsButton variant="secondary" size="sm" icon="plus-circle" @click="addAnexoDraft">
              Incluir anexo na lista
            </DsButton>
          </div>

          <DsAlert v-if="!anexosPendentes.length" variant="info">Nenhum anexo na fila.</DsAlert>
          <DsTable v-else>
            <template #head>
              <tr>
                <th>Descrição</th>
                <th>Nome</th>
                <th>Tipo</th>
                <th />
              </tr>
            </template>
            <tr v-for="(a, idx) in anexosPendentes" :key="`${a.nome}-${idx}`">
              <td>{{ a.descricao }}</td>
              <td>{{ a.nome }}</td>
              <td>{{ a.arquivo ? 'PDF' : a.link ? 'LINK' : 'TEXTO' }}</td>
              <td>
                <div class="flex justify-end">
                  <DsButton variant="danger" size="sm" icon="trash" @click="anexosPendentes.splice(idx, 1)">
                    Remover
                  </DsButton>
                </div>
              </td>
            </tr>
          </DsTable>
        </section>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

interface AnexoPendente {
  descricao: string
  nome: string
  link: string
  arquivo: File | null
}

const referenciasApi = useReferenciasApi()
const swal = useSwal()
const router = useRouter()

const saving = ref(false)
const attemptedSubmit = ref(false)
const metaError = ref('')
const especialidades = ref<Array<{ codEspecialidade: number; nome: string }>>([])
const tipos = ref<Array<{ codTipoRef: number; nome?: string; descricao: string }>>([])
const autores = ref<import('~/composables/useReferenciasApi').AutorItem[]>([])
const autorIds = ref<Set<number>>(new Set())

const form = reactive({
  titulo: '',
  ano: '',
  descricao: '',
  doi: '',
  isbn: '',
  volume: '',
  paginas: '',
  codEspecialidade: '',
  codTipoRef: ''
})

const anexoDraft = reactive({
  descricao: '',
  nome: '',
  link: ''
})
const anexoArquivo = ref<File | null>(null)
const anexosPendentes = ref<AnexoPendente[]>([])

const canSubmit = computed(() => {
  const ano = Number(form.ano)
  return form.titulo.trim().length > 0 && Number.isFinite(ano) && ano >= 1800 && ano <= 2200
})

function toggleAutor(codAutor: number) {
  if (autorIds.value.has(codAutor)) autorIds.value.delete(codAutor)
  else autorIds.value.add(codAutor)
  autorIds.value = new Set(autorIds.value)
}

function onAnexoArquivo(files: FileList | null) {
  anexoArquivo.value = files?.[0] || null
}

function addAnexoDraft() {
  const descricao = anexoDraft.descricao.trim()
  const nome = anexoDraft.nome.trim()
  if (!descricao || !nome) {
    void swal.toast('Informe descrição e nome do anexo.', 'warning')
    return
  }
  anexosPendentes.value.push({
    descricao,
    nome,
    link: anexoDraft.link.trim(),
    arquivo: anexoArquivo.value
  })
  anexoDraft.descricao = ''
  anexoDraft.nome = ''
  anexoDraft.link = ''
  anexoArquivo.value = null
}

function buildPayload() {
  return {
    titulo: form.titulo.trim(),
    ano: Number(form.ano),
    descricao: form.descricao.trim() || undefined,
    doi: form.doi.trim() || undefined,
    isbn: form.isbn.trim() || undefined,
    volume: form.volume.trim() || undefined,
    paginas: form.paginas.trim() || undefined,
    codEspecialidade: form.codEspecialidade ? Number(form.codEspecialidade) : null,
    codTipoRef: form.codTipoRef ? Number(form.codTipoRef) : null
  }
}

async function loadMeta() {
  metaError.value = ''
  try {
    const [meta, autoresRes] = await Promise.all([
      referenciasApi.getMeta(),
      referenciasApi.listAutores()
    ])
    especialidades.value = meta.especialidades || []
    tipos.value = meta.tipos || []
    autores.value = autoresRes.data || []
  } catch (err) {
    metaError.value = err instanceof Error ? err.message : 'Erro ao carregar especialidades, tipos ou autores.'
  }
}

async function onSubmit() {
  attemptedSubmit.value = true
  if (!canSubmit.value) {
    await swal.toast('Preencha os campos obrigatórios: Título e Ano (1800–2200).', 'warning')
    return
  }
  saving.value = true
  try {
    const res = await referenciasApi.createReferencia(buildPayload())
    const codReferencia = res.codReferencia
    if (!codReferencia) throw new Error('Referência criada sem código de retorno.')

    if (autorIds.value.size) {
      await referenciasApi.saveAutoresByReferencia(codReferencia, Array.from(autorIds.value))
    }

    for (const anexo of anexosPendentes.value) {
      const fd = new FormData()
      fd.append('descricao', anexo.descricao)
      fd.append('nome', anexo.nome)
      fd.append('link', anexo.link || '')
      if (anexo.arquivo) fd.append('arquivo', anexo.arquivo)
      await referenciasApi.createAnexo(codReferencia, fd)
    }

    await swal.toast('Referência cadastrada com sucesso.')
    await router.push('/referencias')
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao cadastrar referência', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(loadMeta)
</script>
