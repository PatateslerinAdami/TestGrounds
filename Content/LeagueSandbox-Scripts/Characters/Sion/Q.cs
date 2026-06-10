using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SionQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = false,
            ChargeDuration = 1f,
            ChargeMaxHoldDuration = 2f,
            AutoFaceDirection = false
        };

        private ObjAIBase _sion;
        Particle p1, p2, p3, p4, p5;

        bool channeling = false;
        bool passedOneSecond = false;
        float currentCharge = 0f;

        Buff b;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell, true);
        }

        private void OnLevelUpSpell(Spell spell)
        {
            SetSpell(_sion, "SionQSoundAfterHalf", SpellSlotType.ExtraSlots, 7);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.StopMovement(networked: false);
            InstantStopTest(owner, true, false);
            OverrideAnimation(owner, "Spell1_Chrg", "Attack1", this);
            FaceDirection(end, owner, true);
        }

        public void OnSpellChargeStart(Spell spell)
        {
            b = AddBuff("SionQ", 2f, 1, spell, _sion, _sion);
            _sion.SetStatus(StatusFlags.CanMove, false);
            channeling = true;
            passedOneSecond = false;
            currentCharge = 0f;

            AddParticlePos(_sion, "Sion_Base_Q_Cas.troy", _sion.Position, GetPointFromUnit(_sion, 1000f),
                lifetime: 1, direction: _sion.Direction);
            p1 = AddParticleTarget(_sion, _sion, "sion_base_q_cas1_weapon.troy", _sion, lifetime: 5,
                bone: "Buffbone_Glb_Weapon_1");
            p2 = AddParticlePos(_sion, "Sion_Base_Q_Indicator2.troy", _sion.Position, default, lifetime: 1,
                direction: _sion.Direction);
        }

        public void OnSpellChargeTick(Spell spell,float diff)
        {
                currentCharge += diff / 1000f;

                if (currentCharge >= 1.0f && !passedOneSecond)
                {
                    passedOneSecond = true;
                    p3 = AddParticlePos(_sion, "Sion_Base_Q_Indicator_Red2.troy", _sion.Position, default,
                        lifetime: 1, direction: _sion.Direction);
                    p4 = AddParticleTarget(_sion, _sion, "sion_base_q_cas2_weapon.troy", _sion, lifetime: 5,
                        bone: "Buffbone_Glb_Weapon_1");
                    p5 = AddParticlePos(_sion, "sion_base_q_cas3.troy", _sion.Position,
                        GetPointFromUnit(_sion, 1000f), lifetime: 1, direction: _sion.Direction, size: 1.7f);
                }
        }

        public void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
        {
            LetGo(spell, reason);
        }

        public void OnSpellChargeFire(Spell spell)
        {
            LetGo(spell, ChannelingStopSource.TimeCompleted);
        }

        private void LetGo(Spell spell, ChannelingStopSource reason)
        {
            if (!channeling) return;
            channeling = false;

            // Clear the client's charge HUD bar. Varus Q / Xerath Q get this via
            // SpellCastCharge (which wraps FireCharge internally), but Sion Q applies
            // damage directly in-script with no sub-spell, so we call FireCharge ourselves.
            // Without it the client's charge state never exits and the HUD stays charged
            // forever (including on re-cast).
            spell.FireCharge(_sion.Position);

            p1?.SetToRemove();
            p2?.SetToRemove();
            p3?.SetToRemove();
            p4?.SetToRemove();
            p5?.SetToRemove();

            if (b != null)
            {
                //b.SetToExpired();
                b.DeactivateBuff();
            }

            ClearOverrideAnimation(_sion, "Attack1", this);

            if (reason != ChannelingStopSource.NotCancelled && reason != ChannelingStopSource.TimeCompleted)
            {
                spell.SetCooldown(2.0f, true);
                _sion.SetStatus(StatusFlags.CanMove, true);
                return;
            }

            float chargeRatio = Math.Clamp(currentCharge / 2f, 0f, 1f);
            float currentRange = 400f + 600f * chargeRatio;

            if (currentCharge >= 1.0f)
            {
                PlayAnimation(_sion, "Spell1_Hit2", timeScale: 0.3f);
                AddParticlePos(_sion, "sion_base_q_hit3.troy", _sion.Position, GetPointFromUnit(_sion, currentRange),
                    lifetime: 1, direction: _sion.Direction);
                //SpellCast(_owner, 7, SpellSlotType.ExtraSlots, _owner.Position, _owner.Position, false, Vector2.Zero);
                //Couldnt find a way to add the axe hitting ground sound.
            }
            else
            {
                PlayAnimation(_sion, "Spell1_Hit1", timeScale: 0.3f);
                CreateCustomMissile(_sion, "SionQHitParticleMissile2", _sion.Position,
                    GetPointFromUnit(_sion, currentRange - 200f), new MissileParameters { Type = MissileType.Arc });
            }

            Vector2 dir = Vector2.Normalize(new Vector2(_sion.Direction.X, _sion.Direction.Z));
            if (dir == Vector2.Zero) dir = new Vector2(1, 0);

            foreach (var unit in EnumerateValidUnitsInRange(_sion, _sion.Position, currentRange + 200f, true,
                         SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectNeutral))
            {
                Vector2 toUnit = unit.Position - _sion.Position;
                float projection = Vector2.Dot(toUnit, dir);

                if (projection >= -50f && projection <= currentRange + unit.CollisionRadius)
                {
                    float halfWidth = 112f + (Math.Max(0, projection) / 1000f) * 140f;
                    float perpDistance = Math.Abs(toUnit.X * dir.Y - toUnit.Y * dir.X);

                    if (perpDistance <= halfWidth + unit.CollisionRadius)
                    {
                        float dmg = GetDamage(_sion, unit, chargeRatio, spell.CastInfo.SpellLevel);
                        unit.TakeDamage(_sion, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                            false);
                        //AddParticlePos(_owner, "Sion_Base_Q3_tar.troy", _owner.Position, GetPointFromUnit(_owner, currentRange), lifetime: 1, direction: _owner.Direction);
                        if (currentCharge >= 1.0f)
                        {
                            float scaleCC = Math.Clamp((currentCharge - 1f) / 0.97f, 0f, 1f);
                            float knockupDuration = 0.5f + (0.5f * scaleCC);
                            float stunDuration = 1.25f + (1.0f * scaleCC);

                            var vars = new BuffVariables();
                            vars.Set("KnockupTime", knockupDuration);

                            AddBuff("SionQKnockup", stunDuration, 1, spell, unit, _sion, false, null, vars);
                        }
                        else
                        {
                            AddBuff("SionQSlow", 0.25f, 1, spell, unit, _sion);
                        }
                    }
                }
            }

            _sion.RegisterTimer(new GameScriptTimer(0.25f, () => { _sion.SetStatus(StatusFlags.CanMove, true); }));
        }

        private float GetDamage(ObjAIBase caster, AttackableUnit target, float chargeRatio, int spellLevel)
        {
            float[] baseDmg = { 40f, 60f, 80f, 100f, 120f };
            float[] maxBaseDmg = { 90f, 155f, 220f, 285f, 350f };
            float[] minAdRatio = { 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
            float[] maxAdRatio = { 1.2f, 1.5f, 1.8f, 2.1f, 2.4f };

            int index = Math.Clamp(spellLevel - 1, 0, 4);

            float dmg = baseDmg[index] + (maxBaseDmg[index] - baseDmg[index]) * chargeRatio;
            float adRatio = minAdRatio[index] + (maxAdRatio[index] - minAdRatio[index]) * chargeRatio;

            float totalDmg = dmg + caster.Stats.AttackDamage.Total * adRatio;

            if (target is Monster) totalDmg *= 1.65f;
            else if (target is Minion) totalDmg *= 0.60f;

            return totalDmg;
        }
    }

    public class SionQSoundAfterHalf : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
            IsDamagingSpell = true
        };
    }
}