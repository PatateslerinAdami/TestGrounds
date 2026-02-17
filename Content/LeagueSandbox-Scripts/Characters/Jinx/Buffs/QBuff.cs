using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Configuration;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Buffs
{
    internal class JinxQ : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; }
        Champion owner;
        private static readonly Dictionary<string, string> RocketOverrides = new()
        {
            { "Run", "R_Run" }, 
            { "Run_fast", "R_run_fast" },
            { "Idle1", "R_idle1" },          
            { "Idle2", "R_idle2_BASE" }, 
            { "Spell2", "R_spell2" },
            { "Spell3", "R_spell3" },       
            { "Spell4", "R_spell4" },
        };
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.SetAnimStates(RocketOverrides, buff);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var timerAnm = new GameScriptTimer(0.01f, () =>
            {
                AddBuff("JinxQIcon", 1f, 1, ownerSpell, ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, true);
            });
            unit.RegisterTimer(timerAnm);
            unit.RemoveAnimStates(buff);

        }
    }
    internal class JinxQIcon : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            
            var timerAnm = new GameScriptTimer(0.01f, () =>
            {
                AddBuff("JinxQ", 1f, 1, ownerSpell, ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, true);
            });
            unit.RegisterTimer(timerAnm);
        }
    }
}
