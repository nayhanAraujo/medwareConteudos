<template>
  <div>
    <DsPageHeader title="Pacotes" subtitle="Organização dos modelos de laudo por pacote" icon="box-seam" />
    <DsPageShell>
      <div class="flex justify-end mb-4"><DsButton v-if="auth.isAdmin" variant="success" @click="open()">Novo pacote</DsButton></div>
      <DsAlert v-if="error" variant="error">{{ error }}</DsAlert>
      <DsTable v-else>
        <template #head><tr><th>Código</th><th>Nome</th><th>Descrição</th><th>Scripts</th><th /></tr></template>
        <tr v-for="p in pacotes" :key="p.codPacote">
          <td>{{ p.codPacote }}</td>
          <td><div class="flex items-center gap-3"><span class="package-icon"><img v-if="p.iconeBase64" :src="p.iconeBase64" alt="" width="40" height="40" /><i v-else class="bi bi-box-seam" aria-hidden="true" /></span>{{ p.nome }}</div></td>
          <td>{{ p.descricao || '-' }}</td><td>{{ p.quantidadeScripts }}</td>
          <td><div v-if="auth.isAdmin" class="flex justify-end gap-2"><DsButton size="sm" variant="secondary" @click="open(p)">Editar</DsButton><DsButton size="sm" variant="danger" @click="remove(p)">Excluir</DsButton></div></td>
        </tr>
      </DsTable>
    </DsPageShell>
    <DsModal v-model="modal" :title="editing ? 'Editar pacote' : 'Novo pacote'" size="lg">
      <div class="space-y-3">
        <DsInput v-model="form.nome" label="Nome" /><DsTextarea v-model="form.descricao" label="Descrição" />
        <fieldset class="space-y-3">
          <legend class="text-sm font-medium mb-2">Ícone do pacote</legend>
          <div class="flex items-center gap-3 flex-wrap">
            <span class="package-icon preview"><img v-if="form.iconeBase64" :src="form.iconeBase64" alt="Ícone selecionado" width="64" height="64" /><i v-else class="bi bi-box-seam" aria-hidden="true" /></span>
            <DsButton variant="secondary" :disabled="iconBusy" @click="fileInput?.click()"><i class="bi bi-upload" aria-hidden="true" /> Enviar imagem</DsButton>
            <DsButton v-if="form.iconeBase64" variant="secondary" :disabled="iconBusy" @click="clearIcon"><i class="bi bi-trash" aria-hidden="true" /> Remover</DsButton>
            <input ref="fileInput" type="file" accept="image/png,image/jpeg,image/webp" class="hidden" aria-label="Enviar ícone" @change="upload" />
          </div>
          <div class="icon-gallery" :aria-busy="iconBusy">
            <button v-for="icon in gallery" :key="icon.id" type="button" class="icon-choice" :class="{ chosen: selectedIcon === icon.id }" :disabled="iconBusy" :aria-pressed="selectedIcon === icon.id" :title="icon.label" @click="chooseIcon(icon)">
              <img :src="'/images/pacotes/' + icon.id + '.png'" alt="" width="48" height="48" /><span>{{ icon.label }}</span>
            </button>
          </div>
          <DsAlert v-if="iconError" variant="error">{{ iconError }}</DsAlert>
        </fieldset>
        <div><p class="text-sm font-medium mb-2">Scripts no pacote</p><div class="max-h-64 overflow-auto border rounded-lg p-3 space-y-2"><label v-for="s in scripts" :key="s.codScriptLaudo" class="flex gap-2 text-sm"><input v-model="selected" type="checkbox" :value="s.codScriptLaudo" />{{ s.nome }}</label></div></div>
      </div>
      <template #footer><DsButton variant="secondary" :disabled="saving" @click="modal = false">Cancelar</DsButton><DsButton :disabled="saving || iconBusy" @click="save">{{ saving ? 'Salvando...' : 'Salvar' }}</DsButton></template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
