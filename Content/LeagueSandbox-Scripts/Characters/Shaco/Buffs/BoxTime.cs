using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
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
        // Server-side AreaTriggerSphere id (Riot AreaTriggerSphere). -1 = none. The box is stationary, so a
        // fixed-center sphere at the box position is faithful (no owner-following needed).
        private int _fearZoneId = -1;
        private float _manaTimer = 0f;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _thisBuff = buff;
            _spell = ownerSpell;
            _boxUnit = unit;

            // Fear-arming zone as an AreaTriggerSphere instead of a SpellSector: the first enemy to ENTER
            // the box's radius arms the fear (Riot AreaTriggerI::OnEnter — continuous presence detection).
            // Radius 200 == the old SectorParameters.Length (SpellSector used Max(Length,Width) as radius).
            _fearZoneId = CreateAreaTriggerSphere(unit.Position, 200f, onEnter: OnFearZoneEnter);
        }

        private void OnFearZoneEnter(AttackableUnit target)
        {
            // AreaTrigger fires for all units; filter to enemies (the old sector's OverrideFlags did this).
            if (_boxUnit == null || _boxUnit.IsDead || target == null || target.Team == _boxUnit.Team)
            {
                return;
            }
            AddBuff("BoxFearAttack", 8f, 1, _spell, _boxUnit, _spell.CastInfo.Owner);
            _boxUnit.RemoveBuff(_thisBuff);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_fearZoneId >= 0)
            {
                DeleteAreaTrigger(_fearZoneId);
                _fearZoneId = -1;
            }
            TeamId[] validTeams = { TeamId.TEAM_BLUE, TeamId.TEAM_PURPLE, TeamId.TEAM_NEUTRAL };

            foreach (var team in validTeams)
            {
                if (team != _boxUnit.Team)
                {
                    _boxUnit.SetIsTargetableToTeam(team, true);
                }
            }
        }

        public void OnUpdate(Buff buff, float diff)
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