<template>
  <div>
    <DsPageHeader title="Modelos de mensagens" subtitle="Grupos, textos reutilizáveis, variáveis e emojis" icon="chat-square-heart" />
    <DsPageShell>
      <div class="flex flex-wrap justify-between gap-3 mb-5">
        <DsButton variant="secondary" icon="arrow-clockwise" :loading="loading" @click="loadGroups">Atualizar</DsButton>
        <DsButton v-if="auth.isAdmin" variant="success" icon="folder-plus" @click="openGroup()">Novo grupo</DsButton>
      </div>
      <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <section class="rounded-2xl border border-gray-100 overflow-hidden">
          <header class="px-4 py-3 bg-gray-50 font-semibold">Grupos</header>
          <div v-if="loading" class="p-5 text-sm text-gray-500">Carregando...</div>
          <button v-for="group in groups" :key="group.codGrupoMensagem" type="button"
            class="w-full text-left px-4 py-3 border-0 border-t border-gray-100 flex gap-2 items-center hover:bg-gray-50"
            :class="selectedGroup?.codGrupoMensagem === group.codGrupoMensagem ? 'bg-sky-50' : 'bg-white'" @click="selectGroup(group)">
            <span class="min-w-0 flex-1"><strong class="block truncate">{{ group.nome }}</strong><small class="block truncate text-gray-500">{{ group.descricao || 'Sem descrição' }}</small></span>
            <DsBadge :variant="group.ativo ? 'success' : 'default'">{{ group.ativo ? 'Ativo' : 'Inativo' }}</DsBadge>
            <span v-if="auth.isAdmin" class="flex" @click.stop><DsButton variant="ghost" size="sm" icon="pencil" @click="openGroup(group)" /><DsButton variant="ghost" size="sm" icon="trash" @click="removeGroup(group)" /></span>
          </button>
          <p v-if="!loading && !groups.length" class="p-5 text-sm text-gray-500">Nenhum grupo cadastrado.</p>
        </section>

        <section class="lg:col-span-2">
          <div class="flex justify-between items-center gap-3 mb-3">
            <div><h2 class="font-semibold text-lg">{{ selectedGroup?.nome || 'Selecione um grupo' }}</h2><p class="text-sm text-gray-500">{{ messages.length }} modelo(s)</p></div>
            <DsButton v-if="auth.isAdmin && selectedGroup" variant="success" size="sm" icon="plus" @click="openMessage()">Nova mensagem</DsButton>
          </div>
          <DsAlert v-if="!selectedGroup" variant="info">Selecione um grupo para visualizar as mensagens.</DsAlert>
          <div v-else-if="loadingMessages" class="py-8 text-center text-gray-500">Carregando mensagens...</div>
          <DsAlert v-else-if="!messages.length" variant="info">Nenhuma mensagem neste grupo.</DsAlert>
          <div v-else class="space-y-3">
            <article v-for="item in messages" :key="item.codModeloMensagem" class="rounded-2xl border border-gray-100 p-4">
              <div class="flex items-start gap-3">
                <div class="min-w-0 flex-1"><div class="flex flex-wrap gap-2 items-center"><h3 class="font-semibold">{{ item.titulo }}</h3><DsBadge variant="default">{{ item.tipoMensagem }}</DsBadge><DsBadge :variant="item.ativo ? 'success' : 'default'">{{ item.ativo ? 'Ativa' : 'Inativa' }}</DsBadge></div><p class="mt-2 text-sm whitespace-pre-wrap text-gray-700">{{ item.conteudo }}</p></div>
                <div v-if="auth.isAdmin" class="flex"><DsButton variant="ghost" size="sm" icon="pencil" @click="openMessage(item)" /><DsButton variant="ghost" size="sm" icon="trash" @click="removeMessage(item)" /></div>
              </div>
            </article>
          </div>
        </section>
      </div>
    </DsPageShell>

    <DsModal v-model="groupModal" :title="groupForm.id ? 'Editar grupo' : 'Novo grupo'">
      <form class="space-y-4" @submit.prevent="saveGroup"><DsInput v-model="groupForm.nome" label="Nome *" required /><DsTextarea v-model="groupForm.descricao" label="Descrição" /><DsSelect v-model="groupForm.ativo" label="Status"><option :value="1">Ativo</option><option :value="0">Inativo</option></DsSelect><div class="flex justify-end gap-2"><DsButton variant="secondary" @click="groupModal = false">Cancelar</DsButton><DsButton type="submit" :loading="saving">Salvar</DsButton></div></form>
    </DsModal>

    <DsModal v-model="messageModal" :title="messageForm.id ? 'Editar mensagem' : 'Nova mensagem'" size="xl">
      <form class="space-y-4" @submit.prevent="saveMessage">
        <div class="grid md:grid-cols-3 gap-3"><DsInput v-model="messageForm.titulo" label="Título *" required input-class="md:col-span-2" /><DsSelect v-model="messageForm.tipoMensagem" label="Tipo"><option value="TEXTO">Texto</option><option value="WHATSAPP">WhatsApp</option><option value="SMS">SMS</option><option value="EMAIL">E-mail</option></DsSelect><DsSelect v-model="messageForm.ativo" label="Status"><option :value="1">Ativa</option><option :value="0">Inativa</option></DsSelect></div>
        <DsTextarea v-model="messageForm.conteudo" label="Conteúdo *" :rows="10" required />
        <div class="grid md:grid-cols-2 gap-4">
          <div><label class="block text-sm font-medium mb-2">Inserir variável</label><div class="flex gap-2"><DsSelect v-model="selectedVariable" class="flex-1"><option value="">Selecione</option><option v-for="v in variables" :key="v.codVariaveisPacConectado" :value="v.variavel">{{ v.variavel }} — {{ v.descricao }}</option></DsSelect><DsButton variant="secondary" icon="plus" :disabled="!selectedVariable" @click="insertToken(`[${selectedVariable}]`)">Inserir</DsButton></div></div>
          <div><label class="block text-sm font-medium mb-2">Inserir emoji</label><div class="flex flex-wrap gap-1 max-h-28 overflow-y-auto rounded-xl border border-gray-100 p-2"><button v-for="emoji in flatEmojis" :key="emoji.codigo" type="button" class="text-xl border-0 bg-transparent hover:bg-gray-100 rounded p-1" :title="emoji.nome" @click="insertToken(emoji.emoji)">{{ emoji.emoji }}</button></div></div>
        </div>
        <div class="flex justify-end gap-2"><DsButton variant="secondary" @click="messageModal = false">Cancelar</DsButton><DsButton type="submit" :loading="saving">Salvar</DsButton></div>
      </form>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
