<template>
  <div>
    <DsPageHeader :title="model?.nome || 'Modelo'" subtitle="Seções, variáveis, ordem, layout e geração" icon="grid-3x3-gap" />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-5">
        <DsButton size="sm" variant="secondary" icon="arrow-left" to="/modelos">Modelos</DsButton>
        <DsButton size="sm" variant="success" icon="plus-lg" @click="openCreateSection">Nova seção</DsButton>
        <DsButton size="sm" variant="secondary" icon="eye" @click="preview('html')">Pré-visualizar HTML</DsButton>
        <DsButton size="sm" variant="secondary" icon="filetype-html" @click="download('html')">Baixar HTML</DsButton>
        <DsButton size="sm" variant="secondary" icon="file-text" @click="download('texto')">Baixar modo texto</DsButton>
      </div>

      <div v-if="loading" class="text-center py-10 text-gray-500">Carregando modelo...</div>
      <DsAlert v-else-if="errorMessage" variant="error">{{ errorMessage }}</DsAlert>
      <DsAlert v-else-if="model && !model.secoes.length" variant="info">Este modelo ainda não possui seções.</DsAlert>

      <div v-else-if="model" class="space-y-4">
        <article v-for="(section, sectionIndex) in model.secoes" :key="section.codSecao" class="border border-gray-200 rounded-2xl p-4 bg-white">
          <div class="flex flex-col lg:flex-row lg:items-start justify-between gap-3">
            <div>
              <h2 class="font-semibold text-lg">{{ sectionIndex + 1 }}. {{ section.nome }}</h2>
              <p class="text-xs text-gray-500">Grid: x={{ section.x }}, y={{ section.y }}, largura={{ section.largura }}, altura={{ section.altura }}</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <DsButton size="sm" variant="ghost" icon="arrow-up" :disabled="sectionIndex === 0" @click="moveSection(sectionIndex, -1)">Subir</DsButton>
              <DsButton size="sm" variant="ghost" icon="arrow-down" :disabled="sectionIndex === model.secoes.length - 1" @click="moveSection(sectionIndex, 1)">Descer</DsButton>
              <DsButton size="sm" variant="secondary" icon="pencil" @click="openEditSection(section)">Editar</DsButton>
              <DsButton size="sm" variant="danger" icon="trash" @click="removeSection(section)">Excluir</DsButton>
            </div>
          </div>

          <div v-if="section.variaveis.length" class="mt-4">
            <DsTable>
              <template #head><tr><th>Ordem</th><th>Variável</th><th>Sigla</th><th>Gráfico</th><th /></tr></template>
              <tr v-for="(variable, variableIndex) in section.variaveis" :key="variable.codVariavel">
                <td>{{ variableIndex + 1 }}</td><td>{{ variable.nome }}</td><td><code>{{ variable.sigla }}</code></td><td>{{ variable.exibirGrafico ? 'Sim' : 'Não' }}</td>
                <td><div class="flex justify-end gap-1">
                  <DsButton size="sm" variant="ghost" icon="arrow-up" :disabled="variableIndex === 0" @click="moveVariable(section, variableIndex, -1)" />
                  <DsButton size="sm" variant="ghost" icon="arrow-down" :disabled="variableIndex === section.variaveis.length - 1" @click="moveVariable(section, variableIndex, 1)" />
                </div></td>
              </tr>
            </DsTable>
          </div>
          <p v-else class="text-sm text-gray-500 mt-3">Nenhuma variável vinculada.</p>

          <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mt-4 border-t border-gray-100 pt-4">
            <DsInput v-model="section.x" type="number" label="Posição X" />
            <DsInput v-model="section.y" type="number" label="Posição Y" />
            <DsInput v-model="section.largura" type="number" label="Largura (1-12)" />
            <DsInput v-model="section.altura" type="number" label="Altura" />
          </div>
        </article>
        <div v-if="model.secoes.length" class="flex justify-end"><DsButton :loading="savingLayout" icon="grid" @click="saveLayout">Salvar layout</DsButton></div>
      </div>
    </DsPageShell>

    <DsModal v-model="sectionOpen" :title="editingSectionId ? 'Editar seção' : 'Nova seção'" size="xl">
      <div class="space-y-4">
        <DsInput v-model="sectionForm.nome" label="Nome da seção" required />
        <DsSearchInput v-model="variableSearch" placeholder="Pesquisar variável..." />
        <div class="max-h-96 overflow-y-auto border border-gray-200 rounded-2xl">
          <table class="w-full text-sm">
            <thead class="sticky top-0 bg-gray-50"><tr><th class="p-3 text-left">Usar</th><th class="p-3 text-left">Variável</th><th class="p-3 text-left">Fórmula / normalidade</th><th class="p-3 text-left">Gráfico</th></tr></thead>
            <tbody>
              <tr v-for="variable in filteredVariables" :key="variable.codVariavel" class="border-t border-gray-100">
                <td class="p-3"><input v-model="sectionForm.variableIds" type="checkbox" :value="variable.codVariavel"></td>
                <td class="p-3"><strong>{{ variable.nome }}</strong><br><code>{{ variable.sigla }}</code></td>
                <td class="p-3 text-xs text-gray-500"><div>{{ variable.formula || 'Sem fórmula' }}</div><div>{{ variable.normalidade || 'Sem normalidade' }}</div></td>
                <td class="p-3"><input v-model="sectionForm.chartIds" type="checkbox" :value="variable.codVariavel" :disabled="!sectionForm.variableIds.includes(variable.codVariavel)"></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <template #footer><DsButton variant="secondary" @click="sectionOpen = false">Cancelar</DsButton><DsButton :loading="savingSection" @click="saveSection">Salvar</DsButton></template>
    </DsModal>

    <DsModal v-model="previewOpen" title="Pré-visualização HTML" size="xl">
      <iframe v-if="previewUrl" :src="previewUrl" title="Pré-visualização do modelo" class="w-full h-[65vh] border border-gray-200 rounded-xl" />
      <template #footer><DsButton variant="secondary" @click="previewOpen = false">Fechar</DsButton></template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
