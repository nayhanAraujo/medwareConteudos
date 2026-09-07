<template>
  <div>
    <DsPageHeader title="Banco de frases" subtitle="Banco de frases organizado por grupos" icon="chat-left-text">
      <template #actions>
        <DsButton variant="secondary" size="sm" to="/assistente">Voltar</DsButton>
        <DsButton v-if="auth.isAdmin" variant="success" size="sm" icon="folder-plus" @click="openGroupEditor()">Novo grupo</DsButton>
        <DsButton v-if="auth.isAdmin && selectedGroup" variant="success" size="sm" icon="plus-lg" @click="openPhraseEditor()">Nova frase</DsButton>
      </template>
    </DsPageHeader>

    <DsPageShell>
      <AssistenteNav />
      <DsAlert v-if="!auth.isAdmin" variant="info" class="mb-4">Consulta liberada. Alterações são exclusivas de administradores.</DsAlert>
      <DsAlert v-if="error" variant="error" class="mb-4">{{ error }}</DsAlert>

      <div class="grid gap-4 md:grid-cols-[320px_minmax(0,1fr)] 2xl:grid-cols-[360px_minmax(0,1fr)]">
        <section class="rounded-2xl border border-gray-200 bg-white">
          <div class="border-b border-gray-100 p-4">
            <div class="mb-3 flex items-center justify-between gap-3">
              <div>
                <h2 class="text-base font-semibold text-ds-text">Grupos</h2>
                <p class="text-sm text-gray-500">{{ groups.length }} grupo(s)</p>
              </div>
              <DsButton v-if="auth.isAdmin && selectedGroup" variant="secondary" size="sm" icon="pencil" @click="openGroupEditor(selectedGroup)">Editar</DsButton>
            </div>
            <DsSearchInput v-model="groupSearch" wrapper-class="mb-0" placeholder="Pesquisar grupos..." @enter="loadGroups" />
          </div>

          <div v-if="loadingGroups" class="py-10 text-center text-sm text-gray-500">Carregando grupos...</div>
          <div v-else-if="!groups.length" class="p-5 text-sm text-gray-500">Nenhum grupo encontrado.</div>
          <div v-else class="max-h-[calc(100vh-340px)] min-h-80 overflow-y-auto">
            <button
              v-for="group in groups"
              :key="group.id"
              type="button"
              class="flex w-full items-start justify-between gap-3 border-b border-gray-100 px-4 py-3 text-left transition-colors hover:bg-gray-50"
              :class="selectedGroup?.id === group.id ? 'bg-sky-50' : 'bg-white'"
              @click="selectGroup(group)"
            >
              <span class="min-w-0">
                <span class="block truncate text-sm font-semibold text-ds-text">{{ group.nome }}</span>
                <span class="mt-1 block text-xs text-gray-500">Código {{ group.id }}</span>
              </span>
              <DsBadge :variant="isActive(group) ? 'success' : 'neutral'">{{ isActive(group) ? 'Ativo' : 'Inativo' }}</DsBadge>
            </button>
          </div>
        </section>

        <section class="min-w-0">
          <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold text-ds-text">{{ selectedGroup?.nome || 'Selecione um grupo' }}</h2>
              <p class="text-sm text-gray-500">
                <template v-if="selectedGroup">{{ total }} frase(s) neste grupo</template>
                <template v-else>Escolha um grupo para visualizar as frases</template>
              </p>
            </div>
            <div v-if="selectedGroup" class="flex flex-wrap items-end gap-3">
              <DsSearchInput v-model="phraseSearch" wrapper-class="mb-0 min-w-72" placeholder="Pesquisar frases..." @enter="loadPhrases(1)" />
              <DsSelect v-model="pageSize" label="Itens por página" input-class="min-w-28" @update:model-value="loadPhrases(1)">
                <option :value="10">10</option>
                <option :value="20">20</option>
                <option :value="50">50</option>
              </DsSelect>
            </div>
          </div>

          <DsAlert v-if="!selectedGroup" variant="info">Selecione um grupo à esquerda para visualizar as frases.</DsAlert>
          <div v-else-if="loadingPhrases" class="py-12 text-center text-gray-500">Carregando frases...</div>
          <DsEmptyState v-else-if="!phrases.length" title="Nenhuma frase encontrada" description="Ajuste a pesquisa ou cadastre uma nova frase neste grupo." />
          <DsTable v-else>
            <template #head>
              <tr>
                <th>Código</th>
                <th>Título</th>
                <th>Código da frase</th>
                <th>Status</th>
                <th class="text-right">Ações</th>
              </tr>
            </template>
            <tr v-for="phrase in phrases" :key="phrase.id">
              <td>{{ phrase.id }}</td>
              <td><span class="font-semibold">{{ phrase.titulo }}</span></td>
              <td>{{ phrase.codigoFrase || '-' }}</td>
              <td><DsBadge :variant="isActive(phrase) ? 'success' : 'neutral'">{{ isActive(phrase) ? 'Ativo' : 'Inativo' }}</DsBadge></td>
              <td>
                <div class="flex justify-end gap-2">
                  <DsButton variant="secondary" size="sm" icon="diagram-3" @click="openLinks(phrase)">Vínculos</DsButton>
                  <template v-if="auth.isAdmin">
                    <DsButton variant="secondary" size="sm" @click="openPhraseEditor(phrase)">Editar</DsButton>
                    <DsButton variant="secondary" size="sm" @click="togglePhrase(phrase)">{{ isActive(phrase) ? 'Inativar' : 'Ativar' }}</DsButton>
                    <DsButton variant="danger" size="sm" @click="removePhrase(phrase)">Excluir</DsButton>
                  </template>
                </div>
              </td>
            </tr>
          </DsTable>

          <div v-if="selectedGroup && total > Number(pageSize)" class="mt-4 flex items-center justify-between gap-3 text-sm text-gray-600">
            <span>{{ total }} registros</span>
            <div class="flex gap-2">
              <DsButton variant="secondary" size="sm" :disabled="page <= 1" @click="loadPhrases(page - 1)">Anterior</DsButton>
              <span class="px-2 py-2">Página {{ page }} de {{ pages }}</span>
              <DsButton variant="secondary" size="sm" :disabled="page >= pages" @click="loadPhrases(page + 1)">Próxima</DsButton>
            </div>
          </div>
        </section>
      </div>
    </DsPageShell>

    <DsModal v-model="phraseModal" :title="phraseForm.id ? 'Editar frase' : 'Nova frase'" size="xl">
      <p class="mb-4 text-sm text-gray-600"><span class="font-semibold text-red-600">*</span> Campos obrigatórios</p>
      <div class="grid gap-4 md:grid-cols-2">
        <DsInput v-model="phraseForm.codigoFrase" label="Código da frase *" required />
        <DsInput v-model="phraseForm.titulo" label="Título *" required />
        <DsSelect v-model="phraseForm.codGrupo" label="Grupo *" required>
          <option value="">Selecione</option>
          <option v-for="group in groupOptions" :key="group.id" :value="group.id">{{ group.nome }}</option>
        </DsSelect>
        <DsSelect v-model="phraseForm.status" label="Status *" required>
          <option :value="-1">Ativo</option>
          <option :value="0">Inativo</option>
        </DsSelect>
        <DsTextarea v-model="phraseForm.frase" label="Conteúdo" class="md:col-span-2" :rows="8" />
      </div>
      <template #footer>
        <DsButton variant="secondary" @click="phraseModal = false">Cancelar</DsButton>
        <DsButton :loading="saving" @click="savePhrase">Salvar</DsButton>
      </template>
    </DsModal>

    <DsModal v-model="groupModal" :title="groupForm.id ? 'Editar grupo' : 'Novo grupo'">
      <p class="mb-4 text-sm text-gray-600"><span class="font-semibold text-red-600">*</span> Campos obrigatórios</p>
      <div class="space-y-4">
        <DsInput v-model="groupForm.grupo" label="Nome do grupo *" required />
        <DsSelect v-model="groupForm.codGrupoPai" label="Grupo pai">
          <option value="">Sem grupo pai</option>
          <option v-for="group in parentGroupOptions" :key="group.id" :value="group.id">{{ group.nome }}</option>
        </DsSelect>
        <DsSelect v-model="groupForm.status" label="Status *" required>
          <option :value="-1">Ativo</option>
          <option :value="0">Inativo</option>
        </DsSelect>
      </div>
      <template #footer>
        <DsButton variant="secondary" @click="groupModal = false">Cancelar</DsButton>
        <DsButton :loading="saving" @click="saveGroup">Salvar</DsButton>
      </template>
    </DsModal>

    <DsModal v-model="linksOpen" title="Vínculos do registro" size="xl">
      <AssistenteVinculosPanel v-if="linkedId" domain="frases" :id="linkedId" @updated="loadPhrases()" />
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type { AssistenteEntity } from '~/composables/useAssistenteApi'

