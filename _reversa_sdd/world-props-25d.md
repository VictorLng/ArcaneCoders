# Objetos e ataques 2.5D

Implementação de apresentação em 2026-10-03. O cenário e os atores continuam com sprites; movimento, colisões, alvos e interações continuam no plano XY. A câmera ortográfica e o renderer URP 2D existentes são preservados.

## Modelos e animação

`Runtime/WorldPropArt.cs` contém `WorldProps`, a fábrica de modelos procedurais low-poly, e `WorldPropArt`, a apresentação de cada objeto. Cubo, esfera facetada e torus são meshes compartilhados. Todos os objetos compartilham um material; cores e transparência são configuradas com `MaterialPropertyBlock`.

- Baú: corpo e tampa de madeira, faixas metálicas, fechadura e sombra, com pose inclinada estática.
- Itens: anel com gema, cajado e grimório com páginas/capas. Itens e cartas da loja giram no eixo Y e flutuam levemente.
- Cartas: placa com espessura, moldura e cristal nas duas faces.
- Moedas e XP: recursos volumétricos animados; mantêm a atração/coleta e absorção ao limpar a sala.
- Ataques: fogo com rastro volumétrico; gelo e disparos inimigos com cristal facetado; ondas de área/impacto com torus expansivo que perde opacidade.

O root é a posição lógica do objeto. Rotação e flutuação são aplicadas apenas ao filho `Modelo 3D`, nunca ao ponto usado para colisão/coleta. Não são criados colliders ou rigidbodies 3D. Atualizações partem de `UpdateCombat(dt)`; portanto, menus que interrompem o combate também interrompem os efeitos e a rotação. O inventário mantém o comportamento de tempo já existente. A velocidade é definida em graus/segundo, independente do framerate.

## Renderização

`Resources/WorldProp.shader` usa o passe `SRPDefaultUnlit`, suportado pelo renderer 2D instalado, e sombreamento de faces por normais com luz fixa de apresentação. Ele não depende de luzes 3D e é incluído no build por estar em Resources. Mistura transparente e ausência de escrita no depth buffer permitem combinar os meshes com sprites. Props e sombras são ordenados pela posição Y do root, conforme os atores. Projéteis e ondas preservam as prioridades de desenho 350 e 340.

Meshes gerados são visuais iniciais substituíveis por modelos definitivos sem alterar inventário, dano, preços, compra, abertura de baús ou os métodos de interação. HUD, ícones do inventário e telegraphs de perigo continuam 2D.

## Validação

Os testes PlayMode exercitam os modelos reais no fluxo de abertura/coleta de baú e compra de itens/cartas, recusam compra sem dinheiro, verificam espessura dos meshes e ausência de colliders, rotação sem deslocamento do anchor, independência de FPS e posicionamento dos ataques. Um teste com GPU renderiza uma galeria no renderer 2D existente, verifica objetos visíveis/ausência de shader magenta e salva a prévia em uma pasta temporária isolada. Esse teste visual é ignorado em `-nographics`.

Resultados: `Logs/props-25d-playmode.xml` e `Logs/props-25d-playmode.log`.

Validação concluída no Unity 6000.6.0f1 em 2026-10-03 com GPU Direct3D: 19/19 testes de `GameTests` aprovados, incluindo as regressões de undo/redo que estavam pendentes. Shader importado e renderizado sem erros; prévia inspecionada visualmente. Não foi gerado um novo build nesta tarefa.
