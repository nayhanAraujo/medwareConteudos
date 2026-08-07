<template>
  <div>
    <DsPageHeader title="Impressos" subtitle="Cadastro, importação e arquivos MRD/VBS" icon="printer" />
    <DsPageShell>
      <div class="flex flex-wrap justify-between gap-3 mb-5">
        <DsButton variant="secondary" icon="arrow-clockwise" :loading="loading" @click="load">Atualizar</DsButton>
        <div v-if="auth.isAdmin" class="flex gap-2"><DsButton variant="secondary" icon="upload" @click="openImport">Importar MRD</DsButton><DsButton variant="success" icon="plus" @click="openForm()">Novo impresso</DsButton></div>
      </div>
      <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>
      <div v-if="loading" class="py-10 text-center text-gray-500">Carregando impressos...</div>
      <DsAlert v-else-if="!items.length" variant="info">Nenhum impresso cadastrado.</DsAlert>
      <DsTable v-else>
        <template #head><tr><th>#</th><th>Título</th><th>Opções</th><th>VBS</th><th>Alteração</th><th /></tr></template>
        <tr v-for="item in items" :key="item.codImpresso">
          <td>{{ item.codImpresso }}</td><td><strong>{{ item.titulo }}</strong><small class="block text-gray-500">{{ item.usuarioNome || 'Usuário não informado' }}</small></td>
          <td><span class="text-sm">{{ item.usoGeral ? 'Uso geral' : 'Restrito' }} · {{ item.imprimirCabecalho ? 'Com cabeçalho' : 'Sem cabeçalho' }}</span></td>
          <td><DsBadge :variant="item.temVbs ? 'success' : 'default'">{{ item.temVbs ? 'Anexado' : 'Ausente' }}</DsBadge></td>
          <td>{{ formatDate(item.dthrUltModificacao) }}</td>
          <td><div class="flex flex-wrap justify-end gap-1">
            <DsButton variant="ghost" size="sm" icon="download" @click="download(item, 'mrd')">MRD</DsButton>
            <DsButton v-if="item.temVbs" variant="ghost" size="sm" icon="file-earmark-code" @click="download(item, 'vbs')">VBS</DsButton>
            <DsButton v-if="auth.isAdmin" variant="ghost" size="sm" icon="pencil" @click="openForm(item)" />
            <DsButton v-if="auth.isAdmin" variant="ghost" size="sm" icon="trash" @click="remove(item)" />
          </div></td>
        </tr>
      </DsTable>
    </DsPageShell>

    <DsModal v-model="formModal" :title="form.id ? 'Editar impresso' : 'Novo impresso'" size="xl" :close-on-backdrop="false">
      <form class="space-y-4" @submit.prevent="save">
        <DsInput v-model="form.titulo" label="Título *" required />
        <DsTextarea v-model="form.conteudo" label="Conteúdo MRD *" :rows="16" required />
        <div class="grid md:grid-cols-2 gap-3">
          <label class="flex items-center gap-2 rounded-xl border border-gray-200 p-3"><input v-model="form.usoGeral" type="checkbox" /> Uso geral</label>
          <label class="flex items-center gap-2 rounded-xl border border-gray-200 p-3"><input v-model="form.imprimirCabecalho" type="checkbox" /> Imprimir cabeçalho</label>
        </div>
        <DsFileInput label="Arquivo VBS (opcional)" accept=".vbs" hint="Ao editar, um novo VBS substitui o arquivo existente." @change="files => formVbs = files?.[0] || null" />
        <div v-if="form.temVbs" class="flex items-center justify-between rounded-xl bg-gray-50 p-3"><span class="text-sm">Este impresso possui VBS associado.</span><DsButton v-if="auth.isAdmin" variant="danger" size="sm" icon="trash" @click="removeVbs">Remover VBS</DsButton></div>
        <div class="flex justify-end gap-2"><DsButton variant="secondary" @click="formModal = false">Cancelar</DsButton><DsButton type="submit" :loading="saving">Salvar</DsButton></div>
      </form>
    </DsModal>

    <DsModal v-model="importModal" title="Importar impresso MRD" size="lg" :close-on-backdrop="false">
      <form class="space-y-4" @submit.prevent="importMrd">
        <DsInput v-model="importForm.titulo" label="Título *" required />
        <DsFileInput label="Arquivo MRD *" accept=".mrd" required @change="files => importFile = files?.[0] || null" />
        <DsFileInput label="Arquivo VBS (opcional)" accept=".vbs" @change="files => importVbs = files?.[0] || null" />
        <div class="grid md:grid-cols-2 gap-3"><label class="flex items-center gap-2 rounded-xl border border-gray-200 p-3"><input v-model="importForm.usoGeral" type="checkbox" /> Uso geral</label><label class="flex items-center gap-2 rounded-xl border border-gray-200 p-3"><input v-model="importForm.imprimirCabecalho" type="checkbox" /> Imprimir cabeçalho</label></div>
        <div class="flex justify-end gap-2"><DsButton variant="secondary" @click="importModal = false">Cancelar</DsButton><DsButton type="submit" :loading="saving">Importar</DsButton></div>
      </form>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
