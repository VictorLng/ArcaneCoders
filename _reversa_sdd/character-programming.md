# Programação do personagem

Atualizado em 2026-10-02 a partir da mudança de propriedade do código.

## Regra do jogo

O programa e o rascunho pertencem ao personagem e são armazenados por classe. Equipamentos e itens não carregam scripts. O mago pode programar sem possuir um grimório; grimórios são exclusivos do mago e ampliam sua pool de feitiços. A staff mantém o ataque inicial e os bônus elementais de dano. O grimório não aplica mais o antigo bônus de dano por nível.

O runtime atual continua sendo o mago e sua linguagem continua usando `extends Mago`. O armazenamento suporta outras classes por `ClassId`; adicionar o guerreiro e suas habilidades é trabalho futuro.

## Dados e responsabilidades

| Componente | Responsabilidade |
| --- | --- |
| `CharacterProgramState` | `ClassId`, código aplicado e rascunho, independentes do equipamento. |
| `Profile.CharacterPrograms` / `ProgramFor(classId)` | Persistência e isolamento do programa de cada classe. |
| `Profile.MageStartingGrimoireId` | Preferência de equipamento inicial do mago, independente do código e dos itens obtidos na run. |
| `SavedCharacterProgram` / `Profile.ProgramLibrary` | Cópias nomeadas de programas, identificadas pela classe, sem tipo de grimório. |
| `RunItemInstance` | Identidade, definição e nível do item; não possui campo de código. |
| `RunInventory.Equip(instanceId, classId)` | Equipa itens; rejeita grimórios para classes diferentes de `mage`. |
| `MageLoadout` | Pool elemental e efeitos da staff/runa; não é proprietário do programa. |
| `ArcaneGame` | Compila o programa do personagem conforme a pool e o orçamento disponíveis e o conecta ao combate. |

## Editor, troca e execução

- TAB abre o editor do personagem em uma sala segura, com ou sem grimório.
- Aplicar código válido atualiza o programa do personagem e salva o perfil, inclusive durante a run.
- Fechar o editor guarda o rascunho, mesmo inválido, sem alterar o último programa aplicado.
- Ctrl+Z desfaz edições do rascunho; Ctrl+Shift+Z ou Ctrl+Y refazem. Digitação, exclusão, colagem, autocomplete, indentação, Enter e botões de inserção/exemplos usam o mesmo histórico, com até 100 estados e restauração do cursor/seleção. Uma nova edição elimina o caminho de refazer. O histórico é reiniciado ao abrir o editor, não é salvo no perfil e nunca altera o programa aplicado sem uma nova aplicação explícita.
- Trocar staff ou grimório altera a pool e recompila o programa preservado. Não limpa o código nem o rascunho.
- Se faltar uma magia ou orçamento, um aviso informa que o ataque básico é temporário. Apenas o programa executado muda; o código e o rascunho armazenados permanecem intactos.
- Reequipar as magias necessárias restaura automaticamente a execução do programa original.
- Restaurar ataque básico pelo menu é uma escolha explícita: salva esse novo programa no personagem e mantém o rascunho anterior.
- Ao terminar uma run em que o programa foi alterado, o jogador pode nomear uma cópia na Biblioteca. A opção continuar não apaga o programa já salvo.
- Uma nova run limpa itens e melhorias temporárias, mas carrega o programa e o rascunho da classe.

## Escolha do grimório inicial

No santuário, o mago pode escolher os quatro grimórios do catálogo atual: Bola de Fogo, Lança de Gelo, Onda de Chamas e Nova Congelante. A opção sem grimório continua disponível. A escolha é salva no perfil e é usada tanto na prévia da pool/editor do santuário quanto no início da run.

`RunInventory.BeginRun(classId, startingGrimoireId)` cria uma nova staff e um novo grimório de nível 1. Nenhum nível, identidade de item ou loot da run anterior é reaproveitado. A opção não é aplicada para outras classes. Equipar um grimório dropado durante a run não substitui a preferência inicial; escolher um novo inicial é permitido apenas no hub.

A escolha altera apenas a pool. O programa e o rascunho do personagem permanecem, inclusive se usarem feitiços ausentes do grimório escolhido; nesse caso, a política de ataque básico temporário continua em vigor.

## Migração

O perfil é versão 6. Migrações das versões 1 a 4 preservam código e rascunho antigos em `CharacterPrograms` para `mage`, normalizando as antigas classes de fogo/gelo para `MagoArcanista`. Perfis até a versão 5 recebem `grimoire-fire` como preferência inicial. Perfis atuais com um identificador de grimório desconhecido voltam à escolha Bola de Fogo, preservando o restante do perfil.

Grimórios antigos da Biblioteca viram cópias em `ProgramLibrary`, identificadas por classe. Nomes duplicados recebem sufixos para preservar todas as cópias. Os campos legados permanecem serializados para compatibilidade e consulta, mas não são usados como fonte do programa em runtime. A preferência de tutoriais das versões 4 e 5 é mantida. Reexecutar a migração não duplica entradas.

## Cobertura de regressão

- EditMode: programas e rascunhos isolados por classe; grimório exclusivo do mago; grimório sem bônus de dano; composição de feitiços com staff.
- PlayMode: troca de grimórios preservando código e rascunho inválido; ataque temporário e restauração ao reequipar; salvamento e Biblioteca sem grimório; migração e recarga das versões anteriores.
- Escolha inicial: todas as opções equipadas no nível 1; reset de loot e identidade; escolha preservada ao retornar ao hub e recarregar o perfil; rejeição de escolhas inválidas e de mudanças durante a run; migração da versão 5.

Implementação: `Assets/ArcaneCode/Core/CharacterProgramming.cs`, `Dungeon.cs`, `MageEquipment.cs` e os arquivos parciais `Runtime/ArcaneGame*.cs`.

## Validação no Unity

Executado no Unity 6000.6.0f1 em 2026-10-02: os oito testes de `EquipmentTests` e os catorze testes PlayMode de `GameTests` passaram, incluindo preservação do código, migração de perfis e escolha do grimório inicial. A compilação Core/Runtime/Tests foi bem-sucedida.

A execução completa anterior do EditMode teve 31 aprovações de 33 testes. As duas falhas existentes fora desta mudança são `DungeonGuaranteesAcrossOneThousandSeeds` (a expectativa ainda é oito salas, mas o gerador já cria nove) e `ImportedMageKeepsPurpleAndLastFacingWhenStopped` (a expectativa de posição zero diverge do deslocamento da arte). A validação da seleção inicial executou a suíte de equipamento e a suíte PlayMode do jogo.

Resultados atuais: `Logs/starting-grimoire-editmode.xml` e `Logs/starting-grimoire-playmode.xml`. A execução completa anterior está em `Logs/character-program-editmode.xml`.

Ctrl+Z (2026-10-02): Runtime e PlayTests compilados com sucesso pelo compilador C# do Unity, com saídas isoladas em `Logs/ide-undo-compile.*`, sem substituir as DLLs do editor aberto. Dois testes PlayMode adicionados cobrem undo/redo, seleção, autocomplete, indentação, ramificação de edições, separação do programa aplicado e reset ao reabrir. A execução desses testes ainda está pendente: o Unity recusou a instância batch porque o projeto já estava aberto em outra instância (`Logs/ide-undo-playmode.log`).
