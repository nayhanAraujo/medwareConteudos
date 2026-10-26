import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'
import { computed, reactive, ref, watch } from 'vue'
import { compileScript, parse } from '@vue/compiler-sfc'
import { contentModules, contentRouteDomain } from '../utils/contentNavigation.ts'

test('Content inventory has real, unique destinations and correct route domains', () => {
  assert.equal(new Set(contentModules.map(card => card.nuxtPath)).size, contentModules.length)
  assert.equal(new Set(contentModules.map(card => card.type)).size, contentModules.length)
  for (const card of contentModules) {
    const path = card.nuxtPath
    assert.ok(existsSync(new URL(`../pages${path}/index.vue`, import.meta.url)) || existsSync(new URL(`../pages${path}.vue`, import.meta.url)), path)
    assert.equal(contentRouteDomain(path), card.domain)
    assert.notEqual(card.domain, 'orcamentos')
  }
  assert.equal(contentRouteDomain('/conteudos/modelos-mensagens'), 'conteudos')
  assert.equal(contentRouteDomain('/scripts/123/editar'), 'scripts')
  assert.equal(contentRouteDomain('/studio/voz'), 'studio')
  assert.equal(contentRouteDomain('/conteudos'), undefined, 'Hub permits users to see only their own modules')
  assert.equal(contentRouteDomain('/modelos/1'), 'formulas')
})

test('Library moves budgets to settings and enforces permissions on cards and navigation', async () => {
  const source = await readFile(new URL('../pages/biblioteca/index.vue', import.meta.url), 'utf8')
  const { descriptor } = parse(source)
  const compiled = compileScript(descriptor, { id: 'biblioteca-test' })
  const code = stripTypeScriptTypes(compiled.content)
    .replace(/from ['"]vue['"]/g, `from ${JSON.stringify(import.meta.resolve('vue'))}`)
    .replace(/from ['"]~\/utils\/contentNavigation['"]/g, `from ${JSON.stringify(new URL('../utils/contentNavigation.ts', import.meta.url).href)}`)
  const { default: component } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
  const rights = reactive(new Set()), state = reactive({ admin: true }), calls = []
  const auth = { get isAdmin() { return state.admin }, can: (domain, action) => state.admin || rights.has(`${domain}.${action}`) }
  const globals = { ref, computed, watch, definePageMeta() {}, useAuthStore: () => auth, useMigracao: () => ({ emMigracao() {}, irOuMigracao: path => calls.push(path) }) }
  const originals = Object.fromEntries(Object.keys(globals).map(key => [key, globalThis[key]]))
  Object.assign(globalThis, globals)
  try {
    const page = component.setup({}, { expose() {} })
    assert.equal(page.visibleContentModules.value.length, contentModules.length)
    const settings = page.visibleSections.value.find(tab => tab.id === 'configuracoes')
    const budget = settings.cards.find(card => card.domain === 'orcamentos')
    assert.equal(budget.nuxtPath, '/biblioteca/orcamentos')
    assert.equal(page.visibleContentModules.value.some(card => card.nuxtPath.includes('orcamentos')), false)
    assert.equal(source.includes('title="Central de Orçamentos"'), false, 'No budget card outside the section renderer')
    assert.equal(source.includes('Novo Módulo'), false)
    state.admin = false
    assert.equal(page.visibleContentModules.value.length, 0)
    assert.equal(page.visibleSections.value.some(tab => tab.cards.some(card => card.domain === 'orcamentos')), false)
    page.openCard(budget); assert.equal(calls.length, 0)
    rights.add('orcamentos.visualizar')
    assert.ok(page.tabs.value.some(tab => tab.id === 'configuracoes'))
    page.openCard(budget); assert.equal(calls.at(-1), '/biblioteca/orcamentos')
    rights.add('scripts.visualizar')
    assert.deepEqual(page.visibleContentModules.value.map(card => card.domain), ['scripts'])
    assert.ok(page.visibleQuickRef.value.every(action => !action.label.startsWith('Novo')))
    const studio = contentModules.find(card => card.domain === 'studio')
    const before = calls.length; page.openContent(studio); assert.equal(calls.length, before)
    rights.add('studio.visualizar'); page.openContent(studio); assert.equal(calls.at(-1), '/studio')
    assert.equal(page.visibleSections.value.some(tab => tab.cards.some(card => card.adminOnly)), false)
  } finally { Object.assign(globalThis, originals) }
})

test('Direct content routes enforce domain permissions without blocking public approval links', async () => {
  const source = await readFile(new URL('../middleware/auth.global.ts', import.meta.url), 'utf8')
  const code = stripTypeScriptTypes(source).replace(/from ['"]~\/utils\/contentNavigation['"]/g, `from ${JSON.stringify(new URL('../utils/contentNavigation.ts', import.meta.url).href)}`)
  const rights = new Set(['scripts.visualizar'])
  const globals = {
    defineNuxtRouteMiddleware: handler => handler,
    useAuthStore: () => ({ isAuthenticated: true, loadFromStorage() {}, can: (domain, action) => rights.has(`${domain}.${action}`) }),
    createError: error => error, abortNavigation: error => error, navigateTo: target => target
  }
  const originals = Object.fromEntries(Object.keys(globals).map(key => [key, globalThis[key]]))
  Object.assign(globalThis, globals)
  try {
    const { default: guard } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
    const to = path => ({ path, fullPath: path, meta: {} })
    assert.equal(guard(to('/scripts/sistema')), undefined)
    assert.equal(guard(to('/conteudos')), undefined)
    assert.equal(guard(to('/studio')).statusCode, 403)
    assert.equal(guard(to('/assistente/frases')).statusCode, 403)
    assert.equal(guard(to('/relatorios')).statusCode, 403)
    assert.equal(guard(to('/biblioteca/orcamentos')).statusCode, 403)
    assert.equal(guard(to('/scripts/aprovar/token-publico')), undefined)
    rights.add('orcamentos.visualizar')
    assert.equal(guard(to('/biblioteca/orcamentos')), undefined)
    assert.equal(guard(to('/biblioteca/orcamentos/configuracoes')), undefined)
    rights.add('studio.visualizar')
    assert.equal(guard(to('/studio')), undefined)
    assert.equal(guard({ ...to('/studio/voz'), meta: { studioAction: 'voz' } }).statusCode, 403)
  } finally { Object.assign(globalThis, originals) }
})
