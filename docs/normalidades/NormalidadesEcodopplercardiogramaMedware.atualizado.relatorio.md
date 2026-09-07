# Normalidades ecocardiograficas: revisao e compatibilidade

Data da pesquisa: 2026-09-06. Populacao-alvo: adultos. Documento tecnico de rastreabilidade; nao implementa diagnostico automatico.

## Ajuste para importacao (2026-09-06, pos-revisao)

Correcao aplicada em `NormalidadesEcodopplercardiogramaMedware.atualizado.json` para o contrato real do Studio / `/variaveis/importar` / script HTML de eco:

### Regra de `COMENTARIOTEXTO`

- `COMENTARIOTEXTO.comentario` e a **abreviacao curta da normalidade** (ex.: `≥ 1200`, `≤ 5`, `> 35`), nao o texto de revisao medica.
- Notas cientificas longas foram movidas para `_meta.ObservacaoRevisao`.
- Exemplos: `VR_DP_DT` → `≥ 1200`; `VR_ESPESSURA_PAREDE_LIVRE_VD` → `≤ 5`; `VR_FACVD` → `> 35`; `VR_TAPSE_VD` → `> 17`; `VR_TRONCO_PULMONAR` → `< 25` (corte revisado); medidas novas como `VR_E_SPETAL` → `> 6`, `VR_PSAP` → `< 35`.

### Graus restaurados (operacionais/historicos)

Nas 30 medidas em que a revisao cientifica havia removido `leve`/`moderado`/`grave` (ou esvaziado o bloco de sexo), as divisoes do JSON original foram recolocadas para o HTML/importador pintar faixas. Quando ja existia `normal` revisado pela diretriz, esse `normal` foi **preservado** e apenas `leve`/`moderado`/`grave` voltaram do original.

Medidas com graus restaurados:

`VR_AE`, `VR_ANELAORTICO`, `VR_ANELAORTICO_INDEX`, `VR_AO_ASCENDENTE`, `VR_AO_JUNCAO`, `VR_AO_JUNCAO_INDEX`, `VR_AO_SEIOS_VALSALVA`, `VR_AO_SEIO_VALSALVA_INDEX`, `VR_AREA_DVD_INDEX`, `VR_DIFERENCA_AR_A`, `VR_DP_DT`, `VR_EIXO_CURTO_AD_INDEX`, `VR_EIXO_LONGO_AD_INDEX`, `VR_E_LINHA`, `VR_FETEICHOLZ`, `VR_FE_3D_VD`, `VR_GLS`, `VR_IPM`, `VR_ONDA_A_MITRAL`, `VR_ONDA_E_MITRAL`, `VR_ONDA_S_ANEL_TRICUSPIDE`, `VR_ONDA_S_DOPPLER_COLORIDO`, `VR_RELACAO_ELINHA_ALINHA`, `VR_RELACAO_E_A`, `VR_RELACAO_E_ELINHA`, `VR_STRAIN_PAREDE_LIVRE_VD`, `VR_TAPSE_VD`, `VR_TEMPO_DESACELERACAO_E_MITRAL`, `VR_VIA_SAIDA_VE`, `VR_VOLUME_AD_INDEX`.

**Aviso:** graus restaurados sem respaldo de diretriz sao **operacionais/historicos** (uteis para o script e para o importador). A justificativa cientifica permanece em `_meta.ObservacaoRevisao` e nas secoes abaixo.

### Medidas novas sem graus inventados

As 20 medidas acrescentadas na revisao nao receberam `leve`/`moderado`/`grave` inventados. Blocos de sexo vazios permanecem apenas em `VR_PRESSAO_CAPILAR_PULMONAR` e `VR_VELOCIDADE_PROPAGACAO_FLUXO_MITRAL` (sem faixa numerica publicada no JSON).

### Importador

- Com faixas `min`/`max` restauradas + comentario curto, o arquivo volta a ser processavel por `/variaveis/importar` (`JsonVariaveisParser`).
- O save de comentario em `ImportacaoVariaveisService` passa por `Iso88591SafeText.ForStorage` para `≥`/`≤` nao virarem `?` no Firebird ISO8859_1.
- Continua valendo: `_meta.Fonte` precisa casar com `REFERENCIA.TITULO` ou selecionar a referencia no preview; `NOMEALTERNATIVO` ainda e ignorado pelo parser.

## Entrega

- 58 medidas originais preservadas; 20 acrescentadas; 78 itens no total.
- Graus presentes nos blocos principais: 267 no original e 141 na revisao. Os remanescentes foram conferidos nas tabelas; nao houve extrapolacao de graus.
- **Pos-ajuste importacao:** graus operacionais do original foram recolocados nas 30 medidas listadas acima; totais de graus no JSON atualizado voltam a cobrir as divisoes necessarias ao script HTML.
- JSON original preservado. Nenhum projeto, MRD ou importador editado.
- Banco consultado com transacao Firebird READ / CONCURRENCY e ROLLBACK. Apenas SELECT; nenhuma gravacao no banco. O arquivo fisico pode ter metadados administrativos alterados pelo servidor, portanto hash do FDB nao prova ausencia de escrita SQL.
- Extraidos 156 cadastros, 18 alternativas e 4 nomes clinicos. Listas vazias significam ausencia de cadastro correspondente, nao ausencia de sinonimos na literatura.

## Principais correcoes

