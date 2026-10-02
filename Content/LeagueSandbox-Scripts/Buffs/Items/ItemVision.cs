using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class HextechSweeperArea : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
            MaxStacks = 100,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Spell _spell;
        private Particle _area;
        private Vector2 _position;
        private float _radius;
        private float _elapsed;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = buff.SourceUnit;
            _position = buff.Variables.Get<Vector2>("position");
            _radius = buff.Variables.GetFloat("radius");
            _spell = spell;
            _area = AddParticle(
                _owner,
                null,
                "odin_hextechsweeper_tar_green.troy",
                _position,
                buff.Duration,
                size: _radius / 520f,
                enemyParticle: "odin_hextechsweeper_tar.troy"
            );
            Reveal();
        }

        private void Reveal()
        {
            var unitsInRange = EnumerateUnitsInRange(_position, _radius, true);
            foreach (var unit in unitsInRange)
            {
                if (unit is Champion && unit.Team != _owner.Team)
                {
                    ItemVisionHelpers.Reveal(_owner, unit, 3);
                    AddBuff("OdinLightbringer", 3, 1, _spell, unit, _owner);
                }
            }
        }

        public void OnUpdate(float diff)
        {
            _elapsed += diff;
            if (_elapsed < 250)
            {
                return;
            }

            _elapsed %= 250;
            Reveal();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_area);
        }
    }

    public class OdinLightbringer : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }

    public class OracleExtractSight : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _buff;
        private float _elapsed;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = buff.SourceUnit;
            _buff = buff;
        }

        public void OnUpdate(float diff)
        {
            if (_owner.IsDead)
            {
                RemoveBuff(_buff);
                return;
            }

            _elapsed += diff;
            if (_elapsed < 250)
            {
                return;
            }

            _elapsed %= 250;
            ItemVisionHelpers.Detect(_owner, 600, false);
        }
    }
}
