# Tutorial: Trevor e Jacob com personagens da Mixamo

Neste tutorial você vai trocar os bonecos de blocos por personagens 3D de verdade, baixados de graça da **Mixamo** (da Adobe), e entender o que acontece em cada etapa.

O jogo já está pronto para isso:

- **Troca de protagonista**: segure **Alt**, mova o mouse para o lado do personagem e solte. A câmera sobe para o céu e desce no outro, como no GTA V.
- **Cada um tem a própria vida**: dinheiro, vida, colete, armas, roupa e casa (Trevor na Vila Rosa, letra **C** no radar; Jacob no Porto Dev, letra **J**).
- Sem os arquivos da Mixamo, os dois continuam como bonecos de blocos. Quando você colocar os modelos, eles aparecem sozinhos.

> Não dá para trocar durante uma missão nem com a polícia atrás de você, igual no GTA V.

---

## Parte 1 — Entendendo as peças (5 minutos de teoria)

| Palavra | O que é | No nosso jogo |
|---|---|---|
| **Modelo (mesh)** | A "pele" 3D do personagem | `Trevor.fbx`, `Jacob.fbx` |
| **Rig / esqueleto** | Os ossos dentro do modelo que fazem ele se mexer | A Mixamo coloca sozinha |
| **Humanoid** | Um "padrão de esqueleto humano" do Unity | Faz **qualquer** animação servir em **qualquer** personagem |
| **Avatar** | O mapa que liga os ossos do modelo ao padrão Humanoid | Criado pela ferramenta |
| **Animation clip** | Um movimento gravado (andar, correr...) | Os `.fbx` sem pele |
| **Animator Controller** | O "cérebro" que escolhe qual animação tocar | `DevCityAnimator.controller` |
| **Blend tree** | Mistura animações por um número | Parado → andar → correr → disparada pela **velocidade** |
| **Camada com máscara** | Animação só em parte do corpo | Mirar e socar **só nos braços**, enquanto as pernas andam |

O código (`Characters/Humanoid.cs`) só manda números para o Animator: `Speed` (velocidade), `Aim` (mirando), `HandsUp`, `Sitting` (no carro), `Cower` (com medo) e `Punch` (soco). Quem escolhe a animação é o Animator.

---

## Parte 2 — Baixando da Mixamo

### 2.1 Entrar

1. Abra **https://www.mixamo.com** e clique em **Log in**.
2. Entre com uma conta **Adobe** (é grátis; se não tiver, crie uma).

### 2.2 Baixar os dois personagens

1. Clique na aba **Characters**.
2. Escolha um personagem para o **Trevor** (sugestão: homem mais velho, cara de durão, roupa simples) e clique nele.
3. Clique em **Download** e use exatamente:
   - **Format:** `FBX for Unity(.fbx)`
   - **Pose:** `T-pose`
