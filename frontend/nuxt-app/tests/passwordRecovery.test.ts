import test from 'node:test'
import assert from 'node:assert/strict'
import { readRecoveryToken, validateRecoveryPassword } from '../utils/passwordRecovery.ts'

test('reads only a valid token from the URL fragment', () => {
  const token = 'a'.repeat(64)
  assert.equal(readRecoveryToken(`#token=${token}`), token)
  for (const hash of ['', '#token=', '#token=invalid', `#other=${token}`, `#token=${token}x`, `#token=${token}%0A`]) {
    assert.equal(readRecoveryToken(hash), '')
  }
})

test('requires matching passwords with at least six characters', () => {
  assert.equal(validateRecoveryPassword('123456', '123456'), '')
  assert.equal(validateRecoveryPassword('12345', '12345'), 'Senha deve ter no mínimo 6 caracteres.')
  assert.equal(validateRecoveryPassword('      ', '      '), 'Senha deve ter no mínimo 6 caracteres.')
  assert.equal(validateRecoveryPassword('123456', '654321'), 'As senhas não coincidem.')
})
