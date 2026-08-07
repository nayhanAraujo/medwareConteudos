<template>
  <div>
    <DsPageHeader title="Vínculos do Assistente" subtitle="Consulte e gerencie as relações entre os cadastros" icon="diagram-3-fill">
      <template #actions><DsButton variant="secondary" to="/assistente">Voltar</DsButton></template>
    </DsPageHeader>
    <DsPageShell>
      <AssistenteNav />
      <div class="grid gap-5 lg:grid-cols-[minmax(300px,38%)_1fr]">
        <section class="rounded-2xl border border-gray-200 bg-white p-4">
          <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-1 xl:grid-cols-2">
            <DsSelect v-model="domain" label="Domínio" @update:model-value="changeDomain">
              <option v-for="item in domains" :key="item.value" :value="item.value">{{ item.label }}</option>
            </DsSelect>
            <DsSelect v-model="filter" label="Filtro" @update:model-value="load(1)">
              <option value="todos">Todos</option><option value="com">Com vínculos</option><option value="sem">Sem vínculos</option>
            </DsSelect>
          </div>
          <DsSearchInput v-model="search" wrapper-class="my-4" placeholder="Pesquisar por nome ou código" @enter="load(1)" />
          <DsAlert v-if="error" variant="error" class="mb-3">{{ error }}</DsAlert>
          <div v-if="loading" class="py-10 text-center text-gray-500">Carregando...</div>
          <div v-else-if="!items.length" class="py-10 text-center text-sm text-gray-500">Nenhum registro encontrado.</div>
          <div v-else class="max-h-[58vh] space-y-2 overflow-y-auto pr-1">
            <button v-for="item in items" :key="item.id" type="button" class="flex w-full items-center justify-between gap-3 rounded-xl border p-3 text-left transition" :class="selectedId===item.id?'border-black bg-gray-50':'border-gray-200 hover:border-gray-400'" @click="selectItem(item.id)">
              <span><strong class="block text-sm">{{ item.nome }}</strong><small class="text-gray-500">#{{ item.id }}</small></span>
              <DsBadge variant="neutral">{{ item.totalVinculos }}</DsBadge>
            </button>
          </div>
          <div v-if="total>pageSize" class="mt-4 flex items-center justify-between text-sm"><DsButton size="sm" variant="secondary" :disabled="page<=1" @click="load(page-1)">Anterior</DsButton><span>{{ page }}/{{ pages }}</span><DsButton size="sm" variant="secondary" :disabled="page>=pages" @click="load(page+1)">Próxima</DsButton></div>
        </section>
        <section class="min-w-0 rounded-2xl border border-gray-200 bg-white p-4">
          <AssistenteVinculosPanel v-if="selectedId" :key="`${domain}-${selectedId}`" :domain="domain" :id="selectedId" @updated="load(page)" />
          <DsEmptyState v-else title="Selecione um registro" description="Os vínculos aparecerão aqui, organizados por tipo de relação." />
        </section>
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import type { AssistenteLinksSummaryItem } from '~/composables/useAssistenteApi'
definePageMeta({layout:'default'})
const route=useRoute();const router=useRouter();const api=useAssistenteApi()
const domains=[
  {value:'procedimentos',label:'Procedimentos'},{value:'scripts',label:'Scripts'},{value:'paginas-fotos',label:'Modelos MRD'},
  {value:'frases',label:'Frases'},{value:'grupos',label:'Grupos'},{value:'especialidades',label:'Especialidades'},
  {value:'referencias',label:'Referências'},{value:'esquemas',label:'Esquemas'},{value:'esquemas-fotos',label:'Esquemas de fotos'},
  {value:'operadoras',label:'Operadoras'},{value:'grupos-operadoras',label:'Grupos de operadoras'}
]
const initialDomain=typeof route.query.domain==='string'&&domains.some(x=>x.value===route.query.domain)?route.query.domain:'procedimentos'
const domain=ref(initialDomain);const filter=ref('todos');const search=ref('');const items=ref<AssistenteLinksSummaryItem[]>([]);const selectedId=ref(Number(route.query.id)||0);const page=ref(1);const pageSize=20;const total=ref(0);const loading=ref(false);const error=ref('');let timer:ReturnType<typeof setTimeout>|undefined
const pages=computed(()=>Math.max(1,Math.ceil(total.value/pageSize)))
async function load(target=page.value){loading.value=true;error.value='';try{const result=await api.linksSummary(domain.value,search.value,filter.value,target,pageSize);items.value=result.items||[];total.value=result.total||0;page.value=result.page||target;if(selectedId.value&&!items.value.some(x=>x.id===selectedId.value)&&!route.query.id)selectedId.value=0}catch(reason){error.value=reason instanceof Error?reason.message:'Não foi possível pesquisar os vínculos.';items.value=[]}finally{loading.value=false}}
function selectItem(id:number){selectedId.value=id;router.replace({query:{domain:domain.value,id:String(id)}})}
function changeDomain(){selectedId.value=0;page.value=1;router.replace({query:{domain:domain.value}});load(1)}
watch(search,()=>{clearTimeout(timer);timer=setTimeout(()=>load(1),350)})
onMounted(()=>load(1));onUnmounted(()=>clearTimeout(timer))
</script>
