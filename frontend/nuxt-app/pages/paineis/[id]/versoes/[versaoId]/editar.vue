<template>
  <div><DsPageHeader title="Editar versão" subtitle="Atualize configuração e adicione novos arquivos" icon="pencil-square" /><DsPageShell>
    <form class="space-y-6" @submit.prevent="save">
      <div class="grid md:grid-cols-2 gap-4"><DsInput v-model="form.numeroVersao" label="Número da versão" required /><label class="flex items-center gap-2 pt-8"><input v-model="form.publicado" type="checkbox"> Publicado</label></div>
      <DsTextarea v-model="form.observacoes" label="Observações" />
      <DsSectionTitle :title="tipo==='POWERBI'?'Configuração Power BI':'Configuração API'" />
      <div v-if="tipo==='POWERBI'" class="grid md:grid-cols-2 gap-4"><DsInput v-model="form.diretorioPbix" label="Diretório PBIX"/><DsInput v-model="form.nomeArquivoPbix" label="Nome do PBIX"/><DsInput v-model="form.workspacePowerbi" label="Workspace"/><DsInput v-model="form.datasetPowerbi" label="Dataset"/><DsInput v-model="form.gatewayPowerbi" label="Gateway"/><DsInput v-model="form.frequenciaAtualizacao" label="Frequência"/><DsInput v-model="form.responsavelAtualizacao" label="Responsável"/><DsInput v-model="form.publicLink" label="Link público"/><DsTextarea v-model="form.iframeCode" label="Código iframe" class="md:col-span-2"/></div>
      <div v-else class="grid md:grid-cols-2 gap-4"><DsInput v-model="form.enderecoApi" label="Endereço da API"/><DsInput v-model="form.responsavelApi" label="Responsável"/></div>
      <DsSectionTitle title="Substituir/adicionar arquivos" />
      <div class="grid md:grid-cols-4 gap-4 text-sm"><label>JSON<input class="block mt-2 w-full" type="file" accept=".json" @change="pick($event,'json')"></label><label>Manual<input class="block mt-2 w-full" type="file" accept=".docx" @change="pick($event,'manual')"></label><label>Insights<input class="block mt-2 w-full" type="file" accept=".docx" @change="pick($event,'insights')"></label><label>Novas imagens<input class="block mt-2 w-full" type="file" accept="image/*" multiple @change="pickImages"></label></div>
      <DsAlert v-if="error" variant="error">{{error}}</DsAlert><div class="flex gap-2"><DsButton type="submit" :loading="saving">Salvar</DsButton><DsButton variant="secondary" :to="`/paineis/${painelId}/versoes/${versaoId}`">Cancelar</DsButton></div>
    </form>
  </DsPageShell></div>
</template>

<script setup lang="ts">
definePageMeta({layout:'default'})
const route=useRoute(),api=usePaineisVersoesApi();const painelId=computed(()=>Number(route.params.id)),versaoId=computed(()=>Number(route.params.versaoId));const tipo=ref('API'),saving=ref(false),error=ref('')
const form=reactive({numeroVersao:'',publicado:false,observacoes:'',diretorioPbix:'',nomeArquivoPbix:'',workspacePowerbi:'',datasetPowerbi:'',gatewayPowerbi:'',frequenciaAtualizacao:'',responsavelAtualizacao:'',publicLink:'',iframeCode:'',enderecoApi:'',responsavelApi:''});const files=reactive<{json?:File,manual?:File,insights?:File,imagens:File[]}>({imagens:[]})
function pick(e:Event,type:'json'|'manual'|'insights'){files[type]=(e.target as HTMLInputElement).files?.[0]} function pickImages(e:Event){files.imagens=Array.from((e.target as HTMLInputElement).files||[])}
function payload(){const fd=new FormData();Object.entries(form).forEach(([k,v])=>fd.append(k,String(v)));if(files.json)fd.append('jsonApi',files.json);if(files.manual)fd.append('manualDocx',files.manual);if(files.insights)fd.append('insightsDocx',files.insights);files.imagens.forEach(x=>fd.append('imagens',x));return fd}
async function save(){saving.value=true;error.value='';try{await api.update(painelId.value,versaoId.value,payload());await navigateTo(`/paineis/${painelId.value}/versoes/${versaoId.value}`)}catch(e){error.value=e instanceof Error?e.message:'Erro ao atualizar.'}finally{saving.value=false}}
onMounted(async()=>{try{const d=(await api.get(painelId.value,versaoId.value)).data;tipo.value=d.versao.tipoPainel;form.numeroVersao=d.versao.numeroVersao;form.publicado=!!d.versao.publicado;form.observacoes=d.versao.observacoes||'';const c=d.configuracao||{};Object.keys(form).forEach(k=>{if(k in c)(form as any)[k]=c[k]??''})}catch(e){error.value=e instanceof Error?e.message:'Erro ao carregar versão.'}})
</script>
