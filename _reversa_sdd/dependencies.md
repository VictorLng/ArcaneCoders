# Dependências — Arcane Code

> Gerado pelo Scout em 2026-09-11.

## Ambiente

| Componente | Versão | Evidência |
| --- | --- | --- |
| Unity | `6000.6.0f1` | `ProjectSettings/ProjectVersion.txt` |
| C# | 🟡 Versão fornecida pelo compilador da Unity instalada | arquivos `.cs` do projeto |

## Pacotes Unity

| Pacote | Versão | Uso inferido |
| --- | --- | --- |
| `com.unity.2d.sprite` | `1.0.0` | Recursos de sprites 2D. |
| `com.unity.inputsystem` | `1.19.0` | Entrada do jogador. |
| `com.unity.render-pipelines.universal` | `17.6.0` | Universal Render Pipeline / renderização 2D. |
| `com.unity.test-framework` | `1.8.0` | Testes EditMode e PlayMode. |
| `com.unity.ugui` | `2.6.0` | UI Unity. |
| `com.unity.modules.screencapture` | `1.0.0` | Captura de tela. |

## Gerenciamento e distribuição

- 🟢 Gerenciador de pacotes: Unity Package Manager (`Packages/manifest.json` e `Packages/packages-lock.json`).
- 🟢 Builds previstos: Linux e Windows, disparados pelo menu de editor em `ArcaneBuild.cs`.
- 🟢 Não foram encontrados `package.json`, `composer.json`, `requirements.txt`, `go.mod` ou outros manifests de aplicação.
- 🟢 Não foram encontrados Dockerfile, docker-compose, workflows GitHub Actions, Jenkinsfile ou GitLab CI versionados.