interface VariableOption { codVariavel: number; nome: string; sigla: string; formula?: string; normalidade?: string }
interface SectionVariable { codVariavel: number; nome: string; sigla: string; exibirGrafico: boolean; ordem: number }
interface Section { codSecao: number; nome: string; ordem: number; x: number | string; y: number | string; largura: number | string; altura: number | string; variaveis: SectionVariable[] }
interface ModelDetail { codModelo: number; nome: string; secoes: Section[] }

const route = useRoute(); const api = useApi(); const auth = useAuthStore(); const swal = useSwal(); const modelId = Number(route.params.id)
const model = ref<ModelDetail | null>(null); const variables = ref<VariableOption[]>([]); const loading = ref(false); const errorMessage = ref('')
const sectionOpen = ref(false); const editingSectionId = ref<number | null>(null); const savingSection = ref(false); const savingLayout = ref(false); const variableSearch = ref('')
const previewOpen = ref(false); const previewUrl = ref('')
const sectionForm = reactive({ nome: '', variableIds: [] as number[], chartIds: [] as number[] })
const filteredVariables = computed(() => { const term = variableSearch.value.trim().toLocaleLowerCase(); return !term ? variables.value : variables.value.filter(v => `${v.nome} ${v.sigla}`.toLocaleLowerCase().includes(term)) })

