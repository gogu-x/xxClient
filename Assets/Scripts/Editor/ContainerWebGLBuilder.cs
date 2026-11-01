using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace XiuXian.EditorTools
{
    /// <summary>Non-interactive WebGL build entry point for container builds.</summary>
    public static class ContainerWebGLBuilder
    {
        public static void Build()
        {
            const string scene = "Assets/Scenes/Bootstrap.unity";
            const string output = "BuildOutput/WebGL";
            if (!File.Exists(scene))
                throw new FileNotFoundException("Build scene not found", scene);
            if (PlayerSettings.WebGL.template != "PROJECT:MobilePortrait")
                throw new InvalidOperationException("The MobilePortrait WebGL template must be configured.");

            Directory.CreateDirectory(output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report == null || report.summary.result != BuildResult.Succeeded ||
                !File.Exists(Path.Combine(output, "index.html")))
                throw new InvalidOperationException("WebGL build failed; see the Unity log above.");
        }
    }
}
