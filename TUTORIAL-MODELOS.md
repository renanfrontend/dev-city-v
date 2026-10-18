# Tutorial: cenário, armas e NPCs com modelos 3D grátis

Este tutorial explica como o Dev City V troca caixas por modelos 3D de verdade, sem pagar nada e sem perder desempenho na Intel HD 4000. Ele cobre o cenário, as armas e os NPCs. Para os carros e as motos, veja o [TUTORIAL-VEICULOS.md](TUTORIAL-VEICULOS.md).

| Parte | De onde veio | Licença | Ferramenta no Unity |
|---|---|---|---|
| Prédios, casas, galpões, árvores, caçambas | City Kits da [Kenney](https://kenney.nl) | CC0 | (nenhuma, entram direto em `Resources/Cidade`) |
| Armas | Ultimate Guns Pack da [Quaternius](https://quaternius.com) | CC0 | **Dev City → Preparar armas (.glb)** |
| NPCs | Ultimate Modular Men/Women da Quaternius | CC0 (2 são CC-BY) | **Dev City → Preparar NPCs (.glb)** |

> Regra de ouro: **leia a licença**. CC0 pode tudo. CC-BY pode tudo **desde que dê crédito**. No nosso jogo o crédito aparece no rodapé da tela de título (`HUD.BuildTitle`).

---

## Parte 1 — Cenário (City Kits da Kenney)

**Onde:** `Assets/Resources/Cidade/Comercial`, `Suburbio`, `Industrial` e `Ruas`. Cada pasta tem seus `.fbx` e uma `Textures/colormap.png`.

**Como funciona:** o `CityGenerator` continua decidindo **onde** vai cada prédio (centro, bairros, casas, porto). Só a parte de **desenhar** mudou: `World/KitModels.cs` escolhe um modelo, ajusta o tamanho e vira a frente para a rua.

1. **Escala com limite.** Os modelos da Kenney são pequenos (um prédio tem ~1 unidade). Se esticarmos para caber em qualquer terreno, as janelas ficam gigantes. Então cada tipo tem uma faixa de escala, por exemplo de 10× a 16× para prédios comerciais. O código sorteia um modelo que caiba nessa faixa (`KitModels.PlaceFitting`).
2. **Frente para a rua.** `FaceStreet` olha qual lado do terreno está mais perto da rua e gira o prédio para lá. Se as portas aparecerem viradas para o fundo, mude `KitModels.FrontYaw` (0 ou 180).
3. **Um material por kit.** Cada `.fbx` vem com a sua cópia do material da paleta. O código troca todas por uma só (`ShareMaterial`).
4. **Static batching.** No fim, `KitModels.CombineAll` junta os 600+ modelos parados em poucos "pacotes" para a placa de vídeo. É por isso que a cidade 3D roda **mais rápido** que a de caixas. Para funcionar, os modelos precisam de **Read/Write**, que `Editor/VehicleImportSettings.cs` liga sozinho para essa pasta.

**Exercício:** abra **Dev City → Medir modelos da cidade**. O Console mostra o tamanho de cada modelo. Depois, em `CityGenerator.MidriseKit`, troque `10f, 16f` (escala mínima e máxima) por `14f, 20f` e veja os prédios ficarem maiores.

---

## Parte 2 — Armas (Quaternius)

**Onde:** os originais `.glb` ficam em `Assets/Armas/Originais`. A ferramenta gera os prefabs em `Assets/Resources/Armas`.

A ferramenta `Editor/WeaponImporter.cs` usa o mesmo leitor de `.glb` das motos (`Editor/GlbReader.cs`) e:

1. deita a arma no eixo Z e descobre **qual ponta é o cano**: a mais "fina" (a coronha e o cabo são mais altos);
2. ajusta o **tamanho real**: pistola com 21 cm, fuzil com 90 cm (tabela `Sizes`);
3. põe a **origem no cabo**, onde a mão segura (tabela `Grips`);
4. cria o ponto **"Cano"**, de onde saem o tiro e o clarão.

No jogo, cada `WeaponDef` (em `Combat/Weapons.cs`) tem um campo `File`, por exemplo `"arma-pistola"`. O `Humanoid` cria o modelo na mão direita na primeira vez e depois só liga e desliga.

**Exercício — adicionar o revólver:**
1. No [pacote de armas](https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F), abra um "Revolver" e baixe em GLB como `Assets/Armas/Originais/arma-revolver.glb`.
2. Rode **Dev City → Preparar armas (.glb)**. As tabelas já têm a entrada `"revolver"`.
3. Em `Weapons.cs`, troque o `File` da pistola para `"arma-revolver"` e gere o jogo.

---

## Parte 3 — NPCs (Quaternius + glTFast)

**Onde:** `Assets/Resources/NPCs/npc-*.glb`. Os NPCs são **animados** (esqueleto e 24 animações dentro do arquivo), então o nosso leitor simples não serve. Quem importa é o pacote oficial **glTFast** (`com.unity.cloud.gltfast`, no `Packages/manifest.json`).

O truque: **todos os personagens dos pacotes "Ultimate Modular" usam o mesmo esqueleto** (62 ossos com os mesmos nomes). Então:

1. `Editor/NpcImporter.cs` copia 8 animações de um personagem só (parado, andar, correr, mirar, soco, morte, encolhido, mãos para cima) para `Resources/NPCs/Animacoes`, com o "loop" certo. O glTFast marca todas como repetição, até a de morte.
2. Monta **um** Animator Controller (`NpcAnimator.controller`) com os **mesmos parâmetros** do jogador (`Speed`, `Aim`, `HandsUp`, `Cower`, `Punch`). Assim o `Humanoid.cs` funciona igual para o Trevor e para um pedestre.
3. `Characters/NpcModels.cs` tem as listas de modelos por tipo: civis, polícia e gangue. O `Outfit.Random()` sorteia um modelo.

Diferenças do esqueleto da Mixamo:
- É **genérico**, não "Humanoid" da Unity. Por isso os ossos são achados pelo nome (`Wrist.R` para a mão, `Chest` para o peito).
- **Não tem animação de sentar.** Por isso, dentro do carro o motorista continua sendo o boneco de blocos; quando ele sai, vira o pedestre 3D.

### Desempenho (o mais importante)

Na primeira versão, os NPCs 3D derrubaram o jogo de ~40 para ~25 FPS. Para descobrir a causa com certeza, criamos dois modos no autotest:
- `-npcblocos`: roda com os bonecos de blocos;
- `-fpsonly`: só mede o FPS por 10 s e fecha.

Rodando cada versão duas vezes, alternando, a comparação deixou claro que eram os NPCs. O que resolveu:

| Otimização | Onde |
|---|---|
| Materiais PBR do glTF trocados pelo `Standard` | `NpcModels.SimplifyMaterials` |
| NPC sem sombra | `Humanoid.BuildRigged` |
| Animação 1 vez a cada 4 quadros a mais de 30 m | `Humanoid.ThrottleFarAnimation` |
| Escondido (e sem animar) a mais de 70 m | `Humanoid.ThrottleFarAnimation` |
| Cada vértice segue 2 ossos em vez de 4 (GPU fraca) | `GameManager` (`QualitySettings.skinWeights`) |

Resultado: ~31–36 FPS com NPCs 3D, contra ~39–41 com blocos.

**Exercício:** mude o `70f` de `ThrottleFarAnimation` para `50f` e meça com `DevCityV.exe -autotest -fpsonly`. Vale a pena?

---

**Créditos:** City Kits e Car Kit por Kenney (CC0). Ultimate Guns Pack e Ultimate Modular Men/Women por Quaternius (CC0; os modelos "Suit" e "Worker" femininos são CC-BY 3.0). As listas completas estão em `Assets/Resources/Cidade/LICENCA-Kenney.txt`, `Assets/Armas/Originais/LICENCA-Armas.txt` e `Assets/Resources/NPCs/LICENCA-NPCs.txt`.
