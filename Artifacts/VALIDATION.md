# Validação do protótipo

Data: 07/09/2026. Unity 6000.6.0f1. Artefatos gerados a partir do código final do projeto.

| Verificação | Resultado |
| --- | --- |
| Testes EditMode | 20 aprovados, 0 falhas |
| Testes PlayMode | 3 aprovados, 0 falhas |
| Build Linux x86_64 | Gerado e executado |
| Build Windows x86_64 | Gerado; formato PE32+ confirmado |
| Execução nativa no Windows | Não realizada: ambiente disponível é Linux |

Relatórios completos: [EditMode](Tests/editmode.xml) e [PlayMode](Tests/playmode.xml). Logs de build e execução ficam em `Logs/`.

## Verificação no executável Linux

Executado em tela virtual Xvfb, OpenGL por software, com perfil separado do jogador.

- Grimório: fonte incorporada, destaque de sintaxe, exemplo com loop e condição, contador de complexidade, aplicação de código e rejeição de rascunho inválido.
- Autocomplete: digitação de `this.fi` e Ctrl+espaço produziram `this.fireball` após a correção da ordem dos eventos do campo de texto.
- Partida de fogo: movimentação, transição de sala, ataques automáticos, esqueletos, projéteis sinalizados do arqueiro, morte de inimigos, fragmentos e coleta de experiência.
- Level-up alcançado jogando; escolha de complexidade e aplicação do programa carregado durante a pausa.
- Encerramento da tentativa, retorno à base e fechamento normal do executável.
- Nenhuma exceção de jogo ou falha de fonte encontrada no log final. A tela virtual não fornece dispositivo de áudio; o protótipo não inclui áudio.

Capturas: [grimório](Screenshots/grimorio.png), [combate](Screenshots/combate.png) e [level-up](Screenshots/level-up.png).

## Limites da validação

Os testes integrados cobrem fogo e gelo, lançamento de projéteis, recompensas, salas, chefe, pausa e persistência. No teste de ciclo, a conclusão dos encontros usa o handler de morte para verificar a progressão de forma determinística; isso não equivale a vencer o chefe jogando nem a uma avaliação de equilíbrio.

A dificuldade, a arte provisória e a experiência de edição ainda devem receber playtests humanos. O executável Windows requer validação em uma máquina Windows antes de distribuição pública.
