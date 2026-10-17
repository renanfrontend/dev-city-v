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
| SUV | `suv.fbx` |
| Táxi | `taxi.fbx` |
| Van | `van.fbx` |
| Viatura | `police.fbx` |
| Blindado SWAT | `truck.fbx` |
| Carro-forte | `delivery.fbx` |

### Exercício 1: trocar um carro (sem programar quase nada)

1. Baixe o Car Kit (link no topo) e abra a pasta `Models/FBX format`.
2. Copie, por exemplo, `suv-luxury.fbx` para `Assets/Resources/Veiculos/`.
3. Em `ModelName`, troque `case VehicleKind.SUV: return "suv";` por `return "suv-luxury";`.
4. Gere o jogo (**Dev City → Gerar jogo para Windows**). Todos os SUVs da cidade agora são o modelo de luxo.

### Exercício 2: um tipo novo (ambulância)

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

1. **Cores variadas**: hoje todo sedã tem a cor da textura. Em `BuildFromModel`, crie uma cópia do material da carroceria (`new Material(...)`) e mude `color` por um tom aleatório só para carros civis.
2. **Peças voando na explosão**: o Car Kit tem `debris-door.fbx`, `debris-bumper.fbx`, `debris-tire.fbx`... Em `VehicleController.Explode()`, crie 4 ou 5 dessas peças com `Rigidbody` e jogue para cima.
3. **Animação de entrar no carro**: na Mixamo há **Entering Car** e **Exiting Car**. Junte com o tutorial de personagens e troque o "deslize" de `PlayerController.EnterRoutine` pela animação.
4. **Moto**: o Kit não tem moto, mas há modelos CC0 na Quaternius. Comece com 2 rodas e um "estabilizador" que mantém a moto em pé (`Body.AddTorque`).

---

**Crédito:** veículos do **Car Kit** por [Kenney](https://www.kenney.nl), licença CC0. A licença original está em `Assets/Resources/Veiculos/LICENCA-Kenney.txt`.
