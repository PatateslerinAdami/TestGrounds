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
    internal class NamiQDebuff : IBuffGameScript {
        private ObjAIBase _nami;
        private Particle _p1, _p2;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.STUN,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1,
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _nami = buff.SourceUnit;
            
            ForceMove(unit, GetRandomPointInArea(unit.Position, 6f, 6f), 6f, gravity: 5f);
            _p1 = AddParticleTarget(_nami, unit, "Nami_Base_Q_debuff", unit, buff.Duration,size: (unit.CharData.GameplayCollisionRadius * 0.01f) + 0.3f, bone: "C_BUFFBONE_GLB_CENTER_LOC");
            _p2 = AddParticleTarget(_nami, unit, "Nami_Base_Q_tar", unit, buff.Duration,size: (unit.CharData.GameplayCollisionRadius * 0.01f) + 0.3f, bone: "C_BUFFBONE_GLB_CENTER_LOC");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_p1);
            RemoveParticle(_p2);
            if (unit.IsDead) return;
            AddParticleTarget(_nami, unit, "Nami_Base_Q_pop", unit, buff.Duration);
        }
    }
}
