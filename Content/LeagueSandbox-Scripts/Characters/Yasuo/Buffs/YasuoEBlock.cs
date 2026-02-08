using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoEBlock : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; }
        private SpellSector _targetZone;
        private Particle timer;
        bool doSpin = false;
        ObjAIBase owner;
        Spell origSpell;
        bool knockUp = false;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            doSpin = false;
            owner = ownerSpell.CastInfo.Owner;
            timer = AddParticleTarget(owner, unit, "Yasuo_base_E_timer1", unit);
            //dash
            ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnMoveSuccess, true);
            origSpell = owner.Spells[0];
            ApiEventManager.OnSpellPress.AddListener(this, origSpell, OnSpellPress, true);
            var flash = owner.GetSpell("SummonerFlash");
            ApiEventManager.OnSpellPress.AddListener(this, flash, OnSpellPress2, true);
            var target = unit;
            AddParticleTarget(owner, owner, "Yasuo_Base_E_Dash", owner);
            AddParticleTarget(owner, target, "Yasuo_Base_E_dash_hit", target);
            var to = Vector2.Normalize(target.Position - owner.Position);
            ForceMovement(owner, "Spell3", new Vector2(owner.Position.X + to.X * 375f, owner.Position.Y + to.Y * 375f), 750f + owner.Stats.GetTrueMoveSpeed() * 0.6f, 0, 0, 0, consideredAsCC: false);
        }
        public void OnMoveSuccess(AttackableUnit unit)
        {
            if (doSpin)
            {
                knockUp = false;
                if (owner.HasBuff("YasuoQ3W"))
                {
                    AddParticleTarget(owner, owner, "yasuo_base_eq3_cas", owner);
                    owner.RemoveBuffsWithName("YasuoQ3W");
                    knockUp = true;
                }
                AddParticleTarget(owner, owner, "yasuo_base_eq_cas", owner);
                PlayAnimation(owner, "Spell1_Dash", timeScale: 0.8f);
                var timerAnm = new GameScriptTimer(0.5f, () =>
                {
                    StopAnimation(unit, "Spell1_Dash", fade: true);
                });
                unit.RegisterTimer(timerAnm);

                _targetZone = origSpell.CreateSpellSector(new SectorParameters
                {
                    Type = SectorType.Area,
                    BindObject = owner,
                    Length = 200f,
                    Tickrate = 30,
                    Lifetime = 2f,
                    SingleTick = true,
                    OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes,
                });
                ApiEventManager.OnSpellSectorHit.AddListener(this, _targetZone, OnTargetZoneHit, true);
            }
        }
        public void OnTargetZoneHit(SpellSector sector, AttackableUnit target)
        {
            var enemies = GetUnitsInRangeDiffTeam(owner.Position, 200f, true, owner);
            if (enemies.Count != 0)
            {
                if (owner.HasBuff("YasuoQ"))
                {
                    owner.RemoveBuffsWithName("YasuoQ");
                    AddBuff("YasuoQ3W", 9f, 1, origSpell, owner, owner);
                }
                else if (!knockUp)
                {
                    AddBuff("YasuoQ", 3f, 1, origSpell, owner, owner);
                }

                if (knockUp) PlaySound("Play_sfx_Yasuo_YasuoQ3W_hit", owner);
                else PlaySound("Play_sfx_Yasuo_YasuoQ_hit", owner);
            }
            foreach (var enemy in enemies)
            {

                enemy.TakeDamage(owner, 10f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, true, origSpell);
                if (knockUp)
                {
                    AddBuff("YasuoQ3Mis", 1.2f, 1, origSpell, enemy, owner);
                }
            }
        }
        public void OnSpellPress(Spell sp, SpellCastInfo sc)
        {
            doSpin = true;
        }
        public void OnSpellPress2(Spell sp, SpellCastInfo sc)
        {
            sp.CastInfo.Owner.SetDashingState(false);
            sp.Cast(sc.Position, sc.EndPosition, sc.TargetUnit);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(timer);
        }
    }
}
