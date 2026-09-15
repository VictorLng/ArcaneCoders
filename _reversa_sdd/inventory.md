# Inventário do Sistema — Arcane Code

> Gerado pelo Scout em 2026-09-11.  
> Escopo: mapeamento superficial; detalhes de comportamento serão extraídos nas próximas fases.

## Visão geral

🟢 **CONFIRMADO** — `Arcane Code` é um protótipo de roguelite 2D feito em Unity. O jogador controla um mago e programa ataques automáticos por meio de uma linguagem de feitiços interpretada pelo próprio jogo.

## Estrutura relevante

| Caminho | Papel |
| --- | --- |
| `Assets/ArcaneCode/Core/` | Lógica independente de Unity: dungeon, perfil, linguagem e máquina de feitiços. |
| `Assets/ArcaneCode/Runtime/` | Ciclo do jogo, combate, UI, configuração e arte gerada em código. |
| `Assets/ArcaneCode/Editor/` | Preparação do projeto e builds Linux/Windows no editor Unity. |
| `Assets/ArcaneCode/Tests/Editor/` | Testes EditMode da lógica e geração de dungeon. |
| `Assets/ArcaneCode/Tests/PlayMode/` | Testes PlayMode do ciclo de jogo e persistência. |
| `Assets/Resources/` | `GameConfig.asset`, fontes e material usados em runtime. |
| `Assets/Scenes/ArcaneCode.unity` | Cena principal indicada pelo README. |
| `Packages/` | Manifesto de pacotes Unity. |
| `ProjectSettings/` | Configurações do projeto Unity 6000.6.0f1. |

## Assemblies e camadas

| Assembly | Dependências | Responsabilidade |
| --- | --- | --- |
| `ArcaneCode.Core` | nenhuma referência à engine | Modelo de dungeon/perfil e interpretador de feitiços. |
| `ArcaneCode.Runtime` | Core, Input System e URP | Jogo, combate, interface e apresentação. |
| `ArcaneCode.Editor` | Core, Runtime e URP | Provisionamento de assets/cena e geração de builds. |
| `ArcaneCode.Tests` | Core, Runtime | Testes EditMode. |
| `ArcaneCode.PlayTests` | Core, Runtime | Testes PlayMode. |

## Pontos de entrada

- 🟢 `Assets/Scenes/ArcaneCode.unity` — cena principal de execução.
- 🟢 `Assets/ArcaneCode/Runtime/ArcaneGame.cs` — inicialização após o carregamento de cena; contém `Awake` e `Update`.
- 🟢 `Assets/ArcaneCode/Runtime/ArcaneGame.UI.cs` — interface imediata via `OnGUI`.
- 🟢 `Assets/ArcaneCode/Editor/ArcaneBuild.cs` — menus `Arcane Code/Preparar projeto`, `Build Linux` e `Build Windows`.

## Dados e integrações

- 🟢 Persistência local em `profile.json`, sob `Application.persistentDataPath` ou no diretório definido por `ARCANE_PROFILE_DIR`.
- 🟢 Não foram encontradas migrations, ORM, banco de dados remoto, chamadas HTTP/API, Docker ou pipeline CI/CD versionado.
- 🟢 O save inclui escrita temporária, substituição/backup e preservação de perfil inválido.

## Testes

- 🟢 Unity Test Framework, com 2 arquivos de suíte: `CoreTests.cs` (EditMode) e `GameTests.cs` (PlayMode).
- 🟡 O README informa cobertura de linguagem, limites, 1.000 sementes de dungeon, classes, projéteis, recompensas, salas, chefe, pausa e persistência; a cobertura percentual não foi encontrada.

## Itens fora do escopo de código-fonte

`Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/` e `Assets/_Recovery/` são artefatos de editor, build, cache ou recuperação; foram excluídos da análise de implementação.
