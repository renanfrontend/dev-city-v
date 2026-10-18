# Dev City V

Jogo de mundo aberto em 3D para Windows, feito em **Unity 6 + C#**, inspirado nas mecânicas de **Grand Theft Auto V**.
A lógica é toda em código (cidade, trânsito, polícia, missões, sons e interface). Os modelos 3D são gratuitos (Kenney e Quaternius, CC0/CC-BY) e já vêm no repositório; sem eles, o jogo volta para os modelos de caixas.

> Projeto de fã, sem fins comerciais. Nomes, marcas, armas e lugares são fictícios.
> Veículos: Car Kit por [Kenney](https://www.kenney.nl) (CC0). Motos do [Poly Pizza](https://poly.pizza): "Motorcycle" por Poly by Google e "Vespa" por Jasmine Roberts (CC-BY 3.0), "Cartoony Purple Motorcycle" por AliceCassie (CC0). Detalhes em `Assets/Veiculos/MotosOriginais/LICENCA-Motos.txt`.
> Cidade: City Kits por Kenney (CC0). Armas e NPCs: Ultimate Guns Pack e Ultimate Modular Men/Women por [Quaternius](https://quaternius.com) (CC0; os modelos femininos "Suit" e "Worker" são CC-BY 3.0). Listas em `Assets/Resources/Cidade/LICENCA-Kenney.txt`, `Assets/Armas/Originais/LICENCA-Armas.txt` e `Assets/Resources/NPCs/LICENCA-NPCs.txt`.

## Como abrir

1. Instale o **Unity Hub** e o **Unity 6** (testado com `6000.6.5f1`) e ative a licença Personal no Hub.
2. No Hub: **Add → Add project from disk** → escolha a pasta `C:\Projetos\DevCityV`.
3. Abra a cena `Assets/Scenes/DevCity.unity` e aperte **Play**. A cena e as configurações do projeto já foram preparadas.

## Como gerar o .exe

- No editor: **File → Build Profiles → Windows → Build**. O jogo já tem uma cena incluída no perfil de build.
- Pela linha de comando:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe" -batchmode -quit -projectPath "C:/Projetos/DevCityV" -executeMethod DevCity.EditorTools.BuildTools.BuildWindows -logFile build.log
```

## Controles

| Tecla | A pé | No carro |
|---|---|---|
| WASD | andar | acelerar / frear / ré / virar |
| Mouse | câmera | câmera (volta sozinha para trás) |
| Shift | correr (gasta estamina) | — |
| Espaço | pular | freio de mão (derrapar) |
| F | entrar no carro / roubar | sair (andando rápido = pular do carro) |
| Botão direito | mirar (mira assistida) | mirar (drive-by com arma curta) |
| Botão esquerdo | atirar / socar | atirar |
| R | recarregar | recarregar |
| Tab (segurar) | roda de armas (câmera lenta) | roda de armas |
| 1–7 / rodinha | trocar arma | trocar arma |
| E | interagir (lojas, casa) | buzina |
| H / G | — | faróis / sirene (viatura) |
| M | mapa (clique marca destino no GPS) | mapa |
| Alt (segurar) | trocar entre Trevor e Jacob | trocar entre Trevor e Jacob |
| Esc | pausa | pausa |

## O que tem no jogo

- **Dois protagonistas, Trevor e Jacob**: cada um com dinheiro, armas, roupa e casa próprios. Segure **Alt** para trocar, com a câmera do céu do GTA V. Os modelos podem vir da Mixamo: veja o [TUTORIAL-PERSONAGENS.md](TUTORIAL-PERSONAGENS.md).
- **Carros 3D do Car Kit da Kenney** (CC0): sedã, esportivo, SUV, táxi, van, viatura, blindado e carro-forte, com rodas de verdade girando e esterçando. Veja o [TUTORIAL-VEICULOS.md](TUTORIAL-VEICULOS.md).

- **Cenário, armas e NPCs 3D**: prédios, casas, galpões e contêineres dos City Kits da Kenney; 5 armas 3D da Quaternius na mão dos personagens; 15 tipos de NPC animados (executivos, operários, punks, SWAT...). Veja o [TUTORIAL-MODELOS.md](TUTORIAL-MODELOS.md).
- **Cidade aberta**: 10×10 quadras com centro de arranha-céus, prédios médios, bairro de casas com piscina, porto com contêineres, praças, praia e mar (dá para nadar), semáforos, postes, ciclo de dia e noite (1 minuto do jogo = 2 s, como no GTA V) com janelas acendendo à noite.
- **Personagem em 3ª pessoa**: corre com estamina, pula, nada, leva dano de queda, vida de 200 que regenera até 50%, colete.
- **Veículos com física**: 9 carros (sedã, esportivo, SUV, táxi, van, viatura, blindado SWAT, carro-forte, ambulância) e 5 motos (Raio 600, Café Racer 250, Lobo Chopper, Roxinha e a scooter Vespina 50; 2 rodas, deitam nas curvas), suspensão, derrapagem no freio de mão, dano, fumaça, fogo, explosão com destroços, faróis, sirene. Entrar e sair com as animações da Mixamo, roubar com motorista dentro, pular do carro andando.
- **Cidade viva**: pedestres que andam pelas calçadas, atravessam na faixa, fogem de tiros, se encolhem, levantam as mãos, ligam para a polícia e deixam dinheiro cair; trânsito que respeita semáforo, buzina, desvia e foge.
- **Nível de procurado (1–5★)**: polícia com cone de visão; estrelas piscam quando ela te perde de vista; some em 30/45/60/75/90 s; helicóptero com atirador, bloqueios e SWAT nos níveis altos; WASTED e BUSTED com hospital, delegacia e multas.
- **Combate**: 7 armas fictícias, mira assistida, tiro na cabeça, granadas, soco, drive-by.
- **Lojas**: arsenal, roupas, mercadinho (comprar ou assaltar), Pinta & Foge (repinta e despista a polícia), casa (salvar e dormir).
- **Missões**: 5 missões de história com contatos (Dudu, Dona Neide, Gordo, Professor) e o Racha da Orla (contra o relógio).
- **Interface estilo GTA V**: radar que gira com a câmera e mostra a rota do GPS, mapa grande com destino, estrelas, dinheiro, arma, legendas, "MISSÃO CUMPRIDA".

## Estrutura do código

```
Assets/Scripts
├─ Core/        GameManager (monta tudo), Mats (materiais/texturas), Gen (malhas e sprites), Sfx (sons), Fx (efeitos, explosão, pickups)
├─ World/       CityGenerator (cidade + grafo de ruas + textura do mapa), DayNightCycle, Gps/Marker/SaveSystem, ShopSystem
├─ Characters/  PlayerController, CameraRig, Humanoid (boneco animado), Ped (NPC), PedestrianSystem
├─ Vehicles/    VehicleController (física), VehicleFactory (modelos), TrafficSystem (+ IA do trânsito)
├─ Police/      WantedSystem, PoliceSystem (+ IA das viaturas), Helicopter
├─ Combat/      Weapons (armas, mira, tiro, granada)
├─ Missions/    MissionSystem e as missões
└─ UI/          HUD, MinimapSystem
Assets/Editor/BuildTools.cs   configuração e build automático para Windows
```

O guia de onde melhorar cada parte está em [MELHORIAS.md](MELHORIAS.md).

O progresso é salvo em `DevCityV_Save.json` ao lado do executável. Para distribuir o jogo, copie a pasta `Build` inteira, não apenas o `.exe`.
