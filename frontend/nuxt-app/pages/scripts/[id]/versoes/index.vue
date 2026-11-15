<template>
  <div>
    <DsPageHeader :title="pageTitle" icon="layers" />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-4">
        <DsButton v-if="canCreate" variant="success" size="sm" icon="plus-circle" :to="`/scripts/${id}/versoes/nova`">Nova Versão</DsButton>
        <DsButton variant="secondary" size="sm" icon="arrow-left-right" :to="`/scripts/${id}/comparar-versoes`">Comparar versões</DsButton>
        <DsButton variant="secondary" size="sm" :to="voltarPath">Voltar</DsButton>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-6">
        <DsInput v-model="filtroNumero" label="Nº versão" placeholder="Ex: V1" />
        <DsSelect v-model="filtroAprovado" label="Aprovação">
          <option value="">Todas</option>
          <option value="1">Aprovadas</option>
          <option value="0">Pendentes</option>
        </DsSelect>
        <DsSelect v-model="filtroAtivo" label="Status">
          <option value="">Todas</option>
          <option value="1">Ativas</option>
          <option value="0">Inativas</option>
        </DsSelect>
      </div>
      <DsButton variant="secondary" size="sm" class="mb-4" @click="carregar">Filtrar</DsButton>

      <DsTable>
        <template #head>
          <tr>
            <th>Versão</th>
            <th>Descrição</th>
            <th>Data</th>
            <th>Aprovação</th>
            <th>Ativa</th>
            <th>Ações</th>
          </tr>
        </template>
        <tr v-for="v in versoes" :key="v.codVersao">
          <td class="font-medium">{{ v.numeroVersao }}</td>
          <td class="max-w-xs truncate text-sm text-gray-600">{{ v.descricaoAlteracoes || '—' }}</td>
          <td class="text-sm">{{ formatDate(v.dataCriacao) }}</td>
          <td>
            <DsBadge :variant="v.aprovado === 'T' ? 'primary' : 'warning'">
              {{ v.aprovado === 'T' ? 'Aprovada' : 'Pendente' }}
            </DsBadge>
            <span v-if="v.aprovado === 'T'" class="block text-xs text-gray-500 mt-1 break-words">{{ v.ambienteAprovacao || 'Ambiente não informado' }}</span>
          </td>
          <td>
            <DsBadge :variant="v.ativo === 'T' ? 'success' : 'warning'">{{ v.ativo === 'T' ? 'Ativa' : 'Inativa' }}</DsBadge>
            <DsButton v-if="canActivate && v.ativo === 'T'" variant="secondary" size="sm" icon="pause-circle" :disabled="busy" @click="alterarEstado(v, 'inativar')">Inativar</DsButton>
            <DsButton v-else-if="canActivate" variant="secondary" size="sm" icon="play-circle" :disabled="busy" @click="ativar(v.codVersao)">Ativar</DsButton>
          </td>
          <td>
            <div class="flex flex-wrap gap-1">
              <DsButton variant="ghost" size="sm" :to="`/scripts/${id}/versoes/${v.codVersao}`">Detalhes</DsButton>
              <DsButton v-if="canEdit" variant="ghost" size="sm" :to="`/scripts/${id}/versoes/${v.codVersao}/editar`">Editar</DsButton>
              <DsButton
                v-if="canApprove && v.aprovado !== 'T'"
                variant="ghost"
                size="sm"
                :disabled="busy"
                @click="aprovar(v.codVersao)"
              >
                Aprovar
              </DsButton>
              <DsButton v-if="canApprove && v.aprovado === 'T'" variant="secondary" size="sm" icon="hand-thumbs-down" :disabled="busy" @click="alterarEstado(v, 'desaprovar')">Desaprovar</DsButton>
              <DsButton
                v-if="canDelete"
                variant="danger"
                size="sm"
                icon="trash"
                :title="v.ativo === 'T' ? 'Ative outra versão antes de excluir esta' : 'Excluir versão'"
                :aria-label="`Excluir versão ${v.numeroVersao}`"
                :disabled="busy || v.ativo === 'T'"
                :loading="deleting === v.codVersao"
                @click="excluir(v)"
              >Excluir</DsButton>
            </div>
          </td>
        </tr>
      </DsTable>
      <p v-if="!versoes.length && !loading" class="text-center text-gray-500 py-6 text-sm">Nenhuma versão encontrada.</p>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { VersaoDto } from '~/composables/useScriptsApi'

