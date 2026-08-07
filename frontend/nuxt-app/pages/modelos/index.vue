<template>
  <div>
    <DsPageHeader title="Modelos e seções" subtitle="Modelos de modo texto do usuário autenticado" icon="layout-text-window-reverse" />
    <DsPageShell>
      <div class="flex flex-col md:flex-row gap-3 mb-5 items-end">
        <DsInput v-model="search" label="Nome" class="flex-1" @enter="load(1)" />
        <DsButton size="sm" icon="search" @click="load(1)">Pesquisar</DsButton>
        <DsButton size="sm" variant="success" icon="plus-lg" @click="openCreate">Novo modelo</DsButton>
      </div>
      <div v-if="loading" class="text-center py-10 text-gray-500">Carregando modelos...</div>
      <DsAlert v-else-if="errorMessage" variant="error">{{ errorMessage }}</DsAlert>
      <DsAlert v-else-if="!items.length" variant="info">Nenhum modelo encontrado.</DsAlert>
      <DsTable v-else>
        <template #head><tr><th>Modelo</th><th>Seções</th><th /></tr></template>
        <tr v-for="item in items" :key="item.codModelo">
          <td><strong>{{ item.nome }}</strong><br><small class="text-gray-500">#{{ item.codModelo }}</small></td>
          <td>{{ item.totalSecoes }}</td>
          <td><div class="flex justify-end gap-2">
            <DsButton size="sm" variant="secondary" icon="sliders" :to="`/modelos/${item.codModelo}`">Seções e layout</DsButton>
            <DsButton size="sm" variant="ghost" icon="pencil" @click="openEdit(item)">Renomear</DsButton>
            <DsButton size="sm" variant="danger" icon="trash" @click="remove(item)">Excluir</DsButton>
          </div></td>
        </tr>
      </DsTable>
      <div v-if="totalPages > 1" class="flex justify-center items-center gap-2 mt-6">
        <DsButton size="sm" variant="secondary" :disabled="page <= 1" @click="load(page - 1)">Anterior</DsButton>
        <span class="text-sm text-gray-500">Página {{ page }} de {{ totalPages }}</span>
        <DsButton size="sm" variant="secondary" :disabled="page >= totalPages" @click="load(page + 1)">Próxima</DsButton>
      </div>
    </DsPageShell>
    <DsModal v-model="editorOpen" :title="editingId ? 'Renomear modelo' : 'Novo modelo'" size="md">
      <DsInput v-model="name" label="Nome" required @enter="save" />
      <template #footer><DsButton variant="secondary" @click="editorOpen = false">Cancelar</DsButton><DsButton :loading="saving" @click="save">Salvar</DsButton></template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
interface ModelItem { codModelo: number; nome: string; totalSecoes: number }
const api = useApi(); const auth = useAuthStore(); const swal = useSwal()
const items = ref<ModelItem[]>([]); const search = ref(''); const page = ref(1); const totalPages = ref(0)
const loading = ref(false); const saving = ref(false); const errorMessage = ref(''); const editorOpen = ref(false); const editingId = ref<number | null>(null); const name = ref('')
async function load(target = 1) { loading.value = true; errorMessage.value = ''; page.value = target; try { const q = new URLSearchParams({ page: String(target), pageSize: '10', nome: search.value }); const r = await api.get<{ data: ModelItem[]; totalPages: number }>(`/api/web/modelos?${q}`); items.value = r.data || []; totalPages.value = r.totalPages || 0 } catch (e) { errorMessage.value = msg(e) } finally { loading.value = false } }
function openCreate() { editingId.value = null; name.value = ''; editorOpen.value = true }
function openEdit(item: ModelItem) { editingId.value = item.codModelo; name.value = item.nome; editorOpen.value = true }
async function save() { if (!name.value.trim()) return; saving.value = true; try { if (editingId.value) await api.put(`/api/web/modelos/${editingId.value}`, { nome: name.value }); else await api.post('/api/web/modelos', { nome: name.value }); editorOpen.value = false; await swal.toast('Modelo salvo com sucesso.'); await load(page.value) } catch (e) { await swal.toast(msg(e), 'error') } finally { saving.value = false } }
async function remove(item: ModelItem) { const ok = await swal.confirm('Excluir modelo', `Todas as seções de "${item.nome}" serão excluídas. Continuar?`); if (!ok?.isConfirmed) return; try { await api.del(`/api/web/modelos/${item.codModelo}`); await swal.toast('Modelo excluído.'); await load(page.value) } catch (e) { await swal.toast(msg(e), 'error') } }
function msg(e: unknown) { return e instanceof Error ? e.message : 'Erro inesperado.' }
onMounted(() => load(1))
</script>
