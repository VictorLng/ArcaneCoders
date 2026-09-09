using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArcaneCode.Editor
{
    public static class ArcaneBuild
    {
        const string ScenePath = "Assets/Scenes/ArcaneCode.unity";
        [MenuItem("Arcane Code/Preparar projeto")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
            foreach (string fontPath in new[] {"Assets/Resources/DejaVuSans.ttf","Assets/Resources/DejaVuSansMono.ttf"})
            {
                var importer=AssetImporter.GetAtPath(fontPath) as TrueTypeFontImporter;
                if (importer!=null && !importer.includeFontData) { importer.includeFontData=true; importer.SaveAndReimport(); }
            }
            if (AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(),"Assets/Resources/GameConfig.asset");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            if (pipeline == null) throw new Exception("Pipeline URP do template não encontrado.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i); QualitySettings.renderPipeline = pipeline; }
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/WorldMaterial.mat") == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null) throw new Exception("Shader Sprite-Lit do URP não encontrado.");
                AssetDatabase.CreateAsset(new Material(shader),"Assets/Resources/WorldMaterial.mat");
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6.5f;
                camera.transform.position = new Vector3(0,0,-10); camera.backgroundColor = new Color(.035f,.045f,.075f);
                camera.clearFlags = CameraClearFlags.SolidColor; cameraObject.AddComponent<AudioListener>();
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var light = new GameObject("Ambient Light").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = .7f;
                new GameObject("Arcane Code").AddComponent<ArcaneGame>();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName = "ArcaneWorkshop"; PlayerSettings.productName = "Arcane Code";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            Debug.Log("ARCANE_SETUP_OK");
        }
        [MenuItem("Arcane Code/Build Linux")]
        public static void Linux() { Setup(); Build(BuildTarget.StandaloneLinux64,"Builds/Linux/ArcaneCode.x86_64"); }
        [MenuItem("Arcane Code/Build Windows")]
        public static void Windows() { Setup(); Build(BuildTarget.StandaloneWindows64,"Builds/Windows/ArcaneCode.exe"); }
        static void Build(BuildTarget target,string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target)) throw new Exception("Módulo de build não instalado: "+target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[] {ScenePath},locationPathName=path,target=target,options=BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build falhou: "+report.summary.result);
            File.Copy("Assets/Resources/DejaVu-LICENSE.txt",Path.Combine(Path.GetDirectoryName(path),"DejaVu-LICENSE.txt"),true);
            Debug.Log("ARCANE_BUILD_OK "+target+" "+report.summary.totalSize+" bytes");
        }
    }
}
