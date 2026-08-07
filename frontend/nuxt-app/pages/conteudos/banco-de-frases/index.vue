<template>
  <div>
    <DsPageHeader title="Banco de frases" subtitle="Grupos e frases com conteúdo HTML ou RTF legado" icon="chat-left-text" />
    <DsPageShell>
      <div class="flex flex-wrap justify-between gap-3 mb-5">
        <DsButton variant="secondary" icon="arrow-clockwise" :loading="loading" @click="loadGroups">Atualizar</DsButton>
        <DsButton v-if="auth.isAdmin" variant="success" icon="folder-plus" @click="openGroup()">Novo grupo</DsButton>
      </div>

      <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <section class="lg:col-span-1 rounded-2xl border border-gray-100 overflow-hidden">
          <header class="px-4 py-3 bg-gray-50 font-semibold">Grupos</header>
          <div v-if="loading" class="p-5 text-sm text-gray-500">Carregando...</div>
          <div v-else-if="!groups.length" class="p-5 text-sm text-gray-500">Nenhum grupo cadastrado.</div>
          <button
            v-for="group in groups"
            :key="group.codGrupo"
            type="button"
            class="w-full text-left px-4 py-3 border-0 border-t border-gray-100 flex items-center gap-3 hover:bg-gray-50"
            :class="selectedGroup?.codGrupo === group.codGrupo ? 'bg-sky-50' : 'bg-white'"
            @click="selectGroup(group)"
          >
            <span class="min-w-0 flex-1">
              <span class="block font-medium truncate">{{ group.nome }}</span>
              <small class="text-gray-500">#{{ group.codGrupo }}</small>
            </span>
            <DsBadge :variant="group.status ? 'success' : 'default'">{{ group.status ? 'Ativo' : 'Inativo' }}</DsBadge>
            <span v-if="auth.isAdmin" class="flex gap-1" @click.stop>
              <DsButton variant="ghost" size="sm" icon="pencil" @click="openGroup(group)" />
              <DsButton variant="ghost" size="sm" icon="trash" @click="removeGroup(group)" />
            </span>
          </button>
        </section>

        <section class="lg:col-span-2">
          <div class="flex items-center justify-between gap-3 mb-3">
            <div>
              <h2 class="font-semibold text-lg">{{ selectedGroup?.nome || 'Selecione um grupo' }}</h2>
              <p class="text-sm text-gray-500">{{ phrases.length }} frase(s)</p>
            </div>
            <DsButton v-if="auth.isAdmin && selectedGroup" variant="success" size="sm" icon="plus" @click="openPhrase()">Nova frase</DsButton>
          </div>
          <DsAlert v-if="!selectedGroup" variant="info">Selecione um grupo para visualizar suas frases.</DsAlert>
          <div v-else-if="loadingPhrases" class="py-8 text-center text-gray-500">Carregando frases...</div>
          <DsAlert v-else-if="!phrases.length" variant="info">Nenhuma frase neste grupo.</DsAlert>
          <DsTable v-else>
            <template #head><tr><th>Código</th><th>Título</th><th>Formato</th><th>Status</th><th /></tr></template>
            <tr v-for="phrase in phrases" :key="phrase.codFrase">
              <td><code>{{ phrase.codigo }}</code></td>
              <td>{{ phrase.titulo }}</td>
              <td><DsBadge variant="default">{{ phrase.conteudoRtf ? 'RTF' : 'HTML/texto' }}</DsBadge></td>
              <td>{{ phrase.status ? 'Ativo' : 'Inativo' }}</td>
              <td><div class="flex justify-end gap-1">
                <DsButton variant="ghost" size="sm" icon="eye" @click="previewPhrase(phrase)" />
                <DsButton v-if="auth.isAdmin" variant="ghost" size="sm" icon="pencil" @click="openPhrase(phrase)" />
                <DsButton v-if="auth.isAdmin" variant="ghost" size="sm" icon="trash" @click="removePhrase(phrase)" />
              </div></td>
            </tr>
          </DsTable>
        </section>
      </div>
    </DsPageShell>

    <DsModal v-model="groupModal" :title="groupForm.codGrupo ? 'Editar grupo' : 'Novo grupo'">
      <form class="space-y-4" @submit.prevent="saveGroup">
        <DsInput v-model="groupForm.nome" label="Nome *" required />
        <DsSelect v-model="groupForm.status" label="Status"><option :value="1">Ativo</option><option :value="0">Inativo</option></DsSelect>
        <div class="flex justify-end gap-2"><DsButton variant="secondary" @click="groupModal = false">Cancelar</DsButton><DsButton type="submit" :loading="saving">Salvar</DsButton></div>
      </form>
    </DsModal>

    <DsModal v-model="phraseModal" :title="phraseForm.codFrase ? 'Editar frase' : 'Nova frase'" size="xl">
      <form class="space-y-4" @submit.prevent="savePhrase">
        <div class="grid md:grid-cols-3 gap-3">
          <DsInput v-model="phraseForm.codigo" label="Código *" required />
          <DsInput v-model="phraseForm.titulo" label="Título *" required input-class="md:col-span-2" />
          <DsSelect v-model="phraseForm.status" label="Status"><option :value="1">Ativa</option><option :value="0">Inativa</option></DsSelect>
        </div>
        <DsTextarea v-model="phraseForm.conteudo" label="Conteúdo HTML ou RTF" :rows="14" hint="RTF legado é preservado; novos conteúdos HTML são gravados em UTF-8." />
        <div class="flex justify-end gap-2"><DsButton variant="secondary" @click="phraseModal = false">Cancelar</DsButton><DsButton type="submit" :loading="saving">Salvar</DsButton></div>
      </form>
    </DsModal>

    <DsModal v-model="previewModal" :title="preview?.titulo || 'Visualização'" size="xl">
      <iframe v-if="preview" class="w-full min-h-[420px] rounded-xl border border-gray-200 bg-white" sandbox :srcdoc="preview.conteudoHtml" title="Prévia da frase" />
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
type Group = { codGrupo: number; nome: string; status: number }
type Phrase = { codFrase: number; codGrupo: number; codigo: string; titulo: string; conteudo: string; conteudoHtml: string; conteudoRtf: string; status: number }
type Api<T> = { success: boolean; data: T; message?: string }

