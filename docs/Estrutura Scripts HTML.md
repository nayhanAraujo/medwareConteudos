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
- A implementação de elementos com ``id="Collapse"`` deve seguir a mesma quantidade de títulos para cada conjunto de medidas. Seu nome OBRIGATORIAMENTE deve ser o mesmo que seu elemento interno que representa o título para as medidas. Por exemplo, um campo ``<div id="CollapseDadosPaciente">`` tem um título interno com nome Dados do Paciente. Cada título de sessão fica sozinho no seu ``Collapse``, como primeiro elemento desse ``div``. Vários títulos no mesmo ``Collapse`` fazem a sessão vazia sair na impressão quando outra sessão do bloco tem medida preenchida. Essas regras devem ser seguidas e unificadas também a partir das regras de 6 Sistema de Impressão > 6.1 Configuração para Impressão

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

**2.4 Informações sobre o modelo**

Todo script HTML do Laudos UX deve ter, como primeiro elemento dentro de ``#containerHtml``, o bloco de informações do modelo. O bloco explica, na própria tela, quais equações e referências o laudo usa. Ele não entra na impressão e não guarda valor de laudo.

Regras:

- A ``div`` raiz usa ``class="ecodop-sobre-laudo"``, ``id="ecodopSobreLaudo"`` e ``ats="naoimprimir"``.
- O título é ``Sobre esse modelo``.
- O resumo cita, em poucas linhas, o que o modelo calcula e a fonte de cada cálculo. O botão ``Detalhes`` abre ``#detalhesReferencia`` com o restante da explicação.
- O clique fica no ``onclick`` do botão. Não use ``addEventListener`` nesse botão.
- Nenhum elemento desse bloco recebe ``id`` iniciado por ``VR_``.
- O visual segue as classes ``ecodop-sobre-laudo``, ``ecodop-sobre-laudo-inner``, ``ecodop-sobre-icone``, ``ecodop-sobre-texto``, ``ecodop-sobre-desc``, ``ecodop-sobre-btn-detalhes`` e ``ecodop-detalhes-paragrafo``.

```html
<div id="containerHtml">
    <div class="ecodop-sobre-laudo" id="ecodopSobreLaudo" ats="naoimprimir">
        <div class="ecodop-sobre-laudo-inner">
            <div class="ecodop-sobre-icones">
                <span class="ecodop-sobre-icone"><i class="fas fa-book-medical"></i></span>
            </div>
            <div class="ecodop-sobre-texto">
                <h3>Sobre esse modelo</h3>
                <p class="ecodop-sobre-desc">
                    <strong>O que calcula:</strong> resumo da equação e da referência<br>
                </p>
            </div>
            <button type="button" class="ecodop-sobre-btn-detalhes" id="btnDetalhes"
                onclick="var d=document.getElementById('detalhesReferencia'); var c=document.getElementById('ecodopSobreLaudo'); if(!d||!c)return; d.classList.toggle('show'); var o=d.classList.contains('show'); c.classList.toggle('detalhes-aberto', o); this.setAttribute('aria-expanded', o);"
                aria-expanded="false">
                <span>Detalhes</span>
                <i class="fas fa-chevron-down"></i>
            </button>
        </div>
        <div id="detalhesReferencia">
            <p class="ecodop-detalhes-paragrafo">
                Explicação das equações, faixas e limites usados neste modelo.
            </p>
        </div>
    </div>
</div>
```


# 3. Inicialização de Scripts

**Regras:**

Para inicializar funções que devem ser chamadas ao carregar o script, devemos implementar a função "iniciarFuncoes" no início da tag ``<script>`` e dentro de seu corpo chamar toda função necessária para inicialização e funcionamento correto do script. Essa função deve ser chamada no fundo da tag ``<script>`` após toda a lógica, para permitir a inicialização das funções obrigatórias. As seguintes funções do JavaScript não devem ser usadas para inicialização de funções, pois não funcionam no sistema laudosUX: "document.addEventListener('DOMContentLoaded')", "window.onload" e "setTimeout".

