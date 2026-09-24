# Compatibilidade pública v1 — Python / .NET 10

Referência: `../routes/api.py`, inspecionado em 08/09/2026. Escopo: middleware de parceiros, serviço público, operações de scripts e helpers de contrato, integrados ao portal e aos filtros OpenAPI.

## Estado para integração

Implementação integrada. Builds, testes xUnit e testes do proxy executados; verificações HTTP usam cópias produzidas por backup/restore Firebird. A execução das fixtures Python usa somente funções extraídas por AST e cursores em memória. A cobertura de contrato não equivale à homologação de todos os consumidores de produção.

Validações executadas: geração e rechecagem das fixtures Python e `git diff --check` nos arquivos desta entrega. Isso não substitui compilação, execução xUnit nem smoke HTTP/Firebird em cópia descartável.

## JWT

- HS256 fixo; segredo convertido diretamente para bytes UTF-8, inclusive curto ou não ASCII. Não há SHA-256, padding ou decodificação base64 do segredo na emissão normal.
- `HMACSHA256` evita a restrição de comprimento do IdentityModel sem modificar o segredo usado pelo Python. A comparação da assinatura usa `CryptographicOperations.FixedTimeEquals`; algoritmos alternativos, `none`, payload destacado e assinatura alterada são rejeitados.
- Token pode ser enviado puro ou com `Bearer `, conforme Python. O prefixo é case-sensitive; espaços externos são removidos.
- `exp` é opcional, mas, quando presente, é validado sem tolerância adicional: `exp <= agora` resulta em 401 / `Token expirado` / `O JWT está expirado`. Inteiros em string e truncamento de valores numéricos seguem PyJWT. Valores inválidos falham fechados com 401.
- `iat`, `nbf`, `aud`, `sub` e `jti` também recebem as validações usadas pelo PyJWT 2.11.0 da referência local. Não foram acrescentados issuer, audience ou exp obrigatórios à emissão.
- `senha` prevalece sobre `password`, salvo valor vazio/falso; o mesmo vale para `datahora` / `datetime`. Assinatura/claims JWT são avaliados antes do erro de configuração 503.
- Data/hora ISO com offset é normalizada para UTC; sem offset significa **UTC**, independentemente do fuso do host. Precisão excedente a microssegundos é truncada. Regras, nesta ordem: não futura; idade não superior à tolerância; mesmo dia UTC. O limite exato da tolerância é aceito, mas a meia-noite UTC continua invalidando tokens do dia anterior.
- Emissão mantém apenas `senha` e `datahora`, esta em `yyyy-MM-ddTHH:mm:ssZ`, sem fração. Envelope: `success`, `token`, `expires_info`.

### Migração opcional do hash anterior

Por padrão, `AcceptLegacyHashedKey=false` e `JwtLegacyHashedKeyUntilUtc=null`. Portanto tokens assinados com SHA-256 do segredo curto deixam de ser aceitos.

Somente quando necessário para transição, configurar ambos na seção existente `ApiPartner`:

```json
{
  "ApiPartner": {
    "AcceptLegacyHashedKey": true,
    "JwtLegacyHashedKeyUntilUtc": "2026-09-09T00:00:00Z"
  }
}
```

O prazo acima é apenas ilustrativo; escolher deliberadamente uma janela curta. Também são aceitas as variáveis padrão do binder .NET `ApiPartner__AcceptLegacyHashedKey` e `ApiPartner__JwtLegacyHashedKeyUntilUtc`; não foram criados aliases `API_JWT_*` novos.

Prazo ausente, inválido, sem indicação explícita UTC, com offset não zero ou já atingido desabilita o fallback. Somente `Z` ou `+00:00` são aceitos. Em `agora == prazo`, o hash anterior já é recusado. Apenas segredos com menos de 32 bytes UTF-8 tinham esse hash na implementação anterior. A emissão sempre usa a chave raw; senha, data/hora, exp e demais regras continuam obrigatórias durante a janela. Esta opção não representa rotação de segredo/senha.