const api = useApi(); const auth = useAuthStore(); const swal = useSwal()
const groups = ref<Group[]>([]); const phrases = ref<Phrase[]>([]); const selectedGroup = ref<Group | null>(null)
const loading = ref(false); const loadingPhrases = ref(false); const saving = ref(false); const errorMsg = ref('')
const groupModal = ref(false); const phraseModal = ref(false); const previewModal = ref(false); const preview = ref<Phrase | null>(null)
const groupForm = reactive({ codGrupo: 0, nome: '', status: 1 })
const phraseForm = reactive({ codFrase: 0, codigo: '', titulo: '', conteudo: '', status: 1 })

async function loadGroups() { loading.value = true; errorMsg.value = ''; try { const r = await api.get<Api<Group[]>>('/api/web/conteudos/grupos-frases'); groups.value = r.data || []; if (selectedGroup.value) { selectedGroup.value = groups.value.find(g => g.codGrupo === selectedGroup.value?.codGrupo) || null; if (selectedGroup.value) await loadPhrases() } } catch (e) { errorMsg.value = message(e) } finally { loading.value = false } }
async function selectGroup(group: Group) { selectedGroup.value = group; await loadPhrases() }
async function loadPhrases() { if (!selectedGroup.value) return; loadingPhrases.value = true; try { const r = await api.get<Api<{ grupo: string; frases: Phrase[] }>>(`/api/web/conteudos/grupos-frases/${selectedGroup.value.codGrupo}/frases`); phrases.value = r.data.frases || [] } catch (e) { phrases.value = []; await swal.toast(message(e), 'error') } finally { loadingPhrases.value = false } }
function openGroup(group?: Group) { Object.assign(groupForm, { codGrupo: group?.codGrupo || 0, nome: group?.nome || '', status: group?.status ?? 1 }); groupModal.value = true }
async function saveGroup() { saving.value = true; try { const body = { nome: groupForm.nome, status: Number(groupForm.status) }; if (groupForm.codGrupo) await api.put(`/api/web/conteudos/grupos-frases/${groupForm.codGrupo}`, body); else await api.post('/api/web/conteudos/grupos-frases', body); groupModal.value = false; await loadGroups(); await swal.toast('Grupo salvo com sucesso.') } catch (e) { await swal.toast(message(e), 'error') } finally { saving.value = false } }
async function removeGroup(group: Group) { const c = await swal.confirm('Excluir grupo', `Deseja excluir "${group.nome}"?`); if (!c?.isConfirmed) return; try { await api.del(`/api/web/conteudos/grupos-frases/${group.codGrupo}`); if (selectedGroup.value?.codGrupo === group.codGrupo) { selectedGroup.value = null; phrases.value = [] } await loadGroups(); await swal.toast('Grupo excluído.') } catch (e) { await swal.toast(message(e), 'error') } }
function openPhrase(phrase?: Phrase) { Object.assign(phraseForm, { codFrase: phrase?.codFrase || 0, codigo: phrase?.codigo || '', titulo: phrase?.titulo || '', conteudo: phrase?.conteudo || '', status: phrase?.status ?? 1 }); phraseModal.value = true }
async function savePhrase() { if (!selectedGroup.value) return; saving.value = true; try { const body = { codGrupo: selectedGroup.value.codGrupo, codigo: phraseForm.codigo, titulo: phraseForm.titulo, conteudo: phraseForm.conteudo, status: Number(phraseForm.status) }; if (phraseForm.codFrase) await api.put(`/api/web/conteudos/frases/${phraseForm.codFrase}`, body); else await api.post('/api/web/conteudos/frases', body); phraseModal.value = false; await loadPhrases(); await swal.toast('Frase salva com sucesso.') } catch (e) { await swal.toast(message(e), 'error') } finally { saving.value = false } }
async function removePhrase(phrase: Phrase) { const c = await swal.confirm('Excluir frase', `Deseja excluir "${phrase.titulo}"?`); if (!c?.isConfirmed) return; try { await api.del(`/api/web/conteudos/frases/${phrase.codFrase}`); await loadPhrases(); await swal.toast('Frase excluída.') } catch (e) { await swal.toast(message(e), 'error') } }
function previewPhrase(phrase: Phrase) { preview.value = phrase; previewModal.value = true }
function message(e: unknown) { return e instanceof Error ? e.message : 'Ocorreu um erro inesperado.' }
onMounted(loadGroups)
</script>
