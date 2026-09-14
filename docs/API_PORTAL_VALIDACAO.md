# Validação do portal — 09/09/2026

## Executado

- Build da API .NET 10 aprovado. Permanecem avisos de nulabilidade em `ReferenciasService`.
- Suíte .NET: **241 aprovados, 16 ignorados**, nenhuma falha. Inclui JWT/fixtures Python, ecodo, datas/workflow, ZIP, 20 operações/18 aliases, schemas/exemplos OpenAPI, acesso administrativo, isolamento e caminhos dos arquivos.
- Frontend/proxy: **24 testes aprovados**, incluindo navegação serializada na URL, geração de exemplos, redação de credenciais, allowlist, limites, multipart e bloqueios de execução.
- Build Nuxt aprovado; avisos de CSS Bootstrap `@charset` e de dependências não impediram o build.
- Browser: desktop, celular 390 px e tablet 820 px; busca por método, expansão de endpoint, aba Respostas preservada no reload, consulta health com retorno 200, escrita desabilitada no modo atual. Nenhum erro/warning do console observado nessa navegação.
- `Test-Portal.ps1 -AllowCopyWrites`: emissão de JWT, login exclusivo da cópia, definições protegidas 401/403/200, comparação entre prefixos para 11 consultas, painéis, erro de parâmetro 400, bloqueio de PUT no modo atual, confirmação obrigatória e PUT conservando os valores na cópia, usuário comum descartável com acesso administrativo recusado.
- Download ZIP real: mesmos nomes de entradas e hashes SHA-256 do conteúdo descompactado nos dois prefixos; header de origem legado conferido.
- `.homologacao` e configuração local ignorados pelo Git; `git diff --check` aprovado.

## Ambiente usado

API Homologacao em loopback 5081 e Nuxt de QA em 3001, ambos usando somente as cópias preparadas por `gbak`. Para exercitar ambos os modos do proxy sem iniciar produção, os destinos atual e homologação do **processo QA** apontaram para `localhost:5081` e `127.0.0.1:5081`, respectivamente. Isso não foi gravado na configuração normal do projeto.

Os originais não receberam alterações. As cópias e seus usuários de teste foram preservados fora do Git. Não houve commit, PR nem mudança no tráfego de produção.

## Ainda necessário antes do cutover

- Homologar com os consumidores reais e suas combinações de filtros/tokens, especialmente datas não ISO permissivas do Python.
- Validar todas as variantes de download (UX/Flex, múltiplos MRDs, ausência de versão ativa, fallback, imagens, relatórios e PBIX). A amostra ZIP real não comprova todas as combinações.
- Executar os 16 testes preexistentes de publicação com sua fixture específica, quando esse fluxo estiver em escopo; eles não foram habilitados contra bancos originais.
- Configurar URLs de implantação, contato de suporte, segredo original de parceiros e eventual prazo de transição da antiga chave derivada; conferir proxy/IIS e acesso administrativo no endereço final.

Essas verificações não autorizam automaticamente o cutover. Ver [API_PORTAL.md](API_PORTAL.md), [API_HOMOLOGACAO.md](API_HOMOLOGACAO.md) e [API_V1_COMPATIBILIDADE.md](API_V1_COMPATIBILIDADE.md).
