# Sobrevivência na Dev City

O jogo abre a cidade normalmente em Build/DevCityV.exe. A história usa o mesmo mapa, personagens, carros e motos existentes. O protótipo Vila do Poço continua arquivado.

## O despertar e sua família

Você acorda em um quarto destruído no terreno de sua casa, dentro da cidade. Enter ou Esc permitem concluir a abertura mais cedo. Ouça a gravação de Helena sobre a mesa com E. Sua esposa e sua filha Lia estão vivas. J abre o diário com as pistas que você leu. A história começa com o apagão das 21:40 e leva à descoberta de um emissor alienígena.

## Primeira noite

Selecione a pistola com 2, ligue a lanterna com L e abra M para seguir a rota amarela. Recupere o fusível na oficina segurando E e leve-o ao gerador do abrigo. O painel do gerador pede a ordem dos três relés. A gravação contém a pista. Use 1, 2 e 3 para selecionar os símbolos. Depois de resolver, a instalação inicia a defesa. Defenda a área por 18 segundos e elimine as criaturas próximas ao gerador para concluir. A recompensa inclui dinheiro, SMG, munição e kit médico.

## A fechadura do hospital

Leia o registro junto ao portão e combine-o com a gravação. Digite o código no painel; Enter verifica e Backspace apaga. A resposta está nos documentos, sem depender de adivinhação. Esc permite sair e consultar o diário.

## Resgate motorizado

Use um carro com banco traseiro, como sedan, SUV, táxi ou van; esportivos de dois lugares e motos não levam Helena e Lia. Pare junto à pessoa marcada e segure E. Depois do primeiro embarque, você tem 150 segundos para buscar a segunda pessoa e entregar ambas ao abrigo. Os passageiros aparecem dentro do veículo. Trocar de carro exige voltar ao carro do resgate. Se ele for destruído ou o prazo acabar, o resgate pode ser repetido; a energia já recuperada permanece.

## Hospital e chefes

Destrua os três ninhos marcados com as armas. O Vigia aparece à noite nas ruas do hospital. O Carrasco é o objetivo seguinte. O círculo vermelho avisa a investida: saia para o lado antes do impacto. Depois do golpe, o chefe fica vulnerável por pouco mais de dois segundos; tiros na cabeça rendem mais dano. Abaixo da metade da vida, o chefe chama zumbis. Ao amanhecer as ameaças desaparecem; um chefe incompleto reaparece à noite.

## O emissor e o final

Após vencer o Carrasco, examine o diagrama junto ao emissor. A última matriz funciona como um tabuleiro: cada chave inverte a própria luz e as vizinhas ortogonais. Apague todas. WASD/setas movem a seleção e Espaço/Enter ativa.

Desligar o sinal encerra as ondas e leva ao desfecho da família. A cidade permanece de dia para exploração. Prólogo, documentos, enigmas e final são salvos.

## Movimento e combate

- WASD movimenta; Shift corre e consome fôlego.
- Ctrl esquiva na direção pressionada; sem direção, recua. A esquiva custa 28 pontos, tem breve janela de proteção e intervalo entre usos.
- Espaço salta ou transpõe uma barreira baixa quando há espaço para aterrissar. O controlador físico continua respeitando paredes.
- H, a pé, aplica um kit durante 1,8 segundo e recupera até 110 de vida. Andar ou receber dano interrompe sem consumir o kit. Dentro do carro H continua controlando os faróis.
- E recolhe e instala objetos e embarca pessoas. Dentro do carro continua sendo a buzina.
- Botão direito mira; esquerdo atira; R recarrega; Tab abre a roda de armas.

Zumbis comuns são lentos e recebem mais dano na cabeça. O palhaço corre em dupla, a possuída para quando observada, o psicopata persegue, o monstro aguenta mais dano, o rastejante ataca baixo e a dama aumenta a pressão de medo. Use carros para deslocar-se e escapar, mas preserve a lataria durante resgates.

## Recursos e salvamento

As noites duram cerca de cinco minutos, com um período diurno curto para comprar munição e kits. O abrigo energizado reduz o medo e recarrega a lanterna quando não há criaturas próximas. Lojas de equipamentos abrem de dia. Kits portáteis custam $90, com limite de oito.

