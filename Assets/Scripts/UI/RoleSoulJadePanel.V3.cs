using System.Collections.Generic;
namespace XiuXian.Core
{
    public sealed partial class RoleSoulJadePanel
    {
        public sealed class ArtJade { public int Index,Icon,Level; public string Name,Affixes; public bool Equipped; }
        public List<ArtJade> ArtInventory()
        {
            var rows=new List<ArtJade>();
            for(int i=0;i<ownedJades.Count;i++)
            {
                var j=ownedJades[i];var lines=new List<string>();foreach(var a in j.Affixes)lines.Add(FormatAffix(a));
                rows.Add(new ArtJade{Index=i,Icon=j.JadeIndex,Level=j.Level,Name=j.Name,Affixes=string.Join("\n",lines),Equipped=System.Array.IndexOf(equipped,i)>=0});
            }
            return rows;
        }
        public int ArtEquipped(int slot){return equipped[UnityEngine.Mathf.Clamp(slot,0,5)];}
        public void ArtEquip(int index,bool remove)
        {
            if(index<0||index>=ownedJades.Count)return;
            BindService();
            if (remove)
            {
                int slot = System.Array.IndexOf(equipped, index);
                if (slot >= 0) soulService.Equip(visibleSlotIds[slot], string.Empty);
            }
            else soulService.Equip(visibleSlotIds[slotSelected], ownedJades[index].InstanceId);
        }
    }
}