## Matriz de contratos

### Inventário de operações

Prefixo público `/apiconteudos/v1`. A coluna Alias indica a mesma operação em `/api/v1`. `ApiV1RouteTests` protege esse inventário; OPTIONS de fórmulas/ecodo são adicionais. O catálogo OpenAPI descreve os filtros, campos, nulabilidade e respostas por operação.

| Método e caminho relativo | Alias | Entrada / saída principal |
| --- | --- | --- |
| POST `/token` | sim | senha → success/token/expires_info; 400/401/503 |
| GET `/health` | sim | sem JWT → success/status/database/timestamp |
| GET `/variaveis` | sim | filtros de variável/especialidade → lista/total |
| GET `/variaveis/{codvariavel}` | sim | código → variável com fórmulas, normalidades e comentários; 404 |
| GET `/normalidades` | sim | variável, sexo, referência → lista/total |
| GET `/normalidades_ecodopplercardiograma` | sim | referência query → mapa de faixas legado |
| GET `/normalidades/ecodopplercardiograma/{referencia}` | sim | referência path → mesmo mapa |
| GET `/formulas` | sim | funções/equações no formato consumido pelo legado |
| GET `/referencias` | sim | filtros bibliográficos → lista/total |
| GET `/sistema/info` | sim | estatísticas e especialidade/total em minúsculas |
| GET `/especialidades` | sim | especialidades e contagem de variáveis |
| GET `/relatorios` | sim | filtros de relatório → lista/total |
| GET `/relatorios/{codrelatorio}/download` | sim | bytes e nome de arquivo; 404 |
| GET `/scripts` | sim | sistema, aprovado, ativo, pacote, incluir_arquivos → lista |
| GET `/scripts/ultimo-verificado` | sim | último verificado, workflow_key; 204 quando ausente |
| GET `/scripts/{codscriptlaudo}/imagem` | sim | índice → bytes de imagem; 404 |
| GET `/scripts/{codscriptlaudo}/download` | sim | tipo → ZIP com JSON/DLL/MRD e headers de origem |
| GET `/paineis` | não | filtros de painel → lista/total |
| GET `/paineis/{codpainel}/download` | não | PBIX binário; 404 |
| PUT `/normalidades/{codnormalidade}` | sim | limites, sexo e idades → success/message/timestamp; 400/404 |

Os dois endpoints novos por cliente não substituem nenhuma dessas 20 operações. **Mudança intencional de segurança:** a API interna `/api/v1` agora exige JWT de parceiro ou JWT web com permissão correspondente. Clientes que acessavam anonimamente precisam enviar a credencial; health, token e preflight continuam sendo exceções.