Salve na casa. Os capítulos concluídos, ninhos destruídos, fôlego da lanterna, medo e kits são persistidos junto ao save existente. O resgate em andamento reinicia ao carregar porque o carro e seus passageiros não são salvos. Concluir um capítulo salva automaticamente fora do autotest.

## Desenvolvimento e teste

Comentários dos scripts estão em português. CityHorrorMissions concentra os objetivos; CityHorrorBoss controla aviso, investida e vulnerabilidade; CityHorrorMobility executa ações sem somar seu deslocamento ao andar normal. Todos os alvos usam IDamageable, permitindo aproveitar armas e veículos existentes.

Feche o Editor antes de executar o build pela linha de comando. Use apenas Build/, preservando Build/Teste. O autotest completo cobre também portas, amassados, física e poses originais. O argumento -terroronly verifica cenário, modelos, sobrevivência, campanha, chefes, mobilidade, salvamento e integridade do áudio. -terrorjogo verifica a população reduzida e a horda na configuração normal. -fpsonly permite comparações alternadas com o mesmo save em 1280x720.


## Abrigo e arsenal — revisão de 20/10/2026

Na porta da casa, E abre ABRIGO DA FAMÍLIA. Entre na sala ou use os serviços pelo menu. O quarto do despertar agora tem passagem para a sala, com cozinha, sofá, armário de armas, bancada médica e rádio. Dentro, aproxime-se de cada bancada e aperte E. O rádio também oferece Gravar progresso.

- Guardar uma arma transfere a arma e toda sua munição para o armário. Retirar devolve o mesmo estoque. Armas guardadas não ficam disponíveis durante a exploração.
- O estoque inicial possui dois coletes e quatro curativos. Vestir um colete consome uma unidade e repõe a proteção até 100. Curativos podem ser transferidos entre a mochila (até oito) e o armário (até vinte).
- H aplica um curativo portátil em 1,8 s e recupera até 110 pontos de vida. Andar ou receber dano interrompe sem gastar o item. Há compra de reposição nos menus do abrigo, com pagamento e limites de estoque.
- Gravar progresso salva também armas, munição e suprimentos guardados. Use a gravação depois de reorganizar o estoque.
- Segure TAB para abrir o carrossel. Use a roda do mouse, as setas ou A/D; solte TAB para equipar. Somente armas carregadas na mochila são selecionadas. Os cartões mostram os modelos, munição, dano, alcance e capacidade. O tempo fica lento durante a escolha.
- Carros em movimento atingem criaturas paradas ou em movimento. Elas caem e deslizam com colisão; sangue e rastros dos pneus desaparecem em 35 s e têm limite de 96 marcas. Chefes mantêm a resistência própria.

O mobiliário é geometria original simples com os materiais CC0 já preparados no projeto. As miniaturas do carrossel vêm dos modelos de armas existentes; não foram adicionadas licenças de terceiros nesta revisão.


## Operação Santa Aurora

No terceiro capítulo, o GPS leva à equipe de resgate extraterrestre. Os visitantes não causaram o acidente: tentam conter o experimento humano da ala psiquiátrica. Investigue a mansão do Dr. Mota, usando o corredor de serviço para contornar os cães ou enfrentando o pátio. O portão abre com a soma correta das válvulas; tiros e uma carga remota oferecem outra entrada.

O prontuário e dois estabilizadores estão nas alas opostas. O documento explica a ordem dos blocos do painel clínico. Depois de ligar a rede, destrua os três ninhos do Hospital Central. No Santa Aurora, enfraqueça o gigante até um quarto da vitalidade e segure E por um segundo em cada âncora. A nave remove o experimento vivo e conclui o capítulo. As naves também resgatam civis para quarentena.

B instala uma carga; B novamente detona. O estoque de cargas é comprado no balcão de armas e salvo com a campanha. As lojas de suprimentos e o balcão de emergência funcionam à noite.

## Celular e navegador

No celular, use o analógico à esquerda e arraste a metade direita para olhar. ARMAS abre o carrossel, as setas escolhem e EQUIPAR confirma. VOLTAR fecha documentos, lojas, mapa e enigmas. O botão USAR também pode ser mantido pressionado nos objetivos. Os controles podem ser inspecionados no Windows com -touchcontrols. Consulte README e MELHORIAS para a disponibilidade real dos builds Android e WebGL.
