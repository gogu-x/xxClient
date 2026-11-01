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
                var j=ownedJades[i];var lines=new List<string>();foreach(var a in j.Affixes)lines.Add(AffixNames[a.Key]+" +"+a.Value.ToString("0.#")+"%");
                rows.Add(new ArtJade{Index=i,Icon=j.JadeIndex,Level=j.Level,Name=Jades[j.JadeIndex].Name,Affixes=string.Join("\n",lines),Equipped=System.Array.IndexOf(equipped,i)>=0});
            }
            return rows;
        }
        public int ArtEquipped(int slot){return equipped[UnityEngine.Mathf.Clamp(slot,0,5)];}
        public void ArtEquip(int index,bool remove)
        {
            if(index<0||index>=ownedJades.Count)return;
            int slot=System.Array.IndexOf(SlotNames,Jades[ownedJades[index].JadeIndex].Type);if(slot<0)return;
            equipped[slot]=remove?-1:index;Save();
        }
    }
}
