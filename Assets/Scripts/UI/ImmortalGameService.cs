using System;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;
using XiuXian.Network;

namespace XiuXian.Core
{
    /// <summary>Single owner of V3 UI requests. All writes require explicit UI actions.</summary>
    public sealed class ImmortalGameService : MonoBehaviour
    {
        public event Action Changed;
        public bool Authenticated { get; private set; }
        public bool Busy { get { return !string.IsNullOrEmpty(_pending); } }
        public string Status { get; private set; } = "请选择账号并连接服务器";
        public string Account { get; private set; } = "";
        public uint ServerId { get; set; } = 1;
        public RoleEquipmentData Equipment { get; private set; }
        public readonly List<ServerAccount> Accounts = new List<ServerAccount>();
        public readonly List<ActivityInfo> Activities = new List<ActivityInfo>();
        public ActivityProgress Progress { get; private set; }
        public GuildInfo Guild { get; private set; }
        public bool ActivitiesLoaded { get; private set; }
        private NetworkManager _net;
        private string _pending, _password;
        private float _deadline;
        private ulong _activityId, _guildId;
        private bool _queryAfterConnect;
        private readonly List<string> _handlers = new List<string>();
        private void OnEnable()
        {
            _handlers.Clear();_net=NetworkManager.Instance;
            _net.OnConnected+=Connected;_net.OnDisconnected+=Disconnected;_net.OnError+=Failed;
            Bind("ServerListAck",ServerListAck.Parser,(ServerListAck a)=>{if(!Finish("ServerListAck"))return;Accounts.Clear();if(a.Code==ErrCode.Ok){Accounts.AddRange(a.Accounts);if(Accounts.Count>0)ServerId=Accounts[0].ServerId;}Set(a.Code==ErrCode.Ok?(Accounts.Count==0?"暂无角色，请选择区服创建角色":"角色区服已同步"):LoadoutErrors.ToUserMessage(a.Code));});
            Bind("LoginAck",LoginAck.Parser,(LoginAck a)=>{if(!Finish("LoginAck"))return;Authenticated=a.Code==ErrCode.Ok;_password=null;Set(Authenticated?"登录成功":LoadoutErrors.ToUserMessage(a.Code));});
            Bind("RegisterAck",RegisterAck.Parser,(RegisterAck a)=>{if(!Finish("RegisterAck"))return;if(a.Code==ErrCode.Ok)Send(new LoginReq{Account=Account,Password=_password??"",ServerId=ServerId},"LoginAck","正在登录…",false);else{_password=null;Set(LoadoutErrors.ToUserMessage(a.Code));}});
            Bind("GetRoleEquipmentAck",GetRoleEquipmentAck.Parser,(GetRoleEquipmentAck a)=>{if(!Finish("GetRoleEquipmentAck"))return;ApplyEquipmentResponse(a.HasCode,a.Code,a.Data,"装备已同步");});
            Bind("ChangeRoleEquipmentAck",ChangeRoleEquipmentAck.Parser,(ChangeRoleEquipmentAck a)=>{if(!Finish("ChangeRoleEquipmentAck"))return;ApplyEquipmentResponse(a.HasCode,a.Code,a.Data,"装备操作完成");});
            Bind("GetActivityListAck",GetActivityListAck.Parser,(GetActivityListAck a)=>{if(!Finish("GetActivityListAck"))return;Activities.Clear();ActivitiesLoaded=a.HasCode&&a.Code==ErrCode.Ok;if(!ActivitiesLoaded){Set(LoadoutErrors.ResponseMessage(a.HasCode,a.Code));return;}Activities.AddRange(a.Activities);Set(Activities.Count==0?"当前无开放活动":"活动列表已同步");});
            Bind("JoinActivityAck",JoinActivityAck.Parser,(JoinActivityAck a)=>{if(!Finish("JoinActivityAck"))return;Set(a.Code==ErrCode.Ok?"参与成功":LoadoutErrors.ToUserMessage(a.Code));if(a.Code==ErrCode.Ok)LoadProgress(_activityId);});
            Bind("GetProgressAck",GetProgressAck.Parser,(GetProgressAck a)=>{if(!Finish("GetProgressAck"))return;Progress=a.Code==ErrCode.Ok?a.Progress:null;Set(a.Code==ErrCode.Ok?"活动进度已同步":LoadoutErrors.ToUserMessage(a.Code));});
            Bind("ClaimRewardAck",ClaimRewardAck.Parser,(ClaimRewardAck a)=>{if(!Finish("ClaimRewardAck"))return;Set(a.Code==ErrCode.Ok?"奖励领取成功":LoadoutErrors.ToUserMessage(a.Code));if(a.Code==ErrCode.Ok)LoadProgress(_activityId);});
            Bind("GetGuildAck",GetGuildAck.Parser,(GetGuildAck a)=>{if(!Finish("GetGuildAck"))return;Guild=a.Code==ErrCode.Ok?a.Guild:null;Set(a.Code==ErrCode.Ok?"宗门信息已同步":LoadoutErrors.ToUserMessage(a.Code));});
            Bind("CreateGuildAck",CreateGuildAck.Parser,(CreateGuildAck a)=>{if(!Finish("CreateGuildAck"))return;if(a.Code==ErrCode.Ok)Guild=a.Guild;Set(a.Code==ErrCode.Ok?"宗门已创建":LoadoutErrors.ToUserMessage(a.Code));});
            Bind("JoinGuildAck",JoinGuildAck.Parser,(JoinGuildAck a)=>{if(!Finish("JoinGuildAck"))return;Set(a.Code==ErrCode.Ok?"加入成功":LoadoutErrors.ToUserMessage(a.Code));if(a.Code==ErrCode.Ok)LoadGuild(_guildId);});
            Bind("LeaveGuildAck",LeaveGuildAck.Parser,(LeaveGuildAck a)=>{if(!Finish("LeaveGuildAck"))return;if(a.Code==ErrCode.Ok)Guild=null;Set(a.Code==ErrCode.Ok?"已退出宗门":LoadoutErrors.ToUserMessage(a.Code));});
        }
        private void ApplyEquipmentResponse(bool hasCode, ErrCode code, RoleEquipmentData data, string success)
        {
            if (!hasCode || code != ErrCode.Ok || data == null)
            {
                Equipment = null;
                Set(!hasCode || code != ErrCode.Ok ? LoadoutErrors.ResponseMessage(hasCode, code) : "装备数据暂不可用");
                return;
            }
            Equipment = data;
            Set(success);
        }
        private void Bind<T>(string name,Google.Protobuf.MessageParser<T> parser,Action<T> cb) where T:IMessage<T>
        {_handlers.Add(name);_net.RegisterProtoHandler(name,parser,cb);}
        private void OnDisable(){if(_net==null)return;_net.OnConnected-=Connected;_net.OnDisconnected-=Disconnected;_net.OnError-=Failed;foreach(var n in _handlers)_net.UnregisterHandler(n);_password=null;}
        private void Update(){if(Busy&&Time.unscaledTime>_deadline){_net.Disconnect();Disconnected("");Set("请求超时，连接已重置；请重新登录确认操作结果");}}
        private void Connected(){if(_queryAfterConnect){_queryAfterConnect=false;_pending=null;QueryServers(Account);}else Set("连接已建立");}
        private void Disconnected(string reason){Authenticated=false;Equipment=null;Guild=null;Progress=null;Activities.Clear();ActivitiesLoaded=false;Accounts.Clear();_pending=null;_password=null;_queryAfterConnect=false;Set("连接已断开，请重新登录");}
        private void Failed(string reason){_pending=null;_queryAfterConnect=false;_password=null;Set("连接或请求失败，请重试");}
        private bool Finish(string name){if(_pending!=name)return false;_pending=null;return true;}
        private void Set(string message){Status=string.IsNullOrEmpty(message)?"操作失败，请稍后重试":message;Changed?.Invoke();}
        private bool Send(IMessage request,string response,string text,bool needsLogin=true)
        {
            if(Busy){Set("请求处理中，请稍候");return false;}
            if(!_net.IsConnected||(needsLogin&&!Authenticated)){Set("请先连接并登录服务器");return false;}
            _pending=response;_deadline=Time.unscaledTime+12;Status=text;_net.SendProto(request);Changed?.Invoke();return true;
        }
        public void QueryServers(string account)
        {
            if(Busy)return;account=(account??"").Trim();if(account.Length==0){Set("请输入账号");return;}
            if(Account!=account){Accounts.Clear();Equipment=null;Guild=null;Activities.Clear();Progress=null;ActivitiesLoaded=false;Authenticated=false;}
            Account=account;
            if(!_net.IsConnected){_queryAfterConnect=true;_pending="connect";_deadline=Time.unscaledTime+12;Status="正在连接…";_net.Connect();Changed?.Invoke();return;}
            Send(new GetServerListReq{Account=Account},"ServerListAck","正在查询角色区服…",false);
        }
        public void Login(string account,string password,bool register)
        {
            if(Busy)return;if(string.IsNullOrWhiteSpace(account)||string.IsNullOrEmpty(password)){Set("账号和密码不能为空");return;}
            if(ServerId==0){Set("请选择有效区服");return;}Authenticated=false;Equipment=null;Guild=null;Progress=null;Activities.Clear();ActivitiesLoaded=false;Account=account.Trim();_password=password;
            if(register)Send(new RegisterReq{Account=Account,Password=password,ServerId=ServerId},"RegisterAck","正在创建角色…",false);
            else Send(new LoginReq{Account=Account,Password=password,ServerId=ServerId},"LoginAck","正在登录…",false);
        }
        public void LoadEquipment(){Send(new GetRoleEquipmentReq(),"GetRoleEquipmentAck","同步角色装备…");}
        public void Equip(EquipmentItem item,bool remove){if(item==null)return;Send(new ChangeRoleEquipmentReq{Slot=item.Slot,EquipmentId=remove?"":item.InstanceId},"ChangeRoleEquipmentAck","正在更换装备…");}
        public void LoadActivities(){Send(new GetActivityListReq(),"GetActivityListAck","同步活动列表…");}
        public void LoadProgress(ulong id){if(Busy)return;_activityId=id;Progress=null;Send(new GetProgressReq{ActivityId=id},"GetProgressAck","同步活动进度…");}
        public void JoinActivity(ulong id){if(Busy)return;_activityId=id;Send(new JoinActivityReq{ActivityId=id},"JoinActivityAck","正在参与活动…");}
        public void Claim(ulong id){if(Busy)return;if(Progress==null||Progress.ActivityId!=id||Progress.Rewarded||Progress.Progress<Progress.Target){Set("尚未满足领取条件，请先刷新进度");return;}_activityId=id;Send(new ClaimRewardReq{ActivityId=id},"ClaimRewardAck","正在领取奖励…");}
        public void LoadGuild(ulong id){if(Busy)return;_guildId=id;Send(new GetGuildReq{GuildId=id},"GetGuildAck","同步宗门信息…");}
        public void CreateGuild(string name){if(string.IsNullOrWhiteSpace(name)){Set("请输入宗门名称");return;}Send(new CreateGuildReq{Name=name.Trim(),LeaderName=Account,LeaderLevel=(uint)(Equipment==null?1:Mathf.Max(1,Equipment.RoleLevel))},"CreateGuildAck","正在创建宗门…");}
        public void JoinGuild(ulong id){if(Busy)return;_guildId=id;Send(new JoinGuildReq{GuildId=id,MemberName=Account,MemberLevel=(uint)(Equipment==null?1:Mathf.Max(1,Equipment.RoleLevel))},"JoinGuildAck","正在加入宗门…");}
        public void LeaveGuild(){Send(new LeaveGuildReq(),"LeaveGuildAck","正在退出宗门…");}
    }
}
