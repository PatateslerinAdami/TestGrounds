using GameServerCore.Enums;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class SorakaESnare : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SNARE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; }

        Particle root;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            AddBuff("Root", float.MaxValue, 1, ownerSpell, unit, ownerSpell.CastInfo.Owner);
            root = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "soraka_base_e_snare_tar", unit, lifetime: buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveBuff(unit, "Root");
            RemoveParticle(root);
        }
    }
}