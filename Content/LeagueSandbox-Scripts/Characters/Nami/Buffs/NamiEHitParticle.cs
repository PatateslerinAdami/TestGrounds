using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs {
    internal class NamiEHitParticle : IBuffGameScript {
        ObjAIBase        _nami;
        private Particle _hitParticle;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData {
            BuffType    = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _nami        = ownerSpell.CastInfo.Owner;
            _hitParticle = AddParticleTarget(_nami, unit, "Nami_E_tar", unit);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        }
    }
}
    