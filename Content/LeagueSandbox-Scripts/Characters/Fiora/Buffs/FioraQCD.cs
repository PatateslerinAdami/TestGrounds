using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using System.Collections.Generic;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
namespace Buffs
{
    class FioraQCD : IBuffGameScript
    {

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ownerSpell.SetCooldown(0.5f, true);
        }
        
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ownerSpell.SetCooldown(ownerSpell.CastInfo.Cooldown, false);
        }
    }
}