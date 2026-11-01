using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>Page-local art from the approved four-page concepts. Never changes sibling screens.</summary>
    public sealed class ConceptPageSkin : MonoBehaviour
    {
        public const string Art = "ConceptV1/";
        public string Scheme;

        public static void Apply(GameObject root, string scheme)
        {
            var skin = root.GetComponent<ConceptPageSkin>() ?? root.AddComponent<ConceptPageSkin>();
            skin.Scheme = scheme;
        }

        public static string Resolve(Transform parent, string id, Rect original, out Rect uv, out Color tint)
        {
            var resource = CelestialUITheme.Resolve(id);
            uv = CelestialUITheme.UV(resource, original);
            tint = CelestialUITheme.Tint(id);
            var skin = parent != null ? parent.GetComponentInParent<ConceptPageSkin>(true) : null;
            if (skin == null) return resource;
            bool equipment = skin.Scheme == "Equipment", soul = skin.Scheme == "SoulJade";
            if (id.EndsWith("/background"))
            {
                uv = new Rect(0, 0, 1, 1);
                if (equipment) return Art + "equipment_bg";
                if (soul) return Art + "soul_bg";
            }
            if (equipment && id == "Equipment/hero_clean")
            { uv = new Rect(0, 0, 1, 1); return Art + "equipment_hero_cutout"; }
            if (soul && id == "SoulJade/ring")
            { uv = new Rect(0, 0, 1, 1); return Art + "soul_ring"; }
            if (skin.Scheme == "Battle" && id == "Battle/reference_skills")
            { uv = new Rect(0, 0, 1, 1); return Art + "battle_skills"; }
            if (skin.Scheme == "Battle")
            {
                switch (id)
                {
                    case "Battle/reference_log_panel": uv=new Rect(.027911f,.530303f,.641946f,.158692f); break;
                    case "Battle/reference_back": uv=new Rect(.720096f,.489633f,.245614f,.244019f); break;
                    case "Battle/reference_control": uv=new Rect(.025518f,.229665f,.468102f,.129984f); break;
                    case "Lobby/currency_plate": uv=new Rect(.511962f,.248804f,.473684f,.089314f); break;
                    default: return resource;
                }
                return Art + "battle_ui";
            }

            if (!equipment && !soul) return resource;
            string part = null;
            switch (id)
            {
                case "Equipment/header_plate": case "Equipment/power_plate":
                case "Lobby/currency_plate": case "Lobby/location_plate":
                case "Equipment/tab_jade": case "Equipment/tab_gold": part = "row"; break;
                case "Equipment/button_gold": part = "primary"; break;
                case "Equipment/button_jade": part = soul ? "primary" : "row"; break;
                case "Equipment/detail_panel": case "SoulJade/panel": part = "panel"; break;
                case "Equipment/equipment_slot": case "SoulJade/socket": part = "slot"; break;
            }
            if (part == null) return resource;
            uv = AtlasRect(equipment, part);
            return Art + (equipment ? "equipment_ui" : "soul_ui");
        }

        // Alpha bounds measured from source atlases; each rect is an independent UI sprite.
        private static Rect AtlasRect(bool equipment, string part)
        {
            if (equipment)
            {
                switch (part)
                {
                    case "panel": return new Rect(.021531f,.449761f,.633971f,.323764f);
                    case "slot": return new Rect(.691388f,.478469f,.283892f,.261563f);
                    case "primary": return new Rect(.015152f,.214514f,.483254f,.106858f);
                    default: return new Rect(.501595f,.214514f,.483254f,.106858f);
                }
            }
            switch (part)
            {
                case "panel": return new Rect(.027113f,.471292f,.594099f,.261563f);
                case "slot": return new Rect(.638756f,.423445f,.358054f,.348485f);
                case "primary": return new Rect(.010367f,.1874f,.484051f,.108453f);
                default: return new Rect(.508772f,.192982f,.476077f,.100478f);
            }
        }

        public static Color TextColor(Transform parent, bool primary = false)
        {
            var skin = parent.GetComponentInParent<ConceptPageSkin>(true);
            if (skin != null && skin.Scheme == "Equipment")
                return primary ? new Color(.24f,.14f,.075f) : new Color(1f,.93f,.77f);
            if (skin != null && skin.Scheme == "SoulJade") return new Color(.96f,.9f,1f);
            if (skin != null && skin.Scheme == "Battle") return new Color(.88f,.94f,1f);
            return primary ? new Color(.18f,.22f,.15f) : CelestialUITheme.Text;
        }

        public static void CompactArt(Transform parent, string id, ref float x, ref float y, ref float w, ref float h)
        {
            // Callers own visual bounds; implicit shrinking misaligns artwork and hit areas.
        }
    }
}
