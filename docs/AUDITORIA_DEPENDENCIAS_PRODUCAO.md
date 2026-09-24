# Pendências de dependências — 24/09/2026

Auditoria somente leitura com `npm audit --omit=dev --json`. O pacote IIS não deve ser entendido como aprovação de segurança para o corte.

| Área | Dependência | Resultado |
| --- | --- | --- |
| Bridge Studio | `@cursor/sdk@1.0.23` → `@connectrpc/connect-node@1.7.0` → `undici@5.29.0` | 1 alerta alto e 2 moderados na árvore. Sem correção automática compatível indicada pelo npm. |
| Frontend | `devalue <5.9.1` | Moderado, negação de serviço por entrada malformada. Há atualização disponível. |
| Frontend | `svgo 4.0.x` | Alto, sanitização incompleta de SVG. Há atualização disponível. |

Próxima etapa: atualizar as dependências de forma controlada, repetir build/testes e validar os fluxos Studio e Nuxt antes de publicar. Não forçar `npm audit fix --force` nem sobrescrever o major do undici sem testar o transporte do SDK. A auditoria da árvore instalada não comprova, isoladamente, explorabilidade em cada fluxo, mas requer avaliação antes da liberação.

As correções de autenticação, autorização, segredos e controles de abuso são independentes dessas pendências. Os testes Firebird isolados e o smoke IIS remoto também permanecem necessários.
