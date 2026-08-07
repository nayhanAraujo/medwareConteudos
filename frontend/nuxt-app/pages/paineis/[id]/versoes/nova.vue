<template>
  <div>
    <DsPageHeader title="Nova versão" subtitle="Configuração e artefatos do painel" icon="plus-circle" />
    <DsPageShell>
      <form class="space-y-6" @submit.prevent="save">
        <div class="grid md:grid-cols-2 gap-4">
          <DsInput v-model="form.numeroVersao" label="Número da versão" required placeholder="Ex.: 2.0" />
          <label class="flex items-center gap-2 pt-8"><input v-model="form.publicado" type="checkbox"> Publicado</label>
        </div>
        <DsTextarea v-model="form.observacoes" label="Observações" />
        <DsSectionTitle :title="tipo === 'POWERBI' ? 'Configuração Power BI' : 'Configuração API'" />
        <div v-if="tipo === 'POWERBI'" class="grid md:grid-cols-2 gap-4">
          <DsInput v-model="form.diretorioPbix" label="Diretório PBIX" /><DsInput v-model="form.nomeArquivoPbix" label="Nome do PBIX" />
          <DsInput v-model="form.workspacePowerbi" label="Workspace" /><DsInput v-model="form.datasetPowerbi" label="Dataset" />
          <DsInput v-model="form.gatewayPowerbi" label="Gateway" /><DsInput v-model="form.frequenciaAtualizacao" label="Frequência de atualização" />
          <DsInput v-model="form.responsavelAtualizacao" label="Responsável" /><DsInput v-model="form.publicLink" label="Link público" />
          <DsTextarea v-model="form.iframeCode" label="Código iframe" class="md:col-span-2" />
        </div>
        <div v-else class="grid md:grid-cols-2 gap-4">
          <DsInput v-model="form.enderecoApi" label="Endereço da API" /><DsInput v-model="form.responsavelApi" label="Responsável" />
          <FileField label="Arquivo JSON" accept=".json,application/json" @change="jsonApi = $event" />
        </div>
        <DsSectionTitle title="Documentos e imagens" />
        <div class="grid md:grid-cols-3 gap-4">
          <FileField label="Manual (.docx)" accept=".docx" @change="manual = $event" />
          <FileField label="Insights (.docx)" accept=".docx" @change="insights = $event" />
          <label class="text-sm font-medium">Imagens<input class="block mt-2 w-full" type="file" accept="image/jpeg,image/png,image/gif" multiple @change="setImages"></label>
        </div>
        <DsAlert v-if="error" variant="error">{{ error }}</DsAlert>
        <div class="flex gap-2"><DsButton type="submit" :loading="saving">Salvar</DsButton><DsButton variant="secondary" :to="`/paineis/${painelId}/versoes`">Cancelar</DsButton></div>
      </form>
    </DsPageShell>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, h } from 'vue'
definePageMeta({ layout: 'default' })
const FileField = defineComponent({ props: { label: String, accept: String }, emits: ['change'], setup(p,{emit}) { return () => h('label',{class:'text-sm font-medium'},[p.label,h('input',{class:'block mt-2 w-full',type:'file',accept:p.accept,onChange:(e:Event)=>emit('change',(e.target as HTMLInputElement).files?.[0] || null)})]) } })
const route=useRoute(); const api=usePaineisVersoesApi(); const paineis=usePaineisApi(); const painelId=computed(()=>Number(route.params.id)); const tipo=ref('API'); const saving=ref(false); const error=ref('')
const form=reactive({numeroVersao:'',publicado:false,observacoes:'',diretorioPbix:'',nomeArquivoPbix:'',workspacePowerbi:'',datasetPowerbi:'',gatewayPowerbi:'',frequenciaAtualizacao:'',responsavelAtualizacao:'',publicLink:'',iframeCode:'',enderecoApi:'',responsavelApi:''})
const jsonApi=ref<File|null>(null), manual=ref<File|null>(null), insights=ref<File|null>(null), imagens=ref<File[]>([])
function setImages(e:Event){ imagens.value=Array.from((e.target as HTMLInputElement).files || []) }
function payload(){ const fd=new FormData(); Object.entries(form).forEach(([k,v])=>fd.append(k,String(v))); if(jsonApi.value)fd.append('jsonApi',jsonApi.value); if(manual.value)fd.append('manualDocx',manual.value); if(insights.value)fd.append('insightsDocx',insights.value); imagens.value.forEach(x=>fd.append('imagens',x)); return fd }
async function save(){ saving.value=true; error.value=''; try { const result=await api.create(painelId.value,payload()); await navigateTo(`/paineis/${painelId.value}/versoes/${result.data.codVersaoPainel}`) } catch(e){ error.value=e instanceof Error?e.message:'Erro ao criar versão.' } finally{ saving.value=false } }
onMounted(async()=>{ try { tipo.value=(await paineis.getPainel(painelId.value)).data.tipo_painel } catch(e){ error.value=e instanceof Error?e.message:'Erro ao carregar painel.' } })
</script>
