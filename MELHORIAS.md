# Onde trabalhar para deixar o Dev City V mais parecido com o GTA V

Este guia está em ordem de **impacto na sensação de "estou jogando GTA"**. Cada item diz *onde* mexer no código.
Os números de ajuste rápido estão no fim.

### Resultado do teste neste PC

No Intel HD Graphics 4000 (128 MB), a execução com imagem ficou em cerca de 27–31 FPS no título, 36–40 FPS a pé, 38 FPS na perseguição e 28 FPS à noite. O teste automático chegou ao fim com 4 viaturas, 1 helicóptero e nenhum erro ou exceção em uma execução completa. São medidas de um percurso automatizado curto, não de uma sessão de jogo manual. Antes de adicionar efeitos gráficos caros, mantenha uma opção de qualidade baixa e compare o FPS nas mesmas cenas.

---

## 1. Personagens e animação (o maior salto visual)

Hoje o boneco é feito de blocos e animado por código (`Characters/Humanoid.cs`).

- **Trocar por um modelo humanoide com Animator.** Use personagens e animações gratuitos da [Mixamo](https://www.mixamo.com) (idle, walk, run, sprint, jump, aim pistol/rifle, shoot, punch, hands up, sit in car, enter car).
  Crie um `Animator Controller` com parâmetros `Speed`, `Aim`, `HandsUp`, `Sitting`, `Cower` e faça o `Humanoid.cs` apenas repassar esses valores (a interface pública dele já tem exatamente esses campos, então o resto do jogo não muda).
- **IK de mira e dos pés** (`OnAnimatorIK`): a arma aponta exatamente para a mira e os pés acompanham a calçada.
- **Ragdoll de verdade** no lugar do "Knock" (`Ped.Knock`, `PlayerController.Knock`): GameObject → 3D Object → Ragdoll… no modelo e ative ao morrer/ser atropelado. É o que dá aquele "voo" característico do GTA.
- **Animação de entrar no carro** (abrir porta, puxar o motorista): hoje é um deslize de 0,3 s em `PlayerController.EnterRoutine`.

## 2. Veículos

Arquivos: `Vehicles/VehicleController.cs` (física) e `Vehicles/VehicleFactory.cs` (modelos e números de cada carro).

- **Modelo de pneu melhor**: hoje o atrito lateral é "cancelar a velocidade lateral até um limite". Um modelo por *slip angle* (Pacejka simplificado) deixa drift e perda de traseira bem mais parecidos com o GTA.
- **Câmbio e curva de torque** (marchas com som), transferência de peso na frenagem, ABS/controle de tração por carro.
- **Dano visual**: deformar a malha no ponto da batida (`OnCollisionEnter`), soltar para-choque/capô/portas, vidro quebrando.
- **Modelos 3D de verdade** (low-poly gratuitos: Kenney "Car Kit", Quaternius). Basta montar o carro na `VehicleFactory` com o modelo no lugar das caixas, mantendo as rodas como filhas de `wheelPivot/wheelSpin`.
- **Novos tipos**: moto (equilíbrio e queda do piloto), helicóptero/avião pilotável (`IsAircraft` já existe como gancho), barco (o mar já existe).
- **Rádio do carro** com estações (músicas royalty-free) ao entrar no veículo.

## 3. Gráficos

- **Migrar para URP** (Universal Render Pipeline) e ligar o pós-processamento: *bloom* (neons e faróis), *color grading* com o tom quente de Los Santos, *SSAO*, *motion blur* leve e *vignette*.
  Atenção: os materiais são criados com `Shader.Find("Standard")` em `Core/Mats.cs`; será preciso criar e atribuir o asset URP nas configurações de Graphics, trocar os shaders dos materiais e configurar luzes, câmera e volumes.
  Nesta GPU, faça primeiro uma versão sem SSAO e motion blur; o teste noturno já caiu para cerca de 28 FPS.
- **Prédios modulares**: substituir as caixas de `World/CityGenerator.cs` (`Building`, `BuildDowntown`, `BuildMidrise`, `BuildHouses`) por peças de kits modulares, com térreo de lojas, toldos, letreiros e telhados detalhados.
- **Detalhes de rua**: hidrantes, lixeiras, bancas, pontos de ônibus, placas, árvores com sombra, carros estacionados em vagas pintadas. Use `MeshBuilder`/GPU instancing para manter o desempenho.
- **Cidade menos "grade"**: ruas curvas, avenidas largas com canteiro, viadutos e rodovia (Splines do Unity 6), morros com o sistema de **Terrain**.
- **Água** com shader de ondas e reflexo, **clima** (nublado, chuva com chão molhado e menos aderência dos pneus), **reflection probes** para os carros brilharem.
- **LOD Groups** e *occlusion culling* para aumentar a distância de visão sem perder FPS.

## 4. NPCs e cidade viva

Arquivos: `Characters/Ped.cs`, `Characters/PedestrianSystem.cs`, `Vehicles/TrafficSystem.cs`.

- **NavMesh** (pacote *AI Navigation*): pedestres e policiais a pé desviando de obstáculos, entrando em becos e subindo escadas (hoje eles andam pelo grafo de esquinas com um desvio simples).
- **Rotinas**: gente falando ao celular, correndo, passeando com cachorro, sentada em bancos, esperando ônibus, fumando na porta da loja.
- **Trânsito**: várias faixas e troca de faixa, semáforos com fases diferentes por cruzamento (hoje a cidade inteira é sincronizada em `DayNightCycle`), ônibus com paradas, motorista que desce para brigar quando você bate no carro dele, ambulância e bombeiros indo até acidentes.
- **Gangues por território**: bairros com cor de gangue que te atacam se você entrar armado.
- **Reações faladas**: frases curtas de áudio (gritos, xingamentos, "Polícia! Parado!").

## 5. Polícia e procurado

Arquivos: `Police/WantedSystem.cs`, `Police/PoliceSystem.cs`, `Police/Helicopter.cs`.

- **Táticas**: manobra PIT, viaturas fechando cruzamentos à frente, tapete de pregos nos bloqueios, policiais atirando de dentro do carro.
- **Memória**: a polícia procura pela **cor e modelo do carro** em que você foi visto (trocar de carro escondido ajuda, como no GTA V).
- **Mais unidades**: NOOSE/SWAT descendo de rapel do helicóptero, barcos da polícia no mar, exército em uma área restrita (como Fort Zancudo).
- **Áudio do rádio da polícia** descrevendo o seu carro e a sua direção.

## 6. Combate e mecânicas

Arquivos: `Combat/Weapons.cs`, `Characters/PlayerController.cs`, `Characters/CameraRig.cs`.

- **Sistema de cobertura** (tecla Q): grudar em paredes e carros, atirar de lado/por cima.
- **Mira em primeira pessoa**, coice com padrão por arma, animação de recarga, acessórios (silenciador muda o nível de procurado, mira, carregador estendido).
- **Habilidade especial** (câmera lenta do Michael/Franklin) usando `Time.timeScale` com uma barra amarela embaixo do radar.
- **Troca de personagem** (os 3 protagonistas): exige separar o estado do jogador (dinheiro, armas, roupa) por personagem.
- **Celular**: contatos, mensagens das missões, chamar táxi, câmera.

## 7. Missões e conteúdo

Arquivo: `Missions/MissionSystem.cs` (cada missão é uma classe com `Begin`/`Tick`).

- **Missões como dados** (ScriptableObjects) com passos reaproveitáveis: "vá até", "roube o carro", "mate", "persiga", "despiste a polícia". Assim dá para criar missões sem programar.
- **Cutscenes** com Timeline + Cinemachine antes e depois das missões, **checkpoints** no meio e **medalhas** (ouro/prata/bronze por tempo, precisão, dano).
- **Atividades**: táxi, ambulância, corridas contra IA, assaltos com planejamento (escolha de equipe), colecionáveis espalhados.
- **Economia**: comprar imóveis que rendem dinheiro, garagem para guardar carros, oficina para tunar (cor, rodas, motor, blindagem).

## 8. Som

Arquivo: `Core/Sfx.cs` (tudo sintetizado hoje).

- Trocar por samples reais (freesound.org, licença CC0): tiros com eco que muda entre prédios e campo aberto, motor em camadas por rotação, pneu cantando, passos por tipo de chão.
- Ambiente por bairro (praia, centro, mato à noite) e trilha dinâmica que cresce durante a perseguição.

## 9. Desempenho e escala

- **Pooling** de pedestres e carros: hoje eles são criados e destruídos (`Ped.Spawn` / `VehicleFactory.Create`). Reaproveitar objetos elimina travadinhas.
- **Streaming do mundo** (cenas aditivas ou Addressables) para uma cidade muito maior.
- **Jobs/Burst** para a IA de trânsito e pedestres quando passar de ~100 agentes.

## 10. Qualidade de vida

- **Novo Input System** com suporte a controle de Xbox/PlayStation (gatilhos para acelerar e frear, como no GTA).
- **Menu de configurações**: sensibilidade, inverter Y, FOV, qualidade gráfica, volume.
- **Vários slots de save** e salvar a posição e o carro (`World/Gps.cs` → `SaveSystem`).

---

## Ajustes rápidos (só mudar números)

| O que | Onde |
|---|---|
| Velocidade, aceleração, aderência e peso de cada carro | `VehicleFactory.cs` → dicionário `specs` |
| Dano, cadência, pente e preço das armas | `Weapons.cs` → `Weapons.Init()` |
| Tempo para perder as estrelas (30/45/60/75/90 s) | `WantedSystem.cs` → `ClearTime` |
| Quantas viaturas/SWAT/helicópteros por estrela | `PoliceSystem.cs` → `carsFor`, `swatFor`, `helisFor` |
| Quantidade de carros e pedestres | `TrafficSystem.TargetMoving`, `PedestrianSystem.TargetDay/TargetNight` |
| Duração do dia | `DayNightCycle.HoursPerSecond` |
| Vida, colete e regeneração do jogador | `PlayerController.cs` → `MaxHealth`, `MaxArmor`, `Update()` |
| Tamanho da cidade e das quadras | `CityGenerator.cs` → `N`, `P`, `ROAD`, `WALK` |
| Recompensas das missões | `MissionSystem.cs` → chamadas de `M.Pass(...)` |

## Referências para implementar os próximos passos

- [Unity 6: WheelCollider](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WheelCollider.html) — suspensão e atrito baseado em deslizamento para revisar a física dos carros.
- [Unity 6: AI Navigation](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ai.navigation.html) — NavMesh, obstáculos dinâmicos e conexões para pedestres e policiais.
- [Unity 6: pós-processamento URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/add-post-processing.html) — configuração de câmera, volumes e efeitos.
- [Wanted Level in GTA V](https://www.grandtheftwiki.com/Wanted_Level_in_GTA_V) — referência para escalada das estrelas, unidades policiais e despiste; é uma wiki de fãs, então ajuste o comportamento pela experiência de jogo.
