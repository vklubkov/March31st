using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace March31st {
	[InitializeOnLoad]
	public static class Survivor {
		const string _persistentInfoKey = "March31st:PersistentInfo";
		static PersistentInfo _persistentInfo = PersistentInfo.Default;
		static bool _shouldResume;

        static readonly CancellationTokenSource _cancellationTokenSource = new();

        static Survivor() {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.update += Update;
            EditorApplication.quitting += OnQuitting;
        }

        static void OnBeforeAssemblyReload() {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.update -= Update;
            EditorApplication.quitting -= OnQuitting;

            var json = JsonConvert.SerializeObject(_persistentInfo, new StringEnumConverter());
            SessionState.SetString(_persistentInfoKey, json);
        }

        static void OnAfterAssemblyReload() {
            var defaultJson = JsonConvert.SerializeObject(PersistentInfo.Default, new StringEnumConverter());
            var json = SessionState.GetString(_persistentInfoKey, defaultJson);
            _persistentInfo = JsonConvert.DeserializeObject<PersistentInfo>(json);
            _shouldResume = true;
        }

        static void Update() {
            if (!_shouldResume)
                return;

            switch (_persistentInfo.State) {
                case State.AddScopedRegistry:
                    InstallNuGetForUnity();
                    break;
                case State.InstallNuGetForUnity:
                    InstallTabula();
                    break;
                case State.InstallTabula:
                   SetScriptingDefine();
                    break;
                case State.SetScriptingDefine:
                    Cleanup();
                    break;
                case State.None:
                default:
                    break;
            }

            _shouldResume = false;
        }

        static void OnQuitting() {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.update -= Update;
            EditorApplication.quitting -= OnQuitting;
            _cancellationTokenSource.Cancel();
        }

        public static void InstallDependencies() {
#if MARCH31ST_NUGET_AVAILABLE
            InstallTabula();
#else
            _persistentInfo.State = State.AddScopedRegistry;

            var manifestPath = Application.dataPath + "/../Packages/manifest.json";

            var scopedRegistry = new ScopedRegistryInfo() {
                Name = "package.openupm.com",
                Scopes = new List<string> {  "com.github-glitchenzo.nugetforunity" },
                Url = "https://package.openupm.com"
            };

            var applyChanges = ScopedRegistryAdder.Add(manifestPath, scopedRegistry);
            CompleteStep(applyChanges, InstallNuGetForUnity);
#endif
        }

        static void InstallNuGetForUnity() {
            _persistentInfo.State = State.InstallNuGetForUnity;

            UpmPackageInstaller.InstallAsync(
                "com.github-glitchenzo.nugetforunity",
                _cancellationTokenSource.Token,
                applyChanges => CompleteStep(applyChanges, InstallTabula));
        }

        static void InstallTabula() {
#if MARCH31ST_NUGET_AVAILABLE
            _persistentInfo.State = State.InstallTabula;
            var applyChanges = NuGetPackageInstaller.Install("Tabula");
            CompleteStep(applyChanges, SetScriptingDefine);
#else
#endif
        }

        static void SetScriptingDefine() {
            _persistentInfo.State = State.SetScriptingDefine;
            var scriptingDefineSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (scriptingDefineSymbols.Contains("MARCH31ST_TABULA_AVAILABLE")) {
                CompleteStep(false, Cleanup);
                return;
            }

            if (scriptingDefineSymbols.Length == 0)
                scriptingDefineSymbols = "MARCH31ST_TABULA_AVAILABLE";
            else
                scriptingDefineSymbols += ";MARCH31ST_TABULA_AVAILABLE";

            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, scriptingDefineSymbols);
            CompleteStep(true, Cleanup);
        }

        static void CompleteStep(bool applyChanges, Action onComplete) {
            try {
                if (!applyChanges) {
                    onComplete?.Invoke();
                    return;
                }

                CompilationPipeline.RequestScriptCompilation();
                onComplete?.Invoke();
            }
            catch (Exception e) {
                Debug.LogException(e);
                Cleanup();
            }
            finally {
                onComplete?.Invoke();
            }
        }

        static void Cleanup() {
            _persistentInfo = PersistentInfo.Default;
            _shouldResume = false;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorUtility.RequestScriptReload();
        }
    }
}