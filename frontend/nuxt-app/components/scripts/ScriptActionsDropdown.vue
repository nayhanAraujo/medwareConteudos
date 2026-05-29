<template>
  <div class="btn-group">
    <button type="button" class="btn btn-outline-secondary btn-sm dropdown-toggle" data-bs-toggle="dropdown">
      <i class="bi bi-gear-fill" /> Ações
    </button>
    <ul class="dropdown-menu dropdown-menu-end">
      <li v-if="auth.isAdmin">
        <NuxtLink class="dropdown-item" :to="`/scripts/${item.codScriptLaudo}/variaveis`">
          <i class="bi bi-link-45deg me-2" />Vincular Variáveis
        </NuxtLink>
      </li>
      <li v-if="auth.isAdmin">
        <NuxtLink class="dropdown-item" :to="`/scripts/${item.codScriptLaudo}/mrd`">
          <i class="bi bi-file-earmark-binary me-2" />Gerenciar MRDs
        </NuxtLink>
      </li>
      <li v-if="auth.isAdmin">
        <NuxtLink class="dropdown-item" :to="`/scripts/${item.codScriptLaudo}/editar`">
          <i class="bi bi-pencil-fill me-2" />Editar
        </NuxtLink>
      </li>
      <li v-if="auth.isAdmin"><hr class="dropdown-divider" /></li>
      <li v-if="auth.isAdmin">
        <NuxtLink class="dropdown-item" :to="`/scripts/${item.codScriptLaudo}/versoes`">
          <i class="bi bi-layers me-2" />Gerenciar Versões
        </NuxtLink>
      </li>
      <li v-if="auth.isAdmin">
        <NuxtLink class="dropdown-item" :to="`/scripts/${item.codScriptLaudo}/versoes/nova`">
          <i class="bi bi-plus-circle me-2" />Nova Versão
        </NuxtLink>
      </li>
      <li v-if="showExportJson">
        <a class="dropdown-item" href="#" @click.prevent="exportJson">
          <i class="bi bi-file-earmark-arrow-down-fill me-2" />Exportar JSON
        </a>
      </li>
      <li v-if="azureDisponivel">
        <a class="dropdown-item" href="#" @click.prevent="openAzure">
          <i class="bi bi-git me-2" />Projeto Azure
        </a>
      </li>
      <li v-if="dllDisponivel">
        <a class="dropdown-item" href="#" @click.prevent="exportDll">
          <i class="bi bi-file-earmark-code-fill me-2" />Exportar DLL
        </a>
      </li>
      <template v-if="item.temArquivoMrd && item.mrdList?.length">
        <li v-for="m in item.mrdList" :key="m.codScriptMrd">
          <a class="dropdown-item" href="#" @click.prevent="exportMrd(m.codScriptMrd)">
            <i class="bi bi-file-earmark-binary-fill me-2" />{{ m.nomeArquivo }}
          </a>
        </li>
      </template>
      <li v-if="auth.isAdmin"><hr class="dropdown-divider" /></li>
      <li v-if="auth.isAdmin">
        <a class="dropdown-item" href="#" @click.prevent="toggleAprovacao">
          <i
            :class="item.aprovado ? 'bi bi-hand-thumbs-down-fill text-danger me-2' : 'bi bi-hand-thumbs-up-fill text-success me-2'"
          />
          {{ item.aprovado ? 'Desaprovar' : 'Aprovar' }}
        </a>
      </li>
      <li v-if="auth.isAdmin">
        <a class="dropdown-item" href="#" @click.prevent="toggleAtivo">
          <i :class="item.ativo ? 'bi bi-toggle-off text-secondary me-2' : 'bi bi-toggle-on text-success me-2'" />
          {{ item.ativo ? 'Desativar' : 'Ativar' }}
        </a>
      </li>
      <li><hr class="dropdown-divider" /></li>
      <li>
        <a class="dropdown-item" href="#" @click.prevent="enviarImagensEmail">
          <i class="bi bi-envelope-fill me-2" />Enviar Imagens
        </a>
      </li>
      <li v-if="item.linkTeste">
        <a class="dropdown-item" href="#" @click.prevent="solicitarAprovacao">
          <i class="bi bi-send-fill text-primary me-2" />Solicitar Aprovação
        </a>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { ScriptListItem } from '~/composables/useScriptsApi'

const props = defineProps<{ item: ScriptListItem }>()
const auth = useAuthStore()
const api = useScriptsApi()
const swal = useSwal()

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
