using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class Fear : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.FEAR,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public bool RandomDirection { get; set; } = false;
        public float slowPercent = 0f;
        private AttackableUnit _unit;
        private ObjAIBase _owner;
        private Particle _particle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _owner = buff.SourceUnit;
            AddParticleTarget(_owner, unit, "LOC_fear", unit, buff.Duration, bone: "head");

            // Feared is DERIVED from BuffType.FEAR (AttackableUnit.RecomputeBuffEffects) — overlap-safe.

            // Riot's model: the buff is just a flag + CC source; the AI drives the movement. Record the
            // source + flavour (RandomDirection = wander/AI_FEARED vs flee straight away) and the shared
            // CrowdControlComponent (auto-attached to every BaseAIScript) reads them and drives the run.
            if (unit is ObjAIBase cc)
            {
                cc.CrowdControlSource = _owner;
                cc.CrowdControlWander = RandomDirection;
            }

            ApplyAssistMarker(unit, _owner, 10.0f);
            StatsModifier.MoveSpeed.PercentBonus = -slowPercent;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_particle != null) _particle.SetToRemove();

            if (unit is ObjAIBase cc)
            {
                cc.CrowdControlSource = null;
            }
            unit.StopMovement();
        }
    }
}
