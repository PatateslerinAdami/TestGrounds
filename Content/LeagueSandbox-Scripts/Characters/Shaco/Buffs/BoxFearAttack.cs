using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class BoxFearAttack : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Buff _thisBuff;
        private float _manaTimer = 0f;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _thisBuff = buff;

            var boxMinion = unit as Minion;
            if (boxMinion == null) return;
            boxMinion.Stats.ManaPoints.BaseValue = 5.0f;
            boxMinion.Stats.CurrentMana = 5f;

            // Target selection + firing is owned by ShacoBoxAI (the box's AiScript) + the shared
            // AutoAttackComponent — this buff only marks the box "active" (ShacoBoxAI gates on it),
            // applies the fear pulse below, and runs the mana/lifetime death in OnUpdate.

            var units = EnumerateValidUnitsInRange(boxMinion, boxMinion.Position, 500f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral);
            foreach (var u in units)
            {
                if (u is not LaneTurret)
                {
                    AddBuff("Fear", 3f, 1, ownerSpell, u, boxMinion);
                }
            }
        }

        public void OnUpdate(float diff)
        {
            var boxMinion = _thisBuff.TargetUnit as Minion;
            if (boxMinion == null || boxMinion.IsDead)
            {
                return;
            }

            _manaTimer += diff;
            if (_manaTimer >= 1000f)
            {
                _manaTimer = 0f;
                boxMinion.Stats.CurrentMana -= 1;
                if (boxMinion.Stats.CurrentMana <= 0)
                {
                    boxMinion.Die(CreateDeathData(false, 0, boxMinion, boxMinion, DamageType.DAMAGE_TYPE_TRUE,
                        DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
                }
            }
        }

    }
}