O script é avaliado antes de `#containerHtml` entrar na página. Nesse momento, `document.getElementById` não encontra os campos `VR_` do modelo. Peso, altura, combos e cálculos que leem esses campos só rodam quando `document.body` já contém `#containerHtml`. Se o bloco ainda estiver fora da página, um `MutationObserver` em `document.documentElement` espera a inserção e chama a mesma inicialização. O `#containerHtml` leva o comentário `<!-- iniciarFuncoes -->`. O sistema procura esse nome no HTML da tela e chama `iniciarFuncoes` de novo quando a aba do script é selecionada. 

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

Quando o título da seção tiver ícone, o ícone Font Awesome fica dentro de ``.tituloSessao`` e o texto do título fica em um ``<span class="tituloSessaoTexto">``. O afastamento entre o ícone e o título NÃO deve ser ``margin`` nem ``padding`` no ``<i>``. Na impressão o sistema troca o ``<i>`` por uma ``<img>`` e descarta a margem desse elemento, então o ícone sai colado no título. O espaço precisa estar no texto, que permanece na captura: um ``&nbsp;`` no início do span e ``padding-left`` na classe ``tituloSessaoTexto``.

```html
<div class="tituloSessao"><i class="fas fa-ruler-horizontal"></i><span class="tituloSessaoTexto">&nbsp;Medidas biométricas</span></div>
```

