<template>
  <div>
    <LayoutAppPageHeader
      title="Pesquisas"
      subtitle="Agentes de extração a partir de artigos científicos"
      icon="search"
    />
    <div class="row">
      <div class="col-md-6 mb-4">
        <div class="card h-100 shadow-sm">
          <div class="card-header bg-primary text-white">
            <h5 class="card-title mb-0">
              <i class="bi bi-search me-2" />Pesquisa de Faixas de Normalidade
            </h5>
          </div>
          <div class="card-body">
            <p class="card-text">
              Este agente busca e extrai automaticamente faixas de normalidade para medidas específicas a partir de artigos científicos.
            </p>
            <ul class="list-group list-group-flush mb-3">
              <li class="list-group-item">
                <i class="bi bi-check-circle-fill text-success me-2" />Busca em artigos científicos
              </li>
              <li class="list-group-item">
                <i class="bi bi-check-circle-fill text-success me-2" />Extrai valores de referência
              </li>
              <li class="list-group-item">
                <i class="bi bi-check-circle-fill text-success me-2" />Identifica condições específicas
              </li>
            </ul>
            <button type="button" class="btn btn-primary" @click="abrirFlask('/agente/processar_documento')">
              <i class="bi bi-arrow-right-circle me-2" />Realizar Pesquisa
            </button>
          </div>
        </div>
      </div>
      <div class="col-md-6 mb-4">
        <div class="card h-100 shadow-sm">
          <div class="card-header bg-success text-white">
            <h5 class="card-title mb-0">
              <i class="bi bi-calculator me-2" />Pesquisa de Fórmulas
            </h5>
          </div>
          <div class="card-body">
            <p class="card-text">
              Este agente busca e extrai fórmulas matemáticas para o cálculo de medidas específicas a partir de artigos científicos.
            </p>
            <ul class="list-group list-group-flush mb-3">
              <li class="list-group-item">
                <i class="bi bi-check-circle-fill text-success me-2" />Identifica fórmulas matemáticas
              </li>
              <li class="list-group-item">
                <i class="bi bi-check-circle-fill text-success me-2" />Extrai variáveis e parâmetros
              </li>
              <li class="list-group-item">
                <i class="bi bi-check-circle-fill text-success me-2" />Valida a aplicabilidade
              </li>
            </ul>
            <button type="button" class="btn btn-success" @click="abrirFlask('/agente/processar_formulas')">
              <i class="bi bi-arrow-right-circle me-2" />Realizar Pesquisa
            </button>
          </div>
        </div>
      </div>
    </div>
    <div class="card shadow-sm">
      <div class="card-header">
        <h5 class="card-title mb-0">
          <i class="bi bi-graph-up me-2" />Estatísticas das Pesquisas
        </h5>
      </div>
      <div class="card-body">
        <p class="text-muted mb-0 small">
          Estatísticas em tempo real serão exibidas quando o módulo agente for portado para a API .NET.
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const { abrirFlask } = useMigracao()
</script>
