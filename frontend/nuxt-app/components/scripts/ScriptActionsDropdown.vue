<template>
  <DsDropdown
    align="right"
    trigger-class="inline-flex items-center gap-1 text-sm font-medium px-3 py-1.5 rounded-full border border-gray-200 bg-white hover:bg-gray-50 text-ds-text"
  >
    <template #trigger>
      <i class="bi bi-three-dots" />
      Ações
    </template>
    <template #default="{ close }">
      <DsDropdownItem
        v-if="canLink"
        :to="`/scripts/${item.codScriptLaudo}/variaveis`"
        @click="close"
      >
        <i class="bi bi-link-45deg text-blue-600" /> Vincular Variáveis
      </DsDropdownItem>
      <DsDropdownItem
        v-if="canLink"
        :to="`/scripts/${item.codScriptLaudo}/mrd`"
        @click="close"
      >
        <i class="bi bi-file-earmark-binary text-gray-600" /> Gerenciar MRDs
      </DsDropdownItem>
      <DsDropdownItem
        v-if="canEdit"
        :to="`/scripts/${item.codScriptLaudo}/editar`"
        @click="close"
      >
        <i class="bi bi-pencil-fill text-blue-600" /> Editar
      </DsDropdownItem>
      <DsDropdownDivider v-if="canEdit || canCreate" />
      <DsDropdownItem
        v-if="canEdit"
        :to="`/scripts/${item.codScriptLaudo}/versoes`"
        @click="close"
      >
        <i class="bi bi-layers text-gray-600" /> Gerenciar Versões
      </DsDropdownItem>
      <DsDropdownItem
        v-if="canCreate"
        :to="`/scripts/${item.codScriptLaudo}/versoes/nova`"
        @click="close"
      >
        <i class="bi bi-plus-circle text-green-600" /> Nova Versão
      </DsDropdownItem>
      <DsDropdownItem v-if="showExportJson" @click="exportJson(); close()">
        <i class="bi bi-file-earmark-arrow-down-fill text-gray-600" /> Exportar JSON
      </DsDropdownItem>
      <DsDropdownItem v-if="azureDisponivel" @click="openAzure(); close()">
        <i class="bi bi-git text-orange-500" /> Projeto Azure
      </DsDropdownItem>
      <DsDropdownItem v-if="dllDisponivel" @click="exportDll(); close()">
        <i class="bi bi-file-earmark-code-fill text-gray-600" /> Exportar DLL
      </DsDropdownItem>
      <template v-if="item.temArquivoMrd && item.mrdList?.length">
        <DsDropdownItem
          v-for="m in item.mrdList"
          :key="m.codScriptMrd"
          @click="exportMrd(m.codScriptMrd); close()"
        >
          <i class="bi bi-file-earmark-binary-fill text-gray-600 shrink-0" />
          <span class="truncate">{{ m.nomeArquivo }}</span>
        </DsDropdownItem>
      </template>
      <DsDropdownDivider v-if="canApprove || canActivate" />
      <DsDropdownItem v-if="canApprove" @click="toggleAprovacao(); close()">
        <i
          :class="item.aprovado ? 'bi bi-hand-thumbs-down-fill text-rose-600' : 'bi bi-hand-thumbs-up-fill text-green-600'"
        />
        {{ item.aprovado ? 'Desaprovar' : 'Aprovar' }}
      </DsDropdownItem>
      <DsDropdownItem v-if="canActivate" @click="toggleAtivo(); close()">
        <i :class="item.ativo ? 'bi bi-toggle-off text-gray-600' : 'bi bi-toggle-on text-green-600'" />
        {{ item.ativo ? 'Desativar' : 'Ativar' }}
      </DsDropdownItem>
      <DsDropdownDivider />
      <DsDropdownItem @click="enviarImagensEmail(); close()">
        <i class="bi bi-envelope-fill text-gray-600" /> Enviar Imagens
      </DsDropdownItem>
      <DsDropdownItem v-if="item.linkTeste" @click="solicitarAprovacao(); close()">
        <i class="bi bi-send-fill text-blue-600" /> Solicitar Aprovação
      </DsDropdownItem>
    </template>
  </DsDropdown>
</template>

<script setup lang="ts">
import type { ScriptListItem } from '~/composables/useScriptsApi'

const props = defineProps<{ item: ScriptListItem }>()
const auth = useAuthStore()
const api = useScriptsApi()
const swal = useSwal()
const canCreate = computed(() => auth.can('scripts', 'criar'))
const canEdit = computed(() => auth.can('scripts', 'editar'))
const canLink = computed(() => auth.can('scripts', 'vincular'))
const canApprove = computed(() => auth.can('scripts', 'aprovar'))
const canActivate = computed(() => auth.can('scripts', 'ativar'))