| Área | Contrato preservado/corrigido | Cobertura automatizada / integração |
| --- | --- | --- |
| JWT raw | Chave curta, longa e UTF-8; HS256; comparação de assinatura constante | Fixtures Python + HMAC independente .NET + adulteração de cada byte da assinatura |
| UTC / exp | Naive UTC, offsets ±03:00, microssegundos, futuro, limite de tolerância, virada de dia, exp opcional/exato/string/inválido | 41 casos JWT de referência; xUnit com `TimeProvider` fixo |
| Hash anterior | Opt-in e prazo UTC explícito; corte exato; assinatura antiga não ignora exp | Teorias xUnit; default fechado e emissão raw mesmo durante janela |
| Autenticação HTTP | `/apiconteudos/v1` exige JWT parceiro; `/api/v1` aceita parceiro ou web autorizado; health, token e OPTIONS são exceções | Middleware real e testes de permissões |
| Datas públicas | `datetime.isoformat()`: nenhuma fração se microsegundos zero; senão seis dígitos; não emitir sétimo dígito/fuso para TIMESTAMP naive | Cinco datas Python, ticks submicrosegundo, DateOnly, offset explícito e cultura pt-BR |
| Scripts / n8n | `workflow_key = codscriptlaudo + '|' + data_verificacao`, sem reformatar texto; nulo sem data/código | Fixtures Python e helper usado pelas operações reais |
| Metadados MRD | `mrd_fonte`: `SCRIPT_VERSAO_MRD` ou `SCRIPTLAUDO_MRD` | xUnit; nomes SQL migrados continuam `SCRIPTVERSAOMRD` / `SCRIPTLAUDOMRD` |
| Download scripts | `X-Script-Download-Source`: `SCRIPT_VERSOES` ou `SCRIPTLAUDO` | Fixtures Python + execução real de `ScriptDownloadFileResult` via MVC em memória |
| ZIP scripts | Espaços e acentos preservados no nome base e nas entradas; bytes preservados; sempre ZIP | Criação e leitura de ZIP real em memória; testar seleção MRD/JSON/DLL em cópia do banco |
| `/sistema/info` | Envelope `success`, `data`, `timestamp`, sem `total` externo; chaves `especialidade`/`total` minúsculas mesmo com Firebird uppercase | Projeção explícita + teste de JSON; conferir consulta em cópia |
| Estatísticas | `variaveis_com_formula` usa `COUNT(DISTINCT CODVARIAVEL) FROM FORMULA_VARIAVEL`, como Python | Revisão SQL; contagens precisam de smoke Firebird |
| Listagens públicas | `success`, `data`, `total`, `timestamp`; timestamps Python; cliente moderno mantém seu envelope existente | Helper testado; comparar dados via HTTP após integração |
| Ecodo | Mapper legado independente: BAIXO/LOW → low; MODERADO/MODERATED → moderated; ELEVADO/ELEVATED → elevated; ALTO/HIGH → high; demais → default | 19 cenários executados em Python e comparados ao mapper .NET |
| Ecodo duplicados | Sem classificação: primeiro default vence; classificados: último da zona vence, inclusive default de classificação desconhecida | Fixtures de duplicatas e colisões |
| Ecodo limites/meta | Limite nulo não gera quartis; default isolado completo gera 4 zonas; calculadas herdam Fonte/Pagina, **não Ano**; metadados falsos/zero omitidos | Fixtures de null, zero, sexo vazio→U, metadados e zona explícita |
| Cliente moderno | Normal/Leve/Moderado/Grave continuam normal/leve/moderado/grave; comentários preservados | Testes de isolamento com `ClienteNormalidadesBuilder`; limites nulos não viram zero |
| Relatórios | Trim de filtros, mensagens legadas, vínculo `RELATORIOVALIDACOES` equivalente a `RELATORIO_VALIDACOES`, nome seguro e MIME com charset | Testes de filtros inválidos/nome; vínculo SQL necessita smoke na cópia |
| Outros envelopes | Variável 404 projeta exemplos minúsculos e preserva dica; PUT normalidade usa mensagem sem ponto final | Revisão; testar via HTTP após filter do integrador |

## Fixtures Python reprodutíveis

Arquivos em `backend/ConversorHtml.Tests/Fixtures/ApiV1/`:

- `generate_reference.py`: lê `../routes/api.py`, seleciona funções por AST e executa exclusivamente essas funções com configuração fictícia, relógio fixo e cursores em memória. Não importa o módulo da aplicação, não lê credenciais e não conecta Firebird. Imprime JSON em stdout; `--check` somente compara.
- `python_reference.json`: resultados da execução com PyJWT 2.11.0 e python-dateutil 2.9.0.post0: **41 JWT, 19 Ecodo, 5 datas e 3 headers**, além de token sintético com hash anterior.

Para revalidar, a partir da raiz do repositório:

```powershell
python backend/ConversorHtml.Tests/Fixtures/ApiV1/generate_reference.py --check
```

Os testes .NET consomem o JSON versionado e não dependem de Python instalado. A localização das fixtures usa `CallerFilePath` no checkout de testes, sem alterar o csproj compartilhado; distribuição dos binários de teste para outra máquina sem o checkout exige copiar/resolver também as fixtures.

