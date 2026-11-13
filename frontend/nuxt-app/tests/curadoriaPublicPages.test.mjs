import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'

const read = relative => readFile(new URL(relative, import.meta.url), 'utf8')
const importTs = async relative => import(`data:text/javascript;base64,${Buffer.from(stripTypeScriptTypes(await read(relative))).toString('base64')}`)
const { isPublicCuradoriaPath } = await importTs('../utils/curadoriaPublicRoutes.ts')
const { contentRouteDomain } = await importTs('../utils/contentNavigation.ts')
const { curadoriaPrivacy, curadoriaTerms } = await importTs('../content/curadoriaDocuments.ts')
const middlewareSource = await read('../middleware/auth.global.ts')
const pluginSource = await read('../plugins/auth.client.ts')

function loadDefinition(source, factoryName, dependencies) {
  const code = stripTypeScriptTypes(source).replace(/^import .+ from .+\r?\n/gm, '').replace('export default ', 'return ')
  return new Function(factoryName, ...Object.keys(dependencies), code)(callback => callback, ...Object.values(dependencies))
}
function middleware(auth) {
  return loadDefinition(middlewareSource, 'defineNuxtRouteMiddleware', {
    useAuthStore: () => auth,
    isPublicCuradoriaPath,
    contentRouteDomain,
    navigateTo: target => ({ redirect: target }),
    createError: error => error,
    abortNavigation: error => ({ denied: error })
  })
}
const route = path => ({ path, fullPath: path, meta: {} })
const anonymous = () => ({ loadFromStorage() {}, isAuthenticated: false, can: () => false })

test('only the two exact document paths are added as public routes', () => {
  for (const path of ['/curadoria/privacidade', '/curadoria/termos']) assert.equal(isPublicCuradoriaPath(path), true)
  for (const path of ['/curadoria', '/curadoria/admin', '/curadoria/termos/editar', '/curadoria/privacidade-extra', '/Curadoria/termos', '/api/web/curadoria', '/biblioteca', '/studio'])
    assert.equal(isPublicCuradoriaPath(path), false, path)
})

test('documents bypass auth initialization and the client auth plugin without storage or session', () => {
  const forbidden = new Proxy({}, { get() { throw new Error('Public document accessed auth state') } })
  for (const path of ['/curadoria/privacidade', '/curadoria/termos']) {
    assert.equal(middleware(forbidden)(route(path)), undefined)
    const plugin = loadDefinition(pluginSource, 'defineNuxtPlugin', {
      isPublicCuradoriaPath, useRoute: () => route(path), useAuthStore() { throw new Error('Public document accessed auth plugin') }
    })
    assert.equal(plugin(), undefined)
  }
})

test('other pages remain protected and existing public routes retain their behavior', () => {
  const guard = middleware(anonymous())
  for (const path of ['/biblioteca', '/biblioteca/configuracoes/auditoria', '/scripts', '/curadoria/admin', '/api/web/scripts'])
    assert.deepEqual(guard(route(path)), { redirect: { path: '/login', query: { redirect: path } } })
  for (const path of ['/login', '/forgot-password', '/reset-password', '/apiconteudos/docs', '/scripts/aprovar/token-de-teste'])
    assert.equal(guard(route(path)), undefined)
  assert.deepEqual(middleware({ ...anonymous(), isAuthenticated: true })(route('/login')), { redirect: '/' })
})

test('module permission denial and normal client session initialization remain active outside the documents', () => {
  const auth = { ...anonymous(), isAuthenticated: true }
  assert.equal(middleware(auth)(route('/scripts')).denied.statusCode, 403)
  assert.equal(middleware(auth)(route('/biblioteca/orcamentos')).denied.statusCode, 403)
  let loaded = false
  const plugin = loadDefinition(pluginSource, 'defineNuxtPlugin', {
    isPublicCuradoriaPath, useRoute: () => route('/login'), useAuthStore: () => ({ loadFromStorage() { loaded = true } })
  })
  plugin()
  assert.equal(loaded, true)
})

test('editorial content retains all sections, pilot limitations and the confirmed contact', () => {
  assert.equal(curadoriaPrivacy.sections.length, 8)
  assert.equal(curadoriaTerms.sections.length, 8)
  const privacy = curadoriaPrivacy.sections.map(s => s.text).join('\n')
  const terms = curadoriaTerms.sections.map(s => s.text).join('\n')
  for (const text of [privacy, terms]) {
    assert.ok(text.includes('Desenvolvimento | Demandas'))
    assert.ok(text.includes('nayhan@medware.com.br'))
  }
  for (const fragment of ['depende de instalação e concessão', 'Anexos e links externos não são baixados', 'Não foi definido neste aviso um prazo numérico de retenção', 'Não autoriza gravação automática', 'Microsoft Teams/Microsoft Graph', 'Grok Bot'])
    assert.ok(privacy.includes(fragment), fragment)
  for (const fragment of ['não concedem acesso técnico', 'podem conter erros', 'não autoriza, por si só', 'não deve executar comandos', 'não substitui a avaliação técnica'])
    assert.ok(terms.includes(fragment), fragment)
})

test('dedicated pages/layout retain date, version, reciprocal links and no general app shell', async () => {
  const layout = await read('../layouts/curadoria.vue')
  const document = await read('../components/curadoria/CuradoriaDocument.vue')
  for (const slug of ['privacidade', 'termos']) {
    const page = await read(`../pages/curadoria/${slug}.vue`)
    assert.ok(page.includes("layout: 'curadoria'"))
    assert.ok(page.includes('titleTemplate: null'))
    assert.ok(layout.includes(`to="/curadoria/${slug}"`))
  }
  assert.ok(document.includes('datetime="2026-11-13"'))
  assert.ok(document.includes('Versão 1.0'))
  assert.ok(layout.includes('Ir para o conteúdo'))
  assert.ok(layout.includes("lang: 'pt-BR'"))
  assert.ok(!layout.includes('LayoutAppSidebar'))
  assert.ok(!document.includes('v-html'))
})

test('existing IIS template forwards both document paths to Nuxt, not the API', async () => {
  const xml = await read('../../../tools/production/templates/public.web.config')
  const rules = [...xml.matchAll(/<rule\b[^>]*name="([^"]+)"[^>]*>([\s\S]*?)<\/rule>/g)]
    .filter(([, name]) => !['HTTPS', 'Trusted proxy scheme'].includes(name))
    .map(([, name, body]) => ({ name, match: new RegExp(body.match(/<match url="([^"]+)"/)[1]), body }))
  for (const path of ['curadoria/privacidade', 'curadoria/termos']) {
    const first = rules.find(rule => rule.match.test(path))
    assert.equal(first.name, 'Nuxt SSR')
    assert.ok(first.body.includes('http://127.0.0.1:3000/'))
  }
  assert.equal(rules.find(rule => rule.match.test('api/web/scripts')).name, 'API and Swagger')
})
