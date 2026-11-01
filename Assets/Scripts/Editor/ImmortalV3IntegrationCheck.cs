using System;
using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using UnityEditor;
using UnityEngine;
using XiuXian.Core;
using XiuXian.Network;

namespace XiuXian.Editor
{
    /// <summary>In-memory protocol integration checks. Never contacts a server or modifies inventories.</summary>
    public static class ImmortalV3IntegrationCheck
    {
        private sealed class Socket : IWebSocketTransport
        {
            public bool IsOpen {get;private set;}=true;
            public event Action OnOpen;public event Action<string> OnClose;public event Action<string> OnError;public event Action<byte[]> OnMessage;
            public readonly List<string> Sent=new List<string>();
            public void Connect(){IsOpen=true;OnOpen?.Invoke();}
            public void Send(byte[] bytes){string type;byte[] body;if(!FrameCodec.TryDecode(bytes,out type,out body))throw new Exception("Invalid outgoing frame");Sent.Add(type);}
            public void Close(){IsOpen=false;OnClose?.Invoke("test closed");}
            public void DispatchMessageQueue(){}
        }
        private static NetworkManager net;private static ImmortalGameService service;private static IWebSocketTransport previous;private static Socket socket;
        private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool value,string message){if(!value)throw new Exception(message);}
        private static void Reply(IMessage message){typeof(NetworkManager).GetMethod("HandleMessage",Flags).Invoke(net,new object[]{FrameCodec.Encode(message.Descriptor.Name,message.ToByteArray())});}
        public static string Run()
        {
            if(socket!=null)throw new Exception("Finish previous test first");
            var ui=ImmortalUIArtPreview.OpenLive();service=ui.GetComponent<ImmortalGameService>();net=NetworkManager.Instance;
            var field=typeof(NetworkManager).GetField("_transport",Flags);previous=(IWebSocketTransport)field.GetValue(net);socket=new Socket();field.SetValue(net,socket);
            try
            {
                service.QueryServers("ui_integration_fixture");Reply(new ServerListAck{Code=ErrCode.Ok,Accounts={new ServerAccount{ServerId=1,Uid=99}}});Check(service.Accounts.Count==1,"Server list");
                service.Login("ui_integration_fixture","test-only",false);Reply(new LoginAck{Code=ErrCode.Ok});Check(service.Authenticated,"Login failed");
                var data=new RoleEquipmentData{RoleId="fixture",RoleLevel=30,MaxHp=1200,Attack=280,Defense=96,Speed=12};
                for(int i=1;i<=6;i++)data.Items.Add(new EquipmentItem{InstanceId="fixture-"+i,ConfigId=100+i,Slot=(EquipSlot)i,Level=30,MaxHp=200,Attack=40,Defense=16,Equipped=true});
                Reply(new GetRoleEquipmentAck{Data=data});Check(service.Equipment.Items.Count==6,"Equipment load");
                service.Equip(data.Items[0],false);int before=socket.Sent.Count;service.Equip(data.Items[0],false);Check(socket.Sent.Count==before,"Duplicate mutation sent while pending");
                Reply(new ChangeRoleEquipmentAck{Error="fixture rejection"});Check(!service.Busy&&service.Status=="fixture rejection","Error response");
                service.Equip(data.Items[0],false);Reply(new ChangeRoleEquipmentAck{Data=data});Check(!service.Busy,"Equipment mutation completion");
                service.LoadActivities();Reply(new GetActivityListAck{Activities={new ActivityInfo{Id=101,Name="云海问剑 · 测试数据"},new ActivityInfo{Id=102,Name="七日修行 · 测试数据"}}});Check(service.Activities.Count==2,"Activity list");
                service.JoinActivity(101);Reply(new JoinActivityAck{Code=ErrCode.Ok});Reply(new GetProgressAck{Code=ErrCode.Ok,Progress=new ActivityProgress{ActivityId=101,Progress=3,Target=3}});
                service.Claim(101);Reply(new ClaimRewardAck{Code=ErrCode.Ok});Reply(new GetProgressAck{Code=ErrCode.Ok,Progress=new ActivityProgress{ActivityId=101,Progress=3,Target=3,Rewarded=true}});
                before=socket.Sent.Count;service.Claim(101);Check(socket.Sent.Count==before,"Duplicate reward claim");
                var guild=new GuildInfo{GuildId=77,Name="青云仙宗 · 测试数据",Notice="此处为隔离集成测试回包",Members={new GuildMember{Uid=99,Name="测试角色",Level=30}}};
                service.LoadGuild(77);Reply(new GetGuildAck{Code=ErrCode.Ok,Guild=guild});Check(service.Guild.GuildId==77,"Guild load");
                service.JoinGuild(77);Reply(new JoinGuildAck{Code=ErrCode.Ok});Reply(new GetGuildAck{Code=ErrCode.Ok,Guild=guild});
                service.LeaveGuild();Reply(new LeaveGuildAck{Code=ErrCode.Ok});Check(service.Guild==null,"Guild leave acknowledgement mapping");
                service.CreateGuild("fixture");Reply(new CreateGuildAck{Code=ErrCode.Ok,Guild=guild});Check(service.Guild!=null,"Guild create");
                for(int i=0;i<17;i++){ui.ShowPage(i);Check(ui.CurrentPage==i,"Page route "+i);Canvas.ForceUpdateCanvases();}
                ui.ShowPage(1);
                return "PASS: login/server-list/equipment/error/duplicate-request/activity/progress/reward/guild, all 17 live page builders. Isolated test transport remains installed for screenshots; call Finish.";
            }
            catch {Finish();throw;}
        }
        public static string Finish()
        {
            if(socket==null)return "No active fixture";
            try
            {
                service.LoadEquipment();typeof(ImmortalGameService).GetField("_deadline",Flags).SetValue(service,-1f);
                typeof(ImmortalGameService).GetMethod("Update",Flags).Invoke(service,null);
                Check(!service.Authenticated&&!service.Busy&&service.Equipment==null,"Timeout must clear session and data");
                Reply(new GetRoleEquipmentAck{Data=new RoleEquipmentData{RoleId="late"}});Check(service.Equipment==null,"Late response accepted after timeout");
                return "PASS: timeout clears session; late response ignored. Original transport restored.";
            }
            finally
            {
                typeof(NetworkManager).GetField("_transport",Flags).SetValue(net,previous);socket=null;previous=null;
                var ui=UnityEngine.Object.FindObjectOfType<ImmortalUIArtPreview>();if(ui!=null)ui.ShowPage(0);
            }
        }
    }
}