interface Pacote { codPacote: number; nome: string; descricao: string | null; quantidadeScripts: number; iconeBase64: string | null }
interface Script { codScriptLaudo: number; nome: string; codPacote: number | null }
const api = useApi(), auth = useAuthStore(), swal = useSwal()
const pacotes = ref<Pacote[]>([]), scripts = ref<Script[]>([]), selected = ref<number[]>([])
const error = ref(''), modal = ref(false), editing = ref<Pacote | null>(null), saving = ref(false)
const form = reactive({ nome: '', descricao: '', iconeBase64: null as string | null })
const fileInput = ref<HTMLInputElement>(), iconBusy = ref(false), iconError = ref(''), selectedIcon = ref('')
let formVersion = 0
const gallery = [
  { id: 'cardiologia', label: 'Cardiologia' }, { id: 'ultrassonografia', label: 'Ultrassom' },
  { id: 'vascular', label: 'Vascular' }, { id: 'oftalmologia', label: 'Oftalmologia' },
  { id: 'pediatria', label: 'Pediatria' }, { id: 'nutricao', label: 'Nutrição' }, { id: 'consulta', label: 'Consulta' },
]
async function load() {
  error.value = ''
  try {
    const [p, s] = await Promise.all([api.get<{ data: Pacote[] }>('/api/web/cadastros/pacotes'), api.get<{ data: Script[] }>('/api/web/cadastros/pacotes/scripts')])
    pacotes.value = p.data || []; scripts.value = s.data || []
  } catch (e) { error.value = e instanceof Error ? e.message : 'Erro ao carregar.' }
}
function open(p?: Pacote) {
  formVersion++; editing.value = p || null
  form.nome = p?.nome || ''; form.descricao = p?.descricao || ''; form.iconeBase64 = p?.iconeBase64 || null
  selected.value = p ? scripts.value.filter(s => s.codPacote === p.codPacote).map(s => s.codScriptLaudo) : []
  iconError.value = ''; selectedIcon.value = ''; iconBusy.value = false; modal.value = true
}
function clearIcon() { form.iconeBase64 = null; selectedIcon.value = ''; iconError.value = '' }
async function setImage(blob: Blob, galleryId = '') {
  const version = formVersion
  iconBusy.value = true; iconError.value = ''
  const url = URL.createObjectURL(blob)
  try {
    const image = new Image(); image.src = url; await image.decode()
    const canvas = document.createElement('canvas'); canvas.width = 128; canvas.height = 128
    const context = canvas.getContext('2d')
    if (!context || !image.naturalWidth || !image.naturalHeight) throw new Error('Imagem inválida.')
    const scale = Math.min(128 / image.naturalWidth, 128 / image.naturalHeight)
    const width = image.naturalWidth * scale, height = image.naturalHeight * scale
    context.drawImage(image, (128 - width) / 2, (128 - height) / 2, width, height)
    if (version === formVersion) { form.iconeBase64 = canvas.toDataURL('image/png'); selectedIcon.value = galleryId }
  } catch { if (version === formVersion) iconError.value = 'Não foi possível ler a imagem.' }
  finally { URL.revokeObjectURL(url); if (version === formVersion) iconBusy.value = false }
}
async function chooseIcon(icon: typeof gallery[number]) {
  const version = formVersion
  iconBusy.value = true; iconError.value = ''
  try {
    const response = await fetch('/images/pacotes/' + icon.id + '.png')
    if (!response.ok) throw new Error()
    const blob = await response.blob()
    if (version === formVersion) await setImage(blob, icon.id)
  } catch { if (version === formVersion) iconError.value = 'Não foi possível carregar o ícone.' }
  finally { if (version === formVersion) iconBusy.value = false }
}
async function upload(event: Event) {
  const input = event.target as HTMLInputElement, file = input.files?.[0]; input.value = ''
  if (!file) return
  if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 1024 * 1024) {
    iconError.value = 'Selecione uma imagem PNG, JPEG ou WebP de até 1 MB.'; return
  }
  await setImage(file)
}
async function save() {
  if (saving.value || iconBusy.value) return
  saving.value = true
  try {
    const body = { ...form, codigosScripts: selected.value }
    editing.value ? await api.put('/api/web/cadastros/pacotes/' + editing.value.codPacote, body) : await api.post('/api/web/cadastros/pacotes', body)
    modal.value = false; await load(); await swal.toast('Pacote salvo.')
  } catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro ao salvar.', 'error') }
  finally { saving.value = false }
}
async function remove(p: Pacote) {
  const c = await swal.confirm('Excluir pacote', 'Os scripts serão desvinculados de "' + p.nome + '". Continuar?')
  if (!c?.isConfirmed) return
  try { await api.del('/api/web/cadastros/pacotes/' + p.codPacote); await load() }
  catch (e) { await swal.toast(e instanceof Error ? e.message : 'Erro ao excluir.', 'error') }
}
onMounted(load)
</script>

<style scoped>
.package-icon { display: inline-flex; align-items: center; justify-content: center; width: 48px; height: 48px; flex: 0 0 48px; border-radius: 8px; background: #f4f5f7; color: #64748b; font-size: 24px; }
.package-icon img { object-fit: contain; }
.package-icon.preview { width: 72px; height: 72px; flex-basis: 72px; }
.icon-gallery { display: grid; grid-template-columns: repeat(auto-fit, minmax(88px, 1fr)); gap: 8px; }
.icon-choice { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; min-height: 90px; padding: 8px 4px; border: 1px solid #d1d5db; border-radius: 8px; background: #fff; font-size: 11px; color: #374151; cursor: pointer; }
.icon-choice.chosen { border-color: #2563eb; background: #eff6ff; box-shadow: inset 0 0 0 1px #2563eb; }
.icon-choice:focus-visible { outline: 2px solid #2563eb; outline-offset: 2px; }
.icon-choice:disabled { opacity: .6; cursor: wait; }
</style>
