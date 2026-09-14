import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readDocsLocation, writeDocsLocation } from '../utils/docsNavigation.ts'

test('links do portal preservam definição, ambiente, operação, aba e pesquisa no refresh', () => {
  const state = { definition:'parceiros', environment:'homologacao', operationId:'get_exemplo', section:'endpoints', tab:'respostas', search:'Fórmulas & limites' }
  assert.deepEqual(readDocsLocation(Object.fromEntries(new URLSearchParams(writeDocsLocation(state)))), state)
})
test('URL inválida ou duplicada não seleciona definição/ambiente arbitrário', () => {
  const state = readDocsLocation({definicao:['web','parceiros'],ambiente:'http://arbitrario',aba:'script',operacao:null})
  assert.equal(state.definition,'parceiros'); assert.equal(state.environment,'atual')
  assert.equal(state.tab,'informacoes'); assert.equal(state.section,'visao-geral')
  assert.ok(!('operacao' in writeDocsLocation(state)))
})
