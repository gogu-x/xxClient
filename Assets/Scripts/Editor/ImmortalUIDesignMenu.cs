using UnityEditor;
using UnityEngine;
using XiuXian.Core;

namespace XiuXian.Editor
{
    public static class ImmortalUIDesignMenu
    {
        [MenuItem("仙途/UI/打开全页面设计预览")]
        public static void Open()
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool("ImmortalUI.OpenDesign",true);
                EditorApplication.isPlaying=true;
                return;
            }
            ImmortalUIDesignPreview.Open();
        }
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ImmortalUI.OpenDesign",false))return;
            SessionState.SetBool("ImmortalUI.OpenDesign",false);
            EditorApplication.delayCall += () => { if(EditorApplication.isPlaying)ImmortalUIDesignPreview.Open(); };
        }
    }
}