const azureDisponivel = computed(
  () => props.item.sistema === 'Laudos Flex' && !!props.item.caminhoAzure
)
const dllDisponivel = computed(() => props.item.sistema === 'Laudos Flex' && props.item.temArquivoDll)
const showExportJson = computed(
  () => props.item.sistema === 'Laudos UX' && props.item.temArquivoJson && !props.item.ultimaVersao
)

async function exportJson() {
  await api.download(`/api/web/scripts/${props.item.codScriptLaudo}/exportar-json`, `${props.item.nome}.json`)
}
async function exportDll() {
  await api.download(`/api/web/scripts/${props.item.codScriptLaudo}/exportar-dll`, `${props.item.nome}.dll`)
}
async function exportMrd(codScriptMrd: number) {
  const m = props.item.mrdList.find((x) => x.codScriptMrd === codScriptMrd)
  await api.download(
    `/api/web/scripts/${props.item.codScriptLaudo}/exportar-mrd?codScriptMrd=${codScriptMrd}`,
    m?.nomeArquivo || 'arquivo.mrd'
  )
}
async function openAzure() {
  try {
    const r = await useApi().get<{ url: string }>(`/api/web/scripts/${props.item.codScriptLaudo}/projeto-azure`)
    if (r.url) window.open(r.url, '_blank')
  } catch (e: unknown) {
    await swal.toast(e instanceof Error ? e.message : 'Erro', 'error')
  }
}

async function toggleAprovacao() {
  const acao = props.item.aprovado ? 'desaprovar' : 'aprovar'
  const ok = await swal.confirm(`${acao.charAt(0).toUpperCase() + acao.slice(1)} script?`)
  if (!ok?.isConfirmed) return
  try {
    await api.toggleAprovacao(props.item.codScriptLaudo)
    await swal.toast(`Script ${acao}do com sucesso.`, 'success')
    window.location.reload()
  } catch (e: unknown) {
    await swal.error('Erro ao alterar aprovação', e instanceof Error ? e.message : 'Tente novamente.')
  }
}

async function toggleAtivo() {
  const acao = props.item.ativo ? 'desativar' : 'ativar'
  const ok = await swal.confirm(`${acao.charAt(0).toUpperCase() + acao.slice(1)} script?`)
  if (!ok?.isConfirmed) return
  try {
    await api.toggleAtivo(props.item.codScriptLaudo)
    await swal.toast(`Script ${acao}do com sucesso.`, 'success')
    window.location.reload()
  } catch (e: unknown) {
    await swal.error('Erro ao alterar status', e instanceof Error ? e.message : 'Tente novamente.')
  }
}

async function enviarImagensEmail() {
  if (!import.meta.client) return
  const { default: Swal } = await import('sweetalert2')
  const result = await Swal.fire({
    title: 'Enviar imagens por email',
    input: 'email',
    inputLabel: 'Email do destinatário',
    inputPlaceholder: 'usuario@empresa.com',
    showCancelButton: true,
    confirmButtonText: 'Enviar',
    cancelButtonText: 'Cancelar'
  })
  if (!result.isConfirmed || !result.value) return
  try {
    await api.sendImagesEmail(props.item.codScriptLaudo, result.value, props.item.sistema)
    await swal.toast('Imagens enviadas com sucesso.', 'success')
  } catch (e: unknown) {
    await swal.error('Erro ao enviar imagens', e instanceof Error ? e.message : 'Tente novamente.')
  }
}

async function solicitarAprovacao() {
  if (!import.meta.client) return
  const { default: Swal } = await import('sweetalert2')
  const result = await Swal.fire({
    title: 'Solicitar aprovação',
    html: `
      <input id="swal-emails" class="swal2-input" placeholder="Emails separados por vírgula" />
      <textarea id="swal-msg" class="swal2-textarea" placeholder="Mensagem adicional (opcional)"></textarea>
    `,
    focusConfirm: false,
    showCancelButton: true,
    confirmButtonText: 'Enviar',
    cancelButtonText: 'Cancelar',
    preConfirm: () => {
      const emailsRaw = (document.getElementById('swal-emails') as HTMLInputElement | null)?.value || ''
      const mensagem = (document.getElementById('swal-msg') as HTMLTextAreaElement | null)?.value || ''
      const emails = emailsRaw.split(',').map((e) => e.trim()).filter(Boolean)
      if (!emails.length) {
        Swal.showValidationMessage('Informe ao menos um email.')
        return null
      }
      return { emails, mensagem }
    }
  })
  if (!result.isConfirmed || !result.value) return
  try {
    await api.solicitarAprovacao(props.item.codScriptLaudo, {
      nomeScript: props.item.nome,
      linkTeste: props.item.linkTeste || '',
      emails: result.value.emails,
      mensagem: result.value.mensagem
    })
    await swal.toast('Solicitação enviada com sucesso.', 'success')
  } catch (e: unknown) {
    await swal.error('Erro ao solicitar aprovação', e instanceof Error ? e.message : 'Tente novamente.')
  }
}
</script>
