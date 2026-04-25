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
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _nami = ownerSpell.CastInfo.Owner;
            CancelDash(unit);
            ForceMovement(unit, "RUN", new Vector2(unit.Position.X + 6f, unit.Position.Y + 6f), 6f, 0, 5f, 0);
            AddParticleTarget(_nami, unit, "Nami_Base_Q_debuff", unit, buff.Duration,size: (unit.CharData.GameplayCollisionRadius * 0.01f) + 0.3f, bone: "C_BUFFBONE_GLB_CENTER_LOC");
            AddParticleTarget(_nami, unit, "Nami_Base_Q_tar", unit, buff.Duration,size: (unit.CharData.GameplayCollisionRadius * 0.01f) + 0.3f, bone: "C_BUFFBONE_GLB_CENTER_LOC");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            AddParticleTarget(_nami, unit, "Nami_Base_Q_pop", unit, buff.Duration);
        }
    }
}