definePageMeta({ layout: 'default' })

type GroupRow = AssistenteEntity & { id: number; nome: string; grupo?: string; codGrupoPai?: number | null }
type PhraseRow = AssistenteEntity & { id: number; codigoFrase?: string; titulo?: string; frase?: string; codGrupo?: number | null }

const auth = useAuthStore()
const api = useAssistenteApi()
const swal = useSwal()

const groups = ref<GroupRow[]>([])
const groupOptions = ref<GroupRow[]>([])
const selectedGroup = ref<GroupRow | null>(null)
const phrases = ref<PhraseRow[]>([])
const groupSearch = ref('')
const phraseSearch = ref('')
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const loadingGroups = ref(false)
const loadingPhrases = ref(false)
const saving = ref(false)
const error = ref('')
const phraseModal = ref(false)
const groupModal = ref(false)
const linksOpen = ref(false)
const linkedId = ref<number | null>(null)
const phraseForm = reactive({ id: 0, codigoFrase: '', titulo: '', frase: '', codGrupo: '' as number | string, status: -1 })
const groupForm = reactive({ id: 0, grupo: '', codGrupoPai: '' as number | string, status: -1 })
let groupSearchTimer: ReturnType<typeof setTimeout> | undefined
let phraseSearchTimer: ReturnType<typeof setTimeout> | undefined