```css
.tituloSessao i {
    color: #28679d;
}

.tituloSessaoTexto {
    padding-left: 16px;
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
    font-weight: bold;
    color: #495057;
    font-size: 16px;
    width: 250px
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
    const peso = mVariaveisAgendamento.find(v => v.variavel === ‘VR_PESOPACIENTE’).valor

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

**5.2.1 Peso e altura do prontuário**

Todo modelo que tenha peso e altura deve preencher esses campos ao iniciar, quando ainda estiverem vazios. Os ids são `VR_PESOPAC` e `VR_ALTURAPAC`.

A fonte é a ficha do paciente, nos rótulos `lblProntuarioPeso` e `lblProntuarioAltura`. Se o rótulo não tiver número, use `mVariaveisAgendamento` com `VR_PESOPACIENTE` e `VR_ALTURAPACIENTE`.

Um valor já gravado no laudo permanece. Depois do preenchimento, rode os cálculos que dependem de peso e altura. A chamada fica dentro de `iniciarFuncoes`, e só grava o número quando o modelo já está na página, como na seção 3. Grave também o atributo `value` do input, para a cópia da impressão levar o mesmo número. O rótulo da ficha vem no formato `12,50`. Troque a vírgula por ponto e ignore o texto "Não informado".

```js
function preencherPesoAltura() {
    var pesoEl = document.getElementById('VR_PESOPAC');
    var alturaEl = document.getElementById('VR_ALTURAPAC');
    if (pesoEl && !pesoEl.value) {
        var peso = lerNumeroProntuario('lblProntuarioPeso') || lerVariavelAgendamento('VR_PESOPACIENTE');
        if (peso) pesoEl.value = peso;
    }
    if (alturaEl && !alturaEl.value) {
        var altura = lerNumeroProntuario('lblProntuarioAltura') || lerVariavelAgendamento('VR_ALTURAPACIENTE');
        if (altura) alturaEl.value = altura;
    }
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

**5.4 Navegação com Enter**

Todo modelo HTML que tenha `input` ou `select` de preenchimento deve avançar o foco com Enter. O atalho do sistema, `eventoProximoCampoComEnter`, só percorre campos que possuem `tabIndex`. O HTML personalizado não recebe esse índice, então o Enter do sistema não chega nesses campos.

A função fica no script do modelo, com o nome `eventoEnterCampo`. O clique fica no próprio elemento, em `onkeydown`. Não use `addEventListener` para isso: no prontuário o elemento é recriado e o listener se perde.

Regras:

- Cada `input` e `select` que a pessoa preenche leva `onkeydown="eventoEnterCampo(event, this)"`.
- Campo calculado, com `disabled` ou com a classe `campoSomenteLeitura`, não entra na sequência.
- Campo oculto, dentro de um elemento com a classe `inativo` ou sem `offsetParent`, também fica de fora.
- A ordem é a ordem do HTML. O próximo campo visível e editável recebe o foco. No `select`, o campo perde o foco antes de seguir.
- A função trata Enter pela tecla e pelo código 13, chama `preventDefault` e fica dentro de `try/catch`.

```html
<input class="campoMedida" onkeydown="eventoEnterCampo(event, this)" id="VR_PESOPAC" type="number">
<select class="campoSelect" onkeydown="eventoEnterCampo(event, this)" id="VR_OBJETIVO"></select>
```

```js
function campoPodeReceberEnter(campo) {
    if (!campo || campo.disabled || campo.classList.contains('campoSomenteLeitura')) return false;
    if (campo.closest('.inativo')) return false;
    return campo.offsetParent !== null;
}

function camposEditaveis() {
    var raiz = document.getElementById('containerHtml') || document;
    return Array.prototype.filter.call(raiz.querySelectorAll('input.campoMedida, select.campoSelect'), campoPodeReceberEnter);
}

function eventoEnterCampo(event, atual) {
    try {
        if (!event || (event.key !== 'Enter' && event.keyCode !== 13)) return;
        event.preventDefault();
        if (atual && atual.tagName === 'SELECT') atual.blur();
        var campos = camposEditaveis();
        var indice = campos.indexOf(atual);
        var proximo = indice >= 0 ? campos[indice + 1] : null;
        if (!proximo) return;
        proximo.focus();
        if (typeof proximo.select === 'function') proximo.select();
    } catch (erro) {
        console.error('Erro ao avançar com Enter:', erro);
    }
}
```

# 6. Sistema de Impressão

**6.1 Configuração para Impressão**

**Regras**

O sistema de impressão do UX segue algumas regras rigorosas para permitir a adaptação da interface à impressão e estrutura do layout correta. As personalizações de estilos do script obrigatoriamente devem estar em volta de uma ``<div class="d-flex">`` com a classe **d-flex**, fazendo com que o layout da interface seja replicado e adaptado de forma correta à impressão. Sem o uso da classe, podem ocorrer alterações de estilos personalizados de CSS da interface, causando uma impressão de layout desconfigurado. Todo conteúdo que precisa sair na impressão obrigatoriamente deve ser envolvido por uma ``<div ats="imprimir" percent="50" indexImpressao="1">``. O uso do ``ats="imprimir"`` em uma tag em volta de outras faz com que todo o seu conteúdo seja impresso. O atributo ``percent="50"`` indica a porcentagem da ocupação do conteúdo na impressão. Por exemplo, o uso de ``percent="50"`` faz com que o conteúdo interno ocupe somente metade da folha da impressão na horizontal, a vertical é determinada pelo tamanho do conteúdo impresso da interface. O atributo ``indexImpressao="1"`` é usado para especificar a posição da qual aquele conteúdo será impresso. Além desses atributos para configuração da impressão, devemos adicionar um **id** que inicia com a palavra **Collapse**. Esse nome deve OBRIGATORIAMENTE ser seguido de um sufixo que representa o título do trecho que será impresso, como ``CollapseDadosPaciente``. O id garante que o conteúdo seja impresso somente se ao menos uma medida estiver preenchida, mostrando o resultado do que foi preenchido. Além disso, para garantir a estrutura exata da impressão, os elementos contidos na ``<div>`` com o id **Collapse** não devem ter nenhum outro elemento em torno de seu conteúdo interno. O uso de outro elemento interno para cobrir o conteúdo causa um bloqueio no funcionamento da impressão, impedindo que o campo preenchido seja identificado. O comportamento para os inputs separados é semelhante. Enquanto o ``id="Collapse"`` garante que o conteúdo a ser impresso não mostre todas as medidas, a adição obrigatória da classe ``class="campoMedida"`` garante que, se aquele input estiver preenchido, ele apareça. Caso o campo não contenha a classe, esse campo aparecerá na impressão, independentemente de estar preenchido ou não, causando o comportamento inadequado.

**Um Collapse por título de sessão**

Cada ``.tituloSessao`` tem o seu próprio ``<div id="Collapse..." ats="imprimir" percent="48" indexImpressao="1">``. O título é o primeiro elemento desse ``div``. As linhas de medida vêm em seguida, como irmãs do título. Não envolva o título e as linhas numa ``div`` extra.

A impressão remove a linha cujo ``input.campoMedida`` está vazio. Depois olha os filhos do ``Collapse``, sem contar o primeiro. Se esse restante não tem texto, apaga somente o primeiro filho, que é o título. O bloco sai da folha quando não resta medida preenchida. Medida calculada com valor conta como preenchida, e a sessão dela imprime.

Vários títulos dentro do mesmo ``Collapse`` não passam por essa limpeza. Um campo preenchido em qualquer sessão mantém o bloco inteiro. Os títulos das sessões vazias continuam na folha, porque a impressão só consegue apagar o primeiro título e os demais títulos ainda têm texto.

```html
<!-- Válido: a Aorta sem medida não sai; o ventrículo sai se houver valor -->
<div id="CollapseVentriculoEsquerdo" ats="imprimir" percent="48" indexImpressao="2">
    <div class="tituloSessao"><span class="tituloSessaoTexto">&nbsp;Ventrículo esquerdo</span></div>
    <div class="d-flex laudo-linha">
        <input class="campoMedida" id="VR_LVEDD" type="number">
    </div>
</div>
<div id="CollapseAorta" ats="imprimir" percent="48" indexImpressao="3">
    <div class="tituloSessao"><span class="tituloSessaoTexto">&nbsp;Aorta</span></div>
    <div class="d-flex laudo-linha">
        <input class="campoMedida" id="VR_AOV" type="number">
    </div>
</div>
```

```html
<!-- Inválido: a Aorta vazia sai junto porque o ventrículo tem valor -->
<div id="CollapseMedidasEsquerdas" ats="imprimir" percent="48" indexImpressao="2">
    <div class="tituloSessao"><span class="tituloSessaoTexto">&nbsp;Ventrículo esquerdo</span></div>
    <div class="d-flex laudo-linha">
        <input class="campoMedida" id="VR_LVEDD" type="number">
    </div>
    <div class="tituloSessao"><span class="tituloSessaoTexto">&nbsp;Aorta</span></div>
    <div class="d-flex laudo-linha">
        <input class="campoMedida" id="VR_AOV" type="number">
    </div>
</div>
```

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

**6.2 Acesso ao iframe**

**Regras**

Sempre que um modelo HTML criado para o LaudosUX tiver um iframe, a busca desse elemento OBRIGATORIAMENTE deve usar `window.document.getElementById`. Não pode usar apenas `document.getElementById`. Em uma versão recente do sistema, sem o `window`, o clique no botão que usa o iframe não acontece: a função não encontra o elemento e nada é executado.

```js
// ✅ BOM: o iframe é buscado em window.document
var iframe = window.document.getElementById('iframeImpressaoReceitaOculos');

// ❌ EVITAR: sem window o botão não responde
var iframe = document.getElementById('iframeImpressaoReceitaOculos');
```

**6.3 Combo na impressão**

**Regras**

A impressão do Laudos UX fotografa um clone de cada `div[ats="imprimir"]`. O `cloneNode` copia o HTML do `select`, e a cópia do `.value` não faz a foto pintar a opção escolhida. A imagem sai com a primeira opção, em geral o texto "Selecione...".

Todo `select` de preenchimento que possa aparecer nessa foto precisa de um texto real com a opção escolhida. O `select` continua na tela para a pessoa abrir a lista. Ele sai da foto.

- A caixa já nasce no HTML. O texto da opção inicial fica em `.campoSelectEspelho` e o `select` já leva `ats="naoimprimir"`. Montar essa caixa só por script deixa a foto na primeira opção quando a preparação ainda não rodou.
- O `onchange` escrito no HTML chama `sincronizarSelect(this)` antes das outras funções. A troca da opção atualiza o texto que a foto vai usar.
- O `#containerHtml` contém o comentário `<!-- iniciarFuncoes -->`. O sistema chama `iniciarFuncoes` de novo quando a aba do script é selecionada, porque procura esse nome no HTML da tela. Peso, altura e a sincronização dos combos acontecem nessa chamada, com o modelo já na página.
- A preparação também espera `#containerHtml` entrar em `document.body`. O script do modelo é avaliado antes disso. Se o container ainda não estiver no documento, um `MutationObserver` espera a inserção e então preenche os dados e sincroniza os combos.
- Cada `select` fica dentro da caixa, por cima do texto, com `opacity: 0`, para o clique continuar abrindo a lista.
- O `select` recebe `ats="naoimprimir"`. O aviso da opção vazia, como "Selecione...", também recebe `ats="naoimprimir"`.
- Na preparação e em todo `onchange`, a opção escolhida recebe o atributo `selected`. As demais perdem esse atributo.
- Opção com `value` vazio não gera texto de impressão. A foto fica sem o aviso "Selecione...".
- O texto impresso é o texto visível da opção (`option.text`). Quando o `value` é um código interno, a impressão mostra o rótulo, por exemplo "Hadlock". O cálculo continua lendo o `value`.
- O espelho não leva `id` iniciado por `VR_` e não usa a classe `campoMedida`.

```html
<div class="selectCaixa">
    <div class="campoSelectEspelho">Hadlock</div>
    <div class="selectPlaceholder" ats="naoimprimir">Selecione...</div>
    <select class="campoSelect" id="VR_FORMULA" ats="naoimprimir" onchange="sincronizarSelect(this); calcular()">
        <option value="">Selecione...</option>
        <option value="hadlock" selected="selected">Hadlock</option>
    </select>
</div>
```

```css
.selectCaixa { position: relative; flex: 1 1 240px; min-width: 160px; }
.campoSelectEspelho {
    min-height: 32px;
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    padding: 6px 28px 6px 8px;
    background: #fff;
}
.selectPlaceholder { position: absolute; left: 9px; top: 6px; color: #94a3b8; pointer-events: none; }
.selectCaixa.temValor .selectPlaceholder { display: none; }
.selectCaixa > select { position: absolute; left: 0; top: 0; width: 100%; height: 100%; opacity: 0; }
```

```js
function textoOpcaoSelecionada(select) {
    if (!select || select.selectedIndex < 0) return "";
    var opcao = select.options[select.selectedIndex];
    if (!opcao || !String(opcao.value || "").trim()) return "";
    return String(opcao.text || "").trim();
}

function sincronizarSelect(select) {
    if (!select || !select.options) return;
    var indice = select.selectedIndex;
    Array.prototype.forEach.call(select.options, function (opcao, i) {
        if (i === indice && String(opcao.value || "").trim()) opcao.setAttribute("selected", "selected");
        else opcao.removeAttribute("selected");
    });
    var caixa = select.parentNode;
    if (!caixa || !caixa.classList.contains("selectCaixa")) return;
    var espelho = caixa.querySelector(".campoSelectEspelho");
    if (!espelho) return;
    var texto = textoOpcaoSelecionada(select);
    espelho.textContent = texto;
    if (texto) caixa.classList.add("temValor");
    else caixa.classList.remove("temValor");
}
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

**7.1.4 Quill do laudo descritivo**

**Regras**

O Laudos UX já abre o editor de texto do laudo. A instância fica em `dicTextoLongo['LAUDODESCRITIVO']`. O texto narrativo do modelo entra nesse editor.

O script HTML não cria outro Quill para receber esse texto. Não há `new Quill` no modelo, nem barra de ferramentas, nem `div` de editor dentro do HTML. Campos que a pessoa preenche, como indicação e urgência, continuam no formulário com id `VR_`. A frase montada a partir desses campos é gravada no Quill do sistema.

- A publicação usa o Quill já existente. Se `dicTextoLongo['LAUDODESCRITIVO']` ainda não existir, a consulta se repete até 20 vezes, com 500 ms entre elas. Essa espera vale só para o editor do sistema. A inicialização dos campos do modelo continua na seção 3.
- A colagem troca o conteúdo inteiro com `dangerouslyPasteHTML`, em modo silencioso, para não disparar o histórico do editor a cada tecla.
- Se o foco estiver num `input` ou `textarea` do modelo, a posição do cursor é guardada e devolvida depois da colagem.
- Conteúdo igual ao que já está no editor não é colado de novo.
- O sistema imprime `LAUDODESCRITIVO`. O modelo não reserva um bloco `ats="imprimir"` para um editor local.
- No editor do script, o campo do bloco HTML fica com **Imprimir** em **Não**. Sem isso, a impressão padrão também lista o conteúdo dos campos `VR_` do formulário, além do texto do Quill. Essa opção é do campo no editor. O script HTML não esconde esse texto por conta própria.

```js
function obterQuillLaudoDescritivo() {
    if (typeof dicTextoLongo === 'undefined' || !dicTextoLongo['LAUDODESCRITIVO']) return null;
    return dicTextoLongo['LAUDODESCRITIVO'];
}

function definirHtmlLaudoDescritivo(html) {
    var quill = obterQuillLaudoDescritivo();
    if (!quill || !quill.root || !quill.clipboard || !quill.clipboard.dangerouslyPasteHTML) return;

    var conteudo = html || '<p><br></p>';
    if (quill.root.innerHTML === conteudo) return;

    var elementoAtivo = document.activeElement;
    var laudoRoot = quill.root;
    var restaurarFoco = elementoAtivo &&
        elementoAtivo !== document.body &&
        elementoAtivo !== laudoRoot &&
        !laudoRoot.contains(elementoAtivo);
    var selInicio = null;
    var selFim = null;
    if (restaurarFoco && (elementoAtivo.tagName === 'INPUT' || elementoAtivo.tagName === 'TEXTAREA')) {
        selInicio = elementoAtivo.selectionStart;
        selFim = elementoAtivo.selectionEnd;
    }

    var fonteSilenciosa = (typeof Quill !== 'undefined' && Quill.sources && Quill.sources.SILENT) ? Quill.sources.SILENT : 'silent';
    var tamanho = quill.getLength();
    if (tamanho > 0) quill.deleteText(0, tamanho, fonteSilenciosa);
    quill.clipboard.dangerouslyPasteHTML(0, conteudo, fonteSilenciosa);

    if (restaurarFoco && typeof elementoAtivo.focus === 'function') {
        elementoAtivo.focus();
        if (selInicio !== null && typeof elementoAtivo.setSelectionRange === 'function') {
            elementoAtivo.setSelectionRange(selInicio, selFim);
        }
    }
}

function aguardarLaudoDescritivo(callback, tentativas) {
    tentativas = tentativas || 0;
    if (obterQuillLaudoDescritivo()) {
        callback();
        return;
    }
    if (tentativas >= 20) return;
    setTimeout(function () {
        aguardarLaudoDescritivo(callback, tentativas + 1);
    }, 500);
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

**8.4 Nomes reservados do editor**

**Regras**

O editor executa o `<script>` do modelo na mesma página do Laudos UX, no momento de importar e de novo ao salvar o campo HTML. Uma `function` do modelo com o mesmo nome de uma função do sistema substitui a original. O botão Salvar chama `validarCampos`. Se o modelo declarar `function validarCampos`, essa chamada deixa de validar o campo, o modal não fecha e nada é gravado.

Não declare no modelo, no escopo global, estas funções: `validarCampos`, `atualizarCampo`, `showMsg`, `montarCampoScriptDom` e `iniciarFuncoes` só pode existir como a inicialização do próprio modelo. Dê à função do modelo um nome próprio, como `validarLimitesNutricao`.

Variáveis globais do modelo devem usar `var`. `let` e `const` no topo do script entram no escopo da página. Na segunda execução, feita pelo Salvar depois da importação, o navegador recusa a nova declaração e o script para.

```js
// ✅ BOM
var limitePeso = 300;
function validarLimitesNutricao() { }

// ❌ EVITAR: substitui a validação do editor e o Salvar não grava
function validarCampos() { }
```

**8.5 Sinais que o prontuário troca por ?**

O editor mostra o caractere especial colado no arquivo. O prontuário troca esse caractere por `?` ao gravar o HTML, inclusive no campo HTML do Studio (`htmlPersonalizado`). Letras acentuadas do português permanecem. A conferência vale para título, tooltip, opção, faixa de normalidade, descrição do modelo e texto calculado.

Não cole o glifo Unicode de menor ou igual nem o de maior ou igual no HTML do modelo. Esses dois sinais são a causa direta do `?` na tela. Escreva a entidade, como no `EcodopplercardiogramaGeometryPrime2026.html`: `&le;` no lugar de menor ou igual e `&ge;` no lugar de maior ou igual. A entidade vale no HTML estático e em texto aplicado com `innerHTML`. `textContent` não converte a entidade e mostra o código na tela. Se o `?` já apareceu, o texto foi salvo com o glifo; troque pela entidade e grave de novo.

O sinal de menos comprido e o travessão continuam virando `?`. Nesses casos use o hífen do teclado, `-`. O sinal de multiplicação segue o mesmo cuidado: use `x`. O sinal de mais ou menos segue o mesmo cuidado: use `+/-`.

```html
<span>&le; 0,42</span>
<span>&ge; 0,50</span>
```

```js
elemento.innerHTML = 'Espessura relativa &le; 0,42';
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
