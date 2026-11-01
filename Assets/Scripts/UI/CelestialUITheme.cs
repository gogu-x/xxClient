using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>Shared page palette. Each page retains its own authored artwork.</summary>
    public static class CelestialUITheme
    {
        public const string Background = "Theme/background_mist_v2";
        public const string Arena = "Theme/background_arena_v1";
        public static readonly Color Text = new Color(.97f, .94f, .84f);
        public static readonly Color SecondaryText = new Color(.78f, .82f, .74f);
        public static readonly Color Silver = new Color(.52f, .68f, .86f);

        // Keep item, character, socket and skill artwork intact: their colors convey identity.
        public static string Resolve(string id)
        {
            return id;
        }

        public static Rect UV(string resolvedId, Rect original)
        {
            switch (resolvedId)
            {
                case "Login/celestial_button_v3": return new Rect(0, .23f, 1, .58f);
                case "Login/celestial_row_v3": return new Rect(0, .33f, 1, .37f);
                case "Login/celestial_panel_v3": return new Rect(0, 0, 1, 1);
                case Background: case Arena: case "Theme/scene_sect_night_v1":
                    return new Rect(0, 0, 1, 1);
                default: return original;
            }
        }

        public static Color Tint(string id)
        {
            // Selected tabs retain a brighter silhouette; inactive tabs remain distinguishable.
            return id == "Equipment/tab_jade" ? new Color(.73f, .8f, .75f) : Color.white;
        }
    }
}