const pages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value))))
const parentGroupOptions = computed(() => groupOptions.value.filter(group => group.id !== groupForm.id))

function message(reason: unknown) {
  return reason instanceof Error ? reason.message : 'Não foi possível concluir a operação.'
}

function isActive(row: AssistenteEntity) {
  return row.status === undefined || row.status === true || Number(row.status) === -1 || Number(row.status) === 1
}

async function loadGroups() {
  loadingGroups.value = true
  error.value = ''
  try {
    const result = await api.list<GroupRow>('grupos', 1, 200, groupSearch.value)
    groups.value = result.items || []
    await loadGroupOptions()
    if (selectedGroup.value) {
      selectedGroup.value = groups.value.find(group => group.id === selectedGroup.value?.id)
        || groupOptions.value.find(group => group.id === selectedGroup.value?.id)
        || null
    }
    if (!selectedGroup.value && groups.value.length) await selectGroup(groups.value[0])
    if (selectedGroup.value && !groups.value.some(group => group.id === selectedGroup.value?.id)) phrases.value = []
  } catch (reason) {
    error.value = message(reason)
    groups.value = []
  } finally {
    loadingGroups.value = false
  }
}

async function loadGroupOptions() {
  const result = await api.list<GroupRow>('grupos', 1, 200, '')
  groupOptions.value = result.items || []
}

async function selectGroup(group: GroupRow) {
  selectedGroup.value = group
  phraseSearch.value = ''
  await loadPhrases(1)
}

async function loadPhrases(target = page.value) {
  if (!selectedGroup.value) {
    phrases.value = []
    total.value = 0
    return
  }
  loadingPhrases.value = true
  try {
    const result = await api.list<PhraseRow>('frases', target, Number(pageSize.value), phraseSearch.value, { groupId: selectedGroup.value.id })
    phrases.value = result.items || []
    total.value = result.total ?? phrases.value.length
    page.value = result.page || target
  } catch (reason) {
    await swal.toast(message(reason), 'error')
    phrases.value = []
    total.value = 0
  } finally {
    loadingPhrases.value = false
  }
}

