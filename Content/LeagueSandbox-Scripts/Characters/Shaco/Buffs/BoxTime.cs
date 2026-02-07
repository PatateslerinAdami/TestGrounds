using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class BoxTime : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private Buff _thisBuff;
        private Spell _spell;
        private AttackableUnit _boxUnit;
        private SpellSector _fearZone;
        private float _manaTimer = 0f;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _thisBuff = buff;
            _spell = ownerSpell;
            _boxUnit = unit;
            
            _fearZone = ownerSpell.CreateSpellSector(new SectorParameters
            {
                Type = SectorType.Area,
                BindObject = unit,
                Length = 200f,
                Tickrate = 10,
                OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes,
            });
            ApiEventManager.OnSpellSectorHit.AddListener(this, _fearZone, OnFearZoneHit, false);

        }
        public void OnFearZoneHit(SpellSector sector, AttackableUnit target)
        {
            if (sector == _fearZone && _boxUnit != null && !_boxUnit.IsDead)
            {
                AddBuff("BoxFearAttack", 8f, 1, _spell, _boxUnit, _spell.CastInfo.Owner);
                _boxUnit.RemoveBuff(_thisBuff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_fearZone != null)
            {
                _fearZone.SetToRemove();
            }
            TeamId[] validTeams = { TeamId.TEAM_BLUE, TeamId.TEAM_PURPLE, TeamId.TEAM_NEUTRAL };

            foreach (var team in validTeams)
            {
                if (team != _boxUnit.Team)
                {
                    _boxUnit.SetIsTargetableToTeam(team, true);
                }
            }
            ApiEventManager.OnSpellSectorHit.RemoveListener(this, _fearZone);
        }

        public void OnUpdate(float diff)
        {
            if (_boxUnit == null || _boxUnit.IsDead)
            {
                return;
            }
            _manaTimer += diff;
            if (_manaTimer >= 1000f)
            {
                _manaTimer = 0f;
                _boxUnit.Stats.CurrentMana -= 1;
                if (_boxUnit.Stats.CurrentMana <= 0)
                {
                    _boxUnit.Die(CreateDeathData(false, 0, _boxUnit, _boxUnit, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
                }
            }
        }
    }
}