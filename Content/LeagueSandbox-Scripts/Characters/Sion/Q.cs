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
            TriggersSpellCasts = true,
            ChannelDuration = 1.97f,
            AutoFaceDirection = false
        };

        ObjAIBase _owner;
        Particle p1, p2, p3, p4, p5;

        bool channeling = false;
        bool passedOneSecond = false;
        float currentCharge = 0f;

        Buff b;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell, true);
        }

        private void OnLevelUpSpell(Spell spell)
        {
            SetSpell(_owner, "SionQSoundAfterHalf", SpellSlotType.ExtraSlots, 7);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.StopMovement(networked: false);
            InstantStopTest(owner, true, false);
            OverrideAnimation(owner, "Spell1_Chrg", "Attack1", this);
            FaceDirection(end, owner, true);
        }

        public void OnSpellChannel(Spell spell)
        {
            b = AddBuff("SionQ", 2f, 1, spell, _owner, _owner);
            _owner.SetStatus(StatusFlags.CanMove, false);
            channeling = true;
            passedOneSecond = false;
            currentCharge = 0f;

            AddParticlePos(_owner, "Sion_Base_Q_Cas.troy", _owner.Position, GetPointFromUnit(_owner, 1000f),
                lifetime: 1, direction: _owner.Direction);
            p1 = AddParticleTarget(_owner, _owner, "sion_base_q_cas1_weapon.troy", _owner, lifetime: 5,
                bone: "Buffbone_Glb_Weapon_1");
            p2 = AddParticlePos(_owner, "Sion_Base_Q_Indicator2.troy", _owner.Position, default, lifetime: 1,
                direction: _owner.Direction);
        }

        public void OnUpdate(float diff)
        {
            if (channeling)
            {
                currentCharge += diff / 1000f;

                if (currentCharge >= 1.0f && !passedOneSecond)
                {
                    passedOneSecond = true;
                    p3 = AddParticlePos(_owner, "Sion_Base_Q_Indicator_Red2.troy", _owner.Position, default,
                        lifetime: 1, direction: _owner.Direction);
                    p4 = AddParticleTarget(_owner, _owner, "sion_base_q_cas2_weapon.troy", _owner, lifetime: 5,
                        bone: "Buffbone_Glb_Weapon_1");
                    p5 = AddParticlePos(_owner, "sion_base_q_cas3.troy", _owner.Position,
                        GetPointFromUnit(_owner, 1000f), lifetime: 1, direction: _owner.Direction, size: 1.7f);
                }
            }
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            LetGo(spell, reason);
        }

        public void OnSpellPostChannel(Spell spell)
        {
            LetGo(spell, ChannelingStopSource.TimeCompleted);
        }

        private void LetGo(Spell spell, ChannelingStopSource reason)
        {
            if (!channeling) return;
            channeling = false;

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

            ClearOverrideAnimation(_owner, "Attack1", this);

            if (reason != ChannelingStopSource.PlayerCommand && reason != ChannelingStopSource.TimeCompleted)
            {
                spell.SetCooldown(2.0f, true);
                _owner.SetStatus(StatusFlags.CanMove, true);
                return;
            }

            float chargeRatio = Math.Clamp(currentCharge / 2f, 0f, 1f);
            float currentRange = 400f + 600f * chargeRatio;

            if (currentCharge >= 1.0f)
            {
                PlayAnimation(_owner, "Spell1_Hit2", timeScale: 0.3f);
                AddParticlePos(_owner, "sion_base_q_hit3.troy", _owner.Position, GetPointFromUnit(_owner, currentRange),
                    lifetime: 1, direction: _owner.Direction);
                //SpellCast(_owner, 7, SpellSlotType.ExtraSlots, _owner.Position, _owner.Position, false, Vector2.Zero);
                //Couldnt find a way to add the axe hitting ground sound.
            }
            else
            {
                PlayAnimation(_owner, "Spell1_Hit1", timeScale: 0.3f);
                CreateCustomMissile(_owner, "SionQHitParticleMissile2", _owner.Position,
                    GetPointFromUnit(_owner, currentRange - 200f), new MissileParameters { Type = MissileType.Arc });
            }

            Vector2 dir = Vector2.Normalize(new Vector2(_owner.Direction.X, _owner.Direction.Z));
            if (dir == Vector2.Zero) dir = new Vector2(1, 0);

            foreach (var unit in EnumerateValidUnitsInRange(_owner, _owner.Position, currentRange + 200f, true,
                         SpellDataFlags.AffectEnemies))
            {
                Vector2 toUnit = unit.Position - _owner.Position;
                float projection = Vector2.Dot(toUnit, dir);

                if (projection >= -50f && projection <= currentRange + unit.CollisionRadius)
                {
                    float halfWidth = 112f + (Math.Max(0, projection) / 1000f) * 140f;
                    float perpDistance = Math.Abs(toUnit.X * dir.Y - toUnit.Y * dir.X);

                    if (perpDistance <= halfWidth + unit.CollisionRadius)
                    {
                        float dmg = GetDamage(_owner, unit, chargeRatio, spell.CastInfo.SpellLevel);
                        unit.TakeDamage(_owner, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                            false);
                        //AddParticlePos(_owner, "Sion_Base_Q3_tar.troy", _owner.Position, GetPointFromUnit(_owner, currentRange), lifetime: 1, direction: _owner.Direction);
                        if (currentCharge >= 1.0f)
                        {
                            float scaleCC = Math.Clamp((currentCharge - 1f) / 0.97f, 0f, 1f);
                            float knockupDuration = 0.5f + (0.5f * scaleCC);
                            float stunDuration = 1.25f + (1.0f * scaleCC);

                            var vars = new BuffVariables();
                            vars.Set("KnockupTime", knockupDuration);

                            AddBuff("SionQKnockup", stunDuration, 1, spell, unit, _owner, false, null, vars);
                        }
                        else
                        {
                            AddBuff("SionQSlow", 0.25f, 1, spell, unit, _owner);
                        }
                    }
                }
            }

            _owner.RegisterTimer(new GameScriptTimer(0.25f, () => { _owner.SetStatus(StatusFlags.CanMove, true); }));
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