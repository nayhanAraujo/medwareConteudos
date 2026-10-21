export function readRecoveryToken(hash: string): string {
  const token = new URLSearchParams(hash.replace(/^#/, '')).get('token') || ''
  return token.length === 64 && /^[0-9a-f]{64}$/.test(token) ? token : ''
}

export function validateRecoveryPassword(password: string, confirmation: string): string {
  if (!password.trim() || password.length < 6) return 'Senha deve ter no mínimo 6 caracteres.'
  if (password !== confirmation) return 'As senhas não coincidem.'
  return ''
}
