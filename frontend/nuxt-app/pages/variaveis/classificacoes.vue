<template>
  <div>
    <DsPageHeader
      title="Grupos e classificações"
      subtitle="Cadastros usados nas normalidades por classificação"
      icon="diagram-3"
    />
    <DsPageShell>
      <div class="flex flex-wrap justify-between gap-3 mb-5">
        <DsButton variant="secondary" icon="arrow-left" to="/variaveis">Voltar</DsButton>
        <DsButton variant="success" icon="folder-plus" @click="abrirGrupo()">Novo grupo</DsButton>
      </div>

      <DsAlert v-if="erro" variant="error" class="mb-4">{{ erro }}</DsAlert>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <section class="rounded-2xl border border-gray-100 overflow-hidden bg-white">
          <header class="px-4 py-3 bg-gray-50 font-semibold flex items-center justify-between gap-2">
            <span>Grupos</span>
            <DsBadge variant="default">{{ data.grupos.length }}</DsBadge>
          </header>

          <div v-if="carregando" class="p-5 text-sm text-gray-500">Carregando...</div>
          <p v-else-if="!data.grupos.length" class="p-5 text-sm text-gray-500">Nenhum grupo cadastrado.</p>

          <button
            v-for="g in data.grupos"
            :key="g.codGrupo"
            type="button"
            class="w-full text-left px-4 py-3 border-0 border-t border-gray-100 flex gap-2 items-center hover:bg-gray-50"
            :class="grupoSelecionado?.codGrupo === g.codGrupo ? 'bg-sky-50' : 'bg-white'"
            @click="selecionarGrupo(g)"
          >
            <span class="min-w-0 flex-1">
              <strong class="block truncate">{{ g.nome }}</strong>
              <small class="block text-gray-500">
                {{ contagemPorGrupo(g.codGrupo) }} classificação(ões)
              </small>
            </span>
            <span class="flex shrink-0" @click.stop>
              <DsButton variant="ghost" size="sm" icon="pencil" @click="abrirGrupo(g)" />
              <DsButton variant="ghost" size="sm" icon="trash" @click="excluirGrupo(g)" />
            </span>
          </button>
        </section>

        <section class="lg:col-span-2 space-y-4">
          <div class="flex flex-wrap justify-between items-center gap-3">
            <div>
              <h2 class="font-semibold text-lg">
                {{ grupoSelecionado?.nome || 'Selecione um grupo' }}
              </h2>
              <p class="text-sm text-gray-500">
                <template v-if="grupoSelecionado">
                  {{ classificacoesDoGrupo.length }} classificação(ões) neste grupo
                </template>
                <template v-else>
                  Escolha um grupo à esquerda para gerenciar as classificações
                </template>
              </p>
            </div>
            <DsButton
              v-if="grupoSelecionado"
              variant="success"
              size="sm"
              icon="plus"
              @click="abrirClassificacao()"
            >
              Nova classificação
            </DsButton>
          </div>

          <DsAlert v-if="!grupoSelecionado" variant="info">
            Selecione um grupo para visualizar e cadastrar classificações.
          </DsAlert>

          <DsAlert v-else-if="!classificacoesDoGrupo.length" variant="info">
            Nenhuma classificação neste grupo. Use “Nova classificação” para adicionar.
          </DsAlert>

          <div v-else class="rounded-2xl border border-gray-100 overflow-hidden bg-white">
            <div
              v-for="c in classificacoesDoGrupo"
              :key="c.codClassificacao"
              class="flex items-center justify-between gap-3 px-4 py-3 border-t border-gray-100 first:border-t-0"
            >
              <span class="font-medium text-ds-text">{{ c.nome }}</span>
              <span class="flex shrink-0">
                <DsButton variant="ghost" size="sm" icon="pencil" @click="abrirClassificacao(c)" />
                <DsButton variant="ghost" size="sm" icon="trash" @click="excluirClassificacao(c)" />
              </span>
            </div>
          </div>
        </section>
      </div>
    </DsPageShell>

    <DsModal v-model="modalGrupo" :title="editandoGrupo ? 'Editar grupo' : 'Novo grupo'">
      <form class="space-y-4" @submit.prevent="salvarGrupo">
        <DsInput v-model="formGrupo.nome" label="Nome" required />
        <div class="flex justify-end gap-2">
          <DsButton variant="secondary" type="button" @click="modalGrupo = false">Cancelar</DsButton>
          <DsButton type="submit" :loading="salvando">Salvar</DsButton>
        </div>
      </form>
    </DsModal>

    <DsModal
      v-model="modalClassificacao"
      :title="editandoClassificacao ? 'Editar classificação' : 'Nova classificação'"
    >
      <form class="space-y-4" @submit.prevent="salvarClassificacao">
        <DsInput v-model="formClassificacao.nome" label="Nome" required />
        <p v-if="grupoSelecionado" class="text-sm text-gray-500">
          Grupo: <strong>{{ grupoSelecionado.nome }}</strong>
        </p>
        <div class="flex justify-end gap-2">
          <DsButton variant="secondary" type="button" @click="modalClassificacao = false">
            Cancelar
          </DsButton>
          <DsButton type="submit" :loading="salvando">Salvar</DsButton>
        </div>
      </form>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type { Classificacao, ClassificacaoGrupo } from '~/composables/useVariaveisApi'

