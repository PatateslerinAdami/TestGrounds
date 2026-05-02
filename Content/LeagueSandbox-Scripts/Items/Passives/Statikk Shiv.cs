using System;
using System.Numerics;
using LeagueSandbox.GameServer.API;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3087 : IItemScript
    {
        private ObjAIBase _owner;
        private Vector2 _lastPos;
        private float _movedAccum;
        private const float MOVE_STEP_DIST = 40f; 
        private const byte MOVE_STEP_STACKS = 1;
        private const byte ON_HIT_STACKS = 10;

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            _lastPos = owner.Position;
            _movedAccum = 0f;
            
            AddBuff("ItemStatikShankCharge", float.MaxValue, 1, null, owner, owner);
            
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack, false);
            ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats, false);
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.OnLaunchAttack.RemoveListener(this);
            ApiEventManager.OnUpdateStats.RemoveListener(this);
            var buff = owner.GetBuffWithName("ItemStatikShankCharge");
            if (buff != null)
            {
                buff.SetStacks(1);
                owner.RemoveBuffsWithName("ItemStatikShankCharge");
            }
        }

        private void OnLaunchAttack(Spell spell)
        {
            var buff = _owner.GetBuffWithName("ItemStatikShankCharge");
            if (buff != null && buff.StackCount < 100)
            {
                int currentStacks = buff.StackCount;
                buff.SetStacks(Math.Min(100, currentStacks + ON_HIT_STACKS));
            }
        }

        private void OnUpdateStats(LeagueSandbox.GameServer.GameObjects.AttackableUnits.AttackableUnit who, float diff)
        {
            var cur = _owner.Position;
            var moved = Vector2.Distance(cur, _lastPos);

            if (moved > 1500f) { _lastPos = cur; return; }
            if (moved <= 0f) return;

            _movedAccum += moved;
            _lastPos = cur;

            int steps = (int)(_movedAccum / MOVE_STEP_DIST);

            if (steps > 0)
            {
                _movedAccum -= (steps * MOVE_STEP_DIST);

                var buff = _owner.GetBuffWithName("ItemStatikShankCharge");
                if (buff != null && buff.StackCount < 100)
                {
                    int stacksToAdd = steps * MOVE_STEP_STACKS;
                    int currentStacks = buff.StackCount;
                    buff.SetStacks(Math.Min(100, currentStacks + stacksToAdd));
                }
            }
        }
    }
}