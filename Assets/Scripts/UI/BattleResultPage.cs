using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public static class BattleResultPage
    {
        public static GameObject Show(Transform parent, global::BattleFinishedNtf result, UnityAction retry, UnityAction back)
        {
            GameObject page;
            var d=Q.Page(parent,"BattleResultQuietInkV2","结算",back,out page,showHeader:false);
            UnityAction leave=()=>{ if(page!=null)Object.Destroy(page);back?.Invoke(); };
            Q.Button(d,"Back","‹",22,32,88,88,leave,null,52);
            string title=result==null?"战斗结束":result.Outcome==global::BattleOutcome.AttackerWin?"试炼胜利":
                result.Outcome==global::BattleOutcome.DefenderWin?"试炼未成":result.Outcome==global::BattleOutcome.Draw?"势均力敌":"战斗结束";
            Q.Text(d,"ResultTitle",title,130,125,681,110,62,result!=null&&result.Outcome==global::BattleOutcome.AttackerWin?Q.Jade:Q.Cinnabar);
            Q.Art(d,"result_emblem",135,280,671,560,true);
            Q.Divider(d,80,905,781);
            int own=0,enemy=0;
            if(result!=null)foreach(var unit in result.Units)if(unit.Hp>0){if(unit.Team==global::BattleTeam.Attacker)own++;else enemy++;}
            Q.Text(d,"OwnLabel","我方存活",110,960,470,90,39,Q.Ink,TextAlignmentOptions.Left);
            Q.Text(d,"OwnValue",result==null?"—":own.ToString(),650,955,160,100,68,Q.Ink,TextAlignmentOptions.Right);
            Q.Divider(d,80,1070,781);
            Q.Text(d,"EnemyLabel","敌方存活",110,1090,470,90,39,Q.Ink,TextAlignmentOptions.Left);
            Q.Text(d,"EnemyValue",result==null?"—":enemy.ToString(),650,1085,160,100,68,Q.Ink,TextAlignmentOptions.Right);
            Q.Divider(d,80,1200,781);
            Q.Text(d,"RewardsPending","奖励尚未提供 · 以服务器实际发放为准",80,1240,781,56,27,Q.Muted);
            Q.Button(d,"Retry","再次挑战",65,1340,811,110,()=>{if(page!=null)Object.Destroy(page);retry?.Invoke();},size:42);
            Q.Button(d,"BackToLobby","返回大厅",65,1470,811,110,leave,"button_secondary",42);
            page.transform.SetAsLastSibling();return page;
        }
    }
}
