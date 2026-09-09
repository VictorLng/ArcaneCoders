# Arcane Code

Protótipo Unity 2D de um roguelite com feitiços programáveis. Você controla o movimento de um mago; uma pseudolinguagem tipada controla os ataques automáticos.

## Jogar

Unity **6000.6.0f1**, Universal Render Pipeline 2D.

1. No Unity Hub, adicione esta pasta (`arcane-code`).
2. Abra `Assets/Scenes/ArcaneCode.unity` e pressione **Play**.
3. Escolha fogo ou gelo e entre na dungeon. A classe já começa com um programa funcional.

Os executáveis ficam em `Builds/Linux/ArcaneCode.x86_64` e `Builds/Windows/ArcaneCode.exe`. Mantenha o executável junto das pastas e bibliotecas do respectivo build.

| Controle | Ação |
| --- | --- |
| WASD / setas | Movimentar o mago |
| TAB / E | Editar feitiços em uma sala limpa |
| ESC | Pausar, retomar ou encerrar a tentativa |
| Ctrl + espaço | Completar nomes no editor de código |
| F12 | Capturar a tela em `capture.png`, na pasta do perfil |

A mira e os disparos são automáticos. Encoste nos cristais verdes para ganhar experiência; eles se aproximam do mago. Cristais não coletados permanecem na sala mesmo depois de atravessar uma porta. O minimapa indica chefe (`B`), recompensa (`+`) e descanso (`♡`).

Ao subir de nível, escolha uma de três melhorias e edite seu programa se quiser. O botão **Aplicar programa** ativa apenas código válido. **Voltar ao jogo** preserva o último programa aplicado, mesmo que exista um rascunho inválido.

Se um programa válido não atacar, ou ocorrer erro durante sua execução, use **ESC → Restaurar ataque básico**. O rascunho continua disponível no grimório.

## Feitiços

```csharp
class MagoDeFogo extends Mago {
    void attackOne() {
        for (int i = 0; i < 3; i++) {
            this.charge(1);
        }
        if (this.energia >= 3) {
            Fireball bola = this.fireball();
            bola.cast();
        }
    }
}
```

Esse programa custa **8 pontos**: loop 3, carregamento 1, condição 2 e criação de magia 2. A classe começa com 10 pontos. Também é possível escrever `this.fireball().cast();`.

`attackOne()` é executado repetidamente, sem sobrepor execuções. `charge(n)` acumula de 1 a 10 unidades ao longo do tempo: 0,35 segundo por unidade, até a capacidade do mago. A movimentação continua disponível. Cada disparo consome toda a energia e ganha 25% de dano por unidade. Sem energia, o ataque básico funciona normalmente. O intervalo inicial entre disparos é de 0,6 segundo.

Energia persiste entre chamadas na mesma sala e reinicia ao trocar de sala. O jogo pausado não carrega energia nem avança projéteis. Se não houver alvo visível, o lançamento não consome energia. Fogo aplica queimadura; gelo aplica lentidão e congela quando o disparo consome pelo menos três unidades. Chefes têm menor duração de congelamento.

| Função do kernel | Tipo | Disponibilidade |
| --- | --- | --- |
| `this.fireball()` | `Fireball` | Ataque básico de fogo |
| `this.flameWave()` | `FlameWave` | Magia de área desbloqueável de fogo |
| `this.icebolt()` | `Icebolt` | Ataque básico de gelo |
| `this.frostNova()` | `FrostNova` | Magia de área desbloqueável de gelo |
| `magia.cast()` | `void` | Lançar o objeto de magia |
| `this.charge(int)` | `void` | Carregar energia |

Propriedades somente para leitura: `int this.energia`, `float this.vida`, `int this.inimigos`.

Sintaxe suportada:

