# Revisão ampliada — 20/10/2026

A revisão Windows isolada em Build/Revisao foi compilada com Unity 6000.6.5f1. O autotest completo de Logs/revisao-completo8.log terminou com **FIM | erros 0 | exceções 0**. Verificou veículos, portas e amassados, dez tipos de personagem na bancada, sobrevivência, história, enigmas, estoque do abrigo, atropelamentos, Protocolo Aurora e controles por toque.

O Protocolo Aurora acrescenta mansões, cães, válvulas com rota alternativa por arrombamento, prontuário, dois estabilizadores e o painel clínico. A equipe extraterrestre é aliada: recolhe civis e captura o experimento humano vivo depois que o jogador reduz sua vitalidade e ativa as três âncoras. O chefe CC0 é Giant Mutant por Eldritch Grim; o cão deriva de uma única variante do Wolf de tomek (CC0).

A pausa teve o texto dos botões corrigido e ganhou foco inicial de teclado. Os rigs próximos usam quatro influências por vértice; o custo diminui à distância. A animação procedural do Carrasco continua durante a investida, com cotovelos, joelhos e coluna articulados. A virada do chefe e o início do ataque foram sincronizados. O teste mediu articulação dos dois chefes durante as investidas e cerca de 65 graus de variação máxima no esqueleto do gigante. Isso não equivale a um sistema completo de ragdoll ou animação com captura própria.

Analógico, câmera por arraste, ações, carrossel e retorno de menus foram preparados para toque. A regressão acionou os handlers do Canvas e confirmou abrir, escolher e equipar. O save usa diretório persistente no Android e PlayerPrefs/IndexedDB na web. **APK e WebGL ainda dependem da instalação/compilação e não estão aprovados em dispositivo.**

Desempenho: a regressão 8 mediu 36,8 FPS com oito ameaças e lanterna, e 43,7 FPS na vista do centro em 720p/HD 4000. Essas amostras ocorreram durante outras atividades de preparação; não substituem os pares alternados. A primeira ampliação teve quedas importantes; foram adicionados descarte visual de detalhes a 95 m, luz do abrigo por vértice no perfil fraco e liberação de recursos das miniaturas. O primeiro par alternado (Logs/fps8-antes.log e fps8-depois.log), com o mesmo save SHA-256 e13ff479480ef5a47d206c8b0467074c2ef9b95dea7764442d546769d973ca31, mediu rua 39,3 → 41,3 FPS e centro 57,4 → 56,6 FPS. A variação precisa ser conferida em novas rodadas; uma única dupla não garante o resultado em toda a cidade.

Build/Teste e a partida Windows aberta do usuário foram preservados. Os testes executam numa saída separada e restauram seu próprio save. O mobiliário e os prédios novos continuam sendo geometria simples; não há promessa de qualidade visual de produção nem desempenho aprovado em celulares ainda.

As informações abaixo registram a revisão histórica anterior; os valores de FPS e o papel do alienígena nela não descrevem a revisão ampliada.

---

# Melhorias — Dev City: Última Noite

Atualização de 20/10/2026. O jogo principal continua sendo a Dev City. Não exige -cidadeClassica. A Vila do Poço é apenas um protótipo arquivado fora de Assets.

## Alterações desta revisão

- História com prólogo e cinco capítulos: despertar no abrigo destruído, procurar Helena e Lia, recuperar energia, resgatar a família de carro, destruir os ninhos, vencer Vigia e Carrasco e desligar o emissor.
- Diário com gravação de Helena, registro do hospital e diagrama do emissor. Os documentos explicam o sinal vindo do céu e sua relação com as aparições e criaturas.
- Três enigmas diferentes: sequência de relés, código de horário e matriz que inverte luzes vizinhas. Pistas, respostas corretas e final são persistidos.
- Desfecho com a família presente no abrigo. Desligar o sinal encerra as ondas e mantém o amanhecer para exploração.
- Recompensas de dinheiro, armas, munição e kits; GPS para o objetivo; persistência dos capítulos e ninhos.
- Chefes com antecipação do golpe, investida que respeita colisão, recuperação vulnerável e reforços ao ficar enfurecidos.
- Alienígena genérico original, com preparação de disparo e projétil visível de trajetória fixa, além de fendas desenhadas com linhas leves.
- Zumbis comuns nas ondas, distintos do monstro grande por escala, cor, velocidade, vida e vulnerabilidade na cabeça. Reaproveitam o modelo licenciado de oscar_creativo, com atribuição.
- Esquiva direcional, transposição de barreiras baixas e salto com tolerância na borda. Corrida de 8,2 m/s e gasto de fôlego.
- Kits portáteis: aplicação com espera, interrupção por movimento/dano, compra e estoque limitado.
- Mira assistida para criaturas, retorno visual de dano orgânico e radar de ameaças próximas.
- Som de perseguição reativo, gemidos, vozes sintetizadas com ressonância/eco, rádio e aviso de investida.
- Casas e prédios residenciais com geometria original e texturas CC0, três LODs, janelas quebradas, telhas e madeira aparente. Árvores secas da Poly Haven e vegetação em tons secos. Alguns prédios especiais e outras estruturas continuam com a arte anterior.
- Três carros estacionados danificados usando a lataria já existente. Portas, amassados, motos e direção não foram revertidos.

