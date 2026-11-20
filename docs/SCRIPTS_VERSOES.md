# Scripts — listagem e ciclo de versões

Em **Scripts** (`/scripts`), a lista começa com 10 itens por página. O seletor
oferece 10, 20, 30, 40 ou 50 e volta à primeira página ao mudar a quantidade.
A paginação é feita pela API, com os filtros da tela. Em `/scripts/pacotes`,
use a busca global de modelos para localizar um script sem saber seu pacote.

O catálogo `/conteudos` não exibe mais os cards **Modelos de mensagens**,
**Pacotes** e **Modelos e seções**. Isso é uma mudança na vitrine, não uma remoção das rotas nem uma
mudança de permissão.

## Versões de um script

Na tela `/scripts/{id}/versoes`, as ações são confirmadas antes do envio ao
servidor. Ativação e aprovação são estados independentes:

| Ação | Requer | Efeito |
|---|---|---|
| Inativar | `scripts/ativar` | Limpa o estado ativo da versão, sem apagar arquivos nem ativar outra versão em seu lugar. |
| Desaprovar | `scripts/aprovar` | Remove a aprovação, o aprovador, o ambiente e a data de aprovação, sem mudar o estado ativo. |

As mudanças efetivas registram `INATIVACAO` ou `DESAPROVACAO` no histórico. Repetir
uma ação já aplicada não cria outro registro. Excluir é uma operação separada:
não use exclusão para apenas retirar uma aprovação ou inativar uma versão.
O impacto na publicação deve ser verificado separadamente na tela de
[publicação no Assistente](../README.md#6-publicação-no-assistente).

Contratos da Web Admin (JWT web e permissão de domínio, não API de parceiros):

- `POST /api/web/scripts/versoes/{codVersao}/inativar`
- `POST /api/web/scripts/versoes/{codVersao}/desaprovar`

As duas rotas não exigem corpo e retornam `{"success":true}` no sucesso.
Versão não encontrada retorna HTTP 400 com `message`.

Verificações automatizadas em `frontend/nuxt-app/tests/`:
`scriptPageSize.test.mjs`, `scriptVersionState.test.mjs` e
`scriptVersionDeletion.test.mjs`.
