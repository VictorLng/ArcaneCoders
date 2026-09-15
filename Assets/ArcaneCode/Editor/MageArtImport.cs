using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcaneCode.Editor
{
    public sealed class MageTextureSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath != "Assets/Resources/Characters/Mage/mage-directions.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
        }
    }

    public static class MageArtImport
    {
        const string Output = "Assets/Resources/Characters/Mage/mage-directions.png";
        static bool Background(Color32 p) => p.g > 100 && p.g > p.r * 1.35f && p.g > p.b * 1.35f;

        [MenuItem("Arcane Code/Arte/Preparar poses do mago")]
        public static void Prepare()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes("Artifacts/Art/mage-directions-source.png"))) throw new Exception("Invalid mage source");
            var pixels = source.GetPixels32();
            int cell = source.width / 4;
            var bounds = new RectInt[4];
            float scale = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                int minX = cell, minY = source.height, maxX = 0, maxY = 0;
                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < cell; x++)
                {
                    if (Background(pixels[y * source.width + i * cell + x])) continue;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
                bounds[i] = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                scale = Mathf.Min(scale, 120f / bounds[i].width, 144f / bounds[i].height);
            }
            var sheet = new Texture2D(512, 160, TextureFormat.RGBA32, false);
            var output = new Color32[512 * 160];
            // Source profiles point right, then left; exported order is front/back/left/right.
            int[] order = { 0, 1, 3, 2 };
            for (int pose = 0; pose < 4; pose++)
            {
                int original = order[pose]; var box = bounds[original];
                int width = Mathf.RoundToInt(box.width * scale), height = Mathf.RoundToInt(box.height * scale);
                int offsetX = pose * 128 + (128 - width) / 2;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = original * cell + box.x + Mathf.Min(box.width - 1, Mathf.FloorToInt((x + .5f) / scale));
                    int sy = box.y + Mathf.Min(box.height - 1, Mathf.FloorToInt((y + .5f) / scale));
                    Color32 pixel = pixels[sy * source.width + sx];
                    if (!Background(pixel)) { pixel.a = 255; output[(y + 8) * 512 + offsetX + x] = pixel; }
                }
            }
            sheet.SetPixels32(output); sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllBytes(Output, sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Output);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("MAGE_ART_READY: four transparent 128x160 poses, front/back/left/right");
        }
    }
}
