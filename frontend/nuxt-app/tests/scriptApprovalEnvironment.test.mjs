import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { babelParse, parse } from '@vue/compiler-sfc'

test('Approval requests include environment; cancellation and revocation are distinct', async () => {
  const source = await readFile(new URL('../components/scripts/ScriptActionsDropdown.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const script = descriptor.scriptSetup.content
  const ast = babelParse(script, { sourceType: 'module', plugins: ['typescript'] })
  const handler = ast.program.body.find(node => node.type === 'FunctionDeclaration' && node.id.name === 'toggleAprovacao')
  const code = stripTypeScriptTypes(script.slice(handler.start, handler.end))
  const calls = []
  let environment = null, confirmation = false, failure = false
  const dependencies = {
    props: { item: { codScriptLaudo: 24, aprovado: 0 } }, approving: { value: false },
    swal: {
      approvalEnvironment: async () => environment,
      confirm: async () => ({ isConfirmed: confirmation }),
      toast: async () => {}, error: async (_, message) => calls.push(['error', message])
    },
    api: { toggleAprovacao: async (...args) => { if (failure) throw new Error('Erro na API'); calls.push(args) } },
    window: { location: { reload() {} } }
  }
  const approve = new Function(...Object.keys(dependencies), `return ${code}`)(...Object.values(dependencies))
  await approve()
  assert.deepEqual(calls, [])
  environment = 'Homologação — Clínica'
  await approve()
  assert.deepEqual(calls.pop(), [24, environment])
  dependencies.props.item.aprovado = 1
  await approve()
  assert.deepEqual(calls, [])
  confirmation = true
  await approve()
  assert.deepEqual(calls.pop(), [24, null])
  failure = true
  await approve()
  assert.deepEqual(calls.pop(), ['error', 'Erro na API'])
  assert.equal(dependencies.approving.value, false)
})

test('Script and version API contracts send the environment in JSON', async () => {
  const source = await readFile(new URL('../composables/useScriptsApi.ts', import.meta.url), 'utf8')
  const { useScriptsApi } = await import(`data:text/javascript;base64,${Buffer.from(stripTypeScriptTypes(source)).toString('base64')}`)
  const previous = globalThis.useApi
  const calls = []
  globalThis.useApi = () => ({ post: async (...args) => calls.push(args) })
  try {
    const api = useScriptsApi()
    await api.toggleAprovacao(24, 'Homologação')
    await api.aprovarVersao(21, 'Laudos UX 39')
    await api.postAprovar('token', 'aprovar', 'Teste via e-mail')
    assert.deepEqual(calls, [
      ['/api/web/scripts/24/toggle-aprovacao', { ambienteAprovacao: 'Homologação' }],
      ['/api/web/scripts/versoes/21/aprovar', { ambienteAprovacao: 'Laudos UX 39' }],
      ['/api/web/scripts/aprovar/token', { acao: 'aprovar', ambienteAprovacao: 'Teste via e-mail' }]
    ])
  } finally { if (previous === undefined) delete globalThis.useApi; else globalThis.useApi = previous }
})

test('Script form sends trimmed environment and clears it for unapproved scripts', async () => {
  const source = await readFile(new URL('../utils/scriptFormData.ts', import.meta.url), 'utf8')
  const { buildScriptFormData } = await import(`data:text/javascript;base64,${Buffer.from(stripTypeScriptTypes(source)).toString('base64')}`)
  const previous = globalThis.FileList
  globalThis.FileList = class {}
  try {
    const model = { nome: 'Teste', sistema: 'Laudos UX', codpacote: 1, aprovado: true, ambiente_aprovacao: '  Homologação  ' }
    assert.equal(buildScriptFormData(model, {}).get('ambiente_aprovacao'), 'Homologação')
    model.aprovado = false
    assert.equal(buildScriptFormData(model, {}).get('ambiente_aprovacao'), '')
  } finally { if (previous === undefined) delete globalThis.FileList; else globalThis.FileList = previous }
})
