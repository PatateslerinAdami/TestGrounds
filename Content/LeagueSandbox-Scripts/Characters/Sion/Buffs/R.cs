using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Buffs
{
    internal class SionR : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        Particle p;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            p = AddParticle(unit, unit, "sion_base_r_cas.troy", default, 8f,size:2f);//sion_base_r_skin
            //AddParticlePos(unit, "sion_base_r_cas.troy", unit.Position, unit.Position);
            OverrideAnimation(unit, "spell4_run", "run", this);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            p?.SetToRemove();
            ClearOverrideAnimation(unit, "run", this);
        }
    }
}