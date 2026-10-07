using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArcaneCode.Editor
{
    public sealed class EnemyTextureSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Characters/Enemies/",StringComparison.Ordinal)) return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;
            importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false; importer.filterMode=FilterMode.Point; importer.wrapMode=TextureWrapMode.Clamp;
            importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
        }
    }

    public static class EnemyArtImport
    {
        const string SourceDirectory="_reversa_sdd/enemy-sprite-sources";
        const string OutputDirectory="Assets/Resources/Characters/Enemies";
        static readonly string[] Ids={"skeleton","bat","goblin","rat","mini-mage","spider","black-knight","flame-headless-knight","basilisk-frog"};

        [InitializeOnLoadMethod]
        static void PrepareMissingAfterCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (Ids.Any(NeedsPreparation) && Ids.All(id=>File.Exists(SourcePath(id)))) Prepare();
            };
        }
        [MenuItem("Arcane Code/Arte/Preparar poses dos inimigos")]
        public static void Prepare()
        {
            foreach (string id in Ids) Prepare(id);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("ENEMY_ART_READY: nine transparent sheets with front/back/left/right poses.");
        }
        static void Prepare(string id)
        {
            string sourcePath=SourcePath(id); if (!File.Exists(sourcePath)) throw new FileNotFoundException("Enemy source sheet missing",sourcePath);
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePath))) throw new Exception("Invalid enemy source: "+id);
            Color32[] pixels=source.GetPixels32(); int cell=source.width/4;
            var bounds=new RectInt[4]; float scale=float.MaxValue;
            for (int pose=0;pose<4;pose++)
            {
                int minX=cell,minY=source.height,maxX=-1,maxY=-1;
                for (int y=0;y<source.height;y++) for (int x=0;x<cell;x++)
                {
                    if (pixels[y*source.width+pose*cell+x].a<8) continue;
                    minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); minY=Math.Min(minY,y); maxY=Math.Max(maxY,y);
                }
                if (maxX<minX) throw new Exception("No visible pixels in "+id+" pose "+pose);
                bounds[pose]=new RectInt(minX,minY,maxX-minX+1,maxY-minY+1);
                scale=Mathf.Min(scale,120f/bounds[pose].width,144f/bounds[pose].height);
            }
            var sheet=new Texture2D(512,160,TextureFormat.RGBA32,false); var output=new Color32[512*160];
            for (int pose=0;pose<4;pose++)
            {
                RectInt box=bounds[pose]; int width=Mathf.RoundToInt(box.width*scale),height=Mathf.RoundToInt(box.height*scale);
                int offsetX=pose*128+(128-width)/2;
                for (int y=0;y<height;y++) for (int x=0;x<width;x++)
                {
                    int sx=pose*cell+box.x+Mathf.Min(box.width-1,Mathf.FloorToInt((x+.5f)/scale));
                    int sy=box.y+Mathf.Min(box.height-1,Mathf.FloorToInt((y+.5f)/scale));
                    Color32 pixel=pixels[sy*source.width+sx]; if (pixel.a>=8) output[(y+8)*512+offsetX+x]=pixel;
                }
            }
            sheet.SetPixels32(output); sheet.Apply(); Directory.CreateDirectory(OutputDirectory); File.WriteAllBytes(OutputPath(id),sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(sheet);
        }
        static bool NeedsPreparation(string id)
        {
            string path=OutputPath(id); if (!File.Exists(path)) return true;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            bool valid=texture.LoadImage(File.ReadAllBytes(path)) && texture.width==512 && texture.height==160;
            UnityEngine.Object.DestroyImmediate(texture); return !valid;
        }
        static string SourcePath(string id) => Path.Combine(SourceDirectory,id+"-directions-source.png");
        static string OutputPath(string id) => Path.Combine(OutputDirectory,id+"-directions.png");
    }
}