4. Salve o arquivo como **`Trevor.fbx`** em:
   `C:\Projetos\DevCityV\Assets\Mixamo\Personagens\`
5. Repita para o **Jacob** (sugestão: jovem de moletom/tênis) e salve como **`Jacob.fbx`** na mesma pasta.

> Quer usar um personagem seu (feito no Blender, por exemplo)? Use **Upload Character** na Mixamo: ela coloca o esqueleto sozinha (*auto-rigger*).

### 2.3 Baixar as animações

Com qualquer personagem selecionado, vá na aba **Animations**, procure cada animação da tabela, clique nela e depois em **Download** com:

- **Format:** `FBX for Unity(.fbx)`
- **Skin:** `Without Skin` (só o movimento, o arquivo fica pequeno)
- **Frames per Second:** `30`

Na tela da animação, marque **In Place** nas de andar e correr. Sem isso, o personagem "anda sozinho" para fora do lugar e escorrega.

| Para quê | Procure na Mixamo | Salve como | In Place |
|---|---|---|---|
| Parado | `Idle` | `Idle.fbx` | — |
| Andar | `Walking` | `Walking.fbx` | ✔ |
| Correr | `Running` | `Running.fbx` | ✔ |
| Disparada (Shift) | `Fast Run` | `Sprint.fbx` | ✔ |
| Mirar | `Pistol Idle` | `Aim.fbx` | — |
| Mãos para cima | `Surrender` | `HandsUp.fbx` | — |
| Dirigindo | `Driving` ou `Sitting` | `Sitting.fbx` | — |
| Com medo | `Terrified` | `Cower.fbx` | — |
| Soco | `Punching` | `Punch.fbx` | — |

Salve todas em: `C:\Projetos\DevCityV\Assets\Mixamo\Animacoes\`

> **O nome do arquivo é o que importa.** A ferramenta descobre o uso de cada animação pelo nome (`Idle`, `Walk`, `Run`, `Sprint`, `Aim`, `Hands`, `Sit`, `Cower`, `Punch`). Se não achar exatamente a animação da tabela, escolha uma parecida e salve com o nome da tabela.
>
> As obrigatórias são **Idle, Walking e Running**. As outras deixam o jogo mais bonito, mas são opcionais.

---

## Parte 3 — Preparando no Unity

1. Abra o **Unity Hub** → **Projects** → abra o **DevCityV**.
2. Espere o Unity importar os arquivos novos (barra de progresso no canto inferior direito).
3. No menu do topo, clique em **Dev City → Preparar personagens Mixamo**.
4. Abra a janela **Console** (menu *Window → General → Console*) e confira as mensagens `[Mixamo]`:
   - `Animação "Walking" → andar`: a animação foi reconhecida.
   - `Personagem "Trevor" pronto em Assets/Resources/Personagens/Trevor.prefab`: deu certo.
   - `Faltando (opcional): ...`: animações que você ainda não baixou.

### O que a ferramenta fez por você (para aprender)

Abra o arquivo `Assets/Editor/MixamoImporter.cs` e acompanhe:

1. **`ConfigureCharacter`**: marca o FBX como **Humanoid** e cria o **Avatar**. É o mesmo que, no Inspector do FBX, ir na aba **Rig → Animation Type: Humanoid → Apply**.
2. **`ConfigureAnimation`**: liga o **Loop Time** e "trava" o movimento da raiz (*Bake Into Pose*). É o mesmo que a aba **Animation** do Inspector.
3. **`BuildController`**: cria o **Animator Controller**. Dê dois cliques em `Assets/Resources/Personagens/DevCityAnimator.controller` para ver:
   - camada **Base Layer**: o blend tree **Movimento** e os estados **Sentado**, **MaosParaCima** e **Encolhido**;
   - camada **Bracos**: com a máscara `MascaraBracos`, toca **Mirando** e **Soco** só da cintura para cima.
4. **`MakePrefab`**: junta modelo + Avatar + Animator num prefab dentro de `Resources/Personagens`. O jogo carrega o prefab pelo nome com `Resources.Load("Personagens/Trevor")`.

> Faça você mesmo uma vez, no Inspector, os passos 1 e 2 em um FBX. Depois de entender, deixe a ferramenta repetir para os outros.

---

## Parte 4 — Jogando

1. Menu **Dev City → Gerar jogo para Windows** (ou aperte **Play** no editor, com a cena `Assets/Scenes/DevCity.unity` aberta).
2. Abra `C:\Projetos\DevCityV\Build\DevCityV.exe`.
3. Aperte **Enter** na tela de título. Você começa como **Trevor**.
4. Segure **Alt**, mova o mouse para a direita (Jacob) e solte.

### Se algo der errado

| Problema | Causa provável | Solução |
|---|---|---|
| Personagem em pose de "T" parado | Falta `Idle.fbx` | Baixe o Idle e rode a ferramenta de novo |
| Personagem "desliza" ou sai do lugar | Faltou marcar **In Place** | Baixe de novo com In Place |
| Personagem cinza, sem textura | Texturas não foram extraídas | Apague `Assets/Mixamo/Personagens/Texturas_*` e rode a ferramenta de novo |
| `não virou um humanoide válido` | FBX baixado em outro formato | Baixe de novo como **FBX for Unity** e **T-pose** |
| Continua com os bonecos de blocos | Prefab não foi gerado ou o nome está diferente | O arquivo tem que se chamar **Trevor.fbx** / **Jacob.fbx** |

---

## Parte 5 — Desafios para treinar

1. **Trocar a roupa do Jacob**: baixe outro personagem da Mixamo, salve como `Jacob.fbx` (substituindo) e rode a ferramenta. Nenhuma linha de código muda.
2. **Animação de pulo**: baixe `Jump`, adicione o papel `"pulo"` na lista `Roles` do `MixamoImporter.cs`, crie um parâmetro `Jump` no controller e chame `anim.SetTrigger("Jump")` no `Humanoid.cs` quando o jogador pular (`PlayerController.OnFoot`, onde fica `vy = 6.2f`).
3. **Pedestres com modelos de verdade**: o `Ped.cs` cria os NPCs com `Humanoid.Create(transform, outfit)`. Baixe mais 3 personagens, gere os prefabs e sorteie um nome com `Humanoid.Create(transform, outfit, "NomeDoPrefab")`.
4. **Terceiro protagonista**: em `CharacterSwitch.Init()` crie um terceiro `CharacterProfile` (dinheiro, roupa, casa) e adicione ao array `Profiles`. Dica: hoje o seletor só escolhe o primeiro ou o último; para 3 personagens, troque a lógica do `wheelX` por 3 faixas.

---

**Créditos:** personagens e animações da [Mixamo](https://www.mixamo.com) (Adobe), de uso livre em projetos pessoais e comerciais conforme os termos da Adobe. Não redistribua os arquivos `.fbx` brutos fora do jogo.
