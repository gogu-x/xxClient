using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace XiuXian.Core
{
    public static class ImmortalV4PageKit
    {
        [Serializable] private class Entry { public string id; public float x,y,w,h; }
        [Serializable] private class Catalog { public Entry[] items; }
        private static Dictionary<string,Rect> bounds;
        private static TMP_FontAsset displayFont;
        private static TMP_FontAsset bodyFont;
        public static readonly Color Ink=CelestialUITheme.SecondaryText;
        public static readonly Color Ivory=CelestialUITheme.Text;
        public static RectTransform Rect(Transform p,string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(p,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        public static RectTransform Page(Transform parent,string name,string title, UnityAction close,out GameObject page,string background="Equipment/background",string skin=null,bool showHeader=true)
        {
            var r=Rect(parent,name,0,0,0,0);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            page=r.gameObject;if(skin!=null)ConceptPageSkin.Apply(page,skin);
            var bgPath=ConceptPageSkin.Resolve(r,background,new Rect(0,0,1,1),out _,out _);
            var bg=page.AddComponent<Image>();bg.sprite=Resources.Load<Sprite>("UI/ImmortalV4/"+bgPath);bg.color=Color.white;CelestialBackgroundCover.Apply(bg);
            var design=Rect(r,"Design",0,0,941,1672);design.anchorMin=design.anchorMax=design.pivot=new Vector2(.5f,.5f);design.anchoredPosition=Vector2.zero;design.gameObject.AddComponent<LobbyPortraitLayout>();
            Art(design,background,0,0,941,1672);
            if(showHeader)
            {
                QuietInkUI.Header(design,title,close);
            }
            return design;
        }
        public static RawImage Art(Transform p,string id,float x,float y,float w,float h,bool fit=false)
        {
            if(bounds==null){bounds=new Dictionary<string,Rect>();var json=Resources.Load<TextAsset>("UI/ImmortalV4/asset-bounds");if(json!=null)foreach(var e in JsonUtility.FromJson<Catalog>(json.text).items)bounds[e.id]=new Rect(e.x,e.y,e.w,e.h);}
            var resolved=ConceptPageSkin.Resolve(p,id,bounds.TryGetValue(id,out var value)?value:new Rect(0,0,1,1),out var uv,out var tint);
            var texture=Resources.Load<Texture2D>("UI/ImmortalV4/"+resolved);
            if(texture!=null&&fit){float aspect=texture.width*uv.width/(texture.height*uv.height);float width=Mathf.Min(w,h*aspect),height=width/aspect;x+=(w-width)*.5f;y+=(h-height)*.5f;w=width;h=height;}
            ConceptPageSkin.CompactArt(p,id,ref x,ref y,ref w,ref h);
            var im=Rect(p,id.Replace('/','_'),x,y,w,h).gameObject.AddComponent<RawImage>();im.texture=texture;im.uvRect=uv;im.color=tint;im.raycastTarget=false;return im;
        }
        public static void ChangeArt(RawImage image,string id)
        {
            var resolved=ConceptPageSkin.Resolve(image.transform,id,bounds!=null&&bounds.TryGetValue(id,out var original)?original:new Rect(0,0,1,1),out var uv,out var tint);
            image.texture=Resources.Load<Texture2D>("UI/ImmortalV4/"+resolved);
            image.uvRect=uv;
            image.color=tint;
        }
        public static TMP_Text Text(Transform p,string name,string value,float x,float y,float w,float h,int size=28,bool dark=false)
        {
            if(displayFont==null)displayFont=Resources.Load<TMP_FontAsset>("Fonts/LobbyDisplay") ?? TMP_Settings.defaultFontAsset;
            if(bodyFont==null)bodyFont=Resources.Load<TMP_FontAsset>("Fonts/MSYaHei SDF") ?? displayFont;
            var t=Rect(p,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font=size<=32&&(h>=90||(!string.IsNullOrEmpty(value)&&value.Contains("\n")))?bodyFont:displayFont;
            t.text=value;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;
            t.color=dark?new Color(.16f,.23f,.19f):ConceptPageSkin.TextColor(p);
            t.raycastTarget=false;t.enableWordWrapping=true;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        public static Button Button(Transform p,string name,string label,float x,float y,float w,float h,UnityAction action,string art="Equipment/button_jade",int size=32)
        {
            var r=Rect(p,name,x,y,w,h);var hit=r.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=hit;b.transition=Selectable.Transition.None;
            if(art!=null){var im=Art(r,art,0,0,w,h);b.targetGraphic=im;b.transition=Selectable.Transition.ColorTint;}
            var text=Text(r,"Label",label,12,4,w-24,h-8,size);
            text.color=ConceptPageSkin.TextColor(r,art=="Equipment/button_gold"||art=="Equipment/tab_gold");
            text.enableWordWrapping=false;text.enableAutoSizing=true;text.fontSizeMin=Mathf.Min(size,22);text.fontSizeMax=size;
            if(art==null){b.targetGraphic=text;b.transition=Selectable.Transition.ColorTint;}
            var colors=b.colors;colors.highlightedColor=new Color(1f,.94f,.79f);colors.pressedColor=new Color(.7f,.76f,.65f);colors.disabledColor=new Color(.58f,.61f,.57f,1f);b.colors=colors;
            if(action!=null)b.onClick.AddListener(action);else b.interactable=false;
            return b;
        }
        public static RectTransform Scroll(Transform p,string name,float x,float y,float w,float h)
        {
            var viewport=Rect(p,name,x,y,w,h);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
            var content=Rect(viewport,"Content",0,0,w,h);scroll.viewport=viewport;scroll.content=content;return content;
        }
    }
}
