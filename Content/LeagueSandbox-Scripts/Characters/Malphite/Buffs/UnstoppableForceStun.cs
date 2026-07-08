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


namespace Buffs {
    internal class UnstoppableForceStun : IBuffGameScript {
        private ObjAIBase _malphite;
        private Particle _stunParticle, _tarParticle;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData {
            BuffType    = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _malphite = buff.SourceUnit;
            _stunParticle = AddParticleTarget(_malphite, unit, "Malphite_Base_UnstoppableForce_stun.troy", unit);
            _tarParticle = AddParticleTarget(_malphite, unit, "Malphite_Base_UnstoppableForce_tar.troy",  unit);
            ForceMove(unit, new Vector2(unit.Position.X + 8f, unit.Position.Y + 8f), 8f, gravity: 10f);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            RemoveParticle(_stunParticle);
            RemoveParticle(_tarParticle);
        }
    }
}