- Aorta: ESC 2024, Figura 5, p.3563 (PDF 26). Ascendente F <=36/M <=40 mm; juncao F <=33/M <=38; seios F <=34/M <=40; arco proximal F <=34/M <=37. Indexacao dos seios <22 mm/m2.
- Anel aortico e alguns indices: medias +/- DP nao foram convertidas artificialmente em normalidade. Permanecem sem faixa universal, com estatisticas descritivas e pendencia documentada.
- Cavidades esquerdas: paginas corrigidas, graus de dimensoes, volumes e massa conferidos na Tabela suplementar 3 (p.39.e8, PDF 47). Volume AE indexado: 16-34; leve 35-41; moderado 42-48; grave >48 mL/m2.
- VD: diametro basal <41, medio <35, longitudinal <82 mm; referencias 2025 de TSVD e areas substituem valores antigos. Referencias gerais 2025 nao foram rotuladas como especificas por sexo.
- S' tricuspide >9,5 cm/s. 0,095 m/s = 9,5 cm/s; a sugestao historica de 0,95 cm/s e erro de conversao de fator 10.
- Strain parede livre VD: consenso de novembro/2025, p.995 (PDF 11), F <=-21/M <=-20%. GLS normal <-18%; limitrofe [-18;-16], sem chamar isso de disfuncao leve.
- E/A (0,8;2,0), E/e' e Ar-A foram identificados como pontos de corte/complementos de algoritmos, e nao como diagnostico isolado. Ar-A aceita negativos e <=30 ms apenas significa ausencia do criterio >30.
- Ondas E e A: cm/s e referencias por idade da Tabela 5 (p.551), sem faixa adulta unica ou graus inventados.
- RVP normal <1,5 UW segundo ASE 2025 p.167; >2,0 e anormal. O intervalo entre os dois cortes nao foi classificado como normal.
- IPM e volume AD indexado: faixas condicionadas ao metodo. Teichholz, dP/dt VE e diametro VSVE nao recebem faixa numerica sem respaldo especifico confirmado.

## Fontes e atualidade

Pesquisa priorizou documentos primarios e o catalogo ASE vigente, incluindo publicacoes de 2026. Nao representa revisao sistematica exaustiva. Novidade de publicacao nao basta para substituir o metodo ou a populacao de uma referencia.

