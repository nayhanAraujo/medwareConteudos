import { Agent } from '@cursor/sdk';
import { agentOptions, prompt } from './policy.mjs';
import { readFile } from 'node:fs/promises';
// Pin and verify the SDK exposing the tool allowlist: old SDK versions silently ignore it.
const version=JSON.parse(await readFile(new URL('./node_modules/@cursor/sdk/package.json',import.meta.url),'utf8')).version;
if(version!=='1.0.40')throw new Error('Unsupported SDK for tool-less analysis');
let raw=''; for await(const chunk of process.stdin){raw+=chunk;if(raw.length>16_000_000)throw new Error('Input too large');}
const input=JSON.parse(raw); let agent;
try{
  agent=await Agent.create(agentOptions(process.env.CURSOR_API_KEY,process.env.QUOTE_AI_MODEL,process.cwd()));
  const run=await agent.send({text:prompt({...input,images:input.images.map(i=>({name:i.name}))}),images:input.images.map(i=>({data:i.data,mimeType:i.mimeType}))});
  const result=await run.wait();if(result.status!=='finished' && result.status!=='success' && result.status!=='completed')throw new Error('Analysis did not finish');
  const text=String(result.result??'').trim();if(text.length>100_000)throw new Error('Output too large');
  JSON.parse(text); process.stdout.write(text);
}catch{process.stderr.write('A análise pelo Composer falhou. Nenhum item foi aplicado.');process.exitCode=1;}
finally{try{if(agent&&typeof agent[Symbol.asyncDispose]==='function')await agent[Symbol.asyncDispose]();}catch{process.exitCode=1;}}
