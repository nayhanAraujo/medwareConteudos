<template>
 <DsPageShell>
  <h2 class="text-lg font-semibold">Regras técnicas · versão #{{ version }}</h2>
  <p class="text-sm text-gray-500">Orientações e checklist, sem preços nem horas-base. Alterações publicam outra versão e não mudam análises anteriores. Exemplos iniciais ficam inativos até revisão da gestão.</p>
  <DsAlert v-if="error" variant="error" class="my-3">{{ error }}</DsAlert><DsAlert v-if="message" variant="success" class="my-3">{{ message }}</DsAlert>
  <form v-if="catalog" class="space-y-5 mt-4" @submit.prevent="publish">
   <fieldset :disabled="busy" class="space-y-5">
    <div v-for="group in groups" :key="group.key" class="space-y-3"><h3 class="font-semibold">{{ group.label }}</h3>
     <div v-for="entry in catalog[group.key]" :key="entry.id" class="flex flex-wrap items-center gap-3 border-b pb-3"><DsInput :id="`technical-${group.key}-${entry.id}`" v-model="entry.name" label="Nome" :maxlength="200" /><label class="text-sm flex gap-2"><input v-model="entry.active" type="checkbox">Ativo</label><code class="text-xs text-gray-500 break-all">{{ entry.id }}</code></div>
     <DsButton size="sm" variant="secondary" @click="addEntry(group.key)">Adicionar {{ group.singular }}</DsButton>
    </div>
    <h3 class="font-semibold">Regras de análise</h3>
    <article v-for="(rule,index) in catalog.rules" :key="rule.id" class="border border-gray-200 rounded-xl p-4 space-y-3">
     <div class="flex flex-wrap gap-3 justify-between items-center"><strong>Regra {{ index + 1 }}</strong><label class="text-sm flex gap-2"><input v-model="rule.active" type="checkbox">Ativa na próxima versão</label></div>
     <DsInput :id="`technical-rule-${rule.id}`" v-model="rule.name" label="Nome da regra" :maxlength="200" />
     <div class="grid md:grid-cols-3 gap-3">
      <DsSelect :id="`technical-category-${rule.id}`" :model-value="rule.category || ''" label="Categoria" @update:model-value="rule.category=$event || null"><option value="">Todas</option><option v-for="(label,key) in quoteCategories" :key="key" :value="key">{{ label }}</option></DsSelect>
      <DsSelect :id="`technical-language-${rule.id}`" :model-value="rule.technologyId ?? ''" label="Tecnologia" @update:model-value="rule.technologyId=$event ? Number($event) : null"><option value="">Todas</option><option v-for="t in technologies" :key="t.id" :value="t.id">{{ t.name }}</option></DsSelect>
      <DsSelect :id="`technical-profile-${rule.id}`" :model-value="rule.profileId || ''" label="Perfil de laudo" @update:model-value="rule.profileId=$event || null"><option value="">Todos</option><option v-for="p in catalog.profiles" :key="p.id" :value="p.id">{{ p.name }}{{ p.active?'':' (inativo)' }}</option></DsSelect>
     </div>
     <div class="space-y-2"><p class="text-sm font-medium">Serviços aplicáveis (nenhum selecionado = todos)</p><label v-for="service in catalog.services" :key="service.id" class="text-sm flex gap-2"><input v-model="rule.serviceIds" type="checkbox" :value="service.id">{{ service.name }}</label></div>
     <label class="text-sm block">Orientações técnicas<textarea v-model="rule.instructions" rows="4" maxlength="4000" required class="technical-area" /></label>
     <h4 class="text-sm font-semibold">Verificações do checklist</h4>
     <div v-for="check in rule.checks" :key="check.id" class="border-l-2 pl-3 space-y-2"><DsInput :id="`technical-check-${rule.id}-${check.id}`" v-model="check.label" label="Verificação" :maxlength="500" /><label class="text-sm flex gap-2"><input v-model="check.required" type="checkbox">Obrigatória para aprovação</label><code class="text-xs text-gray-500">{{ check.id }}</code></div>
     <DsButton size="sm" variant="secondary" @click="addCheck(rule)">Adicionar verificação</DsButton>
     <p class="text-xs text-gray-500">Códigos são preservados para rastreabilidade. Para retirar verificações já publicadas, inative a regra e crie outra.</p>
    </article>
    <DsButton variant="secondary" @click="addRule">Adicionar regra técnica</DsButton>
    <DsInput id="technical-publish-reason" v-model="reason" label="Motivo da publicação" :maxlength="1000" required />
    <DsButton type="submit" :loading="busy">Publicar nova versão das regras</DsButton>
   </fieldset>
  </form>
 </DsPageShell>
</template>
<script setup lang="ts">
import {quoteCategories} from '~/utils/orcamentos'
import type {TechnicalCatalog,Technology,TechnicalRule} from '~/utils/orcamentoTechnical'
const api=useApi(),catalog=ref<TechnicalCatalog|null>(null),technologies=ref<Technology[]>([]),version=ref(''),reason=ref(''),busy=ref(false),error=ref(''),message=ref('')
const groups=[{key:'profiles' as const,label:'Perfis de laudo',singular:'perfil'},{key:'services' as const,label:'Serviços solicitados',singular:'serviço'}]
let alive=true
async function load(){const r=await api.get<{data:{version:string;catalog:TechnicalCatalog;technologies:Technology[]}}>('/api/web/orcamentos/regras-tecnicas');if(alive){catalog.value=r.data.catalog;version.value=r.data.version;technologies.value=r.data.technologies}}
function addRule(){catalog.value!.rules.push({id:crypto.randomUUID(),name:'',active:false,category:'laudos',technologyId:null,profileId:null,serviceIds:[],instructions:'',checks:[]})}
function addEntry(key:'profiles'|'services'){catalog.value![key].push({id:crypto.randomUUID(),name:'',active:true})}
function addCheck(rule:TechnicalRule){rule.checks.push({id:crypto.randomUUID(),label:'',required:true})}
async function publish(){if(!catalog.value||busy.value)return;busy.value=true;error.value='';message.value='';try{await api.put('/api/web/orcamentos/regras-tecnicas',{version:version.value,reason:reason.value,catalog:catalog.value});await load();if(alive){message.value='Versão publicada; análises existentes permanecem preservadas.';reason.value=''}}catch(e){if(alive)error.value=e instanceof Error?e.message:'Falha ao publicar'}finally{if(alive)busy.value=false}}
onMounted(()=>{void load().catch(e=>{if(alive)error.value=e instanceof Error?e.message:'Falha ao carregar regras'})});onBeforeUnmount(()=>{alive=false;catalog.value=null})
</script>
<style scoped>.technical-area{display:block;width:100%;padding:.75rem;margin-top:.4rem;border:1px solid #d1d5db;border-radius:12px;background:white;}.technical-area:focus-visible{outline:2px solid #2563eb;outline-offset:2px;}</style>
