<template>
  <div>
    <DsPageHeader title="Pesquisas" subtitle="Agentes de extração a partir de artigos científicos" icon="search" />
    <DsPageShell>
      <div class="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
        <DsFeatureCard
          title="Pesquisa de Faixas de Normalidade"
          desc="Este agente busca e extrai automaticamente faixas de normalidade para medidas específicas a partir de artigos científicos."
          icon="search"
          theme="blue"
          :features="['Busca em artigos científicos', 'Extrai valores de referência', 'Identifica condições específicas']"
        >
          <DsButton icon="arrow-right-circle" @click="abrirFlask('/agente/processar_documento')">Realizar Pesquisa</DsButton>
        </DsFeatureCard>
        <DsFeatureCard
          title="Pesquisa de Fórmulas"
          desc="Este agente busca e extrai fórmulas matemáticas para o cálculo de medidas específicas a partir de artigos científicos."
          icon="calculator"
          theme="green"
          :features="['Identifica fórmulas matemáticas', 'Extrai variáveis e parâmetros', 'Valida a aplicabilidade']"
        >
          <DsButton variant="success" icon="arrow-right-circle" @click="abrirFlask('/agente/processar_formulas')">
            Realizar Pesquisa
          </DsButton>
        </DsFeatureCard>
      </div>
      <div class="rounded-2xl border border-gray-200 bg-white/80 p-6">
        <h3 class="font-semibold font-manrope text-ds-text mb-2 flex items-center gap-2">
          <i class="bi bi-graph-up text-blue-600" />Estatísticas das Pesquisas
        </h3>
        <p class="text-sm text-gray-600 mb-0">
          Estatísticas em tempo real serão exibidas quando o módulo agente for portado para a API .NET.
        </p>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })
const { abrirFlask } = useMigracao()
</script>
