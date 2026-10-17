# Tutorial: carros 3D de verdade no Dev City V

Os carros do jogo usam o **Car Kit da Kenney** ([kenney.nl](https://www.kenney.nl) / [OpenGameArt](https://opengameart.org/content/car-kit)), um pacote gratuito com mais de 45 veículos low-poly.

A licença é **CC0 (domínio público)**: pode usar, mudar e publicar à vontade, até no GitHub público e em jogos comerciais. Por isso os modelos **estão no repositório**, ao contrário dos personagens da Mixamo.

---

## Parte 1 — Como funciona (a ideia principal)

O carro tem **duas partes separadas**:

| Parte | O que faz | Arquivo |
|---|---|---|
| **Física** | Peso, motor, suspensão, atrito dos pneus, dano | `Vehicles/VehicleController.cs` |
| **Visual** | O desenho 3D (carroceria e rodas) | o modelo `.fbx` em `Assets/Resources/Veiculos/` |

A física não sabe se o carro é uma caixa ou um modelo bonito. Ela só precisa saber **onde estão as 4 rodas** e **o tamanho do carro**. Quem faz essa ponte é a função `BuildFromModel` em `Vehicles/VehicleFactory.cs`:

1. **Carrega o modelo** com `Resources.Load("Veiculos/sedan")`.
2. **Acha as rodas** pelos nomes: tudo que tem `wheel` no nome é roda, e quem tem `front` é roda da frente.
3. **Vira o carro** se ele estiver de costas (a frente precisa apontar para +Z, o "para a frente" do Unity).
4. **Ajusta a escala** pela largura do carro e encosta as rodas no chão (y = 0).
5. **Mede cada roda** (centro e raio) e coloca cada uma dentro de dois "pais":
   - `RodaX` (gira para os lados, ou seja, esterça);
   - `Giro` (gira para a frente, ou seja, a roda rolando).
6. **Cria o colisor** (a caixa invisível que bate nas coisas), os faróis, as lanternas e o giroflex das viaturas.

Se o modelo não existir ou não tiver 4 rodas separadas, o jogo usa o carro de caixas antigo, que funciona sempre.

> Abra `VehicleFactory.cs`, procure `BuildFromModel` e leia os comentários. Cada passo acima está marcado lá.

---

## Parte 2 — Que modelo é cada carro

Fica na função `ModelName`, em `VehicleFactory.cs`:

| Tipo no jogo | Arquivo do modelo |
|---|---|
| Sedã | `sedan.fbx` |
| Esportivo (Fúria GT) | `sedan-sports.fbx` |
| SUV | `suv-luxury.fbx` |
| Táxi | `taxi.fbx` |
| Van | `van.fbx` |
| Viatura | `police.fbx` |
| Blindado SWAT | `truck.fbx` |
| Carro-forte | `delivery.fbx` |
| Ambulância | `ambulance.fbx` |

### Exercício 1: trocar um carro (sem programar quase nada) ✅ feito: os SUVs agora são o `suv-luxury`

1. Baixe o Car Kit (link no topo) e abra a pasta `Models/FBX format`.
2. Copie, por exemplo, `suv-luxury.fbx` para `Assets/Resources/Veiculos/`.
3. Em `ModelName`, troque `case VehicleKind.SUV: return "suv";` por `return "suv-luxury";`.
4. Gere o jogo (**Dev City → Gerar jogo para Windows**). Todos os SUVs da cidade agora são o modelo de luxo.

### Exercício 2: um tipo novo (ambulância) ✅ feito: `VehicleKind.Ambulance`, com 4% de chance no trânsito

1. Copie `ambulance.fbx` para `Assets/Resources/Veiculos/`.
2. Em `VehicleController.cs`, adicione `Ambulance` no `enum VehicleKind`.
3. Em `VehicleFactory.cs`:
   - no dicionário `specs`, crie a ficha dela (nome, peso, motor, velocidade máxima...);
   - em `ModelName`, adicione `case VehicleKind.Ambulance: return "ambulance";`.
4. Para ela aparecer no trânsito, coloque uma chance em `RandomCivilianKind()`.

---

## Parte 3 — Deixando o carro "do seu jeito" (só números)

Tudo fica no dicionário `specs` de `VehicleFactory.cs`:

| Campo | O que muda | Dica |
|---|---|---|
| `Mass` | Peso (kg) | Mais pesado = mais estável e empurra os outros |
| `Engine` | Força do motor (newtons) | Mais força = arrancada mais rápida |
| `MaxSpeed` | Velocidade máxima (m/s; × 3,6 = km/h) | 47 m/s ≈ 170 km/h |
| `Grip` | Aderência dos pneus | Abaixo de 1,4 o carro derrapa fácil; acima de 1,8 gruda no chão |
| `AWD` | Tração nas 4 rodas | `true` sai melhor das curvas |
| `Health` | Resistência a batidas e tiros | O carro-forte tem 3500 |

> As medidas (comprimento, largura, altura, raio da roda) **vêm do modelo**, então não adianta mexer nelas na tabela quando o carro usa modelo 3D.

---

## Parte 4 — Usando modelos de outros sites

Qualquer modelo serve, desde que siga 3 regras:

1. **Rodas separadas**: cada roda é um objeto próprio, com `wheel` no nome.
2. As rodas da frente têm `front` no nome, por exemplo `wheel-front-left`. As de trás podem ter `back` ou `rear`.
3. Formato **FBX** (ou outro que o Unity importe), colocado em `Assets/Resources/Veiculos/`.

Se o modelo veio com outros nomes, renomeie as rodas no Blender (ou peça para o modelo ser exportado assim).

Fontes gratuitas boas: **Kenney** (CC0), **Quaternius** (CC0) e **Poly Pizza** (confira a licença de cada modelo).

> Sempre leia a licença. CC0 pode tudo. "CC-BY" exige dar crédito ao autor no jogo. Licenças "editorial" ou "não comercial" não servem para um jogo publicado.

---

## Parte 5 — Desafios

1. **Cores variadas** ✅ feito em `Vehicles/CarPaint.cs`.
   A ideia simples (multiplicar uma cor no material) **não funciona bem aqui**: os carros da Kenney já são coloridos, e verde × azul vira quase preto. O jeito que usamos:
   - a textura `colormap.png` é uma **paleta** de colunas com degradê, e a lataria de cada carro usa uma coluna;
   - o código lê as coordenadas de textura (UV) da malha `body` e acha a coluna **mais usada**, que é a da lataria;
   - cria 10 cópias da paleta (uma por cor) trocando só essa coluna, mas mantendo o degradê de luz e sombra;
   - cada carro civil sorteia uma das 10 versões.
   Para o código conseguir ler a malha e a textura, elas precisam de **Read/Write**. Quem liga isso é `Assets/Editor/VehicleImportSettings.cs`, um **AssetPostprocessor**: código que roda sozinho toda vez que o Unity importa um arquivo daquela pasta.
2. **Peças voando na explosão** ✅ feito em `VehicleController.SpawnDebris()`. As peças ficam em `Assets/Resources/Veiculos/Destrocos/`. Cada explosão solta de 5 a 6 peças queimadas, com `Rigidbody` e `BoxCollider`, que somem depois de 25 s. Experimente mudar a quantidade (`Random.Range(5, 7)`) e a força (`Random.Range(5f, 11f)`).
3. **Animação de entrar no carro** ✅ feito com **Entering Car** e **Exiting Car** da Mixamo (salvas como `EnterCar.fbx` e `ExitCar.fbx` em `Assets/Mixamo/Animacoes/`).
   - O `MixamoImporter` reconhece os nomes (`enter` = entrar, `exit` = sair), cria os estados `EntrandoNoCarro` e `SaindoDoCarro` no Animator e acelera as duas animações (~5,5 s viram ~3,4 s, perto do GTA).
   - **O problema:** a animação move o *corpo* (anda até o banco e senta), mas o objeto do jogador fica parado. Se começar no lugar errado, ele senta fora do carro.
   - **A solução:** o importador mede quanto o corpo anda e gira (entrar: 1,8 m para a frente e 95° para a esquerda) e grava em `Resources/Personagens/AcoesCarro.json`. O jogo (`CarActions.cs`) faz a conta ao contrário: "para terminar no banco, olhando para a frente do carro, tenho que começar 1,8 m antes, girado 95°". Esse é o ponto da porta onde o jogador para.
   - Durante a animação o carro fica freado e o jogador sobe do chão até a altura do banco. No fim, ele "gruda" no banco. O log `[Carro] ... o quadril pulou X m` mostra o tamanho do "pulo" (hoje ~0,2 m ao entrar e 0,04 m ao sair).
   - Só a saída calma com **F** usa a animação. Pular do carro andando, carro destruído ou afundando e a troca de personagem saem na hora.
   - Experimente mudar a velocidade das animações em `MixamoImporter.BuildController` (os números `1.6f` e `1.8f`) e rodar **Dev City → Preparar personagens Mixamo** de novo.
4. **Moto** ✅ feito: a **Raio 600** (`VehicleKind.Moto`), com 4% de chance no trânsito.
   - O visual é montado com caixas e cilindros em `Vehicles/MotoBuilder.cs`. Ainda não há um modelo 3D CC0 de moto na Kenney nem na Quaternius.
   - **2 rodas no meio:** a física é a mesma dos carros, só que usa as rodas 0 (frente) e 2 (trás). A suspensão é 2× mais dura, porque 2 rodas seguram o peso que 4 seguravam.
   - **O estabilizador:** com as rodas no meio, nada segura a moto de lado. `VehicleController.Balance()` calcula uma inclinação-alvo (em pé quando parada, deitada para dentro nas curvas, até 40°) e usa `Body.AddTorque` como uma **mola com amortecedor**: força proporcional ao erro (`err * 60`) menos a velocidade de giro (`rollVel * 9`). Sem o amortecedor a moto balança de um lado para o outro. Teste trocar o 9 por 1!
   - Se a moto tombar mesmo assim (`up.y < 0,4`), o piloto cai no chão.
   - Na moto o jogador monta rápido, sem a animação de entrar no carro.

---

**Crédito:** veículos do **Car Kit** por [Kenney](https://www.kenney.nl), licença CC0. A licença original está em `Assets/Resources/Veiculos/LICENCA-Kenney.txt`.
