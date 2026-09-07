<template>
  <div>
    <DsPageHeader
      title="Referências e Normalidades"
      subtitle="Consulte faixas por medida ou por estudo e mantenha os vínculos consistentes"
      icon="journal-medical"
    />
    <DsPageShell>
      <DsTabs v-model="modo" :tabs="tabs" />

      <!-- Modo: Por variável -->
      <div v-if="modo === 'variavel'" class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <div class="lg:col-span-4">
          <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
            <DsInput
              v-model="variavelListaBusca"
              label="Buscar medida"
              placeholder="Nome, código ou sigla"
              @enter="loadPorVariavel()"
            />
            <div class="flex justify-end mt-2">
              <DsButton variant="secondary" size="sm" icon="search" :loading="loadingVar" @click="loadPorVariavel()">
                Filtrar
              </DsButton>
            </div>
          </div>
          <div class="rounded-2xl border border-gray-200 bg-white p-2 max-h-[65vh] overflow-y-auto">
            <button
              v-for="v in painelVar?.variaveis || []"
              :key="v.codVariavel"
              type="button"
              class="w-full text-left rounded-xl px-3 py-2 transition border mb-2"
              :class="selectedVarId === v.codVariavel ? 'bg-blue-50 border-blue-200' : 'bg-white border-gray-200 hover:bg-gray-50'"
              @click="selectVariavel(v.codVariavel)"
            >
              <p class="font-semibold text-sm mb-1">{{ v.nome }}</p>
              <p class="text-xs text-gray-500 mb-1">{{ v.variavel || '—' }} · {{ v.sigla || 's/sigla' }}</p>
              <div class="text-[11px] text-gray-500">
                <span class="mr-2">Estudos: {{ v.totalReferencias }}</span>
                <span>Faixas: {{ v.totalNormalidades }}</span>
              </div>
            </button>
            <p v-if="!(painelVar?.variaveis || []).length" class="text-sm text-gray-500 p-3 text-center">
              Nenhuma medida com normalidade vinculada.
            </p>
          </div>
        </div>

        <div class="lg:col-span-8">
          <DsAlert v-if="errorMsgVar" variant="error" class="mb-4">{{ errorMsgVar }}</DsAlert>
          <template v-if="painelVar?.variavelSelecionada">
            <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
              <p class="font-semibold text-ds-text mb-1">{{ painelVar.variavelSelecionada.nome }}</p>
              <p class="text-sm text-gray-600 mb-1">
                {{ painelVar.variavelSelecionada.variavel || '—' }} · {{ painelVar.variavelSelecionada.sigla || 's/sigla' }}
              </p>
              <p class="text-sm text-gray-600">
                {{ painelVar.variavelSelecionada.totalReferencias }} estudo(s) ·
                {{ painelVar.variavelSelecionada.totalNormalidades }} faixa(s)
              </p>
              <div class="mt-3 flex flex-wrap gap-2">
                <DsButton size="sm" variant="secondary" icon="eye" @click="abrirModalPorVariavel()">
                  Ver todas as faixas
                </DsButton>
                <DsButton size="sm" variant="secondary" icon="arrow-left-right" @click="abrirConversaoUnidade()">
                  Converter unidade
                </DsButton>
              </div>
            </div>

            <div v-if="painelVar.estudos?.length" class="space-y-4">
              <div
                v-for="estudo in painelVar.estudos"
                :key="estudo.codReferencia"
                class="rounded-2xl border border-gray-200 bg-white p-4"
              >
                <div class="flex flex-wrap items-start justify-between gap-2 mb-3">
                  <div>
                    <p class="font-semibold text-sm mb-0">
                      #{{ estudo.codReferencia }} · {{ estudo.titulo }}
                    </p>
                    <p class="text-xs text-gray-500">Ano: {{ estudo.ano || 's/ano' }}</p>
                    <p v-if="estudo.comentarioTexto" class="text-xs text-gray-700 mt-1">
                      Comentário: <strong>{{ estudo.comentarioTexto }}</strong>
                    </p>
                  </div>
                  <DsButton size="sm" variant="secondary" icon="pencil" @click="abrirModalEstudo(estudo)">
                    Editar faixas
                  </DsButton>
                </div>
                <div class="overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-50">
                      <tr>
                        <th class="text-left px-2 py-2">Sexo</th>
                        <th class="text-left px-2 py-2">Min</th>
                        <th class="text-left px-2 py-2">Max</th>
                        <th class="text-left px-2 py-2">Idade</th>
                        <th class="text-left px-2 py-2">Classificação</th>
                        <th class="text-left px-2 py-2">Pág.</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr
                        v-for="n in estudo.normalidades"
                        :key="n.codNormalidade"
                        class="border-t border-gray-100"
                      >
                        <td class="px-2 py-2">{{ n.sexo || '-' }}</td>
                        <td class="px-2 py-2">{{ n.valorMin ?? '-' }}</td>
                        <td class="px-2 py-2">{{ n.valorMax ?? '-' }}</td>
                        <td class="px-2 py-2">{{ n.idadeMin ?? '-' }}–{{ n.idadeMax ?? '-' }}</td>
                        <td class="px-2 py-2">{{ n.classificacao || '-' }}</td>
                        <td class="px-2 py-2">{{ n.pagina ?? '-' }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
            <DsAlert v-else variant="info">Nenhuma faixa vinculada a estudos para esta medida.</DsAlert>
          </template>
          <DsAlert v-else variant="info">Selecione uma medida para ver as normalidades por estudo.</DsAlert>
        </div>
      </div>

      <!-- Modo: Por referência -->
      <div v-else class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <div class="lg:col-span-4">
          <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
            <DsInput
              v-model="referenciaBusca"
              label="Buscar referência"
              placeholder="Título, código, descrição ou ano"
              @enter="load()"
            />
            <div class="flex justify-end mt-2">
              <DsButton variant="secondary" size="sm" icon="search" @click="load()">Filtrar</DsButton>
            </div>
          </div>
          <div class="rounded-2xl border border-gray-200 bg-white p-2 max-h-[65vh] overflow-y-auto">
            <button
              v-for="r in painel?.referencias || []"
              :key="r.codigo"
              type="button"
              class="w-full text-left rounded-xl px-3 py-2 transition border mb-2"
              :class="selectedRefId === r.codigo ? 'bg-blue-50 border-blue-200' : 'bg-white border-gray-200 hover:bg-gray-50'"
              @click="selectReferencia(r.codigo)"
            >
              <p class="font-semibold text-sm mb-1">#{{ r.codigo }} · {{ r.titulo }}</p>
              <p class="text-xs text-gray-500 mb-1">{{ r.autores || 'Sem autores' }}</p>
              <div class="text-[11px] text-gray-500">
                <span class="mr-2">{{ r.ano || 's/ano' }}</span>
                <span class="mr-2">Vars: {{ r.totalVariaveis }}</span>
                <span>Norms: {{ r.totalNormalidades }}</span>
              </div>
            </button>
          </div>
          <DsAlert variant="warning" class="mt-4">
            Normalidades sem referência: <strong>{{ painel?.totalSemReferencia || 0 }}</strong>
          </DsAlert>
        </div>

        <div class="lg:col-span-8">
          <DsAlert v-if="errorMsg" variant="error" class="mb-4">{{ errorMsg }}</DsAlert>
          <template v-if="painel?.referenciaSelecionada">
            <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
              <p class="font-semibold text-ds-text mb-1">
                #{{ painel.referenciaSelecionada.codigo }} · {{ painel.referenciaSelecionada.titulo }}
              </p>
              <p class="text-sm text-gray-600 mb-1">Ano: {{ painel.referenciaSelecionada.ano || 's/ano' }}</p>
              <p class="text-sm text-gray-600 mb-1">Autores: {{ painel.referenciaSelecionada.autores || 'Sem autores' }}</p>
              <p class="text-sm text-gray-600">{{ painel.referenciaSelecionada.descricao || 'Sem descrição' }}</p>
              <div v-if="painel.anexosReferencia?.length" class="mt-3 text-sm">
                <p class="font-medium mb-1">Anexos:</p>
                <ul class="space-y-1">
                  <li v-for="(a, i) in painel.anexosReferencia" :key="`${i}-${a.caminho || ''}`">
                    <a
                      v-if="a.caminho"
                      :href="mediaUrl(a.caminho)"
                      target="_blank"
                      rel="noopener"
                      class="text-blue-600 hover:underline"
                    >
                      {{ a.descricao || a.tipo || 'Anexo' }}
                    </a>
                    <span v-else>{{ a.descricao || a.tipo || 'Anexo' }}</span>
                  </li>
                </ul>
              </div>
            </div>

            <div class="rounded-2xl border border-gray-200 bg-white p-4 mb-4">
              <div class="flex flex-wrap justify-between items-center gap-2 mb-3">
                <h3 class="font-semibold text-ds-text mb-0">Variáveis vinculadas</h3>
                <div class="flex gap-2 items-center">
                  <DsSelect v-model="importOrigem" input-class="min-w-[220px]">
                    <option value="">Importar de outra referência</option>
                    <option v-for="r in painel.outrasReferencias || []" :key="r.codigo" :value="String(r.codigo)">
                      #{{ r.codigo }} · {{ r.titulo }}
                    </option>
                  </DsSelect>
                  <DsButton variant="secondary" size="sm" icon="box-arrow-in-down" @click="importar">Importar</DsButton>
                </div>
              </div>
              <DsTable v-if="painel.variaveisVinculadas?.length">
                <template #head>
                  <tr>
                    <th>Variável</th>
                    <th>Total Normalidades</th>
                    <th>Comentário</th>
                    <th />
                  </tr>
                </template>
                <tr v-for="v in painel.variaveisVinculadas" :key="v.codVariavel">
                  <td>
                    <p class="font-semibold mb-0">{{ v.nomeVariavel }}</p>
                    <small class="text-gray-500">{{ v.variavel }} · {{ v.sigla || 's/sigla' }}</small>
                  </td>
                  <td>{{ v.totalNormalidades }}</td>
                  <td class="text-xs text-gray-600">{{ v.comentarioTexto || '—' }}</td>
                  <td>
                    <div class="flex justify-end gap-2">
                      <DsButton
                        size="sm"
                        variant="secondary"
                        icon="arrow-left-right"
                        @click="abrirConversaoUnidade(v.codVariavel, selectedRefId)"
                      >
                        Unidade
                      </DsButton>
                      <DsButton
                        size="sm"
                        variant="secondary"
                        icon="eye"
                        @click="abrirNormalidadesRef(v.codVariavel, v.nomeVariavel)"
                      >
                        Ver normalidades
                      </DsButton>
                    </div>
                  </td>
                </tr>
              </DsTable>
              <DsAlert v-else variant="info">Ainda não existem variáveis vinculadas a esta referência.</DsAlert>
            </div>

            <div class="rounded-2xl border border-gray-200 bg-white p-4">
              <div class="flex justify-between items-center mb-3">
                <h3 class="font-semibold text-ds-text mb-0">Vincular novas normalidades</h3>
                <span class="text-xs text-gray-500">
                  Mostrando {{ painel.normalidadesDisponiveis?.length || 0 }} de até {{ painel.limiteDisponiveis || 0 }}
                </span>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-12 gap-3 mb-4">
                <div class="md:col-span-9">
                  <DsInput
                    v-model="variavelBusca"
                    label="Filtrar normalidades disponíveis"
                    placeholder="Nome, variável ou sigla"
                    @enter="load()"
                  />
                </div>
                <div class="md:col-span-3 flex items-end justify-end">
                  <DsButton variant="secondary" size="sm" icon="search" @click="load()">Aplicar filtro</DsButton>
                </div>
              </div>
              <div class="max-h-[45vh] overflow-y-auto border border-gray-100 rounded-xl">
                <table class="w-full text-sm">
                  <thead class="bg-gray-50 sticky top-0">
                    <tr>
                      <th class="text-left px-3 py-2">Sel</th>
                      <th class="text-left px-3 py-2">Variável</th>
                      <th class="text-left px-3 py-2">Faixa</th>
                      <th class="text-left px-3 py-2">Página</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="n in painel.normalidadesDisponiveis || []"
                      :key="n.codNormalidade"
                      class="border-t border-gray-100"
                    >
                      <td class="px-3 py-2">
                        <input
                          type="checkbox"
                          class="rounded"
                          :checked="selectedNorms.has(n.codNormalidade)"
                          @change="toggleNorm(n.codNormalidade)"
                        />
                      </td>
                      <td class="px-3 py-2">
                        <p class="font-medium mb-0">{{ n.nomeVariavel }}</p>
                        <small class="text-gray-500">{{ n.variavel }} · {{ n.sigla || 's/sigla' }} · {{ n.sexo || '-' }}</small>
                      </td>
                      <td class="px-3 py-2">
                        {{ n.valorMin ?? '-' }} — {{ n.valorMax ?? '-' }}<br />
                        <small class="text-gray-500">Idade: {{ n.idadeMin ?? '-' }} a {{ n.idadeMax ?? '-' }}</small>
                      </td>
                      <td class="px-3 py-2 w-28">
                        <input
                          v-model="paginaByNorm[n.codNormalidade]"
                          type="number"
                          min="0"
                          class="w-full px-2 py-1 border border-gray-200 rounded-lg"
                          placeholder="Pg."
                        />
                      </td>
                    </tr>
                    <tr v-if="!(painel.normalidadesDisponiveis || []).length">
                      <td colspan="4" class="px-3 py-4 text-center text-gray-500">
                        Nenhuma normalidade disponível para vincular.
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div class="flex justify-end mt-3">
                <DsButton
                  icon="link-45deg"
                  :loading="saving"
                  :disabled="saving || !selectedNorms.size"
                  @click="vincularSelecionadas"
                >
                  Vincular selecionadas
                </DsButton>
              </div>
            </div>
          </template>
          <DsAlert v-else variant="info">Selecione uma referência para visualizar detalhes e vínculos.</DsAlert>
        </div>
      </div>
    </DsPageShell>

    <DsModal v-model="normalidadesModalOpen" :title="`Normalidades: ${modalVariavelNome}`" size="2xl">
      <div v-if="modalNormalidades.length" class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead class="bg-gray-50">
            <tr>
              <th class="text-left px-2 py-2">Estudo</th>
              <th class="text-left px-2 py-2">Ano</th>
              <th class="text-left px-2 py-2">Sexo</th>
              <th class="text-left px-2 py-2">Valor Min</th>
              <th class="text-left px-2 py-2">Valor Max</th>
              <th class="text-left px-2 py-2">Idade Min</th>
              <th class="text-left px-2 py-2">Idade Max</th>
              <th class="text-left px-2 py-2">Página</th>
              <th class="text-left px-2 py-2">Classificação</th>
              <th class="text-left px-2 py-2"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="n in modalNormalidades" :key="n.codNormalidade" class="border-t border-gray-100">
              <td class="px-2 py-2 max-w-[14rem]">
                <span class="text-xs">#{{ n.codReferencia }} · {{ n.tituloEstudo || '—' }}</span>
              </td>
              <td class="px-2 py-2">{{ n.ano ?? '—' }}</td>
              <td class="px-2 py-2">{{ n.sexo || '-' }}</td>
              <td class="px-2 py-2">
                <input v-model.number="n.valorMin" type="number" step="any" class="w-24 px-2 py-1 border rounded" />
              </td>
              <td class="px-2 py-2">
                <input v-model.number="n.valorMax" type="number" step="any" class="w-24 px-2 py-1 border rounded" />
              </td>
              <td class="px-2 py-2">
                <input v-model.number="n.idadeMin" type="number" class="w-20 px-2 py-1 border rounded" />
              </td>
              <td class="px-2 py-2">
                <input v-model.number="n.idadeMax" type="number" class="w-20 px-2 py-1 border rounded" />
              </td>
              <td class="px-2 py-2">
                <input v-model.number="n.pagina" type="number" class="w-20 px-2 py-1 border rounded" />
              </td>
              <td class="px-2 py-2 text-gray-500">{{ n.classificacao || '-' }}</td>
              <td class="px-2 py-2 whitespace-nowrap">
                <div class="flex gap-1">
                  <DsButton size="sm" variant="secondary" icon="check2" @click="salvarNormalidade(n)">Salvar</DsButton>
                  <DsButton size="sm" variant="danger" icon="x-circle" @click="desvincularNormalidade(n.codNormalidade)">
                    Desvincular
                  </DsButton>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="mt-4 space-y-3">
        <DsSelect v-if="modalEstudosOptions.length > 1" v-model="modalComentarioRefId" label="Estudo do comentário">
          <option v-for="e in modalEstudosOptions" :key="e.codReferencia" :value="String(e.codReferencia)">
            #{{ e.codReferencia }} · {{ e.ano || 's/ano' }} · {{ e.titulo }}
          </option>
        </DsSelect>

        <template v-if="modalComentarioPorIdade">
          <div
            v-for="(b, idx) in modalBandasIdade"
            :key="`${b.idadeMin}-${b.idadeMax}-${idx}`"
          >
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Comentário idade {{ b.idadeMin }}–{{ b.idadeMax }}
            </label>
            <input
              v-model="b.texto"
              type="text"
              maxlength="500"
              class="w-full px-3 py-2 border border-gray-200 rounded-lg"
              placeholder="Ex.: 54-111"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Comentário geral (sem faixa etária)</label>
            <input
              v-model="modalComentario"
              type="text"
              maxlength="500"
              class="w-full px-3 py-2 border border-gray-200 rounded-lg"
              placeholder="Ex.: 0.8 +/- 0.2"
            />
          </div>
          <p class="text-xs text-gray-500">
            Comentários por idade para o Studio no modo “somente comentário”. O geral vale quando não houver banda.
          </p>
        </template>
        <template v-else-if="modalComentarioPorSexo">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Comentário feminino (F)</label>
            <input
              v-model="modalComentarioF"
              type="text"
              maxlength="500"
              class="w-full px-3 py-2 border border-gray-200 rounded-lg"
              placeholder="Ex.: ≤ 36"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Comentário masculino (M)</label>
            <input
              v-model="modalComentarioM"
              type="text"
              maxlength="500"
              class="w-full px-3 py-2 border border-gray-200 rounded-lg"
              placeholder="Ex.: ≤ 40"
            />
          </div>
          <p class="text-xs text-gray-500">
            Comentários por sexo para o Studio no modo “somente comentário”.
          </p>
        </template>
        <div v-else>
          <label class="block text-sm font-medium text-gray-700 mb-1">Comentário de normalidade (texto livre)</label>
          <input
            v-model="modalComentario"
            type="text"
            maxlength="500"
            class="w-full px-3 py-2 border border-gray-200 rounded-lg"
            placeholder="Ex.: > 17"
          />
          <p class="text-xs text-gray-500 mt-1">
            Comentário único (ambos os sexos). Usado no Studio no modo “somente comentário”.
          </p>
        </div>
      </div>
      <DsAlert v-if="!modalNormalidades.length" variant="info">Nenhuma normalidade encontrada para esta variável.</DsAlert>
      <template #footer>
        <DsButton variant="secondary" @click="salvarComentarioModal">Salvar comentário</DsButton>
        <DsButton variant="secondary" @click="normalidadesModalOpen = false">Fechar</DsButton>
      </template>
    </DsModal>

    <DsModal v-model="convOpen" title="Converter unidade de normalidade" size="lg">
      <div class="space-y-3">
        <p class="text-sm text-gray-600">
          Converte faixas e comentários numéricos entre unidades equivalentes (mm↔cm, m/s↔cm/s).
          Faixas que já parecem estar no destino ficam desmarcadas por padrão.
        </p>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
          <DsSelect v-model="convDestino" label="Unidade destino">
            <option value="cm/s">cm/s</option>
            <option value="m/s">m/s</option>
            <option value="cm">cm</option>
            <option value="mm">mm</option>
          </DsSelect>
          <div class="flex items-end">
            <DsButton variant="secondary" size="sm" :loading="convLoading" @click="carregarPreviewConversao">
              Gerar preview
            </DsButton>
          </div>
        </div>
        <DsAlert v-if="convPreview" variant="info">
          {{ convPreview.unidadeOrigem }} → {{ convPreview.unidadeDestino }} (×{{ convPreview.fator }}).
          Incluir {{ convFaixaSel.size }} faixa(s) e {{ convComentSel.size }} comentário(s).
        </DsAlert>
        <div v-if="convPreview?.faixas?.length" class="overflow-x-auto max-h-56 border rounded-lg">
          <table class="w-full text-sm">
            <thead class="bg-gray-50 sticky top-0">
              <tr>
                <th class="px-2 py-1 text-left">Sel</th>
                <th class="px-2 py-1 text-left">Sexo</th>
                <th class="px-2 py-1 text-left">Idade</th>
                <th class="px-2 py-1 text-left">Atual</th>
                <th class="px-2 py-1 text-left">Novo</th>
                <th class="px-2 py-1 text-left">Classe</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="f in convPreview.faixas" :key="f.id" class="border-t">
                <td class="px-2 py-1">
                  <input type="checkbox" :checked="convFaixaSel.has(f.id)" @change="toggleConvFaixa(f.id)" />
                </td>
                <td class="px-2 py-1">{{ f.sexo || '—' }}</td>
                <td class="px-2 py-1">{{ f.idadeMin ?? '—' }}–{{ f.idadeMax ?? '—' }}</td>
                <td class="px-2 py-1">{{ f.valorMinAtual }}–{{ f.valorMaxAtual }}</td>
                <td class="px-2 py-1 font-medium">{{ f.valorMinNovo }}–{{ f.valorMaxNovo }}</td>
                <td class="px-2 py-1 text-xs">{{ f.classificacao }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="convPreview?.comentarios?.length" class="overflow-x-auto max-h-40 border rounded-lg">
          <table class="w-full text-sm">
            <thead class="bg-gray-50 sticky top-0">
              <tr>
                <th class="px-2 py-1 text-left">Sel</th>
                <th class="px-2 py-1 text-left">Atual</th>
                <th class="px-2 py-1 text-left">Novo</th>
                <th class="px-2 py-1 text-left">Classe</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="c in convPreview.comentarios" :key="c.id" class="border-t">
                <td class="px-2 py-1">
                  <input
                    type="checkbox"
                    :disabled="!c.convertivel"
                    :checked="convComentSel.has(c.id)"
                    @change="toggleConvComent(c.id)"
                  />
                </td>
                <td class="px-2 py-1">{{ c.textoAtual }}</td>
                <td class="px-2 py-1 font-medium">{{ c.convertivel ? c.textoNovo : '—' }}</td>
                <td class="px-2 py-1 text-xs">{{ c.classificacao }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <template #footer>
        <DsButton variant="secondary" :disabled="!convPreview || convApplying" :loading="convApplying" @click="aplicarConversao">
          Aplicar conversão
        </DsButton>
        <DsButton variant="secondary" @click="convOpen = false">Fechar</DsButton>
      </template>
    </DsModal>
  </div>
</template>

<script setup lang="ts">
import type {
  ConversaoUnidadePreview,
  NormalidadesPorVariavelPainel,
  ReferenciasNormalidadesPainel
} from '~/composables/useVariaveisApi'

definePageMeta({ layout: 'default' })

const variaveisApi = useVariaveisApi()
const swal = useSwal()

const tabs = [
  { id: 'variavel', label: 'Por variável', icon: 'rulers' },
  { id: 'referencia', label: 'Por referência', icon: 'journal-text' }
]
const modo = ref('variavel')

const loading = ref(false)
const loadingVar = ref(false)
const saving = ref(false)
const errorMsg = ref('')
const errorMsgVar = ref('')

const referenciaBusca = ref('')
const variavelBusca = ref('')
const variavelListaBusca = ref('')
const selectedRefId = ref<number | null>(null)
const selectedVarId = ref<number | null>(null)
const painel = ref<ReferenciasNormalidadesPainel | null>(null)
const painelVar = ref<NormalidadesPorVariavelPainel | null>(null)

const selectedNorms = ref<Set<number>>(new Set())
const paginaByNorm = reactive<Record<number, string>>({})
const importOrigem = ref('')

const convOpen = ref(false)
const convLoading = ref(false)
const convApplying = ref(false)
const convDestino = ref('cm/s')
const convCodVariavel = ref<number | null>(null)
const convCodReferencia = ref<number | null>(null)
const convPreview = ref<ConversaoUnidadePreview | null>(null)
const convFaixaSel = ref<Set<number>>(new Set())
const convComentSel = ref<Set<number>>(new Set())

const normalidadesModalOpen = ref(false)
const modalVariavelNome = ref('')
const modalCodVariavel = ref<number | null>(null)
const modalComentario = ref('')
const modalComentarioF = ref('')
const modalComentarioM = ref('')
const modalComentarioPorSexo = ref(false)
const modalComentarioPorIdade = ref(false)
const modalBandasIdade = ref<Array<{ idadeMin: number; idadeMax: number; texto: string }>>([])
const modalComentarioRefId = ref('')
const modalEstudosOptions = ref<
  Array<{
    codReferencia: number
    titulo: string
    ano?: number | null
    comentarioTexto?: string | null
    comentariosPorSexo?: Record<string, string> | null
    comentariosPorIdade?: Array<{
      sexo?: string
      idadeMin?: number
      idadeMax?: number
      texto?: string
    }> | null
  }>
>([])
const modalNormalidades = ref<
  Array<{
    codNormalidade: number
    codVariavel?: number | null
    codReferencia?: number | null
    tituloEstudo?: string | null
    ano?: number | null
    sexo?: string | null
    valorMin?: number | null
    valorMax?: number | null
    idadeMin?: number | null
    idadeMax?: number | null
    pagina?: number | null
    classificacao?: string | null
  }>
>([])

function sexoNorm(s?: string | null) {
  const v = (s || '').trim().toUpperCase()
  if (v.startsWith('F')) return 'F'
  if (v.startsWith('M')) return 'M'
  return 'A'
}

function bandasIdadeFromNorms(norms?: Array<{ idadeMin?: number | null; idadeMax?: number | null }>) {
  const map = new Map<string, { idadeMin: number; idadeMax: number }>()
  for (const n of norms || []) {
    const imin = n.idadeMin
    const imax = n.idadeMax
    if (imin == null || imax == null || imin < 0 || imax < 0) continue
    map.set(`${imin}-${imax}`, { idadeMin: imin, idadeMax: imax })
  }
  return Array.from(map.values()).sort((a, b) => a.idadeMin - b.idadeMin)
}

function estudoTemFaixasPorIdade(
  comentariosPorIdade?: Array<{ idadeMin?: number; idadeMax?: number }> | null,
  norms?: Array<{ idadeMin?: number | null; idadeMax?: number | null }>
) {
  if ((comentariosPorIdade || []).some((c) => (c.idadeMin ?? -1) >= 0 || (c.idadeMax ?? -1) >= 0)) return true
  return bandasIdadeFromNorms(norms).length > 0
}

function estudoTemFaixasPorSexo(
  comentariosPorSexo?: Record<string, string> | null,
  norms?: Array<{ sexo?: string | null }>
) {
  if (comentariosPorSexo?.F || comentariosPorSexo?.M) return true
  const sexos = new Set((norms || []).map((n) => sexoNorm(n.sexo)).filter((s) => s === 'F' || s === 'M'))
  return sexos.has('F') && sexos.has('M')
}

function carregarComentariosModal(
  estudo: {
    comentariosPorSexo?: Record<string, string> | null
    comentariosPorIdade?: Array<{
      sexo?: string
      idadeMin?: number
      idadeMax?: number
      texto?: string
    }> | null
    comentarioTexto?: string | null
  },
  norms: Array<{ sexo?: string | null; idadeMin?: number | null; idadeMax?: number | null }>
) {
  modalComentarioPorIdade.value = estudoTemFaixasPorIdade(estudo.comentariosPorIdade, norms)
  modalComentarioPorSexo.value = !modalComentarioPorIdade.value && estudoTemFaixasPorSexo(estudo.comentariosPorSexo, norms)
  const por = estudo.comentariosPorSexo || {}
  const porIdade = estudo.comentariosPorIdade || []

  if (modalComentarioPorIdade.value) {
    const bandas = bandasIdadeFromNorms(norms)
    const fromComments = porIdade
      .filter((c) => (c.idadeMin ?? -1) >= 0)
      .map((c) => ({ idadeMin: c.idadeMin!, idadeMax: c.idadeMax ?? c.idadeMin!, texto: c.texto || '' }))
    const merged = new Map<string, { idadeMin: number; idadeMax: number; texto: string }>()
    for (const b of bandas) merged.set(`${b.idadeMin}-${b.idadeMax}`, { ...b, texto: '' })
    for (const c of fromComments) {
      const key = `${c.idadeMin}-${c.idadeMax}`
      const prev = merged.get(key)
      merged.set(key, { idadeMin: c.idadeMin, idadeMax: c.idadeMax, texto: c.texto || prev?.texto || '' })
    }
    modalBandasIdade.value = Array.from(merged.values()).sort((a, b) => a.idadeMin - b.idadeMin)
    const geral = porIdade.find((c) => (c.idadeMin ?? -1) < 0 && (c.idadeMax ?? -1) < 0)
    modalComentario.value = geral?.texto || por.A || ''
    modalComentarioF.value = ''
    modalComentarioM.value = ''
  } else if (modalComentarioPorSexo.value) {
    modalBandasIdade.value = []
    modalComentarioF.value = por.F || ''
    modalComentarioM.value = por.M || ''
    modalComentario.value = ''
  } else {
    modalBandasIdade.value = []
    modalComentario.value = por.A || estudo.comentarioTexto || ''
    modalComentarioF.value = ''
    modalComentarioM.value = ''
  }
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await variaveisApi.getReferenciasNormalidades({
      referenciaId: selectedRefId.value ?? undefined,
      referenciaBusca: referenciaBusca.value || undefined,
      variavelBusca: variavelBusca.value || undefined,
      limite: 50
    })
    painel.value = res.data
    if (!selectedRefId.value && painel.value?.referenciaSelecionada?.codigo) {
      selectedRefId.value = painel.value.referenciaSelecionada.codigo
    }
    selectedNorms.value = new Set()
    Object.keys(paginaByNorm).forEach((k) => delete paginaByNorm[Number(k)])
  } catch (err) {
    errorMsg.value = err instanceof Error ? err.message : 'Erro ao carregar painel de referências.'
  } finally {
    loading.value = false
  }
}

async function loadPorVariavel() {
  loadingVar.value = true
  errorMsgVar.value = ''
  try {
    const res = await variaveisApi.getNormalidadesPorVariavel({
      variavelId: selectedVarId.value ?? undefined,
      busca: variavelListaBusca.value || undefined,
      limite: 200
    })
    painelVar.value = res.data
    if (painelVar.value?.variavelSelecionada?.codVariavel) {
      selectedVarId.value = painelVar.value.variavelSelecionada.codVariavel
    }
  } catch (err) {
    errorMsgVar.value = err instanceof Error ? err.message : 'Erro ao carregar medidas.'
  } finally {
    loadingVar.value = false
  }
}

function selectReferencia(codReferencia: number) {
  selectedRefId.value = codReferencia
  load()
}

function selectVariavel(codVariavel: number) {
  selectedVarId.value = codVariavel
  loadPorVariavel()
}

function toggleNorm(codNormalidade: number) {
  if (selectedNorms.value.has(codNormalidade)) selectedNorms.value.delete(codNormalidade)
  else selectedNorms.value.add(codNormalidade)
  selectedNorms.value = new Set(selectedNorms.value)
}

async function vincularSelecionadas() {
  if (!selectedRefId.value) return
  saving.value = true
  try {
    await variaveisApi.vincularNormalidadesReferencia({
      codReferencia: selectedRefId.value,
      normalidades: Array.from(selectedNorms.value).map((cod) => ({
        codNormalidade: cod,
        pagina: paginaByNorm[cod] ? Number(paginaByNorm[cod]) : null
      }))
    })
    await swal.toast('Normalidades vinculadas com sucesso.')
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao vincular normalidades', 'error')
  } finally {
    saving.value = false
  }
}

function abrirNormalidadesRef(codVariavel: number, nomeVariavel: string) {
  const regs = painel.value?.normalidadesPorVariavel?.[String(codVariavel)] || []
  const ref = painel.value?.referenciaSelecionada
  const vinc = painel.value?.variaveisVinculadas?.find((v) => v.codVariavel === codVariavel)
  const comentBag = painel.value?.comentariosPorVariavel?.[String(codVariavel)]
  const porSexo = vinc?.comentariosPorSexo || comentBag?.comentariosPorSexo || null
  const porIdade = vinc?.comentariosPorIdade || comentBag?.comentariosPorIdade || null
  modalNormalidades.value = regs.map((n) => ({
    ...n,
    codReferencia: ref?.codigo,
    tituloEstudo: ref?.titulo,
    ano: ref?.ano
  }))
  modalVariavelNome.value = nomeVariavel
  modalCodVariavel.value = codVariavel
  modalEstudosOptions.value = ref
    ? [{
        codReferencia: ref.codigo,
        titulo: ref.titulo,
        ano: ref.ano,
        comentarioTexto: vinc?.comentarioTexto || null,
        comentariosPorSexo: porSexo,
        comentariosPorIdade: porIdade
      }]
    : []
  modalComentarioRefId.value = ref ? String(ref.codigo) : ''
  carregarComentariosModal(modalEstudosOptions.value[0] || {}, regs)
  normalidadesModalOpen.value = true
}

function abrirModalPorVariavel() {
  const sel = painelVar.value?.variavelSelecionada
  if (!sel) return
  modalVariavelNome.value = sel.nome
  modalCodVariavel.value = sel.codVariavel
  modalEstudosOptions.value = (painelVar.value?.estudos || []).map((e) => ({
    codReferencia: e.codReferencia,
    titulo: e.titulo,
    ano: e.ano,
    comentarioTexto: e.comentarioTexto,
    comentariosPorSexo: e.comentariosPorSexo,
    comentariosPorIdade: e.comentariosPorIdade
  }))
  modalNormalidades.value = (painelVar.value?.estudos || []).flatMap((e) =>
    e.normalidades.map((n) => ({
      ...n,
      codReferencia: e.codReferencia,
      tituloEstudo: e.titulo,
      ano: e.ano
    }))
  )
  modalComentarioRefId.value = modalEstudosOptions.value[0] ? String(modalEstudosOptions.value[0].codReferencia) : ''
  const estudo0 = modalEstudosOptions.value[0]
  const norms0 = estudo0
    ? modalNormalidades.value.filter((n) => n.codReferencia === estudo0.codReferencia)
    : []
  carregarComentariosModal(estudo0 || {}, norms0)
  normalidadesModalOpen.value = true
}

function abrirModalEstudo(estudo: NormalidadesPorVariavelPainel['estudos'][number]) {
  const sel = painelVar.value?.variavelSelecionada
  if (!sel) return
  modalVariavelNome.value = sel.nome
  modalCodVariavel.value = sel.codVariavel
  modalEstudosOptions.value = [{
    codReferencia: estudo.codReferencia,
    titulo: estudo.titulo,
    ano: estudo.ano,
    comentarioTexto: estudo.comentarioTexto,
    comentariosPorSexo: estudo.comentariosPorSexo,
    comentariosPorIdade: estudo.comentariosPorIdade
  }]
  modalNormalidades.value = estudo.normalidades.map((n) => ({
    ...n,
    codReferencia: estudo.codReferencia,
    tituloEstudo: estudo.titulo,
    ano: estudo.ano
  }))
  modalComentarioRefId.value = String(estudo.codReferencia)
  carregarComentariosModal(estudo, estudo.normalidades)
  normalidadesModalOpen.value = true
}

watch(modalComentarioRefId, (id) => {
  const estudo = modalEstudosOptions.value.find((e) => String(e.codReferencia) === String(id))
  if (!estudo) return
  const norms = modalNormalidades.value.filter((n) => String(n.codReferencia) === String(id))
  carregarComentariosModal(estudo, norms)
})

watch(modo, (m) => {
  if (m === 'variavel') void loadPorVariavel()
  else void load()
})

async function salvarComentarioModal() {
  const codRef = Number(modalComentarioRefId.value) || selectedRefId.value
  const codVarEstudo =
    modalNormalidades.value.find((n) => n.codReferencia === codRef)?.codVariavel
    || modalCodVariavel.value
  if (!codRef || !codVarEstudo) {
    await swal.toast('Selecione o estudo do comentário.', 'warning')
    return
  }
  try {
    if (modalComentarioPorIdade.value) {
      for (const b of modalBandasIdade.value) {
        await variaveisApi.salvarComentarioNormalidade({
          codVariavel: codVarEstudo,
          codReferencia: codRef,
          texto: b.texto,
          sexo: 'A',
          idadeMin: b.idadeMin,
          idadeMax: b.idadeMax
        })
      }
      await variaveisApi.salvarComentarioNormalidade({
        codVariavel: codVarEstudo,
        codReferencia: codRef,
        texto: modalComentario.value,
        sexo: 'A',
        idadeMin: -1,
        idadeMax: -1
      })
    } else if (modalComentarioPorSexo.value) {
      await variaveisApi.salvarComentarioNormalidade({
        codVariavel: codVarEstudo,
        codReferencia: codRef,
        texto: modalComentarioF.value,
        sexo: 'F',
        idadeMin: -1,
        idadeMax: -1
      })
      await variaveisApi.salvarComentarioNormalidade({
        codVariavel: codVarEstudo,
        codReferencia: codRef,
        texto: modalComentarioM.value,
        sexo: 'M',
        idadeMin: -1,
        idadeMax: -1
      })
    } else {
      await variaveisApi.salvarComentarioNormalidade({
        codVariavel: codVarEstudo,
        codReferencia: codRef,
        texto: modalComentario.value,
        sexo: 'A',
        idadeMin: -1,
        idadeMax: -1
      })
    }
    await swal.toast('Comentário salvo.')
    if (modo.value === 'variavel') await loadPorVariavel()
    else await load()
    const opt = modalEstudosOptions.value.find((e) => e.codReferencia === codRef)
    if (opt) {
      if (modalComentarioPorIdade.value) {
        opt.comentariosPorIdade = [
          ...modalBandasIdade.value.map((b) => ({
            sexo: 'A',
            idadeMin: b.idadeMin,
            idadeMax: b.idadeMax,
            texto: b.texto
          })),
          ...(modalComentario.value
            ? [{ sexo: 'A', idadeMin: -1, idadeMax: -1, texto: modalComentario.value }]
            : [])
        ]
        opt.comentarioTexto = [
          ...modalBandasIdade.value.map((b) => `${b.idadeMin}-${b.idadeMax}: ${b.texto}`),
          modalComentario.value
        ].filter(Boolean).join('; ')
      } else if (modalComentarioPorSexo.value) {
        opt.comentariosPorSexo = {
          F: modalComentarioF.value,
          M: modalComentarioM.value
        }
        opt.comentarioTexto = [modalComentarioF.value && `F: ${modalComentarioF.value}`, modalComentarioM.value && `M: ${modalComentarioM.value}`]
          .filter(Boolean)
          .join('; ')
      } else {
        opt.comentariosPorSexo = { A: modalComentario.value }
        opt.comentarioTexto = modalComentario.value
      }
    }
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao salvar comentário', 'error')
  }
}

async function salvarNormalidade(n: {
  codNormalidade: number
  valorMin?: number | null
  valorMax?: number | null
  idadeMin?: number | null
  idadeMax?: number | null
  pagina?: number | null
}) {
  try {
    await variaveisApi.atualizarNormalidadeReferencia({
      codNormalidade: n.codNormalidade,
      valorMin: n.valorMin ?? null,
      valorMax: n.valorMax ?? null,
      idadeMin: n.idadeMin ?? null,
      idadeMax: n.idadeMax ?? null,
      pagina: n.pagina ?? null
    })
    await swal.toast('Normalidade atualizada.')
    if (modo.value === 'variavel') await loadPorVariavel()
    else await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao atualizar normalidade', 'error')
  }
}

async function desvincularNormalidade(codNormalidade: number) {
  const confirm = await swal.confirm('Desvincular normalidade', 'Deseja remover o vínculo desta normalidade com a referência?')
  if (!confirm?.isConfirmed) return
  try {
    const row = modalNormalidades.value.find((n) => n.codNormalidade === codNormalidade)
    await variaveisApi.desvincularNormalidadeReferencia({
      codNormalidade,
      codReferencia: row?.codReferencia || selectedRefId.value || undefined
    })
    await swal.toast('Normalidade desvinculada.')
    modalNormalidades.value = modalNormalidades.value.filter((n) => n.codNormalidade !== codNormalidade)
    if (modo.value === 'variavel') await loadPorVariavel()
    else await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao desvincular normalidade', 'error')
  }
}

async function importar() {
  if (!selectedRefId.value || !importOrigem.value) {
    await swal.toast('Selecione a referência de origem para importação.', 'warning')
    return
  }
  const confirm = await swal.confirm('Importar normalidades', 'Deseja importar normalidades da referência selecionada?')
  if (!confirm?.isConfirmed) return
  try {
    const res = await variaveisApi.importarNormalidadesReferencia({
      codReferenciaDestino: selectedRefId.value,
      codReferenciaOrigem: Number(importOrigem.value)
    })
    await swal.toast(res.data.message || 'Importação concluída.')
    importOrigem.value = ''
    await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao importar normalidades', 'error')
  }
}

function abrirConversaoUnidade(codVariavel?: number | null, codReferencia?: number | null) {
  const codVar = codVariavel ?? selectedVarId.value ?? modalCodVariavel.value
  if (!codVar) {
    void swal.toast('Selecione uma variável.', 'warning')
    return
  }
  convCodVariavel.value = codVar
  convCodReferencia.value = codReferencia ?? selectedRefId.value
  convDestino.value = 'cm/s'
  convPreview.value = null
  convFaixaSel.value = new Set()
  convComentSel.value = new Set()
  convOpen.value = true
  void carregarPreviewConversao()
}

async function carregarPreviewConversao() {
  if (!convCodVariavel.value) return
  convLoading.value = true
  try {
    const res = await variaveisApi.previewConversaoUnidade({
      codVariavel: convCodVariavel.value,
      codReferencia: convCodReferencia.value || undefined,
      unidadeDestino: convDestino.value
    })
    convPreview.value = res.data
    convFaixaSel.value = new Set(res.data.faixas.filter((f) => f.incluirDefault).map((f) => f.id))
    convComentSel.value = new Set(res.data.comentarios.filter((c) => c.incluirDefault).map((c) => c.id))
  } catch (err) {
    convPreview.value = null
    await swal.toast(err instanceof Error ? err.message : 'Erro no preview de conversão', 'error')
  } finally {
    convLoading.value = false
  }
}

function toggleConvFaixa(id: number) {
  const next = new Set(convFaixaSel.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  convFaixaSel.value = next
}

function toggleConvComent(id: number) {
  const next = new Set(convComentSel.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  convComentSel.value = next
}

async function aplicarConversao() {
  if (!convPreview.value || !convCodVariavel.value) return
  const confirm = await swal.confirm(
    'Aplicar conversão',
    `Converter ${convFaixaSel.value.size} faixa(s) e ${convComentSel.value.size} comentário(s) de ${convPreview.value.unidadeOrigem} para ${convPreview.value.unidadeDestino}?`
  )
  if (!confirm?.isConfirmed) return
  convApplying.value = true
  try {
    const res = await variaveisApi.applyConversaoUnidade({
      preview: {
        codVariavel: convCodVariavel.value,
        codReferencia: convCodReferencia.value || undefined,
        unidadeDestino: convDestino.value
      },
      faixaIds: Array.from(convFaixaSel.value),
      comentarioIds: Array.from(convComentSel.value),
      atualizarUnidadeVariavel: true
    })
    await swal.toast(res.data.message || 'Conversão aplicada.')
    convOpen.value = false
    if (modo.value === 'variavel') await loadPorVariavel()
    else await load()
  } catch (err) {
    await swal.toast(err instanceof Error ? err.message : 'Erro ao aplicar conversão', 'error')
  } finally {
    convApplying.value = false
  }
}

onMounted(() => {
  void loadPorVariavel()
})
</script>