## O que executar ao integrar

Após liberar exclusividade dos artefatos .NET, rodar **sequencialmente** (SDK .NET 10), sem outra compilação/teste simultâneo:

```powershell
dotnet test backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj --filter "FullyQualifiedName~ApiV1" -m:1
dotnet test backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj --no-build -m:1
```

As classes novas são `ApiV1JwtContractTests`, `ApiV1PayloadContractTests` e `ApiV1ScriptContractTests`; `ApiV1TestSupport` fornece relógio, fixtures e uma factory que lança exceção se algum teste tentar acessar banco. Não há `WebApplicationFactory`, startup de `Program` ou servidor nos testes desta entrega.

Depois, em instância isolada e **somente cópia descartável do Firebird**, conferir:

1. `/apiconteudos/v1/token` seguido de endpoint protegido com chave curta, e token emitido no Python consumido pelo .NET; testar também o inverso. Repetir host UTC e host America/Sao_Paulo, com tokens perto da meia-noite UTC. Nunca usar credenciais reais nas fixtures.
2. Rotas `/api/v1` e `/apiconteudos/v1`, OPTIONS, health, envelopes de erro e CORS. `Program` e `LegacyApiValidationFilter` padronizam erros de model binding do controller público, sem modificar o envelope das rotas web.
3. `/sistema/info`: nenhuma chave uppercase, nenhum `total` externo, contagens iguais à consulta Python. Variável inexistente deve trazer exemplos com `codvariavel` e `nome` minúsculos.
4. Scripts sem/com versão ativa, com/sem arquivos, nenhum verificado (204), timestamp com zero/100000/123456 microsegundos e estabilidade de `workflow_key`. `imagem_capa` continua usando o caminho legado `/apiconteudos/v1/...`.
5. Downloads UX/Flex, `tipo=json`, `dll`, `mrd`, `mrd_todos` e pacote completo; MRD padrão e secundários, linguagem HTML, versão ativa sem blob com fallback, acentos/espaços nos nomes, ZIP entries e headers de origem. Os testes sem banco não exercitam consultas/seleção de blobs.
6. Relatórios: somente registros com validação, incluindo múltiplas validações (o JOIN preserva a multiplicidade do Python), filtros com espaços, download de conteúdo string/binário e nomes especiais. Validar tabela migrada `RELATORIOVALIDACOES` na cópia. Painéis/PBIX devem preservar contratos binários existentes.
7. Ecodo nas duas URLs e por referência: default duplicado, classificação desconhecida/Moderado, limites nulos e metadados; verificar simultaneamente normalidades modernas do cliente para ausência de regressão. PUT de normalidade somente na cópia descartável.

## Limites e decisões explícitas

- Não se afirma equivalência para toda a gramática permissiva de `dateutil.parse`: formatos ISO completos com/sem fuso são a base do contrato testado. Datas parciais, nomes de fuso e texto livre dependentes do parser precisam de inventário de consumidores antes de prometer suporte.
- A referência Python pode lançar exceção não tratada para certos tipos de claims, como `exp:null`. O .NET falha fechado com 401, não replica 500 acidental.
- `X-Script-Download-Suffix` preserva o valor semântico `padrão` no resultado, mas permanece ASCII `padrao` no header efetivo por restrição padrão do Kestrel. O header solicitado de **Source** preserva exatamente os nomes legados. Content-Disposition usa codificação RFC 5987 do MVC para nomes Unicode; validar nome decodificado, não bytes idênticos do header Flask.
- Os nomes físicos migrados de tabelas não foram renomeados para reproduzir strings públicas. Encoding de headers e CORS foram preservados; documentação Swagger e filtros do controller público foram integrados ao portal.
- Não foram adicionados issuer/audience obrigatórios, novo fluxo de autenticação ou exigência de segredo de 32 bytes. Recomendar segredo forte continua apropriado, mas não cabe substituir silenciosamente a chave raw acordada com parceiros.
