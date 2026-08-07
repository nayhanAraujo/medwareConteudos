<template>
  <div>
    <DsPageHeader
      title="Administração Firebird"
      subtitle="Validação da conexão e consultas controladas de diagnóstico"
      icon="database-gear"
    />

    <DsPageShell>
      <div class="space-y-6">
        <DsAlert v-if="error" variant="error">{{ error }}</DsAlert>
        <DsAlert variant="warning">
          A senha permanece somente na memória desta página, não é exibida pela API e será descartada ao sair ou recarregar.
        </DsAlert>

        <section class="rounded-2xl border border-gray-200 p-5">
          <div class="flex flex-wrap items-start justify-between gap-3 mb-4">
            <div>
              <h2 class="font-semibold text-lg">Configuração da conexão</h2>
              <p class="text-sm text-gray-500">Os dados não sensíveis abaixo são carregados da configuração do servidor.</p>
            </div>
            <span
              class="rounded-full px-3 py-1 text-xs font-medium"
              :class="status?.configured ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-700'"
            >
              {{ status?.configured ? 'Configurada' : 'Não configurada' }}
            </span>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <DsInput v-model="connection.host" label="Host" required autocomplete="off" />
            <DsInput v-model="connection.port" label="Porta" type="number" required autocomplete="off" />
            <DsInput v-model="connection.database" label="Banco de dados" required autocomplete="off" />
            <DsInput v-model="connection.user" label="Usuário" required autocomplete="username" />
            <DsInput v-model="connection.charset" label="Charset" placeholder="UTF8" autocomplete="off" />
            <DsInput
              v-model="connection.password"
              label="Senha"
              type="password"
              required
              autocomplete="new-password"
            />
          </div>

          <div class="flex flex-wrap gap-2 mt-5">
            <DsButton :disabled="busy" icon="plug" @click="testConnection">
              Testar conexão
            </DsButton>
            <DsButton :disabled="busy" variant="secondary" icon="activity" @click="runProbe">
              Executar consulta de teste
            </DsButton>
          </div>

          <DsAlert v-if="connectionResult" variant="success" class="mt-4">
            {{ connectionResult }}
          </DsAlert>
          <DsAlert v-if="probe" variant="info" class="mt-4">
            <strong>{{ probe.message }}</strong>
            <span class="block mt-1 font-mono text-xs">{{ probe.query }} → {{ formatCell(probe.resultado) }}</span>
            <span class="block text-xs mt-1">Charset: {{ probe.charset }}</span>
          </DsAlert>
        </section>

        <section class="rounded-2xl border border-gray-200 p-5">
          <h2 class="font-semibold text-lg">Consulta SQL controlada</h2>
          <p class="text-sm text-gray-500 mb-4">
            Apenas uma consulta de leitura por vez é aceita. Use marcadores <code>?</code> para parâmetros posicionais.
          </p>

          <div class="space-y-4">
            <DsTextarea
              v-model="sql"
              label="SQL"
              :rows="7"
              required
              input-class="font-mono"
              placeholder="SELECT FIRST 10 * FROM RDB$RELATIONS"
            />
            <div class="grid grid-cols-1 md:grid-cols-[1fr_180px] gap-4">
              <DsTextarea
                v-model="paramsJson"
                label="Parâmetros JSON"
                :rows="3"
                input-class="font-mono"
                hint="Informe uma lista JSON, por exemplo: [123, &quot;ATIVO&quot;]."
              />
              <DsInput
                v-model="maxRows"
                label="Limite de linhas"
                type="number"
                hint="De 1 a 500 linhas."
              />
            </div>
            <DsButton :disabled="busy" icon="play-fill" @click="executeSql">
              Executar consulta
            </DsButton>
          </div>

          <div v-if="sqlResult" class="mt-5 space-y-3">
            <DsAlert :variant="sqlResult.truncated ? 'warning' : 'success'">
              {{ sqlResult.message }} {{ sqlResult.rowCount }} linha(s) retornada(s).
              <span v-if="sqlResult.truncated"> O resultado foi truncado no limite de {{ sqlResult.maxRows }}.</span>
            </DsAlert>
            <DsTable v-if="sqlResult.columns.length">
              <template #head>
                <tr><th v-for="column in sqlResult.columns" :key="column">{{ column }}</th></tr>
              </template>
              <tr v-for="(row, rowIndex) in sqlResult.rows" :key="rowIndex">
                <td v-for="(value, columnIndex) in row" :key="columnIndex" class="whitespace-pre-wrap break-all">
                  {{ formatCell(value) }}
                </td>
              </tr>
            </DsTable>
          </div>
        </section>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default', middleware: ['admin'] })