type Group = { codGrupoMensagem: number; nome: string; descricao?: string; ativo: number; dthrUltModificacao?: string }
type Message = { codModeloMensagem: number; codGrupoMensagem: number; titulo: string; conteudo: string; tipoMensagem: string; ativo: number }
type Variable = { codVariaveisPacConectado: number; variavel: string; descricao?: string }
type Emoji = { emoji: string; nome: string; codigo: string }
type Api<T> = { success: boolean; data: T }
const api = useApi(); const auth = useAuthStore(); const swal = useSwal()
const groups = ref<Group[]>([]); const messages = ref<Message[]>([]); const variables = ref<Variable[]>([]); const emojis = ref<Record<string, Emoji[]>>({}); const selectedGroup = ref<Group | null>(null)
const loading = ref(false); const loadingMessages = ref(false); const saving = ref(false); const errorMsg = ref(''); const groupModal = ref(false); const messageModal = ref(false); const selectedVariable = ref('')
const groupForm = reactive({ id: 0, nome: '', descricao: '', ativo: 1 }); const messageForm = reactive({ id: 0, titulo: '', conteudo: '', tipoMensagem: 'TEXTO', ativo: 1 })
const flatEmojis = computed(() => Object.values(emojis.value).flat())
async function loadGroups() { loading.value = true; errorMsg.value = ''; try { const r = await api.get<Api<Group[]>>('/api/web/modelos-mensagens/grupos'); groups.value = r.data || []; if (selectedGroup.value) selectedGroup.value = groups.value.find(g => g.codGrupoMensagem === selectedGroup.value?.codGrupoMensagem) || null } catch (e) { errorMsg.value = msg(e) } finally { loading.value = false } }
async function loadCatalogs() { try { const [v, e] = await Promise.all([api.get<Api<Variable[]>>('/api/web/modelos-mensagens/variaveis'), api.get<Api<Record<string, Emoji[]>>>('/api/web/modelos-mensagens/emojis')]); variables.value = v.data || []; emojis.value = e.data || {} } catch { variables.value = []; emojis.value = {} } }
async function selectGroup(group: Group) { selectedGroup.value = group; await loadMessages() }
async function loadMessages() { if (!selectedGroup.value) return; loadingMessages.value = true; try { const r = await api.get<Api<Message[]>>(`/api/web/modelos-mensagens/grupos/${selectedGroup.value.codGrupoMensagem}/mensagens`); messages.value = r.data || [] } catch (e) { messages.value = []; await swal.toast(msg(e), 'error') } finally { loadingMessages.value = false } }
function openGroup(g?: Group) { Object.assign(groupForm, { id: g?.codGrupoMensagem || 0, nome: g?.nome || '', descricao: g?.descricao || '', ativo: g?.ativo ?? 1 }); groupModal.value = true }
async function saveGroup() { saving.value = true; try { const body = { nome: groupForm.nome, descricao: groupForm.descricao, ativo: Number(groupForm.ativo) }; if (groupForm.id) await api.put(`/api/web/modelos-mensagens/grupos/${groupForm.id}`, body); else await api.post('/api/web/modelos-mensagens/grupos', body); groupModal.value = false; await loadGroups(); await swal.toast('Grupo salvo com sucesso.') } catch (e) { await swal.toast(msg(e), 'error') } finally { saving.value = false } }
async function removeGroup(g: Group) { const c = await swal.confirm('Excluir grupo', `Deseja excluir "${g.nome}"?`); if (!c?.isConfirmed) return; try { await api.del(`/api/web/modelos-mensagens/grupos/${g.codGrupoMensagem}`); if (selectedGroup.value?.codGrupoMensagem === g.codGrupoMensagem) { selectedGroup.value = null; messages.value = [] } await loadGroups(); await swal.toast('Grupo excluído.') } catch (e) { await swal.toast(msg(e), 'error') } }
function openMessage(m?: Message) { Object.assign(messageForm, { id: m?.codModeloMensagem || 0, titulo: m?.titulo || '', conteudo: m?.conteudo || '', tipoMensagem: m?.tipoMensagem || 'TEXTO', ativo: m?.ativo ?? 1 }); selectedVariable.value = ''; messageModal.value = true }
function insertToken(value: string) { messageForm.conteudo += value }
async function saveMessage() { if (!selectedGroup.value) return; saving.value = true; try { const body = { codGrupoMensagem: selectedGroup.value.codGrupoMensagem, titulo: messageForm.titulo, conteudo: messageForm.conteudo, tipoMensagem: messageForm.tipoMensagem, ativo: Number(messageForm.ativo) }; if (messageForm.id) await api.put(`/api/web/modelos-mensagens/mensagens/${messageForm.id}`, body); else await api.post('/api/web/modelos-mensagens/mensagens', body); messageModal.value = false; await loadMessages(); await swal.toast('Mensagem salva com sucesso.') } catch (e) { await swal.toast(msg(e), 'error') } finally { saving.value = false } }
async function removeMessage(m: Message) { const c = await swal.confirm('Excluir mensagem', `Deseja excluir "${m.titulo}"?`); if (!c?.isConfirmed) return; try { await api.del(`/api/web/modelos-mensagens/mensagens/${m.codModeloMensagem}`); await loadMessages(); await swal.toast('Mensagem excluída.') } catch (e) { await swal.toast(msg(e), 'error') } }
function msg(e: unknown) { return e instanceof Error ? e.message : 'Ocorreu um erro inesperado.' }
onMounted(async () => { await Promise.all([loadGroups(), loadCatalogs()]) })
</script>
