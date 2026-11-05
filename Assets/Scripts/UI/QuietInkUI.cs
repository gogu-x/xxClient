using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>Composable portrait UI for the approved colored ink artwork.</summary>
    public static class QuietInkUI
    {
        public const float Width = 941, Height = 1672;
        public const string ResourceRoot = "UI/QuietInkV2/";
        public static readonly Color Paper = new Color32(243,240,232,255);
        public static readonly Color Ink = new Color32(39,51,48,255);
        public static readonly Color Muted = new Color32(112,123,117,255);
        public static readonly Color Jade = new Color32(52,127,120,255);
        public static readonly Color Blue = new Color32(64,126,164,255);
        public static readonly Color Gold = new Color32(189,148,81,255);
        public static readonly Color Cinnabar = new Color32(186,99,77,255);
        public static readonly Color LineColor = new Color32(207,199,180,255);
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static TMP_FontAsset display, body;

        public static void ClearCache() { Cache.Clear(); }
        public static Sprite Sprite(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Cache.TryGetValue(id, out var cached) && cached != null) return cached;
            foreach (var root in new[] { ResourceRoot, "UI/QuietInkV1/" })
            {
                var direct = Resources.Load<Sprite>(root + id);
                if (direct != null) { Cache[id] = direct; return direct; }
                foreach (var sprite in Resources.LoadAll<Sprite>(root + id))
                    if(sprite.name==id){Cache[id]=sprite;return sprite;}
                foreach (var atlas in new[] { "icons-navigation", "icons-items", "ui-controls", "icons-extra", "illustrations", "icons-settings" })
                    foreach (var sprite in Resources.LoadAll<Sprite>(root + atlas))
                        if (sprite.name == id) { Cache[id] = sprite; return sprite; }
            }
            return null;
        }
        public static Sprite Sprite(string atlas, string name) { return Sprite(name); }
        public static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        public static RectTransform Design(Transform parent,string name="Design")
        {
            var d=Rect(parent,name,0,0,Width,Height);
            d.anchorMin=d.anchorMax=d.pivot=new Vector2(.5f,.5f);d.anchoredPosition=Vector2.zero;
            d.gameObject.AddComponent<LobbyPortraitLayout>(); return d;
        }
        public static RectTransform Page(Transform parent,string name,string title,UnityAction close,out GameObject page,string background="bg-paper",bool showHeader=true)
        {
            var r=Rect(parent,name,0,0,0,0);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            page=r.gameObject; var blocker=page.AddComponent<Image>();blocker.color=Paper;
            var d=Design(r);Art(d,background,0,0,Width,Height);
            if(showHeader)Header(d,title,close);return d;
        }
        public static Image Art(Transform p,string id,float x,float y,float w,float h,bool fit=false)
        {
            var image=Rect(p,id.Replace('/','_'),x,y,w,h).gameObject.AddComponent<Image>();
            image.sprite=Sprite(id);image.color=Color.white;image.raycastTarget=false;image.preserveAspect=fit;
            if(!fit && image.sprite!=null && image.sprite.border.sqrMagnitude>0)image.type=Image.Type.Sliced;
            return image;
        }
        public static Image Icon(Transform p,string id,float x,float y,float w,float h) { return Art(p,id,x,y,w,h,true); }
        public static Image Panel(Transform p,string name,float x,float y,float w,float h,string style="info_card")
        { var im=Art(p,style,x,y,w,h);im.name=name;return im; }
        public static RawImage RawArt(Transform p,string id,float x,float y,float w,float h,bool fit=false)
        {
            var sprite=Sprite(id);var uv=new Rect(0,0,1,1);
            if(sprite!=null){var b=sprite.rect;uv=new Rect(b.x/sprite.texture.width,b.y/sprite.texture.height,b.width/sprite.texture.width,b.height/sprite.texture.height);
                if(fit){float a=b.width/b.height;float rw=Mathf.Min(w,h*a),rh=rw/a;x+=(w-rw)/2;y+=(h-rh)/2;w=rw;h=rh;}}
            var raw=Rect(p,id.Replace('/','_'),x,y,w,h).gameObject.AddComponent<RawImage>();raw.texture=sprite==null?null:sprite.texture;raw.uvRect=uv;raw.raycastTarget=false;return raw;
        }
        public static TMP_Text Text(Transform p,string name,string value,float x,float y,float w,float h,int size=28,Color? color=null,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            if(display==null)display=Resources.Load<TMP_FontAsset>("Fonts/LobbyDisplay")??TMP_Settings.defaultFontAsset;
            if(body==null)body=Resources.Load<TMP_FontAsset>("Fonts/MSYaHei SDF")??display;
            var text=Rect(p,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();text.font=size>=38?display:body;
            text.text=value;text.fontSize=size;text.color=color??Ink;text.alignment=alignment;text.raycastTarget=false;
            text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Ellipsis;return text;
        }
        public static Button Button(Transform p,string name,string label,float x,float y,float w,float h,UnityAction action,string style="button_primary",int size=30)
        {
            Image image;
            if(string.IsNullOrEmpty(style)){image=Rect(p,name,x,y,w,h).gameObject.AddComponent<Image>();image.color=Color.clear;}
            else {image=Art(p,style,x,y,w,h);image.name=name;}
            image.raycastTarget=true;var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var colors=b.colors;colors.highlightedColor=new Color(1,.98f,.91f);colors.pressedColor=new Color(.78f,.85f,.8f);colors.disabledColor=new Color(.62f,.65f,.62f);b.colors=colors;
            var t=Text(image.transform,"Label",label,14,4,w-28,h-8,size,style=="button_primary"?Paper:Ink);
            t.enableWordWrapping=false;t.enableAutoSizing=true;t.fontSizeMin=Mathf.Min(size,22);t.fontSizeMax=size;
            if(action!=null)b.onClick.AddListener(action);else b.interactable=false;return b;
        }
        public static Button BackButton(Transform parent,string name,float x,float y,UnityAction action)
        {
            var button=Button(parent,name,"",x,y,160,104,action,null);
            StyleBackButton(button);
            return button;
        }
        public static void StyleBackButton(Button button)
        {
            if(button==null)return;
            var rect=(RectTransform)button.transform;
            rect.sizeDelta=new Vector2(160,104);
            if(rect.Find("QuietInkBackplate")!=null)return;
            var previousChildren=new List<Transform>();
            foreach(Transform child in rect)previousChildren.Add(child);
            foreach(var child in previousChildren)
            {
                child.gameObject.SetActive(false);
                if(Application.isPlaying)Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
            var hit=button.GetComponent<Image>()??button.gameObject.AddComponent<Image>();
            hit.sprite=null;hit.color=Color.clear;hit.raycastTarget=true;
            var plate=Rect(rect,"QuietInkBackplate",0,0,160,104).gameObject.AddComponent<QuietInkBackplate>();
            plate.raycastTarget=false;
            button.targetGraphic=plate;button.transition=Selectable.Transition.ColorTint;
            var colors=button.colors;
            colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1f,.99f,.94f);
            colors.pressedColor=new Color(.81f,.9f,.84f);
            colors.selectedColor=Color.white;
            colors.disabledColor=new Color(.75f,.75f,.71f,.6f);
            colors.fadeDuration=.08f;button.colors=colors;
            var label=Text(rect,"Label","返回",57,14,86,76,30,Jade);
            label.textWrappingMode=TextWrappingModes.NoWrap;
        }
        public static void Header(Transform p,string title,UnityAction back)
        { BackButton(p,"Back",24,24,back);Text(p,"Title",title,210,35,521,88,44); }
        public static Image Divider(Transform p,float x,float y,float w)
        {var im=Rect(p,"Divider",x,y,w,1.5f).gameObject.AddComponent<Image>();im.color=LineColor;im.raycastTarget=false;return im;}
        public static Button Tab(Transform p,string name,string label,float x,float y,float w,bool selected,UnityAction action)
        {var b=Button(p,name,label,x,y,w,75,action,null,30);b.GetComponentInChildren<TMP_Text>().color=selected?Jade:Muted;if(selected){var line=Divider(b.transform,20,72,w-40);line.color=Jade;line.rectTransform.sizeDelta=new Vector2(w-40,3);}return b;}
        public static RectTransform Scroll(Transform p,string name,float x,float y,float w,float h,float contentHeight=0)
        {var v=Rect(p,name,x,y,w,h);v.gameObject.AddComponent<Image>().color=new Color(0,0,0,.001f);v.gameObject.AddComponent<RectMask2D>();var scroll=v.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;var c=Rect(v,"Content",0,0,w,Mathf.Max(h,contentHeight));scroll.viewport=v;scroll.content=c;return c;}
    }
}
