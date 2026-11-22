<script setup lang="ts">
definePageMeta({ compactWorkspace: true })
type Entry = { id: string; document: Record<string, unknown> }
type Preview = { total: number; valid: number; errors: { row: number; message: string }[]; fileSha256: string }
const api = useApi()
const kind = ref('respostas')
const rows = ref<Entry[]>([])
const selected = ref<Entry | null>(null)
const error = ref('')
const notice = ref('')
const busy = ref(false)
const file = ref<File | null>(null)
const preview = ref<Preview | null>(null)
const exhausted = ref(false)
const localDevelopment = import.meta.dev
const demoType = ref('')
async function loadDemo(type: 'diario' | 'semanal') {
  if (busy.value) return
  busy.value = true; error.value = ''; selected.value = null
  try {
    const result = await api.get<{ data: Record<string, unknown> }>(`/api/web/automacoes/chatfast/demonstracao/${type}`)
    selected.value = { id: `demo-${type}`, document: result.data }; demoType.value = type
  } catch (e) { error.value = e instanceof Error ? e.message : 'Demonstração local indisponível.' }
  finally { busy.value = false }
}
async function load(more = false) {
  if (busy.value) return
  busy.value = true; error.value = ''
  try {
    const cursor = more && rows.value.length ? `?before=${encodeURIComponent(rows.value.at(-1)!.id)}` : ''
    const result = await api.get<{ data: Entry[] }>(`/api/web/automacoes/chatfast/${kind.value}${cursor}`)
    rows.value = more ? [...rows.value, ...result.data] : result.data; exhausted.value = result.data.length < 50; selected.value = null; demoType.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : 'Não foi possível consultar a integração.' }
  finally { busy.value = false }
}
function chooseFile(e: Event) { file.value = (e.target as HTMLInputElement).files?.[0] ?? null; preview.value = null; notice.value = ''; error.value = '' }
async function upload(confirm = false) {
  if (!file.value || busy.value || (confirm && (!preview.value || preview.value.errors.length))) return
  busy.value = true; error.value = ''; notice.value = ''
  try {
    const form = new FormData(); form.append('file', file.value); form.append('confirm', String(confirm))
    const result = await api.postForm<Preview & { duplicate?: boolean; imported?: number }>('/api/web/automacoes/chatfast/importacoes', form)
    if (!confirm) preview.value = result
    else { notice.value = result.duplicate ? 'Este arquivo já foi importado. Nenhum registro duplicado.' : `${result.imported} registros importados. Nenhum e-mail ou cartão foi enviado.`; preview.value = null }
  } catch (e) { error.value = e instanceof Error ? e.message : 'Não foi possível importar a planilha.' }
  finally { busy.value = false }
}
watch(kind, () => load())
onMounted(() => load())
useHead({ title: 'ChatFast · Gestão | MDW' })
</script>

