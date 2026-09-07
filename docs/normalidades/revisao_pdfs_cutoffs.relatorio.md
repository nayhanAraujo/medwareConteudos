# Revisao PDF -> cutoffs para VARIAVEIS

## COD 741 — Diastase ASE/EACVI 2016
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\diretriz diastole ve ase e eacvi 2016.pdf`
- Confirmacao textual: sim
- OK VR_E_SPETAL class=1 7-999: e' septal normal >=7
- OK VR_E_SPETAL class=4 -999-7: e' septal reduzido <7
- OK VR_E_LATERAL class=1 10-999: e' lateral normal >=10
- OK VR_E_LATERAL class=4 -999-10: e' lateral reduzido <10
- OK VR_MEDIA_E_ELINHA class=1 -999-14: E/e' medio normal <=14
- OK VR_MEDIA_E_ELINHA class=4 14-999: E/e' medio elevado >14
- OK VR_RELACAO_E_E_SEPTAL class=1 -999-15: E/e' septal normal <=15
- OK VR_RELACAO_E_E_SEPTAL class=4 15-999: E/e' septal elevado >15
- OK VR_RELACAO_E_E_LATERAL class=1 -999-13: E/e' lateral normal <=13
- OK VR_RELACAO_E_E_LATERAL class=4 13-999: E/e' lateral elevado >13
- OK VR_VEL_REG_TRICUSPIDE class=1 -999-2.8: TR velocity normal <=2.8 m/s
- OK VR_VEL_REG_TRICUSPIDE class=4 2.8-999: TR velocity elevada >2.8
- OK VR_VOL_AE_INDEX class=1 -999-34: LAVI normal <=34
- OK VR_VOL_AE_INDEX class=4 34-999: LAVI aumentado >34
- OK VR_VAE_SC_INDEX class=1 -999-34: LAVI (VAE_SC) normal <=34
- OK VR_VAE_SC_INDEX class=4 34-999: LAVI (VAE_SC) >34
- OK VR_RELACAO_E_A class=1 0.8-2.0: E/A normal tipico 0.8-2
- OK VR_TEMPO_DESACELERACAO_E_MITRAL class=1 160-240: DT normal tipico 160-240 ms

## COD 722 — Estenose aortica ASE 2017
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\aortica estenose diretriz ases 2017.pdf`
- OK VR_GradMedAo: <20 / 20-40 / >=40 mmHg (estenose leve/mod/grave)
- OK VR_AVA: AVA

## COD 744 — Valve stenosis ASE/EAE 2009
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\diretriz echocardiographic assessment of valve stenosis eae ase jase 2009.pdf`
- OK VR_GradMedAo (AS 2009)

## COD 784 — Strain DIC/SBC 2023
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\strain eco diretriz dicsbc 2023 funcao diastolica.pdf`
- OK VR_GLS_2D (convenção negativa)
- OK VR_STRAIN_PAREDE_LIVRE_VD

## COD 785 — Strain ESC 2022
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\strain vdf longutuinal revisao 2022 esc.pdf`
- OK VR_GLS_2D (convenção negativa)
- OK VR_STRAIN_PAREDE_LIVRE_VD

## COD 790 — VD disfuncao ESC 2022
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\vd revisao disfuncao 2022 esc.pdf`
- OK VR_TAPSE_VD: TAPSE <17 anormal
- OK VR_FACVD: FAC <35%
- OK VR_ONDA_S_ANEL_TRICUSPIDE: S' <9.5
- OK VR_GLSVD: GLS VD

## COD 747 — Diastase JASE 2009
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\diretriz for the evaluation of lv diastolic fuction by echo jase 2009.pdf`
- OK VR_E_SPETAL class=1 8-999
- OK VR_E_LATERAL class=1 10-999
- OK VR_MEDIA_E_ELINHA class=1 -999-15
- OK VR_MEDIA_E_ELINHA class=4 15-999
- OK VR_VOL_AE_INDEX class=1 -999-34
- OK VR_VOL_AE_INDEX class=4 34-999

## COD 742 — Chagas ASE 2018
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\diretriz doença de chagas recomendacao ase ecosiac e dic sbc 2018.pdf`
- OK VR_GLS_2D (limiares clinicos Chagas/strain)

## COD 732 — ACC/AHA Valvular 2020
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\diretriz 2020 acc aha guideline management of valvular heart disease.pdf`
- OK VR_GradMedAo

## COD 737 — SBC Valvopatias 2017
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\diretriz atualizacao das diretrizes brasileiras de valvopatias sbc 2017.pdf`
- OK VR_GradMedAo

## COD 758 — ESC Cardiomiopatias 2023
- PDF: `C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia\eco diretriz 2023 esc guidelines for the management of cardiomyopathies.pdf`
- OK VR_GLS_2D: GLS

## Resumo
- Faixas a inserir: **63**
- Refs que passam a ter normalidade: [722, 732, 737, 741, 742, 744, 747, 758, 784, 785, 790]
- Refs a excluir (sem cutoff eco aproveitavel): [716, 724, 725, 726, 728, 730, 731, 733, 734, 735, 736, 738, 739, 740, 743, 745, 746, 748, 749, 750, 751, 752, 754, 755, 756, 757, 762, 763, 764, 765, 767, 786, 787]
- PDFs nao encontrados: []
