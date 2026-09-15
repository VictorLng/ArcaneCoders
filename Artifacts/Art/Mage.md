# Mago: quatro direções estáticas

Folha do jogo: `Assets/Resources/Characters/Mage/mage-directions.png`.
512 × 160 pixels, quatro células de 128 × 160, da esquerda para a direita: frente, costas, esquerda e direita. RGBA transparente, filtro Point, sem mipmaps ou compressão. Pivô inferior central (0.5, 0.05), 128 pixels por unidade. As poses mantêm os pés alinhados.

O movimento escolhe a direção dominante; diagonais empatadas usam a direção vertical. Ao parar, mantém a última pose. A arte mantém suas cores originais com iluminação Sprite-Lit do cenário. O dano pisca a opacidade. As animações por quadros ficam para a próxima etapa.

`MageArtImport` permite reconstruir a folha pelo menu **Arcane Code > Arte > Preparar poses do mago**. A fonte em `mage-directions-source.png` tem fundo verde para recorte na importação. O importador remove esse fundo e normaliza a escala das poses. A ordem lateral da fonte é invertida em relação à folha exportada.

Arte criada com a ferramenta integrada de geração de imagens, a partir do conceito aprovado: mago humano idoso, barba branca enorme escondendo o rosto, chapéu roxo grande e pontudo, mãos com luvas grandes e flutuantes, túnica pequena e botas, em pixel art e perspectiva de cima. Quatro vistas estáticas, sem cenário ou efeitos mágicos. O fundo foi alterado para verde puro para viabilizar transparência real na folha importada.
