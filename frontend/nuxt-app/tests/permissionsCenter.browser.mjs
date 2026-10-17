import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { mkdir } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'

// Run against the dev server; all API requests are intercepted in an isolated browser context.
const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright')
const base = process.env.PERMISSIONS_TEST_BASE_URL || 'http://localhost:3000'
const output = new URL('../../../.codex-build/permissions/', import.meta.url)
await mkdir(output, { recursive: true })
const browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_BROWSER_PATH || undefined })
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } })
const names = { studio: 'Studio', biblioteca: 'Biblioteca', 'aprovacao-conteudo': 'Aprovação de Conteúdo', conteudos: 'Conteúdos', relatorios: 'Relatórios', paineis: 'Painéis', assistente: 'Assistente', usuarios: 'Usuários', configuracoes: 'Configurações', variaveis: 'Variáveis', formulas: 'Fórmulas', referencias: 'Referências', scripts: 'Scripts', pacotes: 'Pacotes' }
const catalog = Object.entries(names).flatMap(([domain]) => (domain === 'biblioteca' ? ['visualizar'] : domain === 'studio' ? ['visualizar', 'converter', 'voz'] : ['visualizar', 'criar', 'editar', 'excluir', 'importar', 'exportar', 'ativar']).map(acao => ({ chave: `${domain}.${acao}`, dominio: domain, acao, descricao: ({ visualizar: 'Consultar registros', criar: 'Cadastrar novos registros', editar: 'Modificar registros existentes', excluir: 'Excluir registros' })[acao] || acao, status: -1 })))
const profiles = { comum: catalog.filter(x => x.acao === 'visualizar').map(x => x.chave), revisor: catalog.filter(x => ['visualizar', 'criar'].includes(x.acao)).map(x => x.chave) }
const overrides = { 21: [{ chave: 'relatorios.visualizar', modo: 'NEGAR' }, { chave: 'relatorios.criar', modo: 'PERMITIR' }], 22: [], 23: [] }
const users = [{ codUsuario: 21, nome: 'Ana Lima', identificacao: 'ana.lima', perfil: 'comum', status: -1 }, { codUsuario: 22, nome: 'João Silva', identificacao: 'joao.silva', perfil: 'revisor', status: -1 }, { codUsuario: 23, nome: 'Administrador', identificacao: 'admin', perfil: 'admin', status: -1 }]
const writes = [], errors = []
let failSave = false, failLoad = false
await context.addCookies([{ name: 'mdw_token', value: 'isolated-ui-test', url: base }, { name: 'mdw_user', value: encodeURIComponent(JSON.stringify({ codusuario: 23, nome: 'Administrador', role: 'admin' })), url: base }])
await context.addInitScript(() => {
  localStorage.setItem('mdw_token', 'isolated-ui-test')
  localStorage.setItem('mdw_user', JSON.stringify({ codusuario: 23, nome: 'Administrador', role: 'admin' }))
})
await context.route('**/api-dotnet/**', async route => {
  const path = new URL(route.request().url()).pathname.replace('/api-dotnet', ''), method = route.request().method()
  let data = { success: true }, status = 200
  if (path === '/api/web/users') data.data = users
  else if (path.endsWith('/permissoes/catalogo')) data.data = catalog
  else if (path.endsWith('/permissoes/perfis')) data.data = ['comum', 'revisor', 'admin']
  else if (path.includes('/permissoes/perfis/')) {
    const key = decodeURIComponent(path.split('/').at(-1))
    if (method === 'PUT') {
      writes.push({ path, body: route.request().postDataJSON() })
      if (failSave) { status = 500; data.message = 'Falha simulada ao salvar' }
      else profiles[key] = route.request().postDataJSON().permissoes
    } else {
      data.data = { perfil: key, permissoes: profiles[key] || catalog.map(x => x.chave) }
      if (failLoad) { status = 500; data.message = 'Falha simulada de carregamento' }
    }
  } else if (path.includes('/permissoes/usuarios/')) {
    const id = Number(path.split('/').at(-1))
    if (method === 'PUT') {
      writes.push({ path, body: route.request().postDataJSON() })
      if (failSave) { status = 500; data.message = 'Falha simulada ao salvar' }
      else overrides[id] = route.request().postDataJSON().excecoes
    } else data.data = { codUsuario: id, perfil: users.find(x => x.codUsuario === id).perfil, excecoes: overrides[id], efetivas: [] }
  }
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
})
const page = await context.newPage()
page.setDefaultTimeout(7000)
page.on('pageerror', error => errors.push(error.message))
const search = page.getByLabel('Buscar módulo ou permissão', { exact: true }), filter = page.getByLabel('Filtrar permissões', { exact: true })
const combo = page.getByRole('combobox', { name: 'Usuário', exact: true })
const row = key => page.locator(`[data-permission="${key}"]`)
const click = name => page.getByRole('button', { name, exact: true }).click()
async function settled() { await page.waitForFunction(() => !document.querySelector('.loading-state') && !document.querySelector('.swal2-container')) }
async function selectUser(query) { await combo.fill(query); await combo.press('Enter') }
try {
  console.log('Profile draft and filters')
  await page.goto(base + '/biblioteca/configuracoes/permissoes')
  await page.locator('.module-navigation button').filter({ hasText: 'Relatórios' }).click()
  await row('relatorios.criar').locator('input').check()
  assert.equal(await page.locator('.pending-bar strong').innerText(), '1')
  await page.screenshot({ path: fileURLToPath(new URL('desktop.png', output)), fullPage: true })
  assert.equal(await page.locator('.permission-module:visible .module-check input').evaluate(input => input.indeterminate), true)
  await page.locator('.permission-module:visible .module-check input').click(); await click('Cancelar')
  assert.equal(await page.locator('.pending-bar strong').innerText(), '1')
  await search.fill('RELATORIOS'); assert.equal(await page.locator('.module-navigation button').count(), 1)
  await filter.selectOption('alteradas'); assert.equal(await page.locator('.permission-module:visible .permission-row').count(), 1)
  await search.fill('inexistente'); await click('Limpar filtros')
  assert.equal(await page.locator('.module-navigation button').count(), 14)
  console.log('Cancel/discard context switch and inherited permissions')
  await click('Por usuário'); await click('Cancelar')
  assert.equal(await page.locator('.mode-tabs button[aria-pressed=true]').innerText(), 'Por perfil')
  await click('Por usuário'); await click('Descartar e continuar'); await settled()
  await selectUser('ANA.LIMA'); await settled()
  await page.locator('.module-navigation button').filter({ hasText: 'Relatórios' }).click()
  assert.match(await row('relatorios.visualizar').innerText(), /Negado por exceção/)
  await row('relatorios.visualizar').locator('select').selectOption('')
  assert.match(await row('relatorios.visualizar').innerText(), /Permitido pelo perfil/)
  console.log('Module batch scope with active filters')
  await search.fill('relatorios'); await filter.selectOption('permitidas')
  await page.locator('.permission-module:visible summary').click(); await click('Negar tudo')
  assert.match(await page.locator('.swal2-html-container').innerText(), /7 permissões/)
  await click('Confirmar'); await page.getByText('Nenhuma permissão encontrada', { exact: true }).waitFor()
  assert.equal(writes.length, 0)
  await filter.selectOption('todas'); assert.equal(await page.locator('.permission-module:visible .denied').count(), 7)
  await page.locator('.permission-module:visible summary').click(); await click('Restaurar herança'); await click('Confirmar')
  await settled(); assert.equal(await page.locator('.permission-module:visible select').evaluateAll(items => items.every(item => item.value === '')), true)
  console.log('Save failure preserves draft and selected user')
  failSave = true; await selectUser('joao.silva'); await click('Salvar e continuar')
  await page.getByText('Erro ao salvar permissões', { exact: true }).waitFor()
  assert.match(await combo.inputValue(), /Ana Lima/)
  assert.equal(await page.locator('.pending-bar strong').innerText(), '2')
  await page.locator('.swal2-close').click()
  console.log('Save and continue')
  failSave = false; await selectUser('joao'); await click('Salvar e continuar')
  await page.waitForFunction(() => document.querySelector('[role=combobox]')?.value.includes('João'))
  await page.waitForFunction(() => !document.querySelector('.loading-state'))
  assert.equal(await page.locator('.pending-bar').count(), 0)
  assert.deepEqual(overrides[21], [])
  console.log('Mobile layout and explicit exceptions')
  if (await page.locator('.swal2-close').count()) await page.locator('.swal2-close').click()
  await page.setViewportSize({ width: 390, height: 844 })
  await search.fill('relatorios'); await filter.selectOption('todas')
  await row('relatorios.editar').locator('select').selectOption('NEGAR')
  assert.equal(await page.locator('.pending-bar strong').innerText(), '1')
  assert.match(await row('relatorios.editar').innerText(), /Negado por exceção/)
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
  assert.equal(await page.locator('.context-toolbar').evaluate(el => el.getBoundingClientRect().right <= innerWidth), true)
  assert.equal(await page.locator('.permission-module').first().evaluate(el => el.getBoundingClientRect().right <= innerWidth), true)
  assert.equal(await page.locator('aside').evaluate(el => el.getBoundingClientRect().width), 56)
  const layoutMetrics = await page.locator('.permissions-layout, main, .permissions-center').evaluateAll(items => items.map(el => ({ tag: el.tagName, class: el.className, width: el.getBoundingClientRect().width, padding: getComputedStyle(el).padding })))
  assert.equal(await page.locator('.permissions-center').evaluate(el => el.getBoundingClientRect().width > 300), true, JSON.stringify(layoutMetrics))
  await page.screenshot({ path: fileURLToPath(new URL('mobile.png', output)), fullPage: true })
  for (const width of [320, 768, 1024]) {
    await page.setViewportSize({ width, height: 900 })
    assert.equal(await page.locator('.context-toolbar').evaluate(el => el.scrollWidth <= el.clientWidth + 1), true, `Toolbar fits at ${width}`)
    assert.equal(await page.locator('.permission-module:visible').first().evaluate(el => el.scrollWidth <= el.clientWidth + 1), true, `Module fits at ${width}`)
  }
  await page.setViewportSize({ width: 390, height: 844 })
  console.log('Native unload warning and route protection')
  assert.equal(await page.evaluate(() => { const event = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(event); return event.defaultPrevented }), true)
  await page.getByRole('link', { name: /Voltar/ }).click(); await click('Cancelar')
  assert.match(page.url(), /permissoes/)
  await click('Descartar'); await settled()
  console.log('Administrator is read-only in both views')
  await click('Por perfil'); await settled(); await page.locator('#permission-profile').selectOption('admin'); await settled()
  assert.equal(await page.locator('.permission-row input:enabled').count(), 0)
  await click('Por usuário'); await settled(); await selectUser('admin'); await settled()
  assert.equal(await page.locator('.permission-row select:enabled').count(), 0)
  assert.match(await page.locator('.context-note').first().innerText(), /acesso total/)
  console.log('Failed context load and retry')
  failLoad = true; await selectUser('ana'); await page.locator('.load-error').filter({ hasText: 'Falha simulada de carregamento' }).waitFor()
  assert.equal(await page.locator('.permission-row select:enabled').count(), 0)
  failLoad = false; await click('Tentar novamente'); await settled()
  assert.equal(await page.locator('.load-error').count(), 0)
  console.log('Read-only operator')
  const readerUser = { codusuario: 21, nome: 'Leitor', role: 'comum', permissions: ['configuracoes.visualizar'] }
  await context.addCookies([{ name: 'mdw_user', value: encodeURIComponent(JSON.stringify(readerUser)), url: base }])
  await context.addInitScript(user => { localStorage.setItem('mdw_user', JSON.stringify(user)) }, readerUser)
  await page.reload(); await page.getByText('Somente leitura', { exact: true }).waitFor(); await search.fill('relatorios')
  assert.equal(await page.locator('.permission-row input:enabled, .permission-row select:enabled').count(), 0)
  assert.deepEqual(errors, [])
  console.log('PASS: filters, drafts, navigation, batches, admin, retry and responsive layout')
} catch (error) {
  console.error('URL:', page.url(), '\nBODY:', (await page.locator('body').innerText()).slice(-2400))
  throw error
} finally { await browser.close() }
