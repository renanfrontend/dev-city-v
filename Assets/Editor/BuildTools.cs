using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DevCity.EditorTools
{
    /// <summary>
    /// Menu "Dev City" no editor: configura o projeto (cena, shaders, player) e gera o .exe.
    /// Também roda pela linha de comando:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod DevCity.EditorTools.BuildTools.BuildWindows
    /// </summary>
    public static class BuildTools
    {
        const string ScenePath = "Assets/Scenes/DevCity.unity";

        static readonly string[] RuntimeShaders =
        {
            "Standard", "Skybox/Procedural", "Sprites/Default", "UI/Default", "Legacy Shaders/Particles/Alpha Blended"
        };
        public static void Setup()
        {
            // cena vazia: o jogo se monta sozinho (GameManager com RuntimeInitializeOnLoadMethod)
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Renan";
            PlayerSettings.productName = "Dev City V";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = false;

            // o jogo cria materiais com Shader.Find em tempo de execução: esses shaders precisam ir no build
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in RuntimeShaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) { Debug.LogWarning("Shader não encontrado: " + name); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedProperties();

            QualitySettings.shadowDistance = 150f;
            QualitySettings.antiAliasing = 4;
            AssetDatabase.SaveAssets();
            Debug.Log("[Dev City] Projeto configurado.");
        }
        public static void BuildWindows()
        {
            Setup();
            Directory.CreateDirectory("Build");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/DevCityV.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Dev City] Build: {s.result} | {s.totalSize / (1024 * 1024)} MB | {s.totalTime}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
