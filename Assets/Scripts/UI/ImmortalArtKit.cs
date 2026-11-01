using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>Reusable V3 artwork widgets. No networking or player data writes.</summary>
    public static class ImmortalArtKit
    {
        public static readonly Color Paper = new Color(.98f,.94f,.81f);
        private static readonly Dictionary<string,Sprite> Sprites = new Dictionary<string,Sprite>();
        public static RectTransform Rect(Transform p,string n,float x,float y,float w,float h) { return FeaturePageKit.Rect(p,n,x,y,w,h); }
        public static Sprite Sprite(string atlas,string name)
        {
            string key=atlas+"/"+name; Sprite s;
            if(Sprites.TryGetValue(key,out s))return s;
            foreach(var item in Resources.LoadAll<Sprite>("UI/ImmortalV3/Atlases/"+atlas))
            {
                var rect=item.rect; Vector4 border=Vector4.zero;
                if(atlas=="controls")
                {
                    float top=item.name.StartsWith("tab")?.17f:.18f;
                    rect.y+=rect.height*.07f;rect.height*=1-top-.07f;
                    border=new Vector4(rect.width*.22f,rect.height*.3f,rect.width*.22f,rect.height*.3f);
                }
                if(atlas=="panels")border=new Vector4(rect.width*.4f,rect.height*.4f,rect.width*.4f,rect.height*.4f);
                if(atlas=="panels"||atlas=="controls")
                {
                    var sliced=UnityEngine.Sprite.Create(item.texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);sliced.name=item.name;Sprites[atlas+"/"+item.name]=sliced;
                }
                else Sprites[atlas+"/"+item.name]=item;
            }
            if(!Sprites.TryGetValue(key,out s))Debug.LogError("Missing V3 sprite: "+key);
            return s;
        }
        public static Image Art(Transform p,string atlas,string name,float x,float y,float w,float h,bool preserve=true)
        {
            var im=Rect(p,name,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=Sprite(atlas,name);im.preserveAspect=preserve;im.raycastTarget=false;
            if(!preserve&&(atlas=="panels"||atlas=="controls")){im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=atlas=="panels"?2.8f:4f;}
            return im;
        }
        public static Image Foreground(Transform p,string name,float x,float y,float w,float h)
        {
            var im=Rect(p,name,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=Resources.Load<Sprite>("UI/ImmortalV3/Foregrounds/"+name);im.preserveAspect=true;im.raycastTarget=false;return im;
        }
        public static Image Box(Transform p,string n,float x,float y,float w,float h,Color c) { return FeaturePageKit.Box(p,n,x,y,w,h,c); }
        public static TMP_Text Text(Transform p,string n,string value,float x,float y,float w,float h,int size=24,Color? color=null)
        {
            var t=FeaturePageKit.Text(p,n,value,x,y,w,h,size,color??Paper);
            var shadow=t.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.04f,.08f,.07f,.8f);shadow.effectDistance=new Vector2(1,-2);
            return t;
        }
        public static Button Button(Transform p,string n,string label,float x,float y,float w,float h,Color c,UnityAction action)
        {
            var hit=Box(p,n,x,y,w,h,Color.clear);hit.raycastTarget=true;
            var b=hit.gameObject.AddComponent<Button>();
            if(c.a>0)
            {
                var art=Art(hit.transform,"controls",n.StartsWith("Tab")?"tab_active":"button_jade",-5,-h*.22f,w+10,h*1.44f,false);
                b.targetGraphic=art;
            }
            else b.targetGraphic=hit;
            b.onClick.AddListener(action);
            var text=Text(hit.transform,"Label",label,14,0,w-28,h,22);text.alignment=TextAlignmentOptions.Center;
            var colors=b.colors;colors.highlightedColor=new Color(1,.92f,.72f);colors.pressedColor=new Color(.65f,.8f,.72f);b.colors=colors;
            return b;
        }
        public static TMP_InputField Input(Transform p,string n,string hint,float x,float y,float w,Color c)
        {
            var f=FeaturePageKit.Input(p,n,hint,x,y,w,new Color(.04f,.12f,.1f,.8f));
            var rim=Art(f.transform,"controls","button_jade",0,0,w,54,false);rim.transform.SetAsFirstSibling();
            f.textComponent.color=Paper;((TMP_Text)f.placeholder).color=new Color(.72f,.79f,.68f);return f;
        }
        public static RectTransform Scroll(Transform p,string n,float x,float y,float w,float h,float contentHeight){return FeaturePageKit.Scroll(p,n,x,y,w,h,contentHeight);}
        public static void Background(Transform p,string name,Color fallback){FeaturePageKit.Background(p,name,fallback);}
    }
}