definePageMeta({ layout: 'default' })

const api = useVariaveisApi()
const swal = useSwal()

const data = ref<{ grupos: ClassificacaoGrupo[]; classificacoes: Classificacao[] }>({
  grupos: [],
  classificacoes: []
})
const grupoSelecionado = ref<ClassificacaoGrupo | null>(null)
const carregando = ref(false)
const salvando = ref(false)
const erro = ref('')

const modalGrupo = ref(false)
const modalClassificacao = ref(false)
const editandoGrupo = ref<ClassificacaoGrupo | null>(null)
const editandoClassificacao = ref<Classificacao | null>(null)
const formGrupo = reactive({ nome: '' })
const formClassificacao = reactive({ nome: '' })

const classificacoesDoGrupo = computed(() => {
  if (!grupoSelecionado.value) return []
  const cod = grupoSelecionado.value.codGrupo
  return data.value.classificacoes.filter((c) => Number(c.codGrupo) === Number(cod))
})

function contagemPorGrupo(codGrupo: number) {
  return data.value.classificacoes.filter((c) => Number(c.codGrupo) === Number(codGrupo)).length
}

function selecionarGrupo(g: ClassificacaoGrupo) {
  grupoSelecionado.value = g
}

async function load() {
  carregando.value = true
  erro.value = ''
  try {
    data.value = (await api.getClassificacoes()).data
    if (grupoSelecionado.value) {
      grupoSelecionado.value =
        data.value.grupos.find((g) => g.codGrupo === grupoSelecionado.value?.codGrupo) || null
    }
  } catch (e) {
    erro.value = e instanceof Error ? e.message : 'Erro ao carregar.'
  } finally {
    carregando.value = false
  }
}

function abrirGrupo(g?: ClassificacaoGrupo) {
  editandoGrupo.value = g || null
  formGrupo.nome = g?.nome || ''
  modalGrupo.value = true
}

async function salvarGrupo() {
  const nome = formGrupo.nome.trim()
  if (!nome) {
    await swal.toast('Informe o nome do grupo.', 'warning')
    return
  }
  salvando.value = true
  try {
    if (editandoGrupo.value) {
      await api.updateGrupoClassificacao(editandoGrupo.value.codGrupo, nome)
    } else {
      await api.createGrupoClassificacao(nome)
    }
    modalGrupo.value = false
    await load()
    await swal.toast('Grupo salvo.')
  } catch (e) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao salvar grupo.', 'error')
  } finally {
    salvando.value = false
  }
}

async function excluirGrupo(g: ClassificacaoGrupo) {
  const ok = await swal.confirm('Excluir grupo', `Excluir "${g.nome}"?`)
  if (!ok?.isConfirmed) return
  try {
    await api.deleteGrupoClassificacao(g.codGrupo)
    if (grupoSelecionado.value?.codGrupo === g.codGrupo) grupoSelecionado.value = null
    await load()
    await swal.toast('Grupo excluído.')
  } catch (e) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao excluir grupo.', 'error')
  }
}

function abrirClassificacao(c?: Classificacao) {
  if (!grupoSelecionado.value) return
  editandoClassificacao.value = c || null
  formClassificacao.nome = c?.nome || ''
  modalClassificacao.value = true
}

async function salvarClassificacao() {
  if (!grupoSelecionado.value) return
  const nome = formClassificacao.nome.trim()
  if (!nome) {
    await swal.toast('Informe o nome da classificação.', 'warning')
    return
  }
  salvando.value = true
  try {
    const body = { nome, codGrupo: grupoSelecionado.value.codGrupo }
    if (editandoClassificacao.value) {
      await api.updateClassificacao(editandoClassificacao.value.codClassificacao, body)
    } else {
      await api.createClassificacao(body)
    }
    modalClassificacao.value = false
    await load()
    await swal.toast('Classificação salva.')
  } catch (e) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao salvar classificação.', 'error')
  } finally {
    salvando.value = false
  }
}

async function excluirClassificacao(c: Classificacao) {
  const ok = await swal.confirm('Excluir classificação', `Excluir "${c.nome}"?`)
  if (!ok?.isConfirmed) return
  try {
    await api.deleteClassificacao(c.codClassificacao)
    await load()
    await swal.toast('Classificação excluída.')
  } catch (e) {
    await swal.toast(e instanceof Error ? e.message : 'Erro ao excluir classificação.', 'error')
  }
}

onMounted(load)
</script>