async function openPhraseEditor(row?: PhraseRow) {
  if (!selectedGroup.value && !row) return
  const detail = row?.id ? await api.get<PhraseRow>('frases', row.id).catch(() => row) : row
  phraseForm.id = detail?.id || 0
  phraseForm.codigoFrase = detail?.codigoFrase || ''
  phraseForm.titulo = detail?.titulo || ''
  phraseForm.frase = detail?.frase || ''
  phraseForm.codGrupo = detail?.codGrupo || selectedGroup.value?.id || ''
  phraseForm.status = Number(detail?.status ?? -1)
  if (!groupOptions.value.length) await loadGroupOptions()
  phraseModal.value = true
}

async function savePhrase() {
  if (!phraseForm.codigoFrase.trim()) return void await swal.toast('Preencha Código da frase.', 'warning')
  if (!phraseForm.titulo.trim()) return void await swal.toast('Preencha Título.', 'warning')
  if (!phraseForm.codGrupo) return void await swal.toast('Selecione o grupo.', 'warning')
  saving.value = true
  try {
    const body = {
      codigo: phraseForm.codigoFrase,
      titulo: phraseForm.titulo,
      frase: phraseForm.frase,
      codGrupo: Number(phraseForm.codGrupo),
      status: Number(phraseForm.status)
    }
    if (phraseForm.id) await api.update('frases', phraseForm.id, body)
    else await api.create('frases', body)
    phraseModal.value = false
    const newGroupId = Number(phraseForm.codGrupo)
    selectedGroup.value = groupOptions.value.find(group => group.id === newGroupId) || selectedGroup.value
    await loadPhrases(phraseForm.id ? page.value : 1)
    await swal.toast('Frase salva com sucesso.')
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  } finally {
    saving.value = false
  }
}

function openGroupEditor(row?: GroupRow) {
  groupForm.id = row?.id || 0
  groupForm.grupo = row?.grupo || row?.nome || ''
  groupForm.codGrupoPai = row?.codGrupoPai || ''
  groupForm.status = Number(row?.status ?? -1)
  groupModal.value = true
}

async function saveGroup() {
  if (!groupForm.grupo.trim()) return void await swal.toast('Preencha Nome do grupo.', 'warning')
  saving.value = true
  try {
    const body = {
      grupo: groupForm.grupo,
      codGrupoPai: groupForm.codGrupoPai ? Number(groupForm.codGrupoPai) : null,
      status: Number(groupForm.status)
    }
    if (groupForm.id) await api.update('grupos', groupForm.id, body)
    else await api.create('grupos', body)
    groupModal.value = false
    await loadGroups()
    await swal.toast('Grupo salvo com sucesso.')
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  } finally {
    saving.value = false
  }
}

async function togglePhrase(row: PhraseRow) {
  try {
    await api.setStatus('frases', row.id, !isActive(row))
    await loadPhrases()
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  }
}

async function removePhrase(row: PhraseRow) {
  const confirmation = await swal.confirm('Excluir frase', 'A exclusão será bloqueada caso existam vínculos. Deseja continuar?')
  if (!confirmation?.isConfirmed) return
  try {
    await api.remove('frases', row.id)
    await loadPhrases()
    await swal.toast('Frase excluída com sucesso.')
  } catch (reason) {
    await swal.toast(message(reason), 'error')
  }
}

function openLinks(row: PhraseRow) {
  linkedId.value = row.id
  linksOpen.value = true
}

onMounted(loadGroups)
watch(groupSearch, () => {
  clearTimeout(groupSearchTimer)
  groupSearchTimer = setTimeout(loadGroups, 350)
})
watch(phraseSearch, () => {
  clearTimeout(phraseSearchTimer)
  phraseSearchTimer = setTimeout(() => loadPhrases(1), 350)
})
onUnmounted(() => {
  clearTimeout(groupSearchTimer)
  clearTimeout(phraseSearchTimer)
})
</script>
