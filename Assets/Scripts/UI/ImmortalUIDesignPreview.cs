using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static XiuXian.Core.FeaturePageKit;

namespace XiuXian.Core
{
    /// <summary>Isolated, non-persistent art review. Never sends requests or changes player saves.</summary>
    public sealed class ImmortalUIDesignPreview : MonoBehaviour
    {
        public static readonly string[] PageNames = { "登录", "大厅", "角色", "修炼", "装备", "背包", "天赋", "魂玉", "秘境战斗", "任务", "宗门", "活动", "福利", "伙伴", "排行", "仙府", "设置" };
        public int CurrentPage { get; private set; }
        private RectTransform _design, _body;
        private Color _accent, _ink;
        private TMP_Text _feedback;
        private int _selection, _tab;
        private bool _female;
        private float _volume = .7f;
        private GameObject _modal;
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
        public static ImmortalUIDesignPreview Open()
        {
            var existing = FindObjectOfType<ImmortalUIDesignPreview>();
            if (existing != null) return existing;
            var go = new GameObject("UI Design Preview - no game data", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
            var preview = go.AddComponent<ImmortalUIDesignPreview>(); preview.ShowPage(1); return preview;
        }
        private void Update() { if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) { if (_modal != null) Destroy(_modal); else ShowPage(1); } }
        public void ShowPage(int index)
        {
            CurrentPage = Mathf.Clamp(index,0,PageNames.Length-1); _selection=0; _tab=0; Build();
        }
        private void Build()
        {
            if (_design != null) { _design.gameObject.SetActive(false); Destroy(_design.gameObject); }
            _modal=null; Palette();
            _design=Rect(transform,"Page_"+CurrentPage,0,0,1600,900); _design.gameObject.AddComponent<FeaturePageFit>();
            Background(_design,Art(),_ink);
            Box(_design,"Atmosphere",0,0,1600,900,new Color(.01f,.02f,.03f,.18f));
            Box(_design,"HeaderShade",186,0,1414,140,Alpha(_ink,.65f));
            Box(_design,"FooterShade",186,838,1414,62,Alpha(_ink,.85f));
            Box(_design,"Navigation",0,0,186,900,Alpha(_ink,.97f));
            Text(_design,"Brand","仙 途 · 设计册",20,20,166,50,24,_accent);
            for(int i=0;i<PageNames.Length;i++)
            {
                int n=i; var active=i==CurrentPage;
                Button(_design,"PageButton"+i,(i+1).ToString("00")+"   "+PageNames[i],12,88+i*42,162,37,active?Alpha(_accent*.35f,1):_ink,()=>ShowPage(n));
                if(active)Box(_design,"Active",12,94+i*42,3,25,_accent);
            }
            Text(_design,"Mode","UI 设计预览\n所有数值均为示意",20,815,160,58,17,Paper);
            Text(_design,"Title",PageNames[CurrentPage],224,22,600,64,42);
            Text(_design,"Subtitle",Subtitle(),226,86,1060,38,20,_accent);
            Button(_design,"ClosePreview","退出预览",1390,34,168,48,Alpha(_ink,.95f),()=>Destroy(gameObject));
            Box(_design,"HeaderRule",226,137,1330,2,Alpha(_accent,.65f));
            _body=Rect(_design,"Content",226,158,1330,670);
            _feedback=Text(_design,"Feedback","设计交互预览 · 不连接服务器、不修改存档",226,850,1328,32,19);
            switch(CurrentPage)
            {
                case 0: Login();break; case 1: Lobby();break; case 2: Character();break;
                case 3: Cultivation();break;case 4: Equipment();break;case 5: Inventory();break;
                case 6: Talents();break;case 7: SoulJade();break;case 8: Battle();break;
                case 9: Tasks();break;case 10: Sect();break;case 11: Events();break;
                case 12: Gifts();break;case 13: Partners();break;case 14: Ranking();break;
                case 15: Estate();break;case 16: Settings();break;
            }
        }
        private static Color Alpha(Color c,float a){c.a=a;return c;}
        private void Palette()
        {
            string accent="#A8CFFF",ink="#111C35";
            switch(CurrentPage)
            {
                case 3:case 4:case 5:accent="#F6C679";ink="#302018";break;
                case 9:case 10:case 15:accent="#A4DFC0";ink="#102E27";break;
                case 11:case 12:accent="#FFD19C";ink="#431D25";break;
                case 6:case 7:case 13:accent="#D8B8FF";ink="#271B3D";break;
                case 14:accent="#E9D69D";ink="#1C2738";break;
            }
            _accent=Hex(accent);_ink=Hex(ink);
        }
        private string Art()
        {
            if(CurrentPage==0)return "UI/Login/Login_Background";
            if(CurrentPage==8)return "UI/Battle/block";
            if(CurrentPage==3||CurrentPage==4||CurrentPage==5)return "UI/Destinations/AmberSanctuary";
            if(CurrentPage==9||CurrentPage==10||CurrentPage==15)return "UI/Destinations/JadeMonastery";
            if(CurrentPage==11||CurrentPage==12)return "UI/Destinations/VermilionFestival";
            return "UI/Lobby/Lobby_Background";
        }
        private string Subtitle()
        {
            return new[]{"三千大道，自在修仙","观星海 · 入仙途","一袭长衣，踏遍山河","凝神入定 · 万法归心","藏器于身，待时而动","一方行囊，收尽天地","四系灵根 · 万法同源","魂光流转 · 玉蕴天机","破云入境 · 以战问道","山河为卷，仙途为章","同道相逢，共问长生","云间盛会，与君同行","仙途有礼，不负相逢","仙路同行，羁绊相生","群英论道 · 天下争锋","一方洞天，四时安宁","随心而行 · 自在修仙"}[CurrentPage];
        }
        private Transform Card(string name,float x,float y,float w,float h)
        {var c=Box(_body,name,x,y,w,h,Alpha(_ink,.92f));Box(c.transform,"Trim",0,0,w,2,Alpha(_accent,.7f));return c.transform;}
        private void Note(string value){if(_feedback!=null)_feedback.text="预览 · "+value;}
        private void Action(Transform p,string label,float x,float y,float w,UnityEngine.Events.UnityAction action)
        {Button(p,label,label,x,y,w,52,Alpha(_accent*.33f,1),action);}
        private void Icon(Transform p,int index,float x,float y,float size)
        {
            index=Mathf.Clamp(index,0,15);
            var r=Rect(p,"Icon"+index,x,y,size,size).gameObject.AddComponent<RawImage>();
            r.texture=Resources.Load<Texture2D>("UI/Destinations/ItemAtlas");
            float[] left={0,320,640,960}, right={320,640,960,1254};
            float[] top={0,344,636,930}, bottom={344,636,930,1254};
            int col=index%4,row=index/4;
            r.uvRect=new Rect(left[col]/1254f,1-bottom[row]/1254f,(right[col]-left[col])/1254f,(bottom[row]-top[row])/1254f);
            r.raycastTarget=false;
        }
        private void Hero(Transform p,bool female,float x,float y,float w,float h)
        {
            var r=Rect(p,"HeroArt",x,y,w,h).gameObject.AddComponent<RawImage>();
            r.texture=Resources.Load<Texture2D>(female?"UI/Lobby/Lobby_Female_Character_Cutout":"UI/Lobby/Lobby_Character_Cutout");r.raycastTarget=false;
            // Match the 2:3 source aspect without stretching the figure.
            r.rectTransform.sizeDelta=new Vector2(h*2/3,h);
        }
        private void JadeIcon(Transform p,int index,float x,float y,float size)
        {
            string[] names={"21_Lieyang","22_Guanri","23_Xuanwu","24_Canglang","25_Chixiao","26_Fulong"};
            var sprite=Resources.Load<Sprite>("UI/SoulJade/DesignElements/"+names[index%6]);
            if(sprite==null){Icon(p,new[]{4,8,13,9,11,15}[index%6],x,y,size);return;}
            var im=Rect(p,"JadeIcon"+index,x,y,size,size).gameObject.AddComponent<Image>();
            im.sprite=sprite;im.preserveAspect=true;im.raycastTarget=false;
        }
        private void Tabs(string[] names,float width=180)
        {for(int i=0;i<names.Length;i++){int n=i;Button(_body,"Tab"+i,names[i],i*(width+12),0,width,48,_tab==i?Alpha(_accent*.4f,1):Alpha(_ink,.94f),()=>{_tab=n;_selection=0;Build();});}}
        private void Login()
        {
            Text(_body,"Slogan","三 千 大 道\n自 在 修 仙",36,150,700,260,70);
            Text(_body,"Poem","御剑乘风起，问道九重天。",42,430,650,55,28,_accent);
            var c=Card("LoginCard",884,40,446,580);
            Text(c,"Welcome","重返仙途",30,28,386,60,38,_accent);
            Input(c,"Account","账号 / 道号",30,124,386,Alpha(_ink,1));
            var password=Input(c,"Password","密码",30,206,386,Alpha(_ink,1));password.contentType=TMP_InputField.ContentType.Password;
            Action(c,"一区 · 云海仙门   ﹀",30,294,386,ServerPicker);
            Action(c,"进入仙途",30,394,386,()=>ShowPage(1));
            Action(c,"创建账号",30,476,180,()=>Modal("创建账号","账号注册表单将在功能开发阶段接入。"));
            Action(c,"游客预览",236,476,180,()=>ShowPage(1));
        }
        private void ServerPicker()
        {
            var c=Modal("选择仙域","服务器列表为设计示意");
            for(int i=0;i<4;i++){int n=i;Action(c,new[]{"一区  云海仙门 · 流畅","二区  青莲剑域 · 流畅","三区  紫霄天境 · 繁忙","四区  蓬莱仙岛 · 维护"}[i],32,150+i*64,576,()=>{Note("已选择仙域 "+(n+1));Destroy(_modal);});}
        }
        private void Lobby()
        {
            var player=Card("Player",0,0,340,132);Icon(player,0,12,16,100);
            Text(player,"Name","清风道人",125,20,200,45,28,_accent);Text(player,"Realm","筑基 · 初期",125,75,200,34,22);
            Text(_body,"Currency","灵石  12,800     仙玉  680",890,0,440,50,25);
            Hero(_body,_female,470,-18,450,675);
            var quest=Card("Quest",0,390,345,190);Text(quest,"Heading","仙途指引",24,18,296,44,27,_accent);Text(quest,"Goal","前往青云秘境\n完成第一次试炼",24,70,296,80,23);
            Action(quest,"前往  →",174,132,146,()=>ShowPage(9));
            int[] pages={10,11,12,14};string[] labels={"宗门","活动","福利","排行"};
            for(int i=0;i<4;i++){int p=pages[i];var c=Card("Shortcut"+p,1110,80+i*112,220,96);Icon(c,i+8,8,8,76);Action(c,labels[i],88,22,120,()=>ShowPage(p));}
            int[] bottom={2,3,4,5,7,8};string[] bnames={"角色","修炼","装备","背包","魂玉","秘境"};
            for(int i=0;i<6;i++){int p=bottom[i];Action(_body,bnames[i],i*224,608,210,()=>ShowPage(p));}
        }
        private void Character()
        {
            Hero(_body,_female,180,-10,450,675);
            var c=Card("CharacterStats",754,0,576,540);
            Text(c,"Name",_female?"云梦仙子":"清风道人",36,28,504,68,44,_accent);
            Text(c,"Class",_female?"灵法 · 远程 · 控制":"剑修 · 近战 · 爆发",36,112,504,48,25);
            Text(c,"Lore","御剑乘风，云海为伴。\n心有一念，万法归一。",36,195,504,110,28);
            Text(c,"Stats","生命  12,600          攻击  1,280\n防御     680          速度     125",36,354,504,100,25);
            Action(c,"查看装备",36,468,236,()=>ShowPage(4));Action(c,"灵根天赋",304,468,236,()=>ShowPage(6));
            Action(_body,"清风道人",28,590,270,()=>{_female=false;Build();});Action(_body,"云梦仙子",320,590,270,()=>{_female=true;Build();});
            Action(_body,"选定此角色",754,590,576,()=>Note("已切换展示角色；未写入角色存档。"));
        }
        private void Cultivation()
        {
            var c=Card("Realm",0,0,474,670);Icon(c,12,145,40,180);
            Text(c,"RealmName","筑基 · 初期",40,250,394,72,42,_accent);
            Text(c,"Progress","灵气积累    6,800 / 10,000",40,360,394,48,24);
            Bar(c,40,430,394,.68f,_accent);Text(c,"Hint","静心凝神，温养灵根。",40,475,394,60,25);
            Action(c,"凝神修炼",40,580,394,()=>Modal("凝神修炼","这里将展示修炼收益与突破条件。\n当前仅为 UI 设计，不结算修为。"));
            string[] names={"灵根修习","灵器养成","秘境历练"};int[] pages={6,4,8};
            for(int i=0;i<3;i++){int p=pages[i];var card=Card("Path"+i,504,i*230,826,210);Icon(card,i+1,24,30,120);Text(card,"Title",names[i],170,24,580,52,32,_accent);Text(card,"Text",new[]{"四系灵根，各有所长。","淬炼锋芒，铸就仙器。","以战悟道，踏破迷境。"}[i],170,90,580,44,24);Action(card,"前往 →",590,144,206,()=>ShowPage(p));}
        }
        private void Equipment()
        {
            Hero(_body,_female,252,0,430,645);
            string[] slots={"灵剑","仙冠","法袍","护腕","玉佩","云履"};
            for(int i=0;i<6;i++){int n=i;float x=i<3?0:622,y=(i%3)*190;var c=Card("Slot"+i,x,y,168,170);Icon(c,i,44,12,80);Action(c,slots[i],10,108,148,()=>{_selection=n;Build();});}
            var d=Card("EquipmentDetail",820,0,510,670);Icon(d,_selection,170,24,160);
            Text(d,"Name",new[]{"落霞灵剑","玄玉道冠","玄霜仙衣","星纹腕甲","太虚灵佩","追风云履"}[_selection],30,206,450,62,36,_accent);
            Text(d,"Meta","极品   ·   Lv. 30   ·   精炼 +3",30,280,450,48,23);
            Text(d,"Stats","基础属性\n\n攻击  +280\n生命  +1,200\n防御  +96",30,355,450,216,26);
            Action(d,"更换装备",30,594,210,()=>ShowPage(5));Action(d,"精炼",270,594,210,()=>Modal("灵器精炼","材料、消耗与属性变化将在此确认。"));
        }
        private void Inventory()
        {
            Tabs(new[]{"全部","装备","材料","消耗品"},168);
            var grid=Scroll(_body,"ItemGrid",0,78,840,592,660);
            for(int i=0;i<20;i++){int n=i;float x=(i%5)*168,y=(i/5)*160;var b=Button(grid,"Item"+i,"",x,y,150,144,Alpha(_ink,.94f),()=>{_selection=n;Build();});Icon(b.transform,ItemIcon(i),32,4,86);Text(b.transform,"Name",ItemName(i),10,100,130,36,19);if(i==_selection)Box(b.transform,"Selected",0,0,150,3,_accent);}
            var d=Card("ItemDetail",870,0,460,670);Icon(d,ItemIcon(_selection),140,30,170);
            Text(d,"Name",ItemName(_selection),28,224,404,64,34,_accent);Text(d,"Rarity","极品 · 绑定",28,300,404,42,24);
            Text(d,"Description","蕴藏天地灵气的修行之物。\n\n用途、来源、数量与操作入口\n会随道具类型变化。\n\n持有数量：24（示意）",28,376,404,204,23);
            Action(d,"查看来源",28,596,190,()=>Modal("获取途径","青云秘境\n宗门贡献兑换\n限时活动奖励"));Action(d,"使用",242,596,190,()=>Note("使用按钮反馈预览；不扣除道具。"));
        }
        private int ItemIcon(int i){return new[]{0,1,6,7,8,9,10,11}[(i+_tab*2)%8];}
        private string ItemName(int i){return new[]{"落霞灵剑","玄玉道冠","聚灵丹","玄铁","灵石","青莲玉","凝神露","星纹符"}[(i+_tab*2)%8];}
        private void Talents()
        {
            Tabs(new[]{"剑道","灵法","山岳","雷霆"},196);
            Text(_body,"Points","可用天赋点  8",1010,0,320,48,26,_accent);
            var tree=Card("TalentTree",0,80,860,590);
            for(int row=0;row<4;row++)for(int col=0;col<3;col++)
            {int n=row*3+col;float x=74+col*260,y=30+row*136;if(row<3)Box(tree,"Branch"+n,x+51,y+84,2,62,Alpha(_accent,.4f));var b=Button(tree,"Node"+n,"",x,y,104,104,Alpha(_accent,n==_selection?.4f:.12f),()=>{_selection=n;Build();});Icon(b.transform,(_tab*3+col)%15,12,2,80);Text(b.transform,"Rank",row<2?"1 / 3":"0 / 3",8,78,88,24,17);}
            var d=Card("TalentDetail",890,80,440,590);Icon(d,_tab*3,140,20,140);
            Text(d,"Name",new[]{"剑意凝心","灵息流转","磐石守御","紫电惊雷"}[_tab],28,178,384,60,34,_accent);
            Text(d,"Effect","第 "+(_selection+1)+" 节点\n\n当前效果：属性提升 3%\n下级效果：属性提升 6%\n\n修习消耗：1 天赋点",28,264,384,214,24);
            Action(d,"修习",28,510,180,()=>Note("天赋选中与修习反馈预览。"));Action(d,"重置…",232,510,180,()=>Modal("重置天赋","重置确认弹窗预览。\n不会修改已有天赋配置。"));
        }
        private void SoulJade()
        {
            Tabs(new[]{"魂玉镶嵌","魂玉淬炼","魂玉升阶"},196);
            var ring=Card("Sockets",0,80,640,590);Icon(ring,4,230,180,180);
            for(int i=0;i<6;i++){int n=i;float a=i*Mathf.PI/3;float x=265+205*Mathf.Sin(a),y=235-190*Mathf.Cos(a);var b=Button(ring,"Socket"+i,"",x,y,106,106,Alpha(_accent,.25f),()=>{_selection=n;Build();});JadeIcon(b.transform,i,10,0,86);}
            var d=Card("JadeDetail",670,80,660,590);JadeIcon(d,_selection,28,24,130);Text(d,"Name",new[]{"烈阳 · 破阵","贯日 · 长弓","玄武 · 磐石","苍狼 · 疾步","赤霄 · 无妄","伏龙 · 裂空"}[_selection],182,34,450,58,32,_accent);Text(d,"Rarity","神品魂玉 · 近战增益",182,105,450,42,22);
            Text(d,"Affixes","主词条    近战伤害 +8%\n\n附加词条  攻击 +120\n                暴击率 +3%\n                生命 +680",30,212,600,212,26);
            Action(d,new[]{"替换魂玉","预览淬炼","预览升阶"}[_tab],30,494,286,()=>Modal("魂玉操作",new[]{"选择魂玉后确认替换。","新旧词条对比将在这里展示。","升阶材料与属性变化将在这里展示。"}[_tab]));Action(d,"查看背包",344,494,286,()=>ShowPage(5));
        }
        private void Battle()
        {
            Text(_body,"Encounter","青云秘境 · 第一重",0,0,600,60,35,_accent);Text(_body,"Turn","回合  03 / 20",1020,0,310,60,28);
            Hero(_body,false,90,75,310,460);Hero(_body,true,916,75,310,460);
            Text(_body,"Player","清风道人",90,540,400,42,26);Bar(_body,90,590,360,.82f,Hex("#7DD7B0"));
            Text(_body,"Opponent","秘境守卫 · 示意立绘",900,540,420,42,26);Bar(_body,900,590,360,.46f,Hex("#F28B78"));
            var log=Card("BattleTimeline",470,106,400,300);Text(log,"Title","战斗记录",24,16,350,44,28,_accent);Text(log,"Lines","清风道人 施放「御剑诀」\n造成 1,280 点伤害\n\n守卫 触发「护体」\n获得 680 点护盾",24,82,350,190,23);
            Action(_body,"自动战斗 · 开",470,470,190,()=>Note("自动战斗按钮状态示意。"));Action(_body,"战斗速度 ×2",680,470,190,()=>Note("战斗速度切换示意。"));Action(_body,"查看结算",540,590,260,()=>Modal("试炼胜利","战斗结算设计示意\n\n用时 01:28     回合 8\n灵石 ×680     玄铁 ×12\n\n奖励不写入存档。"));
        }
        private void Tasks()
        {
            Tabs(new[]{"主线","每日","成就"},180);
            string[] names={"初入仙途","灵根初醒","锋芒初试","青云之上"};
            for(int i=0;i<4;i++){int n=i;var c=Card("Task"+i,0,76+i*146,830,132);Text(c,"Index","0"+(i+1),22,20,100,70,42,_accent);Text(c,"Name",names[i],126,16,480,46,28);Text(c,"Progress",i==0?"完成第一次秘境试炼   1 / 1":"完成修行目标   0 / 1",126,76,480,32,22);Action(c,"查看",644,40,160,()=>{_selection=n;Build();});}
            var d=Card("TaskDetail",860,76,470,584);Text(d,"Name",names[_selection],30,28,410,70,36,_accent);Text(d,"Narrative","山门初开，仙途漫长。\n循着云海深处的指引，\n完成属于你的修行。",30,130,410,150,27);Text(d,"Rewards","任务奖励",30,310,410,45,25,_accent);Icon(d,10,30,380,100);Icon(d,11,160,380,100);Action(d,_selection==0?"领取奖励":"前往完成",30,504,410,()=>{if(_selection==0)Note("奖励领取反馈示意。功能阶段接入任务数据。");else ShowPage(8);});
        }
        private void Sect()
        {
            Tabs(new[]{"宗门总览","成员名册","宗门事务"},200);
            var hero=Card("SectIdentity",0,80,470,590);Icon(hero,4,140,26,190);Text(hero,"Name","青云仙宗",34,250,402,68,42,_accent);Text(hero,"Meta","宗门等级  5     成员  38 / 50",34,342,402,56,24);Text(hero,"Motto","愿与同道，共问长生。",34,430,402,68,27);Action(hero,"宗门列表",34,516,190,()=>Modal("寻找宗门","按名称或编号查找宗门的交互示意。"));Action(hero,"捐献",246,516,190,()=>Modal("宗门捐献","捐献数量与贡献收益将在这里确认。"));
            var d=Card("SectContent",500,80,830,590);Text(d,"Heading",new[]{"仙门公告","成员名册","宗门事务"}[_tab],28,22,774,56,32,_accent);
            if(_tab==0){Text(d,"Notice","本周宗门试炼将于酉时开启。\n诸位同门可提前备好灵器与丹药。\n\n同心同德，共守山门。",28,118,774,210,28);Text(d,"Contribution","我的贡献  1,280\n本周活跃  860",28,390,774,124,26);}
            else for(int i=0;i<5;i++){Text(d,"Row"+i,_tab==1?new[]{"清虚真人     宗主       Lv. 60","云梦仙子     长老       Lv. 48","清风道人     弟子       Lv. 32","白鹤散人     弟子       Lv. 30","青莲剑客     弟子       Lv. 28"}[i]:new[]{"宗门试炼    每周挑战","灵田照料    日常任务","仙门建设    贡献提升","藏经阁      功法研习","贡献商店    灵器兑换"}[i],28,104+i*82,774,65,26);}
        }
        private void Events()
        {
            string[] titles={"云海问剑","七日登仙","灵玉寻踪"};
            for(int i=0;i<3;i++){int n=i;var c=Card("Event"+i,i*452,0,426,670);Icon(c,i+10,118,36,190);Text(c,"Badge",i==0?"限时挑战":"仙途盛会",28,260,370,40,22,_accent);Text(c,"Name",titles[i],28,320,370,65,40);Text(c,"Description",new[]{"挑战秘境，累积试炼积分。","每日修行，解锁成长馈赠。","游历山河，寻访灵玉秘宝。"}[i],28,418,370,90,26);Text(c,"Time","剩余  3 天 12 小时",28,526,370,42,23,_accent);Action(c,"查看活动",28,594,370,()=>Modal(titles[n],"活动规则、奖励预览与参与进度。\n\n当前进度  3 / 10（示意）\n\n活动开放时间以功能开发后的数据为准。"));}
        }
        private void Gifts()
        {
            Box(_body,"GiftTitleShade",0,0,1330,142,Alpha(_ink,.72f));Text(_body,"Headline","七日修行 · 每日有礼",0,0,900,70,44,_accent);Text(_body,"Sub","连续修行，领取仙途馈赠。",0,86,900,45,27);
            for(int i=0;i<7;i++){int n=i;float x=i<4?i*244:(i-4)*244,y=i<4?166:414;float w=i==6?464:220;var c=Card("Day"+i,x,y,w,226);Text(c,"Day","第 "+(i+1)+" 天",20,14,w-40,42,25,_accent);Icon(c,i==6?14:i+7,20,66,100);Text(c,"Reward",i==6?"珍稀灵器自选箱":"修行馈赠 ×"+(i+1),i==6?148:20,i==6?100:166,w-40,44,22);Button(c,"Claim","",0,0,w,226,Color.clear,()=>Note("第 "+(n+1)+" 天领取状态预览；不发放奖励。"));}
            var big=Card("GrandReward",1000,166,330,474);Icon(big,14,55,26,220);Text(big,"Title","登仙礼盒",28,272,274,58,34,_accent);Action(big,"奖励一览",28,382,274,()=>Modal("登仙礼盒","极品灵器 ×1\n聚灵丹 ×20\n灵石 ×6,800\n\n奖励内容为设计示意。"));
        }
        private void Partners()
        {
            Hero(_body,_female,60,0,450,650);var c=Card("PartnerProfile",660,0,670,670);
            Text(c,"Name",_female?"云梦仙子":"清风道人",32,24,606,70,44,_accent);Text(c,"Role","伙伴图鉴 · 羁绊展示",32,116,606,48,26);
            Text(c,"Story","云海相逢，携手问道。\n\n定位  灵法辅助\n羁绊  青云同道\n技能  云梦回春 · 灵息护体",32,208,606,240,28);
            Action(c,"切换伙伴",32,510,290,()=>{_female=!_female;Build();});Action(c,"查看羁绊",348,510,290,()=>Modal("青云同道","伙伴羁绊与上阵加成的详情示意。"));Action(c,"上阵",32,592,606,()=>Note("伙伴上阵反馈预览；未改变阵容。"));
        }
        private void Ranking()
        {
            Tabs(new[]{"战力榜","秘境榜","宗门榜"},200);
            var podium=Card("Champion",0,80,470,590);Icon(podium,14,135,24,200);Text(podium,"Rank","第一名",32,268,406,60,40,_accent);Text(podium,"Name","清虚真人",32,355,406,62,36);Text(podium,"Score","问道战力  128,600",32,456,406,50,27);
            var list=Card("Ranking",500,80,830,590);string[] names={"云梦仙子","青莲剑客","白鹤散人","清风道人","玄霜道人","蓬莱过客"};
            for(int i=0;i<6;i++){int n=i;Text(list,"Row"+i,(i+2).ToString("00")+"     "+names[i]+"         "+(126800-i*7600).ToString("N0"),28,20+i*88,630,68,27,i==3?_accent:Paper);Action(list,"详情",654,28+i*88,148,()=>Modal(names[n],"玩家名片、角色与宗门信息的设计示意。"));}
        }
        private void Estate()
        {
            var c=Card("EstateInfo",0,0,370,670);Text(c,"Name","云栖洞府",26,30,318,70,40,_accent);Text(c,"Level","洞府等级  3",26,134,318,50,28);Text(c,"Output","每时灵气  +120\n灵田产量  +8%\n静修效率  +6%",26,250,318,190,26);Action(c,"升级洞府",26,588,318,()=>Modal("洞府升级","升级条件、材料消耗与收益变化。"));
            string[] titles={"灵田","炼丹房","藏经阁","静修室"};
            for(int i=0;i<4;i++){int n=i;float x=400+i%2*480,y=i/2*345;var b=Card("Building"+i,x,y,450,320);Icon(b,i+3,24,26,130);Text(b,"Name",titles[i],180,40,240,56,34,_accent);Text(b,"Status",i==0?"灵草即将成熟":"洞府建筑 · Lv. 2",180,116,240,62,23);Action(b,"进入",24,240,402,()=>Modal(titles[n],"建筑内部布局与生产操作将在此展开。\n本轮完成页面设计，不开始计时生产。"));}
        }
        private void Settings()
        {
            Tabs(new[]{"声音","画面","操作"},190);var c=Card("Settings",0,80,1330,590);
            Text(c,"Title",new[]{"声音设置","画面设置","操作设置"}[_tab],34,24,1240,60,34,_accent);
            if(_tab==0){Text(c,"Volume","主音量    "+Mathf.RoundToInt(_volume*100)+"%",34,150,900,60,30);Bar(c,34,252,970,_volume,_accent);Action(c,"−",1050,224,100,()=>{_volume=Mathf.Clamp01(_volume-.1f);Build();});Action(c,"+",1180,224,100,()=>{_volume=Mathf.Clamp01(_volume+.1f);Build();});Text(c,"Help","拖动条与数值状态的设计示意；不会改变设备音量。",34,376,1200,80,25);}
            else if(_tab==1){string[] modes={"流畅","均衡","精致"};for(int i=0;i<3;i++){int n=i;Action(c,modes[i],34+i*420,160,390,()=>Note("已选中画质方案："+modes[n]+"（仅预览）"));}Text(c,"Resolution","显示比例   16:9\n参考分辨率   1600 × 900\n窗口变化时等比缩放，不拉伸美术",34,310,1200,170,28);}
            else Text(c,"Controls","点击条目：查看详情\n滚轮：滚动列表\nESC：关闭弹窗 / 返回大厅\n左侧设计目录：切换所有页面",34,140,1200,300,30);
        }
        private static void Bar(Transform p,float x,float y,float width,float amount,Color color)
        {Box(p,"Track",x,y,width,12,new Color(.04f,.04f,.05f,.8f));Box(p,"Fill",x,y,width*Mathf.Clamp01(amount),12,color);}
        private Transform Modal(string title,string description)
        {
            if(_modal!=null)Destroy(_modal);
            var shade=Box(_design,"Modal",0,0,1600,900,new Color(0,0,0,.72f));shade.raycastTarget=true;_modal=shade.gameObject;
            var c=Box(shade.transform,"Dialog",480,170,640,560,Alpha(_ink,1));c.raycastTarget=true;
            Box(c.transform,"Trim",0,0,640,3,_accent);Text(c.transform,"Title",title,32,24,576,64,36,_accent);Text(c.transform,"Description",description,32,112,576,330,26);
            Action(c.transform,"关闭",32,478,576,()=>Destroy(_modal));return c.transform;
        }
    }
}
