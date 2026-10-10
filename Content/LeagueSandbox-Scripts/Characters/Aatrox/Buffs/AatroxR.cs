using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class AatroxR : IBuffGameScript
    {
        private ObjAIBase _owner;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType= BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            unit.SetAnimStates(new Dictionary<string, string> { { "attack1", "Attack1_ULT" }, { "attack2", "Attack2_ULT" }, { "attack3", "Attack6" }, 
                { "Crit", "Crit2" }, { "Run", "RUN_ULT" }, { "Run_IN", "Run_ULT_IN_BASE" }, { "Run_BASE", "Run_ULT_BASE" }, { "Run2", "RUN_ULT" }, 
                { "Run2_IN", "Run_ULT_IN_BASE" }, { "Run2_BASE", "Run_ULT_BASE" }, { "Run_Haste", "RUN_ULT" }, { "Run_Haste_IN", "Run_ULT_IN_BASE" }, 
                { "Idle_Sel", "Idle_ULT" }, { "idle1", "Idle_ULT" }, { "Idle_in", "Idle_ULT_IN" }, { "Idle_IN_BASE", "Idle_ULT_IN" }, { "Idle1_BASE", "Idle_ULT_BASE" }, 
                { "Idle2_Base", "Idle_ULT_BASE" }, { "Idle3_BASE", "Idle_ULT_BASE" }, { "Spell2", "spell2_ULT" }, { "Spell3", "Spell3_ULT" } },
                buff);
            StatsModifier.AttackSpeed.FlatBonus = 0.4f + 0.1f * (spell.CastInfo.SpellLevel - 1);
            StatsModifier.Range.FlatBonus = 175;
            unit.AddStatModifier(StatsModifier);
        }

        public void SetRank(byte rank)
        {
            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.AttackSpeed.FlatBonus = 0.4f + 0.1f * (rank - 1);
            _owner.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            unit.RemoveAnimStates(buff);
            if (!unit.IsDead && !unit.IsToRemove())
            {
                PlayAnimation(_owner, "Spell4End");
            }
        }
    }
}
