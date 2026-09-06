export interface PermissionCatalogItem {
  chave: string
  dominio: string
  acao: string
  descricao: string
  status: number
}

export interface ProfilePermissionState {
  perfil: string
  permissoes: string[]
}

export interface UserPermissionOverride {
  chave: string
  modo: 'PERMITIR' | 'NEGAR'
}

export interface UserPermissionState {
  codUsuario: number
  perfil: string
  excecoes: UserPermissionOverride[]
  efetivas: string[]
}

export function usePermissoesApi() {
  const api = useApi()
  const base = '/api/web/permissoes'

  return {
    catalogo: () => api.get<{ success: boolean; data: PermissionCatalogItem[] }>(`${base}/catalogo`),
    perfis: () => api.get<{ success: boolean; data: string[] }>(`${base}/perfis`),
    perfil: (perfil: string) => api.get<{ success: boolean; data: ProfilePermissionState }>(`${base}/perfis/${encodeURIComponent(perfil)}`),
    salvarPerfil: (perfil: string, permissoes: string[]) =>
      api.put<{ success: boolean; message?: string }>(`${base}/perfis/${encodeURIComponent(perfil)}`, { permissoes }),
    usuario: (codUsuario: number) => api.get<{ success: boolean; data: UserPermissionState }>(`${base}/usuarios/${codUsuario}`),
    salvarUsuario: (codUsuario: number, excecoes: UserPermissionOverride[]) =>
      api.put<{ success: boolean; message?: string }>(`${base}/usuarios/${codUsuario}`, { excecoes })
  }
}
