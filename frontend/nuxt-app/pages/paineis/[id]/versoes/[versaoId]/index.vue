<template>
  <div>
    <DsPageHeader :title="`Versão ${detail?.versao?.numeroVersao || ''}`" :subtitle="detail?.versao?.nomePainel || 'Detalhes da versão'" icon="layers" />
    <DsPageShell>
      <div class="flex flex-wrap gap-2 mb-6">
        <DsButton variant="secondary" size="sm" icon="arrow-left" :to="`/paineis/${painelId}/versoes`">Versões</DsButton>
        <DsButton v-if="auth.isAdmin" size="sm" icon="pencil" :to="`/paineis/${painelId}/versoes/${versaoId}/editar`">Editar</DsButton>
        <DsButton v-if="auth.isAdmin" size="sm" variant="danger" icon="trash" @click="remove">Excluir</DsButton>
      </div>
      <div v-if="loading" class="py-10 text-center text-gray-500">Carregando...</div><DsAlert v-else-if="error" variant="error">{{ error }}</DsAlert>
      <div v-else-if="detail" class="space-y-8">
        <div class="grid md:grid-cols-3 gap-4 text-sm"><p><b>Tipo:</b> {{ detail.versao.tipoPainel }}</p><p><b>Publicado:</b> {{ detail.versao.publicado ? 'Sim':'Não' }}</p><p><b>Criação:</b> {{ formatDate(detail.versao.dataCriacao) }}</p><p class="md:col-span-3"><b>Observações:</b> {{ detail.versao.observacoes || '—' }}</p></div>
        <section><DsSectionTitle title="Configuração" /><div class="grid md:grid-cols-2 gap-3 text-sm"><p v-for="(value,key) in configFields" :key="key"><b>{{ label(key) }}:</b> {{ value || '—' }}</p></div><DsButton v-if="detail.configuracao?.temJson" class="mt-3" size="sm" variant="secondary" icon="download" @click="download('JSON','configuracao.json')">JSON</DsButton></section>
        <section><DsSectionTitle title="Arquivos" /><DsAlert v-if="!detail.arquivos.length" variant="info">Nenhum documento.</DsAlert><div v-else class="flex flex-wrap gap-2"><DsButton v-for="a in detail.arquivos" :key="a.codArquivoVersao" size="sm" variant="secondary" icon="download" @click="download(a.tipoArquivo,a.nomeArquivo)">{{ a.tipoArquivo }} — {{ a.nomeArquivo }}</DsButton></div></section>
        <section><DsSectionTitle title="Imagens" /><DsAlert v-if="!detail.imagens.length" variant="info">Nenhuma imagem.</DsAlert><div class="grid md:grid-cols-3 gap-4"><div v-for="img in detail.imagens" :key="img.codImagemPainel" class="border rounded-2xl p-3"><p class="text-sm truncate mb-2">{{ img.nomeArquivo }}</p><div class="flex gap-2"><DsButton size="sm" variant="secondary" @click="download('IMAGEM',img.nomeArquivo,img.codImagemPainel)">Baixar</DsButton><DsButton v-if="auth.isAdmin" size="sm" variant="danger" @click="removeImage(img.codImagemPainel)">Excluir</DsButton></div></div></div></section>
        <DataSection title="Métricas" :items="detail.metricas" :admin="auth.isAdmin" @add="addMetric" />
        <DataSection title="Dimensões" :items="detail.dimensoes" :admin="auth.isAdmin" @add="addDimension" />
        <DataSection title="Fontes de dados" :items="detail.fontes" :admin="auth.isAdmin" @add="addSource" />
      </div>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, h } from 'vue'; import type { VersaoDetalhe } from '~/composables/usePaineisVersoesApi'
definePageMeta({layout:'default'})
const DataSection=defineComponent({
  props:{title:String,items:{type:Array,default:()=>[]},admin:Boolean}, emits:['add'],
  setup(p,{emit}) { return () => h('section',[
    h('div',{class:'flex items-center justify-between mb-3'},[
      h('h2',{class:'text-lg font-semibold'},p.title),
      p.admin ? h('button',{class:'text-sm rounded-full bg-black text-white px-4 py-2',onClick:()=>{const value=prompt(`Nome/origem para ${p.title}`);if(value)emit('add',value)}},'Adicionar') : null
    ]),
    p.items.length
      ? h('div',{class:'grid md:grid-cols-2 gap-3'},p.items.map((x:any)=>
          h('div',{class:'border rounded-2xl p-4 text-sm'},Object.entries(x)
            .filter(([k])=>!k.toLowerCase().startsWith('cod'))
            .map(([k,v])=>h('p',[h('b',`${k}: `),String(v??'—')])))))
      : h('p',{class:'text-sm text-gray-500'},'Nenhum registro.')
  ]) }
})
const route=useRoute(), api=usePaineisVersoesApi(), auth=useAuthStore(), swal=useSwal(); const painelId=computed(()=>Number(route.params.id)), versaoId=computed(()=>Number(route.params.versaoId)); const detail=ref<VersaoDetalhe|null>(null), loading=ref(true), error=ref('')
const configFields=computed(()=>Object.fromEntries(Object.entries(detail.value?.configuracao||{}).filter(([k])=>!['codConfiguracao','temJson','observacoes'].includes(k)))); const label=(s:string)=>s.replace(/([A-Z])/g,' $1').replace(/^./,x=>x.toUpperCase()); const formatDate=(v?:string)=>v?new Date(v).toLocaleString('pt-BR'):'—'
async function load(){loading.value=true;try{detail.value=(await api.get(painelId.value,versaoId.value)).data}catch(e){error.value=e instanceof Error?e.message:'Erro ao carregar.'}finally{loading.value=false}}
async function download(tipo:string,nome:string,img?:number){try{await api.download(painelId.value,versaoId.value,tipo,nome,img)}catch(e){await swal.toast(e instanceof Error?e.message:'Erro no download','error')}}
async function remove(){const c=await swal.confirm('Excluir versão?','Todos os arquivos e dados vinculados serão excluídos.');if(!c.isConfirmed)return;await api.remove(painelId.value,versaoId.value);await navigateTo(`/paineis/${painelId.value}/versoes`)}
async function removeImage(id:number){const c=await swal.confirm('Excluir imagem?');if(!c.isConfirmed)return;await api.removeImage(painelId.value,versaoId.value,id);await load()}
async function addMetric(nome:string){await api.addMetric(painelId.value,versaoId.value,{nome,unidadeMedida:'',conceito:'',medidaDax:''});await load()}
async function addDimension(nome:string){await api.addDimension(painelId.value,versaoId.value,{nome,descricao:'',dominioHierarquia:''});await load()}
async function addSource(origem:string){await api.addSource(painelId.value,versaoId.value,{origem,metodoExtracao:'',arquivo:'',periodoAtualizacao:'',responsavel:''});await load()}
onMounted(load)
</script>