## Validação

Regressão completa: Logs/terror-historia5-completo.log — **FIM | erros 0 | exceções 0**, cobrindo a campanha, oito ameaças, os três enigmas, o final, as armas e os sistemas urbanos/veiculares anteriores.

Verificação normal: Logs/terror-historia5-jogo.log — **FIM | erros 0 | exceções 0**, quatro civis, GPS da campanha, três carros abandonados e polícia ausente. A horda com oito ameaças e cenário narrativo mediu **42,4 FPS**, contra 27,1 antes de ajustar sombras/LOD. A revisão final dos controles da abertura e do tipo de carro de resgate foi compilada no build 6 e validada em Logs/terror-historia6-autotest.log e Logs/terror-historia6-jogo.log: ambos **FIM | erros 0 | exceções 0**. A repetição da horda nesse build mediu **43,4 FPS**, pior quadro 117 ms; média das duas medidas posteriores ao ajuste: **42,9 FPS**.

Três pares alternados, mesmo save antes de cada execução e Intel HD 4000 (128 MB), 1280x720:

| Rodada | Rua antes | Rua depois | Centro antes | Centro depois |
|---|---:|---:|---:|---:|
| 1 | 31,7 | 45,3 | 47,6 | 45,4 |
| 2 | 38,5 | 45,0 | 43,6 | 58,2 |
| 3 | 39,7 | 41,7 | 46,6 | 58,4 |
| Média | **36,6** | **44,0** | **45,9** | **54,0** |

Referência: backup etapa4-aprovada, anterior à ampliação de cenário/campanha. A versão nova inclui o perfil de sombras duras/LOD da HD 4000; esta comparação mede o resultado da revisão inteira. Todas as seis execuções terminaram com zero erros e exceções. Logs: Logs/ComparacaoTerror/final-*.log; resumo em resultado-final.json. A primeira série, antes da otimização final, foi preservada em pre-ajuste/.

Há variação entre rodadas e travadas pontuais nos piores quadros; a média não representa uma taxa fixa durante toda a partida.

Os testes de regressão preservam a polícia e população originais somente para testar o jogo urbano anterior; -terrorjogo verifica a configuração normal da campanha. Não confundir a carga dessas duas situações.

## Limites atuais

Os modelos residenciais têm geometria própria e materiais fotográficos. O prólogo tem um pequeno interior em ruínas construído no terreno da casa; os demais prédios não possuem interiores exploráveis. A arte ainda tem mistura de estilos e repetição; esta revisão não equivale a uma cidade totalmente fotorealista. A morte das criaturas usa queda simples, sem ragdoll. A campanha é curta e usa narrativa escrita com áudio sintetizado. O resgate parcial reinicia ao carregar. Algumas animações dos civis ainda apresentam deslizamento de pés em corrida; a regressão registrou até 45%, dentro do limite atual do teste. Chefes incompletos reaparecem à noite após a limpeza do amanhecer. Áudio é síntese própria, sem dublagem.

Na GPU com até 1 GB, o perfil de terror usa sombras duras até 50 m e LOD bias 1, corrigindo o valor 2 herdado da qualidade Ultra.

As texturas usam no máximo 1K, os modelos têm três LODs e o número de ameaças simultâneas é limitado a oito nesta Intel HD 4000. O objetivo é manter a jogabilidade nessa máquina; nenhum resultado de 60 FPS é prometido.

## Arquivos e recuperação

Build principal: Build/DevCityV.exe. Build/Teste e seu save permanecem preservados. Licenças copiadas para Build/Licencas e Build/LICENCA-Terror.txt. Fontes originais e backups anteriores mantidos. Consulte README.md, TUTORIAL-TERROR.md e os arquivos LICENCA-* para atribuições e controles.
