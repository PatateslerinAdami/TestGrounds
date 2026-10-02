using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class SoulAnchorCharges : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _buff = buff;
            buff.Variables.Set("charges", 1);
            ApiEventManager.OnLevelUp.AddListener(this, unit, Level);
            Level(unit);
        }

        private void Level(AttackableUnit unit)
        {
            int[] milestoneLevels = new[]
            { 9, 14 };
            foreach (int level in milestoneLevels)
            {
                bool alreadyGranted = _buff.Variables.GetBool("level" + level);
                if (unit.Stats.Level >= level && !alreadyGranted)
                {
                    _buff.Variables.Set("level" + level, true);
                    int currentCharges = _buff.Variables.GetInt("charges");
                    _buff.Variables.Set("charges", Math.Min(2, currentCharges + 1));
                }
            }

            var trinketItem = _owner.Inventory.GetItem(6);
            if (trinketItem?.ItemData.SpellName == "OdinTrinketRevive")
            {
                SetItemCharges(_owner, 6, _buff.Variables.GetInt("charges"));
            }
        }
    }

    public class OdinReviveSpeed : IBuffGameScript
    {
        public OdinReviveSpeed()
        {
            BuffMetaData.IsHidden = true;
        }

        private Particle _revive;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _owner;
        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = unit;
            _buff = buff;
            ItemHelpers.NativeItemPresentation.Show(buff, "RengarRevive");
            _revive = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "Global_SS_Revive_buf.troy",
                unit,
                0,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            _revive.isInfinite = true;
            Update();
        }

        private void Update()
        {
            _owner.RemoveStatModifier(StatsModifier);
            float decayFactor = Math.Max(0, 1 - (_buff.TimeElapsed / 12f));
            StatsModifier.MoveSpeed.PercentBonus = 1.25f * decayFactor;
            _owner.AddStatModifier(StatsModifier);
        }

        public void OnUpdate(float diff)
        {
            if (_owner.IsDead)
            {
                RemoveBuff(_buff);
                return;
            }

            Update();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_revive);
            ItemHelpers.NativeItemPresentation.Remove(buff);
        }
    }
}
