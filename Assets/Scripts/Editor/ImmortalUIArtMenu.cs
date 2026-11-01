using UnityEditor;
using UnityEngine;
using XiuXian.Core;

namespace XiuXian.Editor
{
    public static class ImmortalUIArtMenu
    {
        [MenuItem("仙途/UI/打开 V3 美术交互预览")]
        public static void Open()
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool("ImmortalUI.OpenArt",true);
                EditorApplication.isPlaying=true;
                return;
            }
            ImmortalUIArtPreview.Open();
        }
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ImmortalUI.OpenArt",false))return;
            SessionState.SetBool("ImmortalUI.OpenArt",false);
            EditorApplication.delayCall += () => { if(EditorApplication.isPlaying)ImmortalUIArtPreview.Open(); };
        }
    }
}
