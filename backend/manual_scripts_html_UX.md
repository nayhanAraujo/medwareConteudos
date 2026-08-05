**Manual de Desenvolvimento de Scripts para LaudosUX**

**Guia técnico**

**Versão 1.0 | Medware Sistemas**

**Sumário**

- [Introdução](#1-introdução)
- [Estrutura HTML Permitida](#2-estrutura-html-permitida)
- [Inicialização de Scripts](#3-inicialização-de-scripts)
- [Estilização e CSS](#4-estilização-e-css)
- [Manipulação de Dados](#5-manipulação-de-dados)
- [Sistema de Impressão](#6-sistema-de-impressão)
- [Recursos e Bibliotecas](#7-recursos-e-bibliotecas)
- [Boas Práticas](#8-boas-práticas)
- [Debugging e Troubleshooting](#9-debugging-e-troubleshooting)
- [Padrões de Desenvolvimento](#10-padrões-de-desenvolvimento)

# 1. Introdução

**Regras**

A descrição de como funciona a criação de um laudo no UX segue em tópicos. Cada parte explica como deve funcionar a criação das tags, classes, ids e lógica. Existem trechos de código simples, mas com tudo o que precisa para permitir a criação correta do modelo. Cada tópico fala sobre uma regra específica, então alguns tópicos podem conter mais ou menos conteúdos nos trechos de código. Porém, a implementação deve sempre adicionar esses recursos que vão sendo detalhados ao longo do documento. Algo que não foi explicado em um tópico pode estar mais à frente para separar e detalhar melhor o que cada regra deve seguir. Com isso, o que não foi detalhado nos tópicos acima deve ser implementado ao código com os novos recursos abaixo, sem alterar o que já existe, somente adicionar e encaixar as novas condições.

**Regras gerais**

- O uso de CSS inline NÃO É PERMITIDO
- A estrutura do layout OBRIGATORIAMENTE deve ser construída em duas colunas. A separação deve ser feita de acordo com a relação entre os campos e seus respectivos títulos. Os títulos estão relacionados com certas medidas de seu grupo e todos esses campos devem estar logo abaixo desse título. Pois, a ideia dessa estrutura é permitir realocar os itens (trechos de código), reestruturando o modelo. Isso permite a adaptabilidade do layout e garante que seja ajustado conforme o necessário. Além disso, esse layout se baseia na análise da imagem enviada para sua construção.
- A implementação de elementos com ``id="Collapse"`` deve seguir a mesma quantidade de títulos para cada conjunto de medidas. Seu nome OBRIGATORIAMENTE deve ser o mesmo que seu elemento interno que representa o título para as medidas. Por exemplo, um campo ``<div id="CollapseDadosPaciente">`` tem um título interno com nome Dados do Paciente. Esse padrão de elementos criados com ``id="Collapse"`` deve ser seguido para cada trecho de medidas que deve ser impresso e ter uma conexão com seu título. Essas regras devem ser seguidas e unificadas também a partir das regras de 6 Sistema de Impressão > 6.1 Configuração para Impressão

**1.1 Objetivo**

Este manual estabelece as diretrizes técnicas para desenvolvimento de scripts HTML compatíveis com o sistema LaudosUX, garantindo funcionalidade, performance e compatibilidade com o backend Medware.

**1.2 Público-alvo**

    - Desenvolvedores Frontend
    - Programadores de laudos médicos
    - Equipe técnica de sistemas médicos
    - Analistas de TI em saúde

# 2. Estrutura HTML permitida

**Regras:**

O desenvolvimento de scripts do sistema laudosUX deve seguir as seguintes regras para garantir que não haja conflito com o sistema. A implementação do código NÃO DEVE possuir as seguintes tags padrões do HTML: ``<!DOCTYPE html>, <html>, <head>, <body>, <meta> e <title>``. OBRIGATORIAMENTE devem possuir três tags principais para sua implementação: ``<style>, <div> e <script>``. A estrutura do script DEVE SEMPRE iniciar com a tag ``<style>``, para permitir a estilização do documento.  Em seguida, a tag ``<div>`` deve ser usada para fazer a função de corpo do documento, sendo OBRIGATÓRIO possuir o ``id="containerHtml"`` como atributo, NÃO PODENDO haver nenhum outro, seja classe ou id. O script é finalizado com a tag ``<script>``, para permitir a criação da interatividade com o laudo.

O uso de tabelas para construção de layout NÃO É permitido, pois a limitação da tag não garante um layout adaptável e dificulta a construção do laudo. Como alternativa mais adequada e atual para a construção de layouts complexos, deve-se utilizar tecnologias como flexbox e grid. O uso de tais tecnologias permite uma construção mais adequada dos layouts e menos trabalhosa, permitindo a manutenção rápida dos modelos. Para isso, as principais tags usadas para essa implementação são: ``<div>``, ``<section>`` e ``<span>``.

A estrutura do layout OBRIGATORIAMENTE deve ser construída em duas colunas. A separação deve ser feita de acordo com a relação entre os campos e seus respectivos títulos. Os títulos estão relacionados com certas medidas de seu grupo e todos esses campos devem estar logo abaixo desse título. Pois, a ideia dessa estrutura é permitir realocar os itens (trechos de código), reestruturando o modelo. Isso permite a adaptabilidade do layout e garante que seja ajustado conforme o necessário. Além disso, esse layout se baseia na análise da imagem enviada para sua construção. Abaixo temos uma imagem de referência para construção do layout.

**2.1 Tags Permitidas**

O LaudosUX possui limitações específicas quanto às tags HTML aceitas:

**✅ Tags permitidas**

```html
<style>   <!-- Estilos CSS -->
<div>     <!-- Containers e estrutura -->
<script>  <!-- JavaScript -->
```

**❌ Tags não permitidas**

```html
<html>    <!-- Tag raiz HTML -->
<head>    <!-- Cabeçalho do documento -->
<body>    <!-- Corpo do documento -->
<link>    <!-- Links externos -->
``` 

**2.2 Estrutura Obrigatória**

Todo conteúdo deve estar envolvido em uma div com ID específico:

```html
<div id="containerHtml">
    <!-- Todo o conteúdo do laudo aqui -->
</div>
```

**2.3 Exemplo de Estrutura Básica**

```html
<style>
    .minha-classe-personalizada {
        color: #333;
        font-size: 14px;
    }
</style>

<div id="containerHtml">
    <div class="header-laudo">
        <h2>Título do Exame</h2>
    </div>
    
    <div class="conteudo-principal">
        <!-- Campos e conteúdo -->
    </div>
</div>

<script>
    function iniciarFuncoes() {
        // Inicialização obrigatória
    }
    
    // Lógica do script
    
    iniciarFuncoes(); // Chamada obrigatória
</script>
```


# 3. Inicialização de Scripts

**Regras:**

Para inicializar funções que devem ser chamadas ao carregar o script, devemos implementar a função "iniciarFuncoes" no início da tag ``<script>`` e dentro de seu corpo chamar toda função necessária para inicialização e funcionamento correto do script. Essa função deve ser chamada no fundo da tag ``<script>`` após toda a lógica, para permitir a inicialização das funções obrigatórias. As seguintes funções do JavaScript não devem ser usadas para inicialização de funções, pois não funcionam no sistema laudosUX: "document.addEventListener('DOMContentLoaded')", "window.onload" e "setTimeout". 

**3.1 Método Obrigatório (Atual)**

O LaudosUX exige uma função específica de inicialização:

```js
function iniciarFuncoes() {
    // Toda lógica de inicialização aqui
    carregarNormalidades();
    configurarEventos();
    obterDadosPaciente();
    calcularValoresIniciais();
}

// Outras funções podem ser declaradas fora
function carregarNormalidades() {
    // Lógica específica
}

function configurarEventos() {
    // Configuração de eventos
}

// OBRIGATÓRIO: Chamar na última linha
iniciarFuncoes();
```

**3.2 Método Obsoleto (Não Usar)**

**❌ Forma antiga (não funciona no UX):**

```js
// NÃO USAR - Não funciona no LaudosUX
document.addEventListener('DOMContentLoaded', function() {
    // Lógica aqui
});

window.onload = function() {
    // Lógica aqui
};

setTimeout(() => {
    // Lógica aqui
}, 1000);
```

**3.3 Estrutura de Inicialização Recomendada**

```js
// Declaração de variáveis globais
let dadosPaciente = {};
let configuracoes = {};

// Função principal de inicialização
function iniciarFuncoes() {
    try {
        obterDadosInterface();
        aplicarNormalidades();
        configurarCalculos();
        definirEventos();
        console.log('Script inicializado com sucesso');
    } catch (error) {
        console.error('Erro na inicialização:', error);
    }
}

// Funções específicas
function obterDadosInterface() {
    // Captura dados do UX (sexo, idade, etc.)
}

function aplicarNormalidades() {
    // Aplica valores de referência
}

function configurarCalculos() {
    // Configura fórmulas automáticas
}

function definirEventos() {
    // Define eventos de campos
}

// Chamada obrigatória
iniciarFuncoes();
```

# 4. Estilização e CSS

**Regras:**

Classes e estilização customizados em CSS devem seguir nomenclaturas seguras, para não conflitar com a estilização do sistema. Classes com nomes comuns como ``.container { }, .button { }, .input { }``, não são recomendadas, podendo ser problemáticas e causar conflitos na interface do sistema. O ideal é usar nomenclaturas adequadas e seguras como ``.laudo-container { }, .exame-button { }, .campo-input { }``, de acordo com o que o elemento representa no laudo. O uso de CSS inline NÃO É PERMITIDO para a construção do layout. Em todo ``<input>`` de número, as setas de auxílio devem ser removidas e o campo ajustado para ficar centralizado ao lado direito com seu **text-align: right**. Para campos que são atribuídos valores de cálculos e não manualmente pelo usuário, devem conter como atributo **disabled** e ter uma coloração levemente acinzentada para indicar ao usuário que não é permitido interagir e indicar que seu valor é baseado em um cálculo. A classe desse tipo de campo obrigatoriamente deve ter o nome **campoSomenteLeitura**.

Elementos que fazem o papel de título devem conter as seguintes configurações de estilização:

```css
.tituloSessao {
        background-color: #e6e6e6;
        border-radius: 5px;
        padding: 5px 0 5px 5px;
        margin-top: 10px;
        font-size: 18px;
        font-weight: bold;
        color: #242424;
        width: 620px;
    }
```

As configurações de estilização para elementos com o atributo **campoMedida** devem ser às seguintes: 

```css
.campoMedida {
        border-radius: 5px;
        border: 1px solid #e3e3e3 !important;
        width: 63px;
        height: 30px;
        margin: 5px 2px 0px 0px;
        padding-right: 6px;
        font-size: 16px;
        text-align: end;
    }
```

Elementos que contêm as unidades de medidas de cada campo devem conter a classe **unidadeMedida** com as seguintes estilizações:

```css
.unidadeMedida {
        font-size: 14px;
        width: 45px;
        text-align: left;
        color: #333;
    }
```

Elementos que contêm as normalidade dos campos devem conter a classe **referencia** com as seguintes estilizações:

```css
.referencia {
        font-size: 14px;
        text-align: left;
        color: #484848;
    }
```

Elementos que representam o nome da medida devem estar estilizados da seguinte maneira:

```css
.descricaoMedida span {
    display: block;
    font-weight: bold;
    color: #495057;
    font-size: 16px;
    width: 250px;
}
```

Os campos com o **id="Collapse"** não devem ter borda. Elementos internos também não devem ter borda e um preenchindo vertical para priorizar o espaçamento. Além disso, o laudo não deve estar centralizado, e sim sempre ao lado esquerdo por padrão.

Elementos que fazem o papel de coluna devem OBRIGATORIAMENTE ter um tamanho máximo de ``620px``

**4.1 Conflitos de Nomenclatura ⚠️ ATENÇÃO CRÍTICA:** O UX possui classes CSS globais que podem conflitar com estilos customizados.

**4.1.1 Classes Problemáticas (Evitar)**

```css
/* NÃO USAR - Afetam toda a interface do UX */
.container { }
.button { }
.input { }
.form { }
.header { }
.footer { }
.modal { }
.table { }
```

**4.1.2 Nomenclatura Segura**

```css 
/* USAR - Nomenclatura específica do laudo */
.laudo-container { }
.exame-button { }
.campo-input { }
.resultado-form { }
.cabecalho-laudo { }
.rodape-relatorio { }
.popup-calculo { }
.tabela-resultados { }
```

**4.2 Padrões de Estilização**

**4.2.1 Prefixos Recomendados**

| Tipo de Elemento | Prefixo | Exemplo |
| ---------------- | ------- | ----------|
| Containers	   | laudo-	 | laudo-container |
| Botões	       | btn-	 | btn-calcular |
| Campos	       | campo-	 | campo-medida |
| Resultados	   | result- | result-normal |
| Seções	       | secao-	 | secao-aorta |

**4.2.2 Exemplo de CSS Seguro**

```css
<style>
/* Estilização específica do laudo */
.laudo-ecocardiograma {
    font-family: 'Segoe UI', Arial, sans-serif;
    max-width: 1200px;
    margin: 0 auto;
}

.secao-medidas {
    background-color: #f8f9fa;
    padding: 15px;
    border-radius: 8px;
    margin-bottom: 20px;
}

.campo-medida {
    width: 80px;
    padding: 5px;
    border: 1px solid #ddd;
    border-radius: 4px;
    text-align: center;
}

.resultado-normal {
    color: #28a745;
    font-weight: bold;
}

.resultado-alterado {
    color: #dc3545;
    font-weight: bold;
}
</style>
```

# 5. Manipulação de Dados

**5.1 Convenção de IDs para Persistência**

Para garantir que os dados sejam salvos, campos inputs devem ter como atributo um id com uma convenção do que representa aquele campo no laudo, como ``<input id"VR_PESO">, <input id"VR_ALTURA">``. Obrigatoriamente, o id desses campos deve ser iniciado com "VR" e, em seguida, o nome do campo, para permitir a persistência dos dados. A captura de dados da interface deve ser implementada por meio da função "obterDadosInterface", usando sempre o dicionário "mVariaveisDicionario" para buscar esses dados do sistema, como ``const peso = mVariaveisAgendamento.find(v => v.valor === 'VR_PESOPACIENTE')``. Os dados capturados devem ser guardados em um objeto global inicialmente vazio chamado "dadosPaciente", atribuindo o valor do dado capturado a uma propriedade do objeto de mesmo nome. Campos que devem manipular funções, devem chamar essa função embutida no próprio html.

Todos os campos que precisam ser salvos devem seguir a convenção:

```html
<!-- OBRIGATÓRIO: ID iniciando com VR_ -->
<input type="text" id="VR_PESO" class="campoMedida" />
<input type="text" id="VR_ALTURA" class="campoMedida" />
<input type="text" id="VR_PRESSAO_SISTOLICA" class="campo-medida" />
```

**5.2 Captura de Dados da Interface**

```js
function obterDadosInterface() {
    // Captura dados básicos do paciente
    // Sempre utilize o dicionário mVariaveisAgendamento para buscar dados do sistema
    const peso = mVariaveisAgendamento.find(v => v.valor === ‘VR_PESOPACIENTE’)

    const altura = parseFloat(document.getElementById('VR_ALTURA')?.value) || 0;
    
    // Armazenar em objeto global
    dadosPaciente = {
        sexo: sexo,
        idade: parseInt(idade) || 0,
        peso: peso,
        altura: altura
    };
   
    console.log('Dados capturados:', dadosPaciente);
}
```

**5.3 Manipulação de Campos**

```js
function configurarCampos() {
    // Configurar eventos de mudança
    const campoPeso = document.getElementById('VR_PESO');
    const campoAltura = document.getElementById('VR_ALTURA');
    
    if (campoPeso) {
        campoPeso.addEventListener('input', calcularIMC);
    }
    
    if (campoAltura) {
        campoAltura.addEventListener('input', calcularIMC);
    }
}
<input oninput=”calcularIMC()” />

function calcularIMC() {
    const peso = parseFloat(document.getElementById('VR_PESO').value) || 0;
    const altura = parseFloat(document.getElementById('VR_ALTURA').value) || 0;
    
    if (peso > 0 && altura > 0) {
        const imc = peso / Math.pow(altura / 100, 2);
        document.getElementById('VR_IMC').value = imc.toFixed(1);
    }
}
```

# 6. Sistema de Impressão

**6.1 Configuração para Impressão**

**Regras**

O sistema de impressão do UX segue algumas regras rigorosas para permitir a adaptação da interface à impressão e estrutura do layout correta. As personalizações de estilos do script obrigatoriamente devem estar em volta de uma ``<div class="d-flex">`` com a classe **d-flex**, fazendo com que o layout da interface seja replicado e adaptado de forma correta à impressão. Sem o uso da classe, podem ocorrer alterações de estilos personalizados de CSS da interface, causando uma impressão de layout desconfigurado. Todo conteúdo que precisa sair na impressão obrigatoriamente deve ser envolvido por uma ``<div ats="imprimir" percent="50" indexImpressao="1">``. O uso do ``ats="imprimir"`` em uma tag em volta de outras faz com que todo o seu conteúdo seja impresso. O atributo ``percent="50"`` indica a porcentagem da ocupação do conteúdo na impressão. Por exemplo, o uso de ``percent="50"`` faz com que o conteúdo interno ocupe somente metade da folha da impressão na horizontal, a vertical é determinada pelo tamanho do conteúdo impresso da interface. O atributo ``indexImpressao="1"`` é usado para especificar a posição da qual aquele conteúdo será impresso. Além desses atributos para configuração da impressão, devemos adicionar um **id** que inicia com a palavra **Collapse**. Esse nome deve OBRIGATORIAMENTE ser seguido de um sufixo que representa o título do trecho que será impresso, como ``CollapseDadosPaciente``. O id garante que o conteúdo seja impresso somente se ao menos uma medida estiver preenchida, mostrando o resultado do que foi preenchido. Além disso, para garantir a estrutura exata da impressão, os elementos contidos na ``<div>`` com o id **Collapse** não devem ter nenhum outro elemento em torno de seu conteúdo interno. O uso de outro elemento interno para cobrir o conteúdo causa um bloqueio no funcionamento da impressão, impedindo que o campo preenchido seja identificado. O comportamento para os inputs separados é semelhante. Enquanto o ``id="Collapse"`` garante que o conteúdo a ser impresso não mostre todas as medidas, a adição obrigatória da classe ``class="campoMedida"`` garante que, se aquele input estiver preenchido, ele apareça. Caso o campo não contenha a classe, esse campo aparecerá na impressão, independentemente de estar preenchido ou não, causando o comportamento inadequado. 

O sistema moderno de impressão utiliza o atributo ats="imprimir", percent="1" e indexImpressao="1":

**6.1.1 Estrutura válida**

```html
<div id="containerHtml">
    <!-- Use a classe d-flex para adaptação do script à impressão -->
    <div class="d-flex">
        <!-- Conteúdo visível na tela, mas NÃO impresso -->
        <div>
            <button onclick="calcular()">Calcular</button>
        </div>

        <!-- Conteúdo que será impresso -->
        <!-- Use o id="CollapseDadosPaciente" para imprimir somente campos preenchidos -->
        <div id="CollapseDadosPaciente" ats="imprimir" percent="50" indexImpressao="1">
            <!-- Não deve conter uma div em volta dos elementos abaixo -->
            <div class="title">DADOS DO PACIENTE</div>
            <div>
                <div>
                    <span>Altura</span>
                </div>
                <div>
                    <!-- Use a classe campoMedida para imprimir somente se preenchido -->
                    <input class="campoMedida" type="number" step="any">
                    <div> 
                        <span>cm</span>
                    </div>
                </div>
            </div>
            <div>
                <div>
                    <span>Peso</span>
                </div>
                <div>
                    <input class="campoMedida" type="number" step="any">
                    <div> 
                        <span>kg</span>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Conteúdo fora do ats="imprimir" NÃO será impresso -->
    <div>
        <p>Opções de configuração (não impresso)</p>
    </div>
</div>
```

```html
<!-- Exemplo de uso do indexImpressao -->
<div id="containerHtml">
    <div class="d-flex">
        <!-- O trecho abaixo será impresso na segunda posição da impressão -->
        <div id="CollapseDadosPaciente" ats="imprimir" percent="50" indexImpressao="2">
            <div class="title">DADOS DO PACIENTE</div>
            <div>
                <div>
                    <span>Altura</span>
                </div>
                <div>
                    <input class="campoMedida" type="number" step="any">
                    <div> 
                        <span>cm</span>
                    </div>
                </div>
            </div>
            <div>
                <div>
                    <span>Peso</span>
                </div>
                <div>
                    <input class="campoMedida" type="number" step="any">
                    <div> 
                        <span>kg</span>
                    </div>
                </div>
            </div>
        </div>
        <!-- O trecho abaixo será impresso na primeira posição da impressão -->
        <div id="CollapseDadosPaciente" ats="imprimir" percent="50" indexImpressao="1">
            <div class="title">Câmaras esquerdas</div>
            <div>
                <div>
                    <span>Anel aórtico</span>
                </div>
                <div>
                    <input class="campoMedida" type="number" step="any">
                    <div> 
                        <span>mm</span>
                    </div>
                </div>
            </div>
            <div>
                <div>
                    <span>Arco aórtico</span>
                </div>
                <div>
                    <input class="campoMedida" type="number" step="any">
                    <div> 
                        <span>mm</span>
                    </div>
                </div>
            </div>
        </div>
    </div>

</div>
```

**6.1.2 Estrutura inválida**

```html
<!-- Não deve conter CSS inline -->
<div class="d-flex" style="flex-wrap: wrap; gap: 20px;"> 

        <!-- =============== DADOS GERAIS =============== -->
        <div id="CollapseDadosGerais" ats="imprimir" percent="50" indexImpressao="1" style="flex: 1 1 300px;">
            <!-- Não pode conter um elemento em volta dos campos -->
            <div class="laudo-secao">
                <div class="laudo-titulo">DADOS GERAIS</div>

                <!-- Altura -->
                <div class="laudo-linha">
                    <div class="laudo-descricao"><span for="VR_ALTURA">Altura</span></div>
                    <div class="laudo-campo-wrapper">
                        <input id="VR_ALTURA" class="campoMedida" type="number" step="any" oninput="calcularSuperficieCorporal()">
                        <span class="laudo-unidade">cm</span>
                    </div>
                </div>

                <!-- Peso -->
                <div class="laudo-linha">
                    <div class="laudo-descricao"><span for="VR_PESO">Peso</span></div>
                    <div class="laudo-campo-wrapper">
                        <input id="VR_PESO" class="campoMedida" type="number" step="any" oninput="calcularSuperficieCorporal()">
                        <span class="laudo-unidade">kg</span>
                    </div>
                </div>

                <!-- Sup. Corp -->
                <div class="laudo-linha">
                    <div class="laudo-descricao"><span for="VR_SUPERFICIE_CORPORAL">Sup. Corp</span></div>
                    <div class="laudo-campo-wrapper">
                        <input id="VR_SUPERFICIE_CORPORAL" class="campoMedida" type="number" step="any" readonly>
                        <span class="laudo-unidade">m²</span>
                    </div>
                </div>
            </div>
        </div>
</div>
```

**6.2 Estilos Específicos para Impressão**

```css
<style>
/* Estilos para tela */
.controles-tela {
    background-color: #e9ecef;
    padding: 10px;
    margin-bottom: 20px;
}

/* Estilos específicos para impressão */
@media print {
    .laudo-container {
        font-size: 12pt;
        line-height: 1.4;
    }
    
    .tabela-resultados {
        width: 100%;
        border-collapse: collapse;
    }
    
    .tabela-resultados td {
        border: 1px solid #000;
        padding: 8px;
    }
}

/* Estilos para o conteúdo imprimível */
.cabecalho-impressao {
    text-align: center;
    margin-bottom: 20px;
}

.resultados-impressao {
    margin: 20px 0;
}

.conclusao-impressao {
    margin-top: 30px;
    page-break-inside: avoid;
}
</style>
```

# 7. Recursos e Bibliotecas

Para carregar arquivos JSON dinamicamente, crie uma função com nome adequado do que será buscado e, a partir disso, use a base descrita no código para carregar e passar parâmetros para outras funções que dependem do arquivo. Dependendo da situação, será necessário tratar o arquivo da para permitir a busca correta.

**7.1 Bibliotecas Disponíveis**

O LaudosUX já inclui diversas bibliotecas:

**7.1.1 Chart.js**

```js
function criarGrafico() {
    const ctx = document.getElementById('meuGrafico').getContext('2d');
    const chart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: ['Jan', 'Feb', 'Mar'],
            datasets: [{
                label: 'Evolução',
                data: [12, 19, 3],
                borderColor: 'rgb(75, 192, 192)',
                tension: 0.1
            }]
        }
    });
}
```

**7.1.2 Bootstrap**

```html
<div class="container">
    <h1 class="font-weight-bold ">Laudo de Ecodopplercardiograma</h1>
    <div>
        <span class="text-dark">Aorta</span>
    </div>
    <div>
        <input type="number">
        <span class="ml-2 text-left">mm</span>
    </div>
</div>
```

**7.1.3 Funções Utilitárias do Sistema**

```js
// Verificar com a equipe de desenvolvimento quais funções estão disponíveis
// Exemplos comuns:
function salvarLaudo() {
    // Função do sistema para salvar
}

function imprimirLaudo() {
    // Função do sistema para imprimir
}

function converterHora(timestamp) {
    // Função do sistema para converter horários
}
```

**7.2 Trabalho com Imagens**

**7.2.1 Imagens Fixas do Servidor**

```html
<!-- Imagens armazenadas no servidor Medware -->
<img src="/laudos/imagens/anatomia_coracao.png" alt="Anatomia do Coração" />
<img src="/laudos/imagens/eco_normal.jpg" alt="Ecocardiograma Normal" />
```

**7.2.2 Carregamento Dinâmico**

```js
function carregarImagemAnatomica(tipo) {
    const img = document.createElement('img');
    img.src = `/laudos/imagens/anatomia_${tipo}.png`;
    img.alt = `Anatomia ${tipo}`;
    document.getElementById('container-imagem').appendChild(img);
}
```

**7.3 Arquivos JSON Dinâmicos**

```js
// Possibilidade de carregar dados de arquivos JSON do servidor
async function carregarParametros() {
    try {
        const response = await fetch('/laudos/parametros/ecocardiograma.json');
        const parametros = await response.json();
        aplicarParametros(parametros);
    } catch (error) {
        console.error('Erro ao carregar parâmetros:', error);
    }
}

function aplicarParametros(parametros) {
    // Aplicar parâmetros carregados
    parametros.normalidades.forEach(item => {
        configurarNormalidade(item.campo, item.valores);
    });
}
```

# 8. Boas Práticas

A implementação dos scripts deve garantir alto desempenho e otimização para impedir ou minimizar lentidão no carregamento. Busque percorrer elementos usando loops mais adequados para cada situação, como uso de um "forEach" para elementos HTML e não uso de "for". Toda função deve estar em volta de um "try/catch", para não impedir a execução ou depuração durante etapas de testes. O "try" deve estar em volta do que deve ser feito pela função, enquanto o "catch" imprime no console uma mensagem de erro com uma frase sugestiva da função chamada ou do que ela devia fazer, como ``console.error('Erro na inicialização:', erro)`` em "iniciarFuncoes" ou ``console.error('Erro na função <nome-da-função>', error)``, em casos que não haja uma frase que justifique o que a função deveria fazer. O uso de adição de funções via JavaScript por meio da chamada do elemento deve ser evitado, para garantir que não haja mau funcionamento. Para implementar uma chamada de função em um elemento, deve-se embutir essa função no próprio elemento ou criar o elemento via JavaScript e incluir a função da qual deve ser chamada. 

**8.1 Performance e Otimização**

**8.1.1 Scripts Leves**

```js
// ✅ BOM: Operações otimizadas
function calcularRapido() {
    const elementos = document.querySelectorAll('.campo-medida');
    elementos.forEach(el => {
        if (el.value) {
            processarValor(el.value);
        }
    });
}

// ❌ EVITAR: Operações pesadas
function calcularLento() {
    for (let i = 0; i < 10000; i++) {
        document.getElementById('resultado').innerHTML += i;
    }
}
```

**8.1.2 Gestão de Memória**

```js
// Limpar referências desnecessárias
function limparMemoria() {
    dadosTemporarios = null;
    graficosAntigos.forEach(g => g.destroy());
    graficosAntigos = [];
}
```

**8.2 Tratamento de Erros**

```js
function iniciarFuncoes() {
    try {
        configurarInterface();
        carregarDados();
    } catch (error) {
        console.error('Erro na inicialização:', error);
        mostrarMensagemErro('Erro ao carregar o laudo. Tente atualizar a página.');
    }
}

function mostrarMensagemErro(mensagem) {
    const div = document.createElement('div');
    div.className = 'alerta-erro';
    div.textContent = mensagem;
    document.getElementById('containerHtml').prepend(div);
}
```

**8.3 Compatibilidade com addEventListener⚠️ Limitação conhecida:** addEventListener funciona principalmente com elementos criados via JavaScript:

```js
// ✅ Funciona bem: Elemento criado via JS
function criarBotaoDinamico() {
    const botao = document.createElement('button');
    botao.textContent = 'Calcular';
    botao.addEventListener('click', calcular);
    document.getElementById('container').appendChild(botao);
}

//⚠️ Pode não funcionar: Elemento HTML estático
function configurarBotaoEstatico() {
    const botao = document.getElementById('botao-html');
    botao.addEventListener('click', calcular); // Pode falhar
}

// ✅ Alternativa: Usar onclick diretamente no HTML
<button onclick="calcular()">Calcular</button>
```

# 9. Padrões de Desenvolvimento

Para permitir a identificação do campo na IA do laudosUX, devemos usar uma estrutura específica para relacionar os elementos e serem interpretados como um conjunto de informações. O nome da medida OBRIGATORIAMENTE deve estar em volta de uma ``<div>`` contendo OBRIGATORIAMENTE a classe padrão ``<div class="descricaoMedida>``. Internamente a esse elemento, deve conter a tag ``<span>`` OBRIGATORIAMENTE com o atributo **for**, com o mesmo nome contido no **id** do ``<input>`` da medida que está relacionado, como ``<span for="VR_VIA_SAIDA_VE">``. Os próximos campos não são obrigatórios estar em volta de uma ``<div>``, porém, para uma estruturação e organização melhor do laudo, usamos a tag para fazer essa separação. Em torno do nosso campo principal, ``<input>``, criamos  uma ``<div>``, e dentro desse ``<input>`` criamos um **id** com a convenção ``<input id="VR_VIA_SAIDE_VE">``. Após, criamos outra ``<div>`` para separar os campos de unidade de medida, normalidade e comentários relacionados a esse campo. As normalidades e comentários são manipulados dinamicamente, enquanto a unidade de medida é sempre um conteúdo fixo, de acordo com a unidade do campo. Usamos ``<span>`` para nossa unidade de medida. As normalidades devem ter OBRIGATORIAMENTE atributos **for** e **id** para manipulação, como ``<span for="VR_VIA_SAIDA_VE" id="VR_VIA_SAIDA_VE_NORMALIDADE">`` e nesse mesmo elemento, o valor da normalidade, não criando outro elemento para inserir o valor, e sim usando o mesmo campo com final **_NORMALIDADE** para acrescentar a normalidade. Por fim, cada comentário deve seguir uma estrutura semelhante, com atributos **for** e **id** únicos, como ``<span for="VR_VIA_SAIDA_VE" id="VR_VIA_SAIDA_VE_COMENTARIONORMALIDADE">``.

**9.1 Exemplo de estrutura para identificação do campo para IA**

```html

<div>
    <!-- descricaoMedida é de uso obrigatório -->
    <div class="descricaoMedida">
        <span for="VR_VIA_SAIDA_VE">Via desaída do VE</span>
    </div>
    <div> 
        <!-- Busque normalizar os nomes das variáveis que tenham relação com o campo principal. -->
        <input id="VR_VIA_SAIDA_VE">
        <div>
            <span>mm</span>
            <!-- Finalize as variáveis dos campos de normalidade com _NORMALIDADE -->
            <span for="VR_VIA_SAIDA_VE" class="referencia ml-2" id="VR_VIA_SAIDA_VE_NORMALIDADE">10 - 21</span>
            <!-- Finalize as variáveis dos campos de comentários da normalidade com _COMENTARIONORMALIDADE -->
            <span for="VR_VIA_SAIDA_VE" id="VR_VIA_SAIDA_VE_COMENTARIONORMALIDADE"></span>
        </div>
    </div>
<div>
```

<div class="laudo-linha">
            <div class="laudo-descricao"><span for="VR_VIA_SAIDA_VE">Via de saída do VE</span></div>
            <div class="laudo-campo-wrapper">
                <input id="VR_VIA_SAIDA_VE" class="campoMedida" type="number" step="any">
                <span class="laudo-unidade">mm</span>
            </div>
</div>
