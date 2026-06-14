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
    // Hidden carrier (replay: type AURA, IsHidden, permanent, added once, never updated).
    // The visible flow meter lives on YasuoPassiveMSCharge, NOT here.
    internal class YasuoPassiveMovementShield : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            PersistsThroughDeath = true,
            BuffType = BuffType.AURA,
            IsHidden = true,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {}
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {}
    }
}