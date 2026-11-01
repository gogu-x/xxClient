using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static XiuXian.Core.ImmortalArtKit;

namespace XiuXian.Core
{
    public sealed partial class ImmortalUIArtPreview
    {
        public bool Live { get; private set; }
        private ImmortalGameService _service;
        private RoleSoulJadePanel _jades;
        private bool _wasAuthenticated;
        private string _accountInput="",_passwordInput="",_guildInput="";
        public static ImmortalUIArtPreview OpenLive()
        {
            var ui=Open();if(ui.Live)return ui;
            UIManager.Instance.HideLegacyPanels();
            ui.Live=true;ui._accountInput=GuestAccount.GetOrCreateAccount();
            ui._service=ui.gameObject.AddComponent<ImmortalGameService>();ui._service.Changed+=ui.LiveChanged;
            var host=new GameObject("LocalSoulJadeStore");host.transform.SetParent(ui.transform,false);
            ui._jades=host.AddComponent<RoleSoulJadePanel>();ui._jades.Close();
            ui.ShowPage(0);return ui;
        }
        public static bool RouteLive(UIScreen screen)
        {
            var ui=FindObjectOfType<ImmortalUIArtPreview>();if(ui==null||!ui.Live)return false;
            if(screen==UIScreen.Battle || screen==UIScreen.Realm){if(ui._design!=null)ui._design.gameObject.SetActive(false);return false;}
            UIManager.Instance.HideLegacyPanels();
            ui.ShowPage(screen==UIScreen.Home?0:screen==UIScreen.Character?2:screen==UIScreen.XianFu?15:1);return true;
        }
        private void LiveChanged()
        {
            if(this==null)return;
            bool login=_service.Authenticated&&!_wasAuthenticated;_wasAuthenticated=_service.Authenticated;
            if(login){_passwordInput="";ShowPage(1);_service.LoadEquipment();return;}
            if(!_service.Authenticated&&CurrentPage!=0){ShowPage(0);return;}
            // Preserve typed form values and focus while pending/status changes arrive.
            if(CurrentPage==0){if(_feedback!=null)_feedback.text=_service.Status;return;}
            if(_design!=null&&_design.gameObject.activeSelf)Build();
        }
        private void LiveButton(Transform p,string label,float x,float y,float w,UnityEngine.Events.UnityAction cb,bool enabled=true)
        {var b=Button(p,label,label,x,y,w,52,Color.white,cb);b.interactable=enabled&&!_service.Busy;}
        private void Confirm(string title,string body,UnityEngine.Events.UnityAction cb)
        {var c=Modal(title,body);LiveButton(c,"确认",44,398,552,()=>{if(_modal!=null){_modal.SetActive(false);Destroy(_modal);}cb();});}
        private void BuildLivePage()
        {
            switch(CurrentPage)
            {
                case 0: LiveLogin();break;
                case 1: Lobby();SetNamed("Name",_service.Account);SetNamed("Realm",_service.Equipment==null?"角色资料待同步":"角色等级 "+_service.Equipment.RoleLevel);SetNamed("Goal","前往秘境 · 开始试炼");break;
                case 2: LiveCharacter();break;
                case 4:case 5: LiveEquipment();break;
                case 6: LiveTalents();break;
                case 7: LiveJades();break;
                case 8: LiveBattle();break;
                case 10: LiveGuild();break;
                case 11:case 12: LiveActivities();break;
                case 16: LiveSettings();break;
                default: MissingPage();break;
            }
        }
        private void SetNamed(string name,string value)
        {foreach(var t in _body.GetComponentsInChildren<TMP_Text>(true))if(t.name==name)t.text=value;}
        private void LiveLogin()
        {
            Foreground(_body,"hero_male",20,-30,620,700);
            Text(_body,"Slogan","三千大道 · 自在修仙",10,540,720,90,42);
            var c=Card("Login",780,0,550,650);Text(c,"Title","重返仙途",40,28,470,60,38,_accent);
            var account=Input(c,"Account","账号",40,132,470,Color.white);account.text=_accountInput;account.onValueChanged.AddListener(v=>_accountInput=v);
            var password=Input(c,"Password","密码",40,214,470,Color.white);password.contentType=TMP_InputField.ContentType.Password;password.text=_passwordInput;password.onValueChanged.AddListener(v=>_passwordInput=v);
            // These remain clickable to support retry after an asynchronous timeout.
            Action(c,"连接 / 查询我的区服",40,304,470,()=>_service.QueryServers(_accountInput));
            Action(c,"选择区服 · "+_service.ServerId,40,382,470,LiveServers);
            Action(c,"进入仙途",40,462,470,()=>_service.Login(_accountInput,_passwordInput,false));
            Action(c,"创建角色",40,540,220,()=>Confirm("创建角色","将在当前选择的区服创建角色，成功后登录。",()=>_service.Login(_accountInput,_passwordInput,true)));
            Action(c,"游客账号",290,540,220,()=>{_accountInput=GuestAccount.GetOrCreateAccount();_passwordInput=GuestAccount.GetPassword();Build();_service.QueryServers(_accountInput);});
        }
        private void LiveServers()
        {
            var c=Modal("选择仙域","已有角色区服来自服务端；新角色区服沿用客户端开放配置。");
            int count=_service.Accounts.Count;
            var list=Scroll(c,"ServerList",44,220,552,240,Mathf.Max(240,(count==0?3:count)*64));
            for(int i=0;i<(count==0?3:count);i++){uint id=count==0?(uint)(i+1):_service.Accounts[i].ServerId;LiveButton(list,"仙域 "+id+(count==0?" · 可创建角色":" · 已有角色"),4,i*64,520,()=>{_service.ServerId=id;Build();});}
        }
        private void LiveCharacter()
        {
            _female=PlayerPrefs.GetInt(CharacterPanel.SelectedCharacterKey,0)==1;Hero(_body,_female,70,0,580,670);
            var c=Card("Character",740,0,590,650);Text(c,"Name",_service.Account,35,24,520,64,38,_accent);
            var e=_service.Equipment;Text(c,"Data",e==null?"角色数据尚未同步":("等级 "+e.RoleLevel+"\n\n装备加成\n生命 +"+e.MaxHp+"\n攻击 +"+e.Attack+"\n防御 +"+e.Defense+"\n速度 +"+e.Speed),35,120,520,350,28);
            LiveButton(c,"刷新资料",35,480,250,_service.LoadEquipment);LiveButton(c,"查看装备",305,480,250,()=>ShowPage(4));
            LiveButton(c,"切换展示角色（本地）",35,560,520,()=>{_female=!_female;PlayerPrefs.SetInt(CharacterPanel.SelectedCharacterKey,_female?1:0);PlayerPrefs.Save();Build();});
        }
        private void LiveEquipment()
        {
            var data=_service.Equipment;Tabs(new[]{"灵剑","仙冠","法袍","护腕","玉佩","云履"},205);
            if(data==null){var c=Card("Waiting",0,100,1330,550);Text(c,"State","尚未获取灵器仓库",50,70,1200,80,38);Text(c,"Hint","登录后刷新装备，属性与穿戴状态以服务器为准。",50,190,1200,70,26);LiveButton(c,"刷新装备",50,370,400,_service.LoadEquipment);return;}
            var list=Scroll(_body,"EquipmentList",0,80,790,570,Mathf.Max(570,data.Items.Count*152));
            EquipmentItem selected=null;int row=0;
            foreach(var item in data.Items)
            {
                if((int)item.Slot!=_tab+1)continue;var captured=item;int index=row;
                var hit=Button(list,"Equipment"+row,"",10,row*152+8,750,140,Color.clear,()=>{_selection=index;Build();});
                ImmortalArtKit.Art(hit.transform,"controls","tab_inactive",0,0,750,140,false);Icon(hit.transform,_tab,15,12,112);
                Text(hit.transform,"Name","灵器 #"+item.ConfigId+" · Lv."+item.Level+(item.Equipped?" · 已穿戴":""),145,18,570,55,27);
                Text(hit.transform,"Attr","攻击 +"+item.Attack+"   生命 +"+item.MaxHp,145,76,570,40,23);
                if(row==_selection)selected=captured;row++;
            }
            if(row==0)Text(list,"Empty","当前槽位没有灵器",35,90,700,60,32);
            var d=Card("EquipmentDetails",840,80,490,570);Icon(d,_tab,160,5,160);
            Text(d,"Info",selected==null?"请选择灵器":"灵器 #"+selected.ConfigId+"\n等级 "+selected.Level+" · 精炼 +"+selected.RefineLevel+"\n\n生命 +"+selected.MaxHp+"\n攻击 +"+selected.Attack+"\n防御 +"+selected.Defense+"\n速度 +"+selected.Speed,30,180,430,260,25);
            var target=selected;LiveButton(d,target!=null&&target.Equipped?"卸下":"穿戴",30,455,200,()=>_service.Equip(target,target.Equipped),target!=null);LiveButton(d,"刷新",260,455,200,_service.LoadEquipment);
            Text(d,"Missing","精炼、材料背包接口尚未提供",30,520,430,36,19);
        }
        private void LiveActivities()
        {
            LiveButton(_body,"刷新活动",1050,0,280,_service.LoadActivities);
            Text(_body,"Heading",CurrentPage==12?"活动奖励":"仙途盛会",0,0,800,60,38,_accent);
            var list=Scroll(_body,"Activities",0,80,760,580,Mathf.Max(580,_service.Activities.Count*190));
            for(int i=0;i<_service.Activities.Count;i++){var a=_service.Activities[i];int n=i;var hit=Button(list,"Activity"+i,"",0,i*190,740,174,Color.clear,()=>{_selection=n;_service.LoadProgress(a.Id);});ImmortalArtKit.Art(hit.transform,"event_art",new[]{"sword_trial","seven_days","jade_cave"}[i%3],0,0,220,170,false);Text(hit.transform,"Name",a.Name,250,22,470,60,30);Text(hit.transform,"State",a.Status.ToString()+" · #"+a.Id,250,100,470,40,21);}
            if(_service.Activities.Count==0)Text(list,"Empty",_service.ActivitiesLoaded?"当前没有开放活动":"点击刷新获取活动列表",30,100,700,70,32);
            var d=Card("ActivityDetails",810,80,520,570);
            if(_service.Activities.Count==0){Text(d,"Hint","活动内容与领奖条件\n由服务器提供。",35,140,450,200,28);return;}
            _selection=Mathf.Clamp(_selection,0,_service.Activities.Count-1);var chosen=_service.Activities[_selection];var progress=_service.Progress;
            Text(d,"Title",chosen.Name,30,30,460,65,34,_accent);
            bool valid=progress!=null&&progress.ActivityId==chosen.Id;
            Text(d,"Progress",valid?("进度 "+progress.Progress+" / "+progress.Target+"\n\n"+(progress.Rewarded?"已领取奖励":"奖励尚未领取")):"选择活动后同步进度",30,170,460,180,28);
            LiveButton(d,"参与",30,390,210,()=>_service.JoinActivity(chosen.Id));LiveButton(d,"查进度",280,390,210,()=>_service.LoadProgress(chosen.Id));
            LiveButton(d,"领取奖励",30,480,460,()=>_service.Claim(chosen.Id),valid&&!progress.Rewarded&&progress.Progress>=progress.Target);
        }
        private void LiveGuild()
        {
            ImmortalArtKit.Art(_body,"navigation","sect",30,20,550,470);
            var c=Card("Guild",640,0,690,650);var guild=_service.Guild;
            Text(c,"Title",guild==null?"寻访仙宗":guild.Name,35,24,620,65,38,_accent);
            var input=Input(c,"GuildInput","宗门编号 / 创建时输入名称",35,112,620,Color.white);input.text=_guildInput;input.onValueChanged.AddListener(v=>_guildInput=v);
            LiveButton(c,"查询",35,195,190,()=>{ulong id;if(ulong.TryParse(_guildInput,out id)&&id>0)_service.LoadGuild(id);else Note("请输入有效宗门编号");});
            LiveButton(c,"加入",250,195,190,()=>{ulong id;if(ulong.TryParse(_guildInput,out id)&&id>0)Confirm("加入宗门","确认加入宗门 #"+id+"？",()=>_service.JoinGuild(id));else Note("请输入有效宗门编号");});
            LiveButton(c,"创建",465,195,190,()=>Confirm("创建宗门","确认创建「"+_guildInput+"」？",()=>_service.CreateGuild(_guildInput)));
            var list=Scroll(c,"Members",35,285,620,250,guild==null?250:Mathf.Max(250,100+guild.Members.Count*45));
            Text(list,"Notice",guild==null?"查询宗门信息，或输入名称创建宗门。":"编号 "+guild.GuildId+"\n"+guild.Notice,0,0,610,90,24);
            if(guild!=null){for(int i=0;i<guild.Members.Count;i++){var m=guild.Members[i];Text(list,"Member"+i,m.Name+"  Lv."+m.Level+"  "+m.Role,0,95+i*45,610,42,22);}LiveButton(c,"退出当前宗门",35,560,620,()=>Confirm("退出宗门","确认退出当前所属宗门？",_service.LeaveGuild));}
        }
        private void LiveTalents()
        {
            Tabs(new[]{"破军","御灵","守御","身法"},195);Text(_body,"Points","本地天赋点 "+RoleTalentPanel.AvailablePoints,940,0,390,50,28);
            var list=Scroll(_body,"TalentNodes",0,80,850,580,960);
            for(int i=0;i<24;i++){int n=i;var b=Button(list,"Talent"+i,"",i%3*275,i/3*120,250,110,Color.clear,()=>{_selection=n;Build();});ImmortalArtKit.Art(b.transform,"frames",RoleTalentPanel.ArtTalentLit(_tab,i)?"socket_selected":"socket_octagon",0,0,100,100);Text(b.transform,"Name",RoleTalentPanel.ArtTalentName(_tab,i),110,24,135,65,22);}
            var c=Card("TalentDetail",900,80,430,570);Text(c,"Title",RoleTalentPanel.ArtTalentName(_tab,_selection),30,28,370,65,32,_accent);Text(c,"Effect",RoleTalentPanel.ArtTalentEffect(_tab,_selection),30,150,370,150,28);
            Text(c,"Scope","沿用本机天赋存档\n尚无服务端天赋同步接口",30,310,370,90,22);
            LiveButton(c,"修习",30,440,170,()=>{bool ok=RoleTalentPanel.ArtUnlock(_tab,_selection);Build();Note(ok?"已保存本地天赋":"前置节点或天赋点不足，或已修习");});LiveButton(c,"重置",230,440,170,()=>Confirm("重置本地天赋","将清空本机已分配天赋点。",()=>{RoleTalentPanel.ArtReset();Build();}));
        }
        private void LiveJades()
        {
            Foreground(_body,"soul_array",0,10,650,650);var rows=_jades.ArtInventory();
            for(int i=0;i<6;i++){int idx=_jades.ArtEquipped(i);float a=i*Mathf.PI/3;var b=Button(_body,"SoulSlot"+i,"",270+205*Mathf.Sin(a),270-210*Mathf.Cos(a),110,110,Color.clear,()=>{if(idx>=0){_selection=idx;Build();}});if(idx>=0&&idx<rows.Count)JadeIcon(b.transform,rows[idx].Icon,5,5,100);}
            var c=Card("SoulDetail",710,0,620,650);Text(c,"Title","魂玉 · 本地配装",30,20,560,65,36,_accent);
            var list=Scroll(c,"JadeInventory",30,115,560,180,Mathf.Max(180,rows.Count*64));
            for(int i=0;i<rows.Count;i++){int n=i;var j=rows[i];Button(list,"Jade"+i,j.Name+" Lv."+j.Level+(j.Equipped?" · 已装备":""),0,i*64,540,55,Color.white,()=>{_selection=n;Build();});}
            if(rows.Count==0){Text(c,"Empty","本地魂玉库为空",30,350,560,90,28);return;}
            _selection=Mathf.Clamp(_selection,0,rows.Count-1);var item=rows[_selection];Text(c,"Affixes",item.Name+"\n"+item.Affixes,30,320,560,170,24);
            LiveButton(c,item.Equipped?"卸下":"装备",30,520,260,()=>{_jades.ArtEquip(item.Index,item.Equipped);Build();});
            Text(c,"Missing","本地存档；升级、洗练及服务器同步未实现",30,590,560,36,19);
        }
        private void LiveBattle()
        {
            Hero(_body,false,20,0,650,650);var c=Card("BattleEntry",760,100,570,460);
            Text(c,"Title","青云试炼",35,25,500,70,42,_accent);Text(c,"Hint","使用现有战斗接口与战报播放。\n\n挑战结果以服务器回包为准。",35,140,500,160,27);
            LiveButton(c,"开始挑战",35,340,500,()=>BattleView.Show(transform),_service.Authenticated);
        }
        private void LiveSettings()
        {
            var c=Card("Settings",220,20,900,610);Text(c,"Title","声音与画面",40,30,820,70,38,_accent);
            Text(c,"Volume","主音量 "+Mathf.RoundToInt(AudioListener.volume*100)+"%",40,160,820,60,30);
            Bar(c,40,260,820,AudioListener.volume,_accent);
            LiveButton(c,"音量 −",40,330,390,()=>{AudioListener.volume=Mathf.Clamp01(AudioListener.volume-.1f);PlayerPrefs.SetFloat("ImmortalV3.Volume",AudioListener.volume);PlayerPrefs.Save();Build();});LiveButton(c,"音量 +",470,330,390,()=>{AudioListener.volume=Mathf.Clamp01(AudioListener.volume+.1f);PlayerPrefs.SetFloat("ImmortalV3.Volume",AudioListener.volume);PlayerPrefs.Save();Build();});
            Text(c,"Ratio","画面等比适配 16:9\n操作：点击选择，滚轮浏览，ESC 返回",40,450,820,110,27);
        }
        private void MissingPage()
        {
            string[] icons={"","","","cultivation","","","","","","task","","","","partner","rank","estate",""};
            string icon=icons[CurrentPage];if(!string.IsNullOrEmpty(icon))ImmortalArtKit.Art(_body,"navigation",icon,40,35,550,550);
            var c=Card("Unavailable",710,80,620,510);Text(c,"Title",PageNames[CurrentPage],35,30,550,70,40,_accent);
            Text(c,"State","功能暂未开放",35,170,550,70,32);
            Text(c,"Detail","当前版本未提供此功能的业务接口。\n界面美术已就绪，暂不展示虚构进度或奖励。",35,270,550,110,25);
            LiveButton(c,"返回大厅",35,420,550,()=>ShowPage(1));
        }
    }
}
