using System;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;

namespace Buffs
{
    internal class YasuoPassiveMSCharge : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData { BuffType = BuffType.INTERNAL };
        public StatsModifier StatsModifier { get; set; } = new StatsModifier();
        private Vector2 _lastPos;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _lastPos = unit.Position;
            ApiEventManager.OnUpdateStats.AddListener(this, unit, OnUpdate, false);
        }

        private void OnUpdate(AttackableUnit unit, float diff)
        {
            var yasuo = unit as ObjAIBase;
            if (yasuo == null) return;

            float dist = Vector2.Distance(unit.Position, _lastPos);
            _lastPos = unit.Position;
            float gain = (dist / 5000f) * yasuo.Stats.ManaPoints.Total;
            yasuo.Stats.CurrentMana = Math.Min(yasuo.Stats.CurrentMana + gain, yasuo.Stats.ManaPoints.Total);
            int stacks = (int)((yasuo.Stats.CurrentMana / yasuo.Stats.ManaPoints.Total) * 100);
            var hudBuff = unit.GetBuffWithName("YasuoPassive");
            if (hudBuff != null)
            {
                hudBuff.SetStacks((byte)Math.Clamp(stacks, 1, 100));
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) => ApiEventManager.OnUpdateStats.RemoveListener(this);
    }
}