<template>
  <div class="chatfast-admin">
    <DsPageHeader title="ChatFast · Gestão" subtitle="Área restrita aos administradores. Nenhuma operação desta tela encerra casos no bot." icon="diagram-3" />
    <div class="links"><NuxtLink to="/automacoes">Automações</NuxtLink><NuxtLink to="/automacoes/chatfast/diario">Relatório diário público</NuxtLink><NuxtLink to="/automacoes/chatfast/semanal">Relatório semanal público</NuxtLink></div>
    <p v-if="error" role="alert" class="feedback error">{{ error }}</p><p v-if="notice" role="status" class="feedback">{{ notice }}</p>
    <section v-if="localDevelopment" class="panel"><h2>Demonstração local · arquivos reais</h2><p>Leitura de snapshots privados, sem inserir registros no banco. Configure AutomacoesChatfast:DemoDirectory na API Development. Nomes e relatos ficam apenas nesta gestão.</p><div class="links"><button :disabled="busy" @click="loadDemo('diario')">Abrir diário real</button><button :disabled="busy" @click="loadDemo('semanal')">Abrir semanal real</button><NuxtLink to="/automacoes/chatfast/diario?demo=1">Diário · agregados públicos</NuxtLink><NuxtLink to="/automacoes/chatfast/semanal?demo=1">Semanal · agregados públicos</NuxtLink></div></section>
    <section class="panel">
      <h2>Importar histórico do Excel</h2><p>Formato Tratados: data, rodada, gestor, itens_tratados, observacao, respondido_por e hora. Até 5 MB / 5.000 linhas. Os horários são interpretados no fuso de Brasília.</p>
      <label>Planilha XLSX <input type="file" accept=".xlsx" :disabled="busy" @change="chooseFile" /></label>
      <button :disabled="!file || busy" @click="upload(false)">Analisar sem gravar</button>
      <div v-if="preview" class="preview"><p>{{ preview.total }} linhas · {{ preview.valid }} válidas · {{ preview.errors.length }} rejeitadas</p><p class="hash">SHA-256: {{ preview.fileSha256 }}</p><ul v-if="preview.errors.length"><li v-for="item in preview.errors" :key="item.row">Linha {{ item.row }}: {{ item.message }}</li></ul><p v-else>Prévia válida. A confirmação grava este arquivo de forma transacional e preserva a origem de cada linha.</p><button :disabled="busy || !!preview.errors.length" @click="upload(true)">Confirmar importação deste arquivo</button></div>
    </section>
    <section class="panel">
      <div class="list-header"><h2>Registros privados</h2><label>Tipo <select v-model="kind" :disabled="busy"><option value="respostas">Respostas dos cartões</option><option value="relatorios">Relatórios do bot</option></select></label><button :disabled="busy" @click="load()">Atualizar</button></div>
      <p v-if="busy" role="status">Processando…</p><p v-else-if="!rows.length">Nenhum registro disponível. Se a integração estiver desabilitada, configure-a antes da ativação.</p>
      <div class="records"><button v-for="row in rows" :key="row.id" class="record" @click="selected = selected?.id === row.id ? null : row"><span>#{{ row.id }} · {{ row.document.source || row.document.type || 'Registro' }}</span><span>Ver detalhes</span></button></div>
      <button v-if="rows.length && !exhausted" :disabled="busy" @click="load(true)">Carregar mais</button>
      <section v-if="selected" class="detail" aria-label="Detalhes privados do relatório"><h3>{{ selected.id.startsWith('demo-') ? 'Demonstração local' : `Registro #${selected.id}` }} · privado</h3><AutomacoesChatfastPrivateReport v-if="selected.document.summary" :document="selected.document" /><pre v-else>{{ JSON.stringify(selected.document, null, 2) }}</pre></section>
    </section>
  </div>
</template>

<style scoped>
.chatfast-admin{max-width:1200px;margin:auto}.links,.list-header{display:flex;gap:20px;flex-wrap:wrap;align-items:center;margin-bottom:20px}.links a{color:#24619b}.panel{padding:24px;border:1px solid #dce4ec;border-radius:18px;background:var(--ds-surface,#fff);margin:24px 0}.panel h2{font-size:18px;font-weight:650;margin-bottom:12px}.panel p{font-size:14px;line-height:1.6;color:#526579;margin:12px 0}.panel label{display:inline-flex;align-items:center;gap:12px;flex-wrap:wrap;margin-right:16px;font-size:14px}.panel input{max-width:100%}.panel button,.panel select{border:1px solid #cbd6e2;border-radius:9px;background:transparent;padding:8px 14px;color:inherit}.panel button:disabled{opacity:.5}.preview,.detail{margin-top:20px;border-top:1px solid #dce4ec;padding-top:16px}.hash{word-break:break-all}.records{display:grid;gap:8px;margin:16px 0}.records .record{display:flex;justify-content:space-between;text-align:left;gap:12px}.feedback{padding:16px;border-radius:12px;background:#e7f5ee}.feedback.error{background:#fff0ed;color:#9a3524}.detail pre{white-space:pre-wrap;overflow-wrap:anywhere;max-height:480px;overflow:auto;background:#f4f7fa;color:#20354a;padding:16px;border-radius:12px;font-size:12px}@media(max-width:600px){.panel{padding:16px}.panel label{display:flex;margin-bottom:12px}.records .record{flex-direction:column}}
</style>