- 2015: [Recommendations for Cardiac Chamber Quantification by Echocardiography in Adults: An Update from the American Society of Echocardiography and the European Association of Cardiovascular Imaging](https://www.asecho.org/wp-content/uploads/2025/04/2015_ChamberQuantificationREV.pdf). DOI: 10.1016/j.echo.2014.10.003.
- 2025: [Guidelines for the Echocardiographic Assessment of the Right Heart in Adults and Special Considerations in Pulmonary Hypertension: Recommendations from the American Society of Echocardiography](https://www.asecho.org/wp-content/uploads/2025/03/PIIS0894731725000379.pdf). DOI: 10.1016/j.echo.2025.01.006.
- 2025: [Recommendations for the Evaluation of Left Ventricular Diastolic Function by Echocardiography and for Heart Failure With Preserved Ejection Fraction Diagnosis: An Update From the American Society of Echocardiography](https://www.asecho.org/wp-content/uploads/2025/07/Left-Ventricular-Diastolic-Function.pdf). DOI: 10.1016/j.echo.2025.03.011.
- 2025: [Clinical Applications of Strain Echocardiography: A Clinical Consensus Statement From the American Society of Echocardiography Developed in Collaboration With the European Association of Cardiovascular Imaging of the European Society of Cardiology](https://www.asecho.org/wp-content/uploads/2025/11/Strain-2025-Guideline-2.pdf). DOI: 10.1016/j.echo.2025.07.007.
- 2024: [2024 ESC Guidelines for the management of peripheral arterial and aortic diseases](https://doi.org/10.1093/eurheartj/ehae179). DOI: 10.1093/eurheartj/ehae179.
- 2005: [Recommendations for Chamber Quantification: A Report from the American Society of Echocardiography's Guidelines and Standards Committee and the Chamber Quantification Writing Group, Developed in Conjunction with the European Association of Echocardiography, a Branch of the European Society of Cardiology](https://doi.org/10.1016/j.echo.2005.10.005). DOI: 10.1016/j.echo.2005.10.005.
- 2017: [Recommendations for Noninvasive Evaluation of Native Valvular Regurgitation: A Report from the American Society of Echocardiography Developed in Collaboration with the Society for Cardiovascular Magnetic Resonance](https://www.asecho.org/wp-content/uploads/2017/04/2017VavularRegurgitationGuideline.pdf). DOI: 10.1016/j.echo.2017.01.007.
- O consenso de strain tambem foi publicado no EHJ Cardiovascular Imaging em 2026 (DOI 10.1093/ehjci/jeag006). O JSON usa a edicao JASE 2025 efetivamente consultada, preservando coerencia entre ano e pagina.
- WASE aorta 2022 foi consultado em HTML como leitura complementar para idade/sexo/raca. As tabelas consultadas apresentam medias +/- DP; limites percentilares e pagina editorial nao foram verificados, portanto nao foram transcritos como faixas.
- Estudos de aplicacao de diastologia publicados em 2026 nao foram tratados como novas tabelas universais: https://doi.org/10.1016/j.jacc.2026.01.091 e https://www.sciencedirect.com/science/article/pii/S0894731726000878 .
- Catalogo consultado: https://www.asecho.org/practice-clinical-resources/ase-guidelines/ .

## Esquema 2.0 e adaptacao futura do importador

- `NOMEALTERNATIVO`: lista de nomes de VARIAVEIS.NOME e VARIAVEISNOMESCLINICOS.NOME. `VARIAVEISALTERNATIVAS`: somente alternativas cadastradas. Alternativas antigas nao confirmadas ficam em `_meta.VariaveisAlternativasArquivoOriginal` e nao sao consideradas vinculos ativos.
- `min`/`max` podem ser null. Isso significa extremo nao estabelecido pela fonte, nao zero, e nao autoriza aceitar valores fisiologicamente impossiveis. Validacao de entrada pertence ao sistema consumidor.
- `minInclusivo`/`maxInclusivo`: booleanos para extremo numerico; null para extremo ausente. Nao substituir por incrementos como 0,01.
- Bloco de sexo vazio significa ausencia de faixa universal **somente** nas medidas novas sem original (`VR_PRESSAO_CAPILAR_PULMONAR`, `VR_VELOCIDADE_PROPAGACAO_FLUXO_MITRAL`). Nas demais, blocos vazios da revisao cientifica foram preenchidos de novo com as divisoes operacionais do original.
- `COMENTARIOTEXTO.comentario` e abreviacao curta; texto de revisao fica em `_meta.ObservacaoRevisao`.
- `CLASSIFICACOESADICIONAIS` contem rotulos como limitrofe/anormal, sem converte-los em leve/moderado/grave.
- `_meta.Pagina` pode ser inteiro ou identificador editorial (ex.: "39.e8"); `_meta.PaginaPDF` e inteiro, contando a primeira pagina como 1. Campos null significam localizacao/faixa nao confirmada; nao fabricar numeros.
- As faixas publicadas com arredondamento mantem lacunas (ex.: 52 a 53 mm). Um valor no intervalo entre categorias deve retornar SEM CLASSIFICACAO. Nao arredondar o valor do paciente sem politica validada pelo medico.
- O importador atual exige min/max numericos, le Pagina como inteiro e nao processa NOMEALTERNATIVO, inclusividade e contextos. **Apos o ajuste de importacao**, o JSON volta a ter min/max e comentarios curtos nas medidas originais e e utilizavel em `/variaveis/importar` (ainda exigindo match de referencia e classificacoes no banco).
- Adaptacao posterior: ler o esquema 2.0, preservar comentarios/unidades/fontes, suportando paginas editoriais, selecionar metodo/idade/sexo, sinalizar vazios/ambiguidades e nao interpretar ausencia de criterio como diagnostico normal.
- Qualquer importacao deve usar as unidades do JSON. Nao multiplicar medidas clinicas automaticamente com base somente no rotulo antigo do banco.

## Pendencias de interpretacao e identidade

| Variavel | Situacao | Observacao |
|---|---|---|
| VR_ANELAORTICO | Revisao medica; banco 189 | A tabela apresenta media e desvio-padrao, nao os intervalos e graus presentes no JSON original. Media +/- 1 DP nao representa intervalo de normalidade. Sem faixa universal validada neste arquivo; considerar referencia por idade, sexo e metodo, incluindo WASE 2022. |
| VR_ANELAORTICO_INDEX | Revisao medica; banco 79 | A tabela apresenta media e desvio-padrao, nao os intervalos e graus presentes no JSON original. Media +/- 1 DP nao representa intervalo de normalidade. Sem faixa universal validada neste arquivo; considerar referencia por idade, sexo e metodo, incluindo WASE 2022. |
| VR_AO_JUNCAO_INDEX | Revisao medica; banco 83 | A tabela apresenta media e desvio-padrao, nao os intervalos e graus presentes no JSON original. Media +/- 1 DP nao representa intervalo de normalidade. Sem faixa universal validada neste arquivo; considerar referencia por idade, sexo e metodo, incluindo WASE 2022. |
| VR_FETEICHOLZ | Revisao medica; banco 45 | A diretriz de 2015 nao recomenda Teichholz/Quinones para calcular volumes do VE na pratica clinica. Nao transferir as faixas do Simpson para este metodo sem validacao especifica. Faixas removidas. |
| VR_IPM | Revisao medica; banco 174 | Indice generico: confirmar VE/VD e Doppler pulsado/tecidual. Para VD, normal <0,40 por pulsado e <0,55 por tecidual. Nao adotar automaticamente o antigo <0,43. |
| VR_ONDA_A_MITRAL | Confirmada por contexto; banco 211 | Sem faixa unica para adultos. Referencias por idade sao percentis 5 e 95, convertidos de m/s para cm/s (x100). Nao confundir os IC dos percentis com o intervalo individual. A fonte usa grupos 20-39, 40-60 e 60-80; a idade 60 e compartilhada entre os dois ultimos e nao deve ser resolvida silenciosamente. |
| VR_ONDA_E_MITRAL | Confirmada por contexto; banco 210 | Sem faixa unica para adultos. Referencias por idade sao percentis 5 e 95, convertidos de m/s para cm/s (x100). Nao confundir os IC dos percentis com o intervalo individual. A fonte usa grupos 20-39, 40-60 e 60-80; a idade 60 e compartilhada entre os dois ultimos e nao deve ser resolvida silenciosamente. |
| VR_PEC | Revisao medica; banco 107 | Origem historica identificada em 2005, nao na tabela de 2015 atribuida pelo arquivo original. A diretriz de 2015 prefere FE volumetrica e ressalta limitacoes das medidas lineares (secao 2.1, paginas 6-9). Faixas historicas confirmadas, aplicabilidade atual requer revisao. |
| VR_TEMPO_DESACELERACAO_E_MITRAL | Revisao medica; banco 198 | O significado depende de idade, relaxamento, FE e pressoes. Nao confirmada faixa universal 150-240/160-240 ms na fonte atribuida; os graus originais se sobrepunham a faixa normal. Faixas removidas. |
| VR_VOLUME_AD_INDEX | Revisao medica; banco 250 | Os antigos 21 +/- 6 (F) e 25 +/- 7 (M) eram medias +/- DP, nao limites superiores. ASE 2025: <30 por discos, <33 por area-comprimento. Sem faixa generica enquanto o metodo nao for informado. |
| VR_AREA_DVD_INDEX | Revisao medica; banco 229 | Cadastro diz area do VD indexada sem confirmar diastole/sistole. Os limites sao diferentes (<14 diastolica; <8 sistolica). Nao associar automaticamente a VR_AREA_DIASTOLICA_VD_INDEX. |
| VR_DP_DT | Revisao medica; banco 24 | Nao localizado respaldo para a faixa e graus originais na diretriz de regurgitacao ASE 2017 consultada. O valor >1200 aparece em revisoes educacionais, mas nao foi adotado sem fonte primaria e pagina verificadas. Nao substituir por dP/dt do VD. Cadastro em % e inconsistente. |
| VR_RELACAO_E_A | Confirmada; banco nao vinculado | Mantida a faixa solicitada >0,8 e <2,0 como faixa operacional, nao como normalidade universal. Pode haver pseudonormalizacao; intervalos populacionais variam com idade. Nao atribuir graus diastolicos somente por E/A. |
| VR_ONDA_S_DOPPLER_COLORIDO | Revisao medica; banco nao vinculado | Nao aplicar o limite de S' por Doppler pulsado ao Doppler colorido. A fonte consultada nao confirma os antigos 6 cm/s e graus. Confirmar sitio e tecnica. |
| VR_FE_3D_VD | Confirmada; banco nao vinculado | Faixa normal confirmada. Graus omitidos devido a sobreposicao/inconsistencia de desigualdades na Tabela 1 consultada (TAPSE 1,3 cm; S' 7,2 cm/s; FE 3D intervalo leve). Nao corrigir silenciosamente a fonte. Para S' tricuspide, 0,095 m/s equivale a 9,5 cm/s, nao a 0,95 cm/s. |
| VR_RELACAO_ELINHA_ALINHA | Revisao medica; banco nao vinculado | Nao confundir e'/a' tricuspide com relacao mitral. O codigo e o cadastro nao confirmam local de amostragem; sem faixa generica. |
| VR_E_LINHA | Revisao medica; banco nao vinculado | O codigo nao distingue e' septal, lateral ou tricuspide. Nao transferir faixas entre sitios. Usar os novos itens especificos. |
| VR_EIXO_CURTO_AD_INDEX | Revisao medica; banco nao vinculado | Media +/- DP nao define normalidade individual. A ASE 2025 fornece cortes para eixos absolutos, nao para estes eixos indexados. Removidas faixas derivadas de +/-1 DP e graus sem respaldo. |
| VR_EIXO_LONGO_AD_INDEX | Revisao medica; banco nao vinculado | Media +/- DP nao define normalidade individual. A ASE 2025 fornece cortes para eixos absolutos, nao para estes eixos indexados. Removidas faixas derivadas de +/-1 DP e graus sem respaldo. |
| VR_VIA_SAIDA_VE | Revisao medica; banco 203 | Fonte original era apenas Editado pelo usuario (2026), sem estudo. Nao confirmadas faixas F 18-23/M 20-25 mm com pagina em fonte primaria; nao confundir diametro VSVE com anel aortico. Sem faixa ate validacao. |
| VR_E_MEDIA | Confirmada; banco nao vinculado | Ponto de corte do algoritmo, nao intervalo populacional nem diagnostico isolado. Aplicar junto aos demais parametros e observar exclusoes da Figura 3: FA, valvopatia/intervencao mitral relevante, MAC moderada/importante, transplante, LVAD, constricao e HP nao cardiaca. As Figuras 2/3 usam <= para e' reduzido; Tabela 6/texto usam <. Aqui foi adotada explicitamente a Figura 3, conforme alinhado. |
| VR_RELACAO_E_E_SEPTAL | Confirmada; banco nao vinculado | Ponto de corte do algoritmo, nao intervalo populacional nem diagnostico isolado. Aplicar junto aos demais parametros e observar exclusoes da Figura 3: FA, valvopatia/intervencao mitral relevante, MAC moderada/importante, transplante, LVAD, constricao e HP nao cardiaca. E/e' medio = E / ((e' septal + e' lateral)/2); nao e a media aritmetica das duas relacoes. |
| VR_RELACAO_E_E_LATERAL | Confirmada; banco nao vinculado | Ponto de corte do algoritmo, nao intervalo populacional nem diagnostico isolado. Aplicar junto aos demais parametros e observar exclusoes da Figura 3: FA, valvopatia/intervencao mitral relevante, MAC moderada/importante, transplante, LVAD, constricao e HP nao cardiaca. E/e' medio = E / ((e' septal + e' lateral)/2); nao e a media aritmetica das duas relacoes. |
| VR_VELOCIDADE_PROPAGACAO_FLUXO_MITRAL | Revisao medica; banco 217 | Vp depende do contexto de FE e tamanho ventricular; pode ser pseudonormal em VE pequeno/hipertrofiado. Sem faixa universal transcrita da referencia consultada. |
| VR_RELACAO_E_MITRAL_VP | Confirmada; banco nao vinculado | E/Vp >=2,5 prediz PCWP >15 mmHg em pacientes com FE reduzida. <2,5 significa ausencia desse criterio, nao normalidade universal. Nao aplicar isoladamente a FE preservada. |
| VR_PRESSAO_CAPILAR_PULMONAR | Revisao medica; banco 225 | Fonte descreve normal <12 a 15 mmHg e limita o uso rotineiro da estimativa. Nao ha justificativa para escolher silenciosamente <14. Sem faixa unica; PCWP invasiva >15 mmHg em repouso e criterio de HFpEF na ASE 2025, pagina 540. |

## Unidades divergentes no banco

| Variavel | Banco | JSON cientifico |
|---|---|---|
| VR_MVE | mm | g |
| VR_VDF_INDEX | mm | mL/m2 |
| VR_VSF_INDEX | mm | mL/m2 |
| VR_PEC | mm | % |
| VR_AO_SEIO_VALSALVA_INDEX | mm | mm/m2 |
| VR_ANELAORTICO_INDEX | mm | mm/m2 |
| VR_AO_JUNCAO_INDEX | mm | mm/m2 |
| VR_AREA_DIASTOLICA_VD_INDEX | unknown | cm2/m2 |
| VR_AREA_SISTOLICA_VD_INDEX | unknown | cm2/m2 |
| VR_AREA_DVD_INDEX | unknown | cm2/m2 |
| VR_IPM | % | adimensional |
| VR_E_SPETAL | m/s | cm/s |
| VR_E_LATERAL | m/s | cm/s |
| VR_RELACAO_E_ELINHA | mm | adimensional |
| VR_ONDA_E_MITRAL | m/s | cm/s |
| VR_ONDA_A_MITRAL | m/s | cm/s |
| VR_VELOCIDADE_PROPAGACAO_FLUXO_MITRAL | m/s | cm/s |
| VR_ERP | mm | adimensional |
| VR_PSAP | mm | mmHg |
| VR_VEL_REG_TRICUSPIDE | mm | m/s |
| VR_RESISTENCIA_VASCULAR_PULMONAR | mrm | UW |
| VR_DP_DT | % | mmHg/s |

## Correspondencias clinicas revisadas

| Chave preservada | CODVARIAVEL | Variavel no banco |
|---|---|---|
| VR_DVD_MEDIO | 157 | VR_VD2 |
| VR_DVD_LONGITUDINAL | 158 | VR_VD3 |
| VR_GLS | 154 | VR_GLS_2D |
| VR_VIA_SAIDA_VE | 203 | VR_DIAMETRO_VSVE |

Colisoes ja existentes entre alternativas e codigos principais do banco:
- VR_E_SPETAL: `{"VR_INDICEELINHA": [28, 101]}`. Conferir antes de resolver codigos por alias; nenhum cadastro foi corrigido nesta entrega.
- VR_E_LATERAL: `{"VR_INDICEELINHA_LAT": [102]}`. Conferir antes de resolver codigos por alias; nenhum cadastro foi corrigido nesta entrega.

## Comparacao por medida original

| Medida | Normal original | Normal revisado | Graus removidos por sexo |
|---|---|---|---|
| VR_ANELAORTICO | F: 17.4 a 21.6; M: 19.0 a 23.4 | Sem faixa validada | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_ANELAORTICO_INDEX | F: 10.5 a 13.3; M: 10.0 a 12.6 | Sem faixa validada | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_AO_SEIOS_VALSALVA | F: 25.7 a 32.9; M: 28.5 a 35.9 | F: <= 34; M: <= 40 | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_AO_ASCENDENTE | F: 22.0 a 30.0; M: 25.0 a 35.0 | F: <= 36; M: <= 40 | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_AO_SEIO_VALSALVA_INDEX | F: 15.4 a 20.6; M: 14.7 a 19.7 | F: < 22; M: < 22 | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_AO_JUNCAO | F: 22.2 a 28.8; M: 24.0 a 31.4 | F: <= 33; M: <= 38 | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_AO_JUNCAO_INDEX | F: 13.2 a 18.0; M: 12.5 a 17.1 | Sem faixa validada | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_AE | F: 27.0 a 38.0; M: 30.0 a 40.0 | F: [27; 38]; M: [30; 40] | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_DDFVE | F: 38.0 a 52.0; M: 42.0 a 58.0 | F: [38; 52]; M: [42; 58] | - |
| VR_DSFVE | F: 22.0 a 35.0; M: 25.0 a 40.0 | F: [22; 35]; M: [25; 40] | - |
| VR_DVD | U: 25.0 a 41.0 | U: < 41 | - |
| VR_ESPESSURA_PAREDE_LIVRE_VD | U: 0.0 a 5.0 | U: < 5 | - |
| VR_FACVD | U: 35.0 a 999.0 | U: > 35 | - |
| VR_FESIMPSON | F: 54.0 a 74.0; M: 52.0 a 72.0 | F: [54; 74]; M: [52; 72] | - |
| VR_FETEICHOLZ | F: 54.0 a 74.0; M: 52.0 a 72.0 | Sem faixa validada | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_IPM | U: 0.0 a 0.43 | Por contexto | U: leve, moderado, grave |
| VR_MVE | F: 67.0 a 162.0; M: 88.0 a 224.0 | F: [67; 162]; M: [88; 224] | - |
| VR_MVE_INDEX | F: 43.0 a 95.0; M: 49.0 a 115.0 | F: [43; 95]; M: [49; 115] | - |
| VR_ONDA_A_MITRAL | M: 0.2 a 0.5 | Por contexto | M: leve, moderado, grave |
| VR_ONDA_E_MITRAL | U: 0.6 a 1.0 | Por contexto | U: leve, moderado, grave |
| VR_PPVE | F: 6.0 a 9.0; M: 6.0 a 10.0 | F: [6; 9]; M: [6; 10] | - |
| VR_PEC | F: 27.0 a 45.0; M: 25.0 a 43.0 | F: [27; 45]; M: [25; 43] | - |
| VR_RELACAO_E_ELINHA | U: 0.0 a 14.0 | U: < 14 | U: leve, moderado, grave |
| VR_SEPTO | F: 6.0 a 9.0; M: 6.0 a 10.0 | F: [6; 9]; M: [6; 10] | - |
| VR_ONDA_S_ANEL_TRICUSPIDE | U: 9.6 a 999.0 | U: > 9.5 | U: leve, moderado, grave |
| VR_TAPSE_VD | U: 17.0 a 999.0 | U: > 17 | U: leve, moderado, grave |
| VR_TEMPO_DESACELERACAO_E_MITRAL | U: 150.0 a 240.0 | Sem faixa validada | U: leve, moderado, grave |
| VR_VDF | F: 46.0 a 106.0; M: 62.0 a 150.0 | F: [46; 106]; M: [62; 150] | - |
| VR_VOLUME_AD_INDEX | F: 0.0 a 27.0; M: 0.0 a 32.0; U: 0.0 a 34.0 | Por contexto | F: leve, moderado, grave; M: leve, moderado, grave; U: leve, moderado, grave |
| VR_VDF_INDEX | F: 29.0 a 61.0; M: 34.0 a 74.0 | F: [29; 61]; M: [34; 74] | - |
| VR_VSF_INDEX | F: 8.0 a 24.0; M: 11.0 a 31.0 | F: [8; 24]; M: [11; 31] | - |
| VR_VSF | F: 14.0 a 42.0; M: 21.0 a 61.0 | F: [14; 42]; M: [21; 61] | - |
| VR_VOL_AE_INDEX | U: 0.0 a 34.0 | U: [16; 34] | - |
| VR_DVD_MEDIO | U: 0.0 a 42.0 | U: < 35 | - |
| VR_AREA_DVD_INDEX | U: 0.0 a 11.5 | Sem faixa validada | U: leve, moderado, grave |
| VR_GLS | U: -999.0 a -18.0 | U: < -18 | U: leve, moderado, grave |
| VR_TRONCO_PULMONAR | F: 0.0 a 21.0; M: 0.0 a 22.0 | F: < 25; M: < 25 | - |
| VR_DP_DT | U: 1000.0 a 9999.0 | Sem faixa validada | U: leve, moderado, grave |
| VR_RELACAO_E_A | U: 0.8 a 1.5 | U: (0.8; 2) | U: leve, moderado, grave |
| VR_DVD_LONGITUDINAL | U: 59.0 a 83.0 | U: < 82 | - |
| VR_TSVD_PARASTERNAL | U: 20.0 a 30.0 | U: < 33 | - |
| VR_TSVD_PROXIMAL | U: 21.0 a 35.0 | U: < 34 | - |
| VR_TSVD_DISTAL | U: 17.0 a 27.0 | U: < 29 | - |
| VR_AREA_DIASTOLICA_VD | F: 8.0 a 20.0; M: 10.0 a 24.0 | F: < 25; M: < 25 | - |
| VR_AREA_DIASTOLICA_VD_INDEX | F: 4.5 a 11.5; M: 5.0 a 12.6 | F: < 14; M: < 14 | - |
| VR_AREA_SISTOLICA_VD | F: 3.0 a 11.0; M: 3.0 a 15.0 | F: < 14; M: < 14 | - |
| VR_AREA_SISTOLICA_VD_INDEX | F: 1.6 a 6.4; M: 2.0 a 7.4 | F: < 8; M: < 8 | - |
| VR_VDF_VD_INDEX_3D | F: 32.0 a 74.0; M: 35.0 a 87.0 | F: < 90; M: < 90 | - |
| VR_VSF_VD_INDEX_3D | F: 8.0 a 36.0; M: 10.0 a 44.0 | F: < 41; M: < 41 | - |
| VR_ONDA_S_DOPPLER_COLORIDO | U: 6.0 a 999.0 | Sem faixa validada | U: leve, moderado, grave |
| VR_STRAIN_PAREDE_LIVRE_VD | U: -999.0 a -20.0 | F: <= -21; M: <= -20 | U: leve, moderado, grave |
| VR_FE_3D_VD | U: 45.0 a 999.0 | U: > 45 | U: leve, moderado, grave |
| VR_RELACAO_ELINHA_ALINHA | U: 0.52 a 0.99 | Sem faixa validada | U: leve, moderado, grave |
| VR_E_LINHA | U: 7.8 a 9.9 | Sem faixa validada | U: leve, moderado, grave |
| VR_EIXO_CURTO_AD_INDEX | F: 1.6 a 2.2; M: 1.6 a 2.2 | Sem faixa validada | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_EIXO_LONGO_AD_INDEX | F: 2.2 a 2.8; M: 2.1 a 2.7 | Sem faixa validada | F: leve, moderado, grave; M: leve, moderado, grave |
| VR_DIFERENCA_AR_A | U: -999.0 a 0.0 | U: <= 30 | U: leve, moderado, grave |
| VR_VIA_SAIDA_VE | F: 18.0 a 23.0; M: 20.0 a 25.0 | Sem faixa validada | - |

## Catalogo final e localizacao

| Medida | Normal por sexo/contexto | Unidade | Ano | Pagina impressa / PDF | Tabela |
|---|---|---|---|---|---|
| VR_ANELAORTICO | Sem faixa validada | mm | 2015 | 32 / 32 | Tabela 14 |
| VR_ANELAORTICO_INDEX | Sem faixa validada | mm/m2 | 2015 | 32 / 32 | Tabela 14 |
| VR_AO_SEIOS_VALSALVA | F: <= 34; M: <= 40 | mm | 2024 | 3563 / 26 | Figura 5 |
| VR_AO_ASCENDENTE | F: <= 36; M: <= 40 | mm | 2024 | 3563 / 26 | Figura 5 |
| VR_AO_SEIO_VALSALVA_INDEX | F: < 22; M: < 22 | mm/m2 | 2024 | 3563 / 26 | Figura 5 |
| VR_AO_JUNCAO | F: <= 33; M: <= 38 | mm | 2024 | 3563 / 26 | Figura 5 |
| VR_AO_JUNCAO_INDEX | Sem faixa validada | mm/m2 | 2015 | 32 / 32 | Tabela 14 |
| VR_AE | F: [27; 38]; M: [30; 40] | mm | 2015 | 39.e14 / 53 | Tabela suplementar 9 |
| VR_DDFVE | F: [38; 52]; M: [42; 58] | mm | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_DSFVE | F: [22; 35]; M: [25; 40] | mm | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_DVD | U: < 41 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_ESPESSURA_PAREDE_LIVRE_VD | U: < 5 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_FACVD | U: > 35 | % | 2025 | 145 / 5 | Tabela 1 |
| VR_FESIMPSON | F: [54; 74]; M: [52; 72] | % | 2015 | 10 / 10 | Tabela 4 |
| VR_FETEICHOLZ | Sem faixa validada | % | 2015 | 3 / 3 | Secao 1.2 |
| VR_IPM | Selecionar contexto | adimensional | 2025 | 145 / 5 | Tabela 1 |
| VR_MVE | F: [67; 162]; M: [88; 224] | g | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_MVE_INDEX | F: [43; 95]; M: [49; 115] | g/m2 | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_ONDA_A_MITRAL | Selecionar contexto | cm/s | 2025 | 551 / 15 | Tabela 5 |
| VR_ONDA_E_MITRAL | Selecionar contexto | cm/s | 2025 | 551 / 15 | Tabela 5 |
| VR_PPVE | F: [6; 9]; M: [6; 10] | mm | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_PEC | F: [27; 45]; M: [25; 43] | % | 2005 | 1448 / 9 | Tabela 6 |
| VR_RELACAO_E_ELINHA | U: < 14 | adimensional | 2025 | 554 / 18 | Figura 3 |
| VR_SEPTO | F: [6; 9]; M: [6; 10] | mm | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_ONDA_S_ANEL_TRICUSPIDE | U: > 9.5 | cm/s | 2025 | 145 / 5 | Tabela 1 |
| VR_TAPSE_VD | U: > 17 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_TEMPO_DESACELERACAO_E_MITRAL | Sem faixa validada | ms | 2025 | 547 / 11 | Tabela 4, Mitral E-wave DT |
| VR_VDF | F: [46; 106]; M: [62; 150] | mL | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_VOLUME_AD_INDEX | Selecionar contexto | mL/m2 | 2025 | 145 / 5 | Tabela 1 |
| VR_VDF_INDEX | F: [29; 61]; M: [34; 74] | mL/m2 | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_VSF_INDEX | F: [8; 24]; M: [11; 31] | mL/m2 | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_VSF | F: [14; 42]; M: [21; 61] | mL | 2015 | 39.e8 / 47 | Tabela suplementar 3 |
| VR_VOL_AE_INDEX | U: [16; 34] | mL/m2 | 2015 | 10 / 10 | Tabela 4 |
| VR_DVD_MEDIO | U: < 35 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_AREA_DVD_INDEX | Sem faixa validada | cm2/m2 | 2025 | 145 / 5 | Tabela 1 |
| VR_GLS | U: < -18 | % | 2025 | 995 / 11 | Secao 3, Clinical Consensus Statements |
| VR_TRONCO_PULMONAR | F: < 25; M: < 25 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_DP_DT | Sem faixa validada | mmHg/s | pendente | pendente / pendente | pendente |
| VR_RELACAO_E_A | U: (0.8; 2) | adimensional | 2025 | 553 / 17 | Figura 2; Figura 3, pagina 554 |
| VR_DVD_LONGITUDINAL | U: < 82 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_TSVD_PARASTERNAL | U: < 33 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_TSVD_PROXIMAL | U: < 34 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_TSVD_DISTAL | U: < 29 | mm | 2025 | 145 / 5 | Tabela 1 |
| VR_AREA_DIASTOLICA_VD | F: < 25; M: < 25 | cm2 | 2025 | 145 / 5 | Tabela 1 |
| VR_AREA_DIASTOLICA_VD_INDEX | F: < 14; M: < 14 | cm2/m2 | 2025 | 145 / 5 | Tabela 1 |
| VR_AREA_SISTOLICA_VD | F: < 14; M: < 14 | cm2 | 2025 | 145 / 5 | Tabela 1 |
| VR_AREA_SISTOLICA_VD_INDEX | F: < 8; M: < 8 | cm2/m2 | 2025 | 145 / 5 | Tabela 1 |
| VR_VDF_VD_INDEX_3D | F: < 90; M: < 90 | mL/m2 | 2025 | 145 / 5 | Tabela 1 |
| VR_VSF_VD_INDEX_3D | F: < 41; M: < 41 | mL/m2 | 2025 | 145 / 5 | Tabela 1 |
| VR_ONDA_S_DOPPLER_COLORIDO | Sem faixa validada | cm/s | 2025 | 157 / 17 | Tissue Doppler Imaging S' Velocity |
| VR_STRAIN_PAREDE_LIVRE_VD | F: <= -21; M: <= -20 | % | 2025 | 995 / 11 | Secao 3, Clinical Consensus Statements |
| VR_FE_3D_VD | U: > 45 | % | 2025 | 145 / 5 | Tabela 1 |
| VR_RELACAO_ELINHA_ALINHA | Sem faixa validada | adimensional | 2025 | 145 / 5 | Tabela 1 |
| VR_E_LINHA | Sem faixa validada | cm/s | 2025 | 554 / 18 | Figura 3 |
| VR_EIXO_CURTO_AD_INDEX | Sem faixa validada | cm/m2 | 2015 | 30 / 30 | Tabela 13 |
| VR_EIXO_LONGO_AD_INDEX | Sem faixa validada | cm/m2 | 2015 | 30 / 30 | Tabela 13 |
| VR_DIFERENCA_AR_A | U: <= 30 | ms | 2025 | 554 / 18 | Figura 3 |
| VR_VIA_SAIDA_VE | Sem faixa validada | mm | pendente | pendente / pendente | pendente |
| VR_ARCOAO | F: <= 34; M: <= 37 | mm | 2024 | 3563 / 26 | Figura 5 |
| VR_AO_ASC_PROX | F: <= 36; M: <= 40 | mm | 2024 | 3563 / 26 | Figura 5 |
| VR_E_SPETAL | U: > 6 | cm/s | 2025 | 554 / 18 | Figura 3 |
| VR_E_LATERAL | U: > 7 | cm/s | 2025 | 554 / 18 | Figura 3 |
| VR_E_MEDIA | U: > 6.5 | cm/s | 2025 | 554 / 18 | Figura 3 |
| VR_RELACAO_E_E_SEPTAL | U: < 15 | adimensional | 2025 | 554 / 18 | Figura 3 |
| VR_RELACAO_E_E_LATERAL | U: < 13 | adimensional | 2025 | 554 / 18 | Figura 3 |
| VR_VELOCIDADE_PROPAGACAO_FLUXO_MITRAL | Sem faixa validada | cm/s | 2025 | 550 / 14 | Tabela 4, Color M-mode Vp |
| VR_RELACAO_E_MITRAL_VP | U: < 2.5 | adimensional | 2025 | 550 / 14 | Tabela 4, Color M-mode Vp |
| VR_TRIV | U: > 70 | ms | 2025 | 554 / 18 | Figura 3 |
| VR_SAER | U: [23; 60] | % | 2025 | 995 / 11 | Secao 3, Clinical Consensus Statements |
| VR_ERP | F: [0.22; 0.42]; M: [0.24; 0.42] | adimensional | 2015 | 17 / 17 | Tabela 6; Figura 6 |
| VR_PSAP | U: < 35 | mmHg | 2025 | 554 / 18 | Figura 3 |
| VR_VEL_REG_TRICUSPIDE | U: < 2.8 | m/s | 2025 | 554 / 18 | Figura 3 |
| VR_VVCI | U: >= 50 | % | 2025 | 160 / 20 | Right Atrial Pressure |
| VR_VEIA_CAVA_EXPIRACAO | U: <= 21 | mm | 2025 | 160 / 20 | Right Atrial Pressure |
| VR_RESISTENCIA_VASCULAR_PULMONAR | U: < 1.5 | UW | 2025 | 167 / 27 | Pulmonary Vascular Resistance |
| VR_PRESSAO_CAPILAR_PULMONAR | Sem faixa validada | mmHg | 2025 | 167 / 27 | Pulmonary Capillary Wedge Pressure |
| VR_TEMPO_ACELERACAO_FLUXO_PULMONAR | U: > 105 | ms | 2025 | 157 / 17 | RVOT Velocity-Time Integral and Acceleration Time |
| VR_VTI_VSVD | U: > 18 | cm | 2025 | 157 / 17 | RVOT Velocity-Time Integral and Acceleration Time |

## Validacao

- JSON reaberto com deteccao de chaves duplicadas; preservadas todas as 58 chaves originais.
- Conferidos tipos, extremos nulos, sinais, listas e ausencia de sobreposicao nos graus principais. Eliminadas sentinelas +/-999 e 9999.
- Testes de fronteira: Ar-A -20 e 30 aceitos, 30,01 excluido; E/A 0,8 e 2 excluidos; E/e' medio 14 excluido; e' septal 6 excluido; S' 0,95 excluido; RVFWLS -20 depende do sexo.
- Faixas numericas principais possuem estudo, ano, pagina impressa, pagina PDF e link. Pendencias sem faixa nao receberam paginas ficticias.
- SHA256 original antes/depois: `B5155E3DB5051063A1DCD40FF3F1E3B9BFD907D2C02FC89FA5F7927A8413CF2A`.
- SHA256 JSON atualizado: `BD2C3F40E80685E9B86ECCC3F76477B553428CB605CBB510586420829BC30BA2`.
- Arquivos de projetos e MRDs nao foram escritos. Banco acessado exclusivamente por SELECT em transacao READ.
