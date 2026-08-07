<template>
  <div>
    <div v-if="loading" class="py-10 text-center text-gray-500">Carregando vínculos...</div>
    <DsAlert v-else-if="error" variant="error">{{ error }}</DsAlert>
    <template v-else-if="detail">
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-gray-50 p-4">
        <div><div class="text-xs uppercase tracking-wide text-gray-500">{{ domainLabel(detail.dominio) }} #{{ detail.id }}</div><div class="font-semibold">{{ detail.nome }}</div></div>
        <div class="flex items-center gap-2"><DsBadge :variant="active ? 'success' : 'neutral'">{{ active ? 'Ativo' : 'Inativo' }}</DsBadge><DsBadge variant="neutral">{{ total }} vínculos</DsBadge></div>
      </div>
      <div v-if="detail.relacoes.length" class="flex flex-wrap gap-2 border-b border-gray-200 pb-3">
        <button v-for="group in detail.relacoes" :key="group.relacao" type="button" class="rounded-full px-4 py-2 text-sm" :class="current?.relacao === group.relacao ? 'bg-black text-white' : 'bg-gray-100 text-gray-700'" @click="select(group)">
          {{ group.titulo }} <span class="ml-1 opacity-70">{{ group.itens.length }}</span>
        </button>
      </div>
      <div v-if="current" class="mt-4">
        <div class="mb-3 flex flex-wrap items-end justify-between gap-2">
          <DsSearchInput v-model="linkSearch" wrapper-class="mb-0" class="min-w-64 flex-1" placeholder="Pesquisar nos vínculos" />
          <DsButton v-if="auth.isAdmin && current.editavel" size="sm" :variant="editing ? 'secondary' : 'primary'" icon="pencil" @click="beginEdit">{{ editing ? 'Cancelar edição' : 'Gerenciar' }}</DsButton>
        </div>
        <div v-if="editing" class="mb-4 rounded-2xl border border-gray-200 p-4">
          <AssistenteMultiSelect v-model="selected" :label="`Selecionar ${current.titulo.toLowerCase()}`" :options="available" />
          <DsAlert v-if="current.obrigatorio" variant="info" class="mt-3">Este vínculo é obrigatório e não pode ficar vazio.</DsAlert>
          <div v-if="current.ordenavel && selected.length" class="mt-3 space-y-2">
            <div v-for="(itemId, index) in selected" :key="itemId" class="flex items-center justify-between rounded-xl bg-gray-50 px-3 py-2 text-sm">
              <span>{{ optionName(itemId) }}</span><div class="flex gap-1"><DsButton size="sm" variant="secondary" :disabled="index === 0" @click="move(index,-1)">↑</DsButton><DsButton size="sm" variant="secondary" :disabled="index === selected.length-1" @click="move(index,1)">↓</DsButton></div>
            </div>
          </div>
          <div class="mt-4 flex justify-end"><DsButton :loading="saving" @click="save">Salvar vínculos</DsButton></div>
        </div>
        <div v-if="!filtered.length" class="rounded-2xl border border-dashed border-gray-300 py-8 text-center text-sm text-gray-500">Nenhum vínculo encontrado.</div>
        <div v-else class="divide-y divide-gray-100 rounded-2xl border border-gray-200">
          <div v-for="item in filtered" :key="item.id" class="flex items-center justify-between gap-3 px-4 py-3">
            <div><div class="font-medium">{{ item.nome }}</div><div class="text-xs text-gray-500">#{{ item.id }}<span v-if="item.sequencia !== null && item.sequencia !== undefined"> · sequência {{ item.sequencia }}</span></div></div>
            <DsButton size="sm" variant="secondary" :to="{ path: '/assistente/vinculos', query: { domain: current.dominioDestino, id: item.id } }">Ver cadastro</DsButton>
          </div>
        </div>
      </div>
      <DsEmptyState v-else title="Sem relações configuradas" description="Este domínio não possui vínculos gerenciáveis." />
    </template>
  </div>
</template>

<script setup lang="ts">
import type { AssistenteLinkGroup, AssistenteLinksDetail, AssistenteOption } from '~/composables/useAssistenteApi'
const props = defineProps<{ domain: string; id: number }>()
const emit = defineEmits<{ updated: [] }>()
const api = useAssistenteApi(); const auth = useAuthStore(); const swal = useSwal()
const detail = ref<AssistenteLinksDetail>(); const current = ref<AssistenteLinkGroup>(); const loading = ref(false); const saving = ref(false); const error = ref(''); const linkSearch = ref(''); const editing = ref(false); const selected = ref<number[]>([]); const available = ref<AssistenteOption[]>([])
const total = computed(() => detail.value?.relacoes.reduce((sum, item) => sum + item.itens.length, 0) || 0)
const active = computed(() => detail.value?.status === undefined || detail.value?.status === null || Number(detail.value.status) === -1)
const filtered = computed(() => { const q=linkSearch.value.trim().toLocaleLowerCase('pt-BR'); return !q ? current.value?.itens || [] : (current.value?.itens || []).filter(x => x.nome.toLocaleLowerCase('pt-BR').includes(q) || String(x.id).includes(q)) })
function domainLabel(value:string){return value.replace('paginas-fotos','Modelos MRD').replace('grupos-operadoras','Grupos de operadoras').replace('esquemas-fotos','Esquemas de fotos').replace(/(^|-)(\w)/g,(_,s,c)=>`${s?' ':''}${c.toUpperCase()}`)}
function select(group: AssistenteLinkGroup){current.value=group; editing.value=false; linkSearch.value=''}
function optionName(id:number){return available.value.find(x=>x.id===id)?.nome || `#${id}`}
function move(index:number,delta:number){const target=index+delta; if(target<0||target>=selected.value.length)return; [selected.value[index],selected.value[target]]=[selected.value[target],selected.value[index]]}
async function load(){loading.value=true;error.value='';try{detail.value=await api.links(props.domain,props.id);const previous=current.value?.relacao;current.value=detail.value.relacoes.find(x=>x.relacao===previous)||detail.value.relacoes[0]}catch(reason){error.value=reason instanceof Error?reason.message:'Não foi possível consultar os vínculos.'}finally{loading.value=false}}
async function beginEdit(){if(editing.value){editing.value=false;return}if(!current.value)return;available.value=await api.options(current.value.dominioDestino);selected.value=current.value.itens.map(x=>x.id);editing.value=true}
async function save(){if(!current.value)return;if(current.value.obrigatorio&&!selected.value.length){await swal.toast('Selecione ao menos um registro.','warning');return}if(current.value.selecaoUnica&&selected.value.length>1){await swal.toast('Este vínculo aceita apenas um registro.','warning');return}if(current.value.itens.some(item=>!selected.value.includes(item.id))){const confirmation=await swal.confirm('Remover vínculos','Os registros desmarcados serão desvinculados. Deseja continuar?');if(!confirmation?.isConfirmed)return}saving.value=true;try{await api.setLinks(props.domain,props.id,current.value.relacao,selected.value,current.value.ordenavel?selected.value.map((id,index)=>({id,sequencia:index})):undefined);editing.value=false;await load();emit('updated');await swal.toast('Vínculos atualizados.')}catch(reason){await swal.toast(reason instanceof Error?reason.message:'Não foi possível atualizar.','error')}finally{saving.value=false}}
watch(()=>[props.domain,props.id],load,{immediate:true})
</script>
