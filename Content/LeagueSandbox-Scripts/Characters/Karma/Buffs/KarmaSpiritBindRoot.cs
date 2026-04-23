using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs
{
    internal class KarmaSpiritBindRoot : IBuffGameScript {
        private ObjAIBase _karma;
        private Particle  _rootParticle;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.SNARE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _karma                               =  ownerSpell.CastInfo.Owner;
            unit.SetStatus(StatusFlags.Rooted, true);
            AddParticleTarget(_karma, unit,   "Karma_Base_W_tar",   unit,   buff.Duration);
            AddParticleTarget(_karma, _karma, "Karma_Base_W_tar02", unit, buff.Duration);
            AddParticleTarget(_karma, _karma, "Karma_Base_W_tar03", unit, buff.Duration);
            AddParticleTarget(_karma, _karma, "Karma_Base_W_tar04", unit, buff.Duration);
            AddParticleTarget(_karma, _karma, "Karma_Base_W_tar05", unit, buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.SetStatus(StatusFlags.Rooted, false);
            RemoveParticle(_rootParticle);
        }
    }
}