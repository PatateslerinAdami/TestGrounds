using System;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;

namespace Buffs
{
    internal class YasuoPassiveMovementShield : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData 
        { 
            BuffType = BuffType.AURA, 
            IsHidden = false, 
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 100 
        };
        public StatsModifier StatsModifier { get; set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {}
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {}
    }
}