- `class Nome extends Mago`, com nome correspondente à classe selecionada.
- Métodos sem argumentos com retorno `void`, `int`, `float`, `bool` ou um tipo de magia. Auxiliares podem ser chamados com `this.nome()` ou `nome()`.
- `return valor;` em métodos tipados; todo caminho deve retornar o tipo declarado. Exemplo: `Fireball forte() { return this.fireball(); }`.
- `speedCast(N)` é um método compartilhado da família `Mago`, desbloqueado permanentemente no santuário. Aceita N literal de 1 a 10, acelera charge e cast em `20% × N` e custa `3 × N` pontos de complexidade.
- Variáveis `int`, `float`, `bool`, tipos de magia e `var` com inferência.
- Declaração com inicialização, atribuição de variáveis e escopos com chaves.
- Operadores `+ - * / %`, comparações, `&&`, `||` e `!`; divisão de inteiros trunca o resultado.
- `if (...) { ... } else { ... }`.
- `for (int i = 0; i < N; i++)`, com N literal de 1 a 10 e no máximo dois loops aninhados, contando auxiliares.
- Comentários `//`.

Declarações e `cast()` não ocupam pontos adicionais. O corpo do loop conta uma vez. Auxiliares são expandidos para contabilizar cada uso; auxiliares não usados também ocupam espaço. Recursão é rejeitada. Limites: 12.000 caracteres, 2.500 tokens, 16 métodos, 32 níveis de expressão/bloco, 128 passos de execução por frame e 2.048 instruções por chamada de `attackOne()`. Valores numéricos ficam entre −1.000.000 e 1.000.000. A linguagem é interpretada pelo jogo e não oferece acesso a C#, arquivos, rede ou APIs arbitrárias da Unity.

## Progressão e conteúdo

O protótipo contém oito salas por tentativa: entrada, quatro combates, recompensa, descanso e chefe. Modelos de salas são montados em um mapa conectado e ramificado, com semente reproduzível. Salas limpas não repovoam.

Recompensas temporárias incluem dano, vida, movimento, energia, complexidade e magia de área. A base vende vida, energia, complexidade inicial e desbloqueio permanente das magias de área. As construções da linguagem estão todas liberadas desde o início.

Morte, vitória e encerramento pelo menu depositam os fragmentos uma única vez. Vitória também concede 30 fragmentos extras. Ao iniciar outra tentativa, níveis e melhorias temporárias reiniciam. O código editado na base define a próxima classe inicial; código e rascunhos feitos durante uma tentativa não concedem funções que ela ainda não possui.

O save é `profile.json` em `Application.persistentDataPath` — normalmente, no Linux, `~/.config/unity3d/ArcaneWorkshop/Arcane Code/`. Há gravação por arquivo temporário, backup da versão anterior e preservação de arquivo inválido. A variável `ARCANE_PROFILE_DIR` permite usar outro diretório, especialmente em testes. Não há retomada de uma tentativa depois de fechar o jogo.

## Desenvolvimento e testes

- **Arcane Code → Preparar projeto** cria os recursos e configura a cena/build caso seja necessário.
- **Arcane Code → Build Linux / Build Windows** gera um executável. O módulo da plataforma deve estar instalado no Hub.
- **Window → General → Test Runner**: execute as suítes EditMode e PlayMode.
- Ajuste vida, velocidade, magias, inimigos e tempos em `Assets/Resources/GameConfig.asset`.

Os testes EditMode verificam a linguagem, os custos, os limites e 1.000 sementes de dungeon. Os testes PlayMode cobrem as duas classes, projéteis, recompensas, troca de sala, chefe, preservação de código válido, pausa e persistência. Eles usam perfis temporários. O teste de ciclo elimina inimigos pelo handler de morte para validar a progressão; não substitui uma avaliação humana de dificuldade.

Assemblies: `ArcaneCode.Core` não depende de Unity; `ArcaneCode.Runtime` contém jogo e apresentação; `ArcaneCode.Editor` prepara cenas e builds. A interface `ISpellWorld` conecta a máquina de feitiços ao combate.

Arte original provisória gerada por código. As fontes DejaVu acompanham sua licença em `Assets/Resources/DejaVu-LICENSE.txt` e nos builds. Esta versão não inclui áudio, multiplayer, outros elementos ou arte final.
# ArcaneCoders