async function load() { loading.value = true; errorMessage.value = ''; try { const [m, v] = await Promise.all([api.get<{ data: ModelDetail }>(`/api/web/modelos/${modelId}`), api.get<{ data: VariableOption[] }>('/api/web/modelos/variaveis')]); model.value = m.data; variables.value = v.data || [] } catch (e) { errorMessage.value = msg(e) } finally { loading.value = false } }
function openCreateSection() { editingSectionId.value = null; variableSearch.value = ''; Object.assign(sectionForm, { nome: '', variableIds: [], chartIds: [] }); sectionOpen.value = true }
function openEditSection(section: Section) { editingSectionId.value = section.codSecao; variableSearch.value = ''; Object.assign(sectionForm, { nome: section.nome, variableIds: section.variaveis.map(v => v.codVariavel), chartIds: section.variaveis.filter(v => v.exibirGrafico).map(v => v.codVariavel) }); sectionOpen.value = true }
async function saveSection() { if (!sectionForm.nome.trim()) { await swal.warning('Nome obrigatório', 'Informe o nome da seção.'); return } savingSection.value = true; const payload = { nome: sectionForm.nome, variaveis: sectionForm.variableIds.map((id, index) => ({ codVariavel: Number(id), exibirGrafico: sectionForm.chartIds.includes(id), ordem: index + 1 })) }; try { if (editingSectionId.value) await api.put(`/api/web/modelos/${modelId}/secoes/${editingSectionId.value}`, payload); else await api.post(`/api/web/modelos/${modelId}/secoes`, payload); sectionOpen.value = false; await swal.toast('Seção salva com sucesso.'); await load() } catch (e) { await swal.toast(msg(e), 'error') } finally { savingSection.value = false } }
async function removeSection(section: Section) { const ok = await swal.confirm('Excluir seção', `Deseja excluir "${section.nome}"?`); if (!ok?.isConfirmed) return; try { await api.del(`/api/web/modelos/${modelId}/secoes/${section.codSecao}`); await swal.toast('Seção excluída.'); await load() } catch (e) { await swal.toast(msg(e), 'error') } }
async function moveSection(index: number, offset: number) { if (!model.value) return; const next = index + offset; if (next < 0 || next >= model.value.secoes.length) return; const copy = [...model.value.secoes]; [copy[index], copy[next]] = [copy[next]!, copy[index]!]; try { await api.put(`/api/web/modelos/${modelId}/secoes/ordem`, { secaoIds: copy.map(s => s.codSecao) }); model.value.secoes = copy; await swal.toast('Ordem das seções atualizada.') } catch (e) { await swal.toast(msg(e), 'error') } }
async function moveVariable(section: Section, index: number, offset: number) { const next = index + offset; if (next < 0 || next >= section.variaveis.length) return; const copy = [...section.variaveis]; [copy[index], copy[next]] = [copy[next]!, copy[index]!]; try { await api.put(`/api/web/modelos/${modelId}/secoes/${section.codSecao}/variaveis/ordem`, { variavelIds: copy.map(v => v.codVariavel) }); section.variaveis = copy; await swal.toast('Ordem das variáveis atualizada.') } catch (e) { await swal.toast(msg(e), 'error') } }
async function saveLayout() { if (!model.value) return; savingLayout.value = true; const layout = model.value.secoes.map(s => ({ codSecao: s.codSecao, x: Number(s.x), y: Number(s.y), largura: Number(s.largura), altura: Number(s.altura) })); try { await api.put(`/api/web/modelos/${modelId}/layout`, { layout }); await swal.toast('Layout salvo com sucesso.'); await load() } catch (e) { await swal.toast(msg(e), 'error') } finally { savingLayout.value = false } }
async function preview(format: string) { try { const blob = await api.getBlob(`/api/web/modelos/${modelId}/gerar?formato=${format}`); if (previewUrl.value) URL.revokeObjectURL(previewUrl.value); previewUrl.value = URL.createObjectURL(blob); previewOpen.value = true } catch (e) { await swal.toast(msg(e), 'error') } }
async function download(format: string) { try { const blob = await api.getBlob(`/api/web/modelos/${modelId}/gerar?formato=${format}&download=true`); const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = `modelo_${modelId}.${format === 'html' ? 'html' : 'txt'}`; link.click(); URL.revokeObjectURL(url) } catch (e) { await swal.toast(msg(e), 'error') } }
function msg(e: unknown) { return e instanceof Error ? e.message : 'Erro inesperado.' }
onBeforeUnmount(() => { if (previewUrl.value) URL.revokeObjectURL(previewUrl.value) })
onMounted(load)
</script>