definePageMeta({ layout: 'default' })

const route = useRoute()
const id = Number(route.params.id)
const scriptsApi = useScriptsApi()
const swal = useSwal()
const auth = useAuthStore()

const versoes = ref<VersaoDto[]>([])
const loading = ref(false)
const deleting = ref<number | null>(null)
const changing = ref(false)
const busy = computed(() => loading.value || deleting.value !== null || changing.value)
const pageTitle = ref('Gerenciar Versões')
const voltarPath = ref('/scripts')
const filtroNumero = ref('')
const filtroAprovado = ref('')
const filtroAtivo = ref('')
const canCreate = computed(() => auth.can('scripts', 'criar'))
const canEdit = computed(() => auth.can('scripts', 'editar'))
const canApprove = computed(() => auth.can('scripts', 'aprovar'))
const canActivate = computed(() => auth.can('scripts', 'ativar'))
const canDelete = computed(() => auth.can('scripts', 'excluir'))

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleString('pt-BR')
}

async function carregar() {
  loading.value = true
  try {
    const res = await scriptsApi.listVersoes(id, {
      numeroVersao: filtroNumero.value || undefined,
      aprovado: filtroAprovado.value || undefined,
      ativo: filtroAtivo.value || undefined
    })
    versoes.value = res.data
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao listar', 'error')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  try {
    const s = await scriptsApi.getScript(id)
    const d = s.data as Record<string, unknown>
    pageTitle.value = `Versões — ${d.nome}`
    voltarPath.value = `/scripts?sistema=${d.sistema}&pacote=${d.codPacote}`
  } catch {
    /* ignore */
  }
  await carregar()
})

async function ativar(codVersao: number) {
  if (!canActivate.value || busy.value) return
  changing.value = true
  try {
    await scriptsApi.ativarVersao(codVersao)
    await swal.toast('Versão ativada!')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally { changing.value = false }
}

async function alterarEstado(versao: VersaoDto, acao: 'inativar' | 'desaprovar') {
  if (busy.value || (acao === 'inativar' ? !canActivate.value : !canApprove.value)) return
  changing.value = true
  try {
    const texto = acao === 'inativar'
      ? 'Sem versão ativa, as exportações e a publicação no Assistente passam a usar o modelo principal, conforme suas regras de aprovação e publicação.'
      : 'A versão perderá a aprovação e o ambiente registrado. Se estiver ativa, sua publicação no Assistente ficará indisponível até nova aprovação; o modelo principal não será selecionado automaticamente.'
    const confirmation = await swal.confirm(`${acao === 'inativar' ? 'Inativar' : 'Desaprovar'} versão ${versao.numeroVersao}?`, texto)
    if (!confirmation?.isConfirmed) return
    if (acao === 'inativar') await scriptsApi.inativarVersao(versao.codVersao)
    else await scriptsApi.desaprovarVersao(versao.codVersao)
    await swal.toast(acao === 'inativar' ? 'Versão inativada' : 'Versão desaprovada')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao alterar versão', 'error')
  } finally { changing.value = false }
}

async function excluir(versao: VersaoDto) {
  if (!canDelete.value || loading.value || deleting.value !== null || changing.value || versao.ativo === 'T') return
  deleting.value = versao.codVersao
  try {
    const confirmation = await swal.confirm(
      `Excluir versão ${versao.numeroVersao}?`,
      'A versão e seus arquivos vinculados serão excluídos permanentemente. Esta ação não pode ser desfeita.'
    )
    if (!confirmation?.isConfirmed) return
    await scriptsApi.excluirVersao(versao.codVersao)
    await swal.toast('Versão excluída')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao excluir versão', 'error')
  } finally { deleting.value = null }
}

async function aprovar(codVersao: number) {
  if (!canApprove.value || busy.value) return
  changing.value = true
  try {
    const ambiente = await swal.approvalEnvironment('Aprovar versão')
    if (ambiente === null) return
    await scriptsApi.aprovarVersao(codVersao, ambiente)
    await swal.toast('Versão aprovada!')
    await carregar()
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  } finally { changing.value = false }
}
</script>