type Item = { codImpresso: number; titulo: string; usoGeral: number; imprimirCabecalho: number; dthrUltModificacao?: string; usuarioNome?: string; temVbs: boolean }
type Detail = { codImpresso: number; titulo: string; conteudo: string; usoGeral: number; imprimirCabecalho: number; scriptVbs?: string }
type Api<T> = { success: boolean; data: T }
const api = useApi(); const auth = useAuthStore(); const swal = useSwal()
const items = ref<Item[]>([]); const loading = ref(false); const saving = ref(false); const errorMsg = ref(''); const formModal = ref(false); const importModal = ref(false)
const form = reactive({ id: 0, titulo: '', conteudo: '', usoGeral: false, imprimirCabecalho: false, temVbs: false }); const formVbs = ref<File | null>(null)
const importForm = reactive({ titulo: '', usoGeral: false, imprimirCabecalho: false }); const importFile = ref<File | null>(null); const importVbs = ref<File | null>(null)
async function load() { loading.value = true; errorMsg.value = ''; try { const r = await api.get<Api<Item[]>>('/api/web/impressos'); items.value = r.data || [] } catch (e) { errorMsg.value = msg(e) } finally { loading.value = false } }
async function openForm(item?: Item) { formVbs.value = null; if (!item) { Object.assign(form, { id: 0, titulo: '', conteudo: '', usoGeral: false, imprimirCabecalho: false, temVbs: false }); formModal.value = true; return } try { const r = await api.get<Api<Detail>>(`/api/web/impressos/${item.codImpresso}`); Object.assign(form, { id: r.data.codImpresso, titulo: r.data.titulo, conteudo: r.data.conteudo, usoGeral: !!r.data.usoGeral, imprimirCabecalho: !!r.data.imprimirCabecalho, temVbs: !!r.data.scriptVbs }); formModal.value = true } catch (e) { await swal.toast(msg(e), 'error') } }
function makeFormData() { const data = new FormData(); data.append('titulo', form.titulo); data.append('conteudo', form.conteudo); data.append('usoGeral', String(form.usoGeral)); data.append('imprimirCabecalho', String(form.imprimirCabecalho)); if (formVbs.value) data.append('arquivoVbs', formVbs.value); return data }
async function save() { saving.value = true; try { const data = makeFormData(); if (form.id) await api.putForm(`/api/web/impressos/${form.id}`, data); else await api.postForm('/api/web/impressos', data); formModal.value = false; await load(); await swal.toast('Impresso salvo com sucesso.') } catch (e) { await swal.toast(msg(e), 'error') } finally { saving.value = false } }
async function remove(item: Item) { const c = await swal.confirm('Excluir impresso', `Deseja excluir "${item.titulo}" e o VBS associado?`); if (!c?.isConfirmed) return; try { await api.del(`/api/web/impressos/${item.codImpresso}`); await load(); await swal.toast('Impresso excluído.') } catch (e) { await swal.toast(msg(e), 'error') } }
async function removeVbs() { if (!form.id) return; const c = await swal.confirm('Remover VBS', 'Deseja remover o arquivo VBS associado?'); if (!c?.isConfirmed) return; try { await api.del(`/api/web/impressos/${form.id}/vbs`); form.temVbs = false; await load(); await swal.toast('VBS removido.') } catch (e) { await swal.toast(msg(e), 'error') } }
function openImport() { Object.assign(importForm, { titulo: '', usoGeral: false, imprimirCabecalho: false }); importFile.value = null; importVbs.value = null; importModal.value = true }
async function importMrd() { if (!importFile.value) { await swal.toast('Selecione um arquivo .mrd.', 'error'); return } saving.value = true; try { const data = new FormData(); data.append('titulo', importForm.titulo); data.append('usoGeral', String(importForm.usoGeral)); data.append('imprimirCabecalho', String(importForm.imprimirCabecalho)); data.append('arquivoMrd', importFile.value); if (importVbs.value) data.append('arquivoVbs', importVbs.value); await api.postForm('/api/web/impressos/importar', data); importModal.value = false; await load(); await swal.toast('Impresso importado com sucesso.') } catch (e) { await swal.toast(msg(e), 'error') } finally { saving.value = false } }
async function download(item: Item, kind: 'mrd' | 'vbs') { try { const blob = await api.getBlob(`/api/web/impressos/${item.codImpresso}/download/${kind}`); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = kind === 'mrd' ? `${item.titulo}.mrd` : `${item.titulo}_script.vbs`; anchor.click(); URL.revokeObjectURL(url) } catch (e) { await swal.toast(msg(e), 'error') } }
function formatDate(value?: string) { return value ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '—' }
function msg(e: unknown) { return e instanceof Error ? e.message : 'Ocorreu um erro inesperado.' }
onMounted(load)
</script>
