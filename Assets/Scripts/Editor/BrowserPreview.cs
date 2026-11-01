using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XiuXian.EditorTools
{
    /// <summary>Editor-only automatic WebGL build and local browser preview.</summary>
    [InitializeOnLoad]
    public static class BrowserPreview
    {
        public const string Url = "http://127.0.0.1:8788/";
        private const string Menu = "Tools/XiuXian/浏览器预览/";
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private static readonly string Project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string Output = Path.Combine(Project, "Builds", "BrowserPreview", "WebGL");
        private static readonly string StateDirectory = Path.Combine(Project, "Library", "BrowserPreview");
        private static readonly string Stamp = Path.Combine(StateDirectory, "inputs.txt");
        private static readonly string StatePath = Path.Combine(StateDirectory, "state.json");
        private static readonly string Preference = "XiuXian.BrowserPreview.Auto." + Project;
        private static bool queued, resumePlay, forceBuild, skipNextPlay;

        [Serializable] private sealed class Health { public string service; public string root; }

        static BrowserPreview()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            if (EditorPrefs.GetBool(Preference, false)) EditorApplication.delayCall += StartExisting;
        }

        [MenuItem(Menu + "每次 Play 自动更新并打开")]
        public static void ToggleAutomatic() => SetAutomatic(!EditorPrefs.GetBool(Preference, false));

        [MenuItem(Menu + "每次 Play 自动更新并打开", true)]
        private static bool ValidateAutomatic()
        {
            UnityEditor.Menu.SetChecked(Menu + "每次 Play 自动更新并打开", EditorPrefs.GetBool(Preference, false));
            return true;
        }

        public static void SetAutomatic(bool enabled)
        {
            EditorPrefs.SetBool(Preference, enabled);
            if (enabled) StartExisting();
            Debug.Log("[BrowserPreview] 每次 Play 自动预览：" + enabled + "；" + Url);
        }

        [MenuItem(Menu + "更新并打开浏览器版")]
        public static void BuildAndOpen() => QueueBuild(false, true);

        [MenuItem(Menu + "打开已构建版本")]
        public static void OpenExisting() { StartExisting(); Application.OpenURL(Url); }

        private static void StartExisting()
        {
            try { SetReady(); StartServer(); }
            catch (Exception e) { Debug.LogError("[BrowserPreview] " + e.Message); }
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            if (skipNextPlay) { skipNextPlay = false; return; }
            if (EditorPrefs.GetBool(Preference, false)) QueueBuild(true, false);
        }

        private static void QueueBuild(bool playAfterwards, bool force)
        {
            if (queued || BuildPipeline.isBuildingPlayer) return;
            resumePlay = playAfterwards; forceBuild = force; queued = true;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        }

        private static void Tick()
        {
            if (!queued || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            queued = false;
            bool play = resumePlay;
            try
            {
                StartServer();
                if (forceBuild || !File.Exists(Path.Combine(Output, "index.html")) ||
                    !File.Exists(Stamp) || File.ReadAllText(Stamp) != InputFingerprint()) BuildNow();
                SetReady();
                Application.OpenURL(Url);
                if (play) { skipNextPlay = true; EditorApplication.isPlaying = true; }
            }
            catch (Exception e)
            {
                SetStatus("failed");
                Debug.LogError("[BrowserPreview] 更新失败，未启动新的运行会话。" + e);
            }
        }

        private static void BuildNow()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("请为当前 Unity 安装 WebGL Build Support。");
            if (!File.Exists(Path.Combine(Project, ScenePath))) throw new FileNotFoundException(ScenePath);
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isDirty) throw new InvalidOperationException("请先保存 Bootstrap 场景，再运行或更新浏览器版。");
            if (PlayerSettings.WebGL.template != "PROJECT:MobilePortrait")
                throw new InvalidOperationException("请先使用 Tools/XiuXian/应用 WebGL 手机版设置。");
            SetStatus("building");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Stamp, string.Empty);
            Debug.Log("[BrowserPreview] 正在增量构建，完成后自动打开 " + Url);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = Output,
                target = BuildTarget.WebGL, options = BuildOptions.Development,
            });
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("WebGL 构建失败，请查看 Console 的构建错误。");
            string index = Path.Combine(Output, "index.html");
            string html = File.ReadAllText(index);
            const string reload = "<script>(function(){let version;setInterval(async()=>{try{const r=await fetch('/__preview/status',{cache:'no-store'});const s=await r.json();if(s.state!=='ready')return;if(version&&version!==s.version)location.reload();version=s.version;}catch(e){}},2000);})();</script>";
            File.WriteAllText(index, html.Replace("</body>", reload + "</body>"), new UTF8Encoding(false));
            File.WriteAllText(Stamp, InputFingerprint());
            Debug.Log("[BrowserPreview] 构建成功：" + Output);
        }

        private static string InputFingerprint()
        {
            var lines = new StringBuilder();
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
                foreach (string path in Directory.EnumerateFiles(Path.Combine(Project, folder), "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                {
                    // Editor analysis state is rewritten during Play and is not a player input.
                    if (folder == "ProjectSettings" && Path.GetFileName(path) == "ProjectAuditorSettings.asset") continue;
                    var file = new FileInfo(path);
                    lines.Append(path).Append('|').Append(file.Length).Append('|').Append(file.LastWriteTimeUtc.Ticks).Append('\n');
                }
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(lines.ToString())));
        }

        private static void SetReady()
        {
            string index = Path.Combine(Output, "index.html");
            bool ready = File.Exists(index) && File.Exists(Stamp) && !string.IsNullOrEmpty(File.ReadAllText(Stamp));
            SetStatus(ready ? "ready" : "waiting", ready ? File.GetLastWriteTimeUtc(index).Ticks.ToString() : "");
        }

        private static void SetStatus(string state, string version = "")
        {
            Directory.CreateDirectory(StateDirectory);
            string temporary = StatePath + ".tmp";
            File.WriteAllText(temporary, "{\"state\":\"" + state + "\",\"version\":\"" + version + "\"}", new UTF8Encoding(false));
            if (File.Exists(StatePath)) File.Replace(temporary, StatePath, null); else File.Move(temporary, StatePath);
        }

        private static bool IsServerReady()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(Url + "__preview/status");
                request.Timeout = 500; request.ReadWriteTimeout = 500; request.KeepAlive = false; request.Proxy = null;
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var health = JsonUtility.FromJson<Health>(reader.ReadToEnd());
                    return health.service == "XiuXianBrowserPreview" &&
                        string.Equals(Path.GetFullPath(health.root), Output, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (WebException) { return false; }
        }

        private static void StartServer()
        {
            if (IsServerReady()) return;
            string python = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines", "WebGLSupport", "BuildTools", "Emscripten", "python", "python.exe");
            string script = Path.Combine(Project, "tools", "serve_preview.py");
            if (!File.Exists(python) || !File.Exists(script)) throw new FileNotFoundException("找不到 WebGL Python 运行时或 tools/serve_preview.py。");
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = python,
                Arguments = "\"" + script + "\" --root \"" + Output + "\" --state \"" + StatePath + "\" --port 8788 --parent " + System.Diagnostics.Process.GetCurrentProcess().Id,
                UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            };
            System.Diagnostics.Process.Start(start);
            for (int i = 0; i < 10; i++)
            {
                System.Threading.Thread.Sleep(100);
                if (IsServerReady()) return;
            }
            throw new InvalidOperationException("本机预览服务未能启动。请检查 8788 端口是否被其他程序占用。");
        }
    }
}