interface PublicConnection {
  host: string
  port: number
  database: string
  user: string
  charset: string
}

interface StatusResult {
  configured: boolean
  summary: string | null
  connection: PublicConnection | null
}

interface ProbeResult {
  message: string
  query: string
  resultado: unknown
  charset: string
}

interface SqlResult {
  message: string
  sql: string
  columns: string[]
  rows: unknown[][]
  rowCount: number
  truncated: boolean
  maxRows: number
  charset: string
}

interface ApiEnvelope<T> {
  success: boolean
  data: T
}

const api = useApi()
const swal = useSwal()
const status = ref<StatusResult | null>(null)
const busy = ref(false)
const error = ref('')
const connectionResult = ref('')
const probe = ref<ProbeResult | null>(null)
const sqlResult = ref<SqlResult | null>(null)
const sql = ref('SELECT 1 AS RESULTADO FROM RDB$DATABASE')
const paramsJson = ref('[]')
const maxRows = ref('100')
const connection = reactive({
  host: '127.0.0.1',
  port: '3050',
  database: '',
  user: 'SYSDBA',
  password: '',
  charset: 'UTF8'
})

function connectionPayload() {
  return {
    host: connection.host.trim(),
    port: Number(connection.port),
    database: connection.database.trim(),
    user: connection.user.trim(),
    password: connection.password,
    charset: connection.charset.trim() || 'UTF8'
  }
}

function validateConnection() {
  const value = connectionPayload()
  if (!value.host || !value.database || !value.user || !value.password)
    throw new Error('Preencha host, porta, banco, usuário e senha.')
  if (!Number.isInteger(value.port) || value.port < 1 || value.port > 65535)
    throw new Error('Informe uma porta válida entre 1 e 65535.')
  return value
}

async function loadStatus() {
  error.value = ''
  try {
    const response = await api.get<ApiEnvelope<StatusResult>>('/api/web/firebird-admin/status')
    status.value = response.data
    if (response.data.connection) {
      const configured = response.data.connection
      connection.host = configured.host
      connection.port = String(configured.port)
      connection.database = configured.database
      connection.user = configured.user
      connection.charset = configured.charset || 'UTF8'
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Não foi possível carregar a configuração Firebird.'
  }
}

async function testConnection() {
  await run(async () => {
    const response = await api.post<ApiEnvelope<{ message: string; summary: string }>>(
      '/api/web/firebird-admin/testar', validateConnection())
    connectionResult.value = `${response.data.message} ${response.data.summary}`
    probe.value = null
  })
}

async function runProbe() {
  await run(async () => {
    const response = await api.post<ApiEnvelope<ProbeResult>>(
      '/api/web/firebird-admin/consulta-teste', { connection: validateConnection() })
    probe.value = response.data
    connectionResult.value = ''
  })
}

async function executeSql() {
  await run(async () => {
    let params: unknown
    try {
      params = JSON.parse(paramsJson.value || '[]')
    } catch {
      throw new Error('Os parâmetros devem ser uma lista JSON válida.')
    }
    if (!Array.isArray(params)) throw new Error('Os parâmetros devem ser uma lista JSON.')

    const limit = Number(maxRows.value)
    if (!Number.isInteger(limit) || limit < 1 || limit > 500)
      throw new Error('O limite de linhas deve estar entre 1 e 500.')

    const response = await api.post<ApiEnvelope<SqlResult>>('/api/web/firebird-admin/sql', {
      sql: sql.value,
      params,
      maxRows: limit,
      connection: validateConnection()
    })
    sqlResult.value = response.data
  })
}

async function run(action: () => Promise<void>) {
  busy.value = true
  error.value = ''
  try {
    await action()
  } catch (caught) {
    const message = caught instanceof Error ? caught.message : 'A operação Firebird falhou.'
    error.value = message
    await swal.toast(message, 'error')
  } finally {
    busy.value = false
  }
}

function formatCell(value: unknown) {
  if (value === null || value === undefined) return 'NULL'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

onMounted(loadStatus)
</script>

