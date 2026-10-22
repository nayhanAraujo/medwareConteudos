import test from 'node:test'
import assert from 'node:assert/strict'
import { auditFiltersFromQuery, auditFilterQuery, formatAuditDate, formatAuditValue } from '../utils/auditoria.ts'

test('defaults to the last seven days and serializes local inputs as explicit UTC', () => {
  const now = new Date('2026-10-22T15:30:00Z')
  const filters = auditFiltersFromQuery({}, now)
  const query = auditFilterQuery(filters)
  assert.equal(query.from, '2026-10-15T15:30:00.000Z')
  assert.equal(query.to, '2026-10-22T15:30:00.000Z')
  assert.deepEqual(auditFilterQuery(auditFiltersFromQuery(query, new Date('2026-11-02T09:00:00Z'))), query)
})

test('URL filters survive reload without discarding long bigint identifiers or correlation IDs', () => {
  const filters = auditFiltersFromQuery({
    from: '2026-10-15T15:30:00Z', to: '2026-10-22T15:30:00Z', actorId: '42',
    module: 'scripts', action: 'excluir', result: 'sucesso', entityType: 'script',
    entityId: '9223372036854775807', correlationId: '4000316d-0006-e300-b63f-84710c7967bb'
  })
  const query = auditFilterQuery(filters)
  assert.equal(query.entityId, '9223372036854775807')
  assert.equal(query.actorId, '42')
  assert.equal(query.correlationId, '4000316d-0006-e300-b63f-84710c7967bb')
  assert.equal(query.action, 'excluir')
  assert.equal(query.result, 'sucesso')
})

test('reload preserves seconds and milliseconds of shared UTC period boundaries', () => {
  const query = { from: '2026-10-15T15:30:12.123Z', to: '2026-10-22T15:30:59.456Z' }
  assert.deepEqual(auditFilterQuery(auditFiltersFromQuery(query)), query)
})

test('rejects reversed, empty and invalid time periods and invalid user IDs', () => {
  const base = auditFiltersFromQuery({}, new Date('2026-10-22T15:30:00Z'))
  for (const fields of [{ from: 'invalid' }, { from: '' }, { to: base.from }, { from: base.to }, { actorId: '-1' }, { actorId: '1 OR 1=1' }, { actorId: '2147483648' }])
    assert.throws(() => auditFilterQuery({ ...base, ...fields }))
})

test('ignores query arrays and separates missing, null, false, zero and empty values', () => {
  assert.equal(auditFiltersFromQuery({ actorId: ['1', '2'] }).actorId, '')
  assert.equal(formatAuditValue(null), 'Não informado')
  assert.equal(formatAuditValue(undefined), 'Não registrado')
  assert.equal(formatAuditValue(false), 'Não')
  assert.equal(formatAuditValue(0), '0')
  assert.equal(formatAuditValue(''), '(vazio)')
  assert.equal(formatAuditValue('<script>alert(1)</script>'), '<script>alert(1)</script>')
  assert.equal(formatAuditDate('not-a-date'), 'Data indisponível')
})
