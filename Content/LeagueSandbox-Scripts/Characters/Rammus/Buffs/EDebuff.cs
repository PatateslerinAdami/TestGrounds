using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    internal class PuncturingTaunt : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.TAUNT,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            // Taunted is DERIVED from BuffType.TAUNT (AttackableUnit.RecomputeBuffEffects) — overlap-safe.
            if (unit is ObjAIBase oa)
            {
                // The AI-driven CrowdControlComponent walks the taunted unit to the taunter (and the
                // engine attacks it); it reads the taunter from CrowdControlSource.
                oa.CrowdControlSource = buff.SourceUnit;
            }
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (unit is ObjAIBase oa) oa.CrowdControlSource = null;
        }
    }
}

