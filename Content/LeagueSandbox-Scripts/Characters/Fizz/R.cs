using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FizzMarinerDoom : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            CastingBreaksStealth = true
        };

        private readonly List<FizzFish> _fish = new List<FizzFish>();

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var aim = FizzHelpers.Aim(spell);
            float distance = MathF.Min(1275, Vector2.Distance(owner.Position, aim) + 50);
            var end = owner.Position + FizzHelpers.Direction(owner, aim) * distance;
            var carrier = owner.GetSpell("FizzMarinerDoomMissile");
            carrier.CastInfo.SpellLevel = spell.CastInfo.SpellLevel;
            var missile = carrier.CreateCustomMissile(owner.Position, end,
                new MissileParameters { Type = MissileType.Circle, ReplicationHeightOffset = 100, ReplicationEndHeightOffset = 100 },
                isOverrideCastPosition: false, customHeightOffset: 0);
            if (missile != null)
            {
                _fish.Add(new FizzFish(spell, missile, end));
            }
        }

        public void OnUpdate(float diff)
        {
            foreach (var fish in _fish.ToArray())
            {
                fish.Update(diff);
                if (fish.Finished)
                {
                    _fish.Remove(fish);
                }
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            foreach (var fish in _fish.ToArray())
            {
                fish.Cancel();
            }

            _fish.Clear();
        }
    }

    public class FizzMarinerDoomMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true
        };
    }

    public class FizzMarinerDoomBomb : FizzMarinerDoomMissile
    {
    }

    public class FizzShark : FizzMarinerDoomMissile
    {
    }

    public class FizzSharkDissappear : FizzMarinerDoomMissile
    {
    }

    public sealed class FizzFish
    {
        private readonly ObjAIBase _owner;
        private readonly Spell _spell;
        private readonly SpellMissile _missile;
        private readonly Vector2 _end;
        private readonly float _damage, _slow;
        private readonly List<Region> _groundVision = new List<Region>();

        private AttackableUnit _target;
        private Minion _shark;
        private Region _attachedVision, _attachedStealthVision;
        private Buff _bomb, _slowBuff;
        private Particle _orbit, _ring, _splash, _groundSplash;

        private float _elapsed;
        private bool _armed, _attachedOnce, _exploded;

        public bool Finished { get; private set; }

        public FizzFish(Spell spell, SpellMissile missile, Vector2 end)
        {
            _spell = spell;
            _owner = spell.CastInfo.Owner;
            _missile = missile;
            _end = end;
            byte rank = spell.CastInfo.SpellLevel;
            _damage = spell.SpellData.GetEffectAmount(1, rank) + spell.SpellData.Coefficient * _owner.Stats.AbilityPower.Total;
            _slow = spell.SpellData.GetEffectAmount(2, rank) / 100;
            ApiEventManager.OnSpellHit.AddListener(this, missile.SpellOrigin, Hit);
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, MissileEnded);
        }

        private void Hit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (missile != _missile)
            {
                return;
            }

            if (_armed)
            {
                return;
            }

            if (Finished)
            {
                return;
            }

            if (!FizzHelpers.ChampionTarget(target))
            {
                return;
            }

            if (!FizzHelpers.Eligible(_owner, target))
            {
                return;
            }

            Attach(target);
            missile.SetToRemove();
        }

        private void MissileEnded(SpellMissile missile)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            if (_armed || Finished)
            {
                return;
            }

            if (Vector2.DistanceSquared(missile.Position, _end) > 4)
            {
                Cancel(removeMissile: false);
                return;
            }

            _armed = true;
            _elapsed = 0;
            MakeIndicators(null, _end);
        }

        private void MakeIndicators(AttackableUnit target, Vector2 position)
        {
            RemoveParticle(_orbit);
            RemoveParticle(_ring);
            string orbit = _owner.SkinID == 3 ? "Fizz_UltimateMissile_Orbit_Lobster.troy" : "Fizz_UltimateMissile_Orbit.troy";
            if (target != null)
            {
                _orbit = AddParticleTarget(_owner, target, orbit, target, lifetime: 2, ignoreCasterVisibility: true);
                _ring = AddParticleTarget(_owner, target, "Fizz_Ring_Green.troy", target, lifetime: 2, flags: (FXFlags)288, enemyParticle: "Fizz_Ring_Red.troy", ignoreCasterVisibility: true);
            }
            else
            {
                _groundVision.Add(AddPosPerceptionBubble(position, 350, 3, _owner.Team, regionType: RegionType.Unknown2));
                _orbit = AddParticlePos(_owner, orbit, position, position, lifetime: 2, ignoreCasterVisibility: true);
                _ring = AddParticlePos(_owner, "Fizz_Ring_Green.troy", position, position, lifetime: 2, enemyParticle: "Fizz_Ring_Red.troy", ignoreCasterVisibility: true);
            }
        }

        private void Attach(AttackableUnit target)
        {
            _armed = true;
            _target = target;
            if (!_attachedOnce)
            {
                _elapsed = 0;
                _attachedOnce = true;
            }

            float remaining = MathF.Max(0, 1.5f - _elapsed);
            var variables = new BuffVariables();
            variables.Set("fish", this);
            _bomb = AddBuff("FizzMarinerDoomBomb", remaining, 1, _spell, target, _owner, buffVariables: variables);
            var slow = new BuffVariables();
            slow.Set("slow", _slow);
            _slowBuff = AddBuff("FizzMarinerDoomSlow", remaining + 0.5f, 1, _spell, target, _owner, buffVariables: slow);
            _attachedVision = AddUnitPerceptionBubble(target, 300, 4, _owner.Team, regionType: RegionType.Unknown2);
            _attachedStealthVision = AddUnitPerceptionBubble(target, 50, 4, _owner.Team, revealStealthed: true, regionType: RegionType.Unknown2);
            MakeIndicators(target, target.Position);
        }

        private void RemoveAttachedVision()
        {
            _attachedVision?.SetToRemove();
            _attachedStealthVision?.SetToRemove();
            _attachedVision = null;
            _attachedStealthVision = null;
        }

        public void BombRemoved(Buff buff)
        {
            if (_bomb != buff || _exploded || Finished)
            {
                return;
            }

            _bomb = null;
            if (_slowBuff != null)
            {
                RemoveBuff(_slowBuff);
            }

            _slowBuff = null;
            _target = null;
            RemoveAttachedVision();
            MakeIndicators(null, buff.TargetUnit.Position);
            _dropPosition = buff.TargetUnit.Position;
        }

        private Vector2? _dropPosition;

        public void Update(float diff)
        {
            if (Finished)
            {
                return;
            }

            if (_owner.IsToRemove())
            {
                Cancel();
                return;
            }

            if (!_armed)
            {
                return;
            }

            _elapsed += diff / 1000;
            if (_exploded)
            {
                if (_elapsed >= 1.3f)
                {
                    Cancel(removeGroundVision: false);
                }

                return;
            }

            if (_target != null && (_target.IsDead || _target.IsToRemove()))
            {
                var bomb = _bomb;
                if (bomb != null)
                {
                    BombRemoved(bomb);
                    if (!bomb.Remove)
                    {
                        RemoveBuff(bomb);
                    }
                }
            }

            if (_target == null && _elapsed < 1.5f)
            {
                foreach (var target in GetUnitsInRange(_owner, _dropPosition ?? _end, 100, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes))
                {
                    if (!FizzHelpers.Eligible(_owner, target) || !FizzHelpers.ChampionTarget(target))
                    {
                        continue;
                    }

                    if (target.GetBuffWithName("FizzMarinerDoomBomb") is { Remove: false })
                    {
                        continue;
                    }

                    Attach(target);
                    break;
                }
            }

            if (_elapsed >= 1.5f)
            {
                Explode();
            }
        }

        private void Explode()
        {
            if (_exploded || Finished)
            {
                return;
            }

            _exploded = true;
            _elapsed = 0;
            var attached = _target;
            var position = attached?.Position ?? _dropPosition ?? _end;
            if (_bomb != null)
            {
                RemoveBuff(_bomb);
            }

            _bomb = null;
            if (_slowBuff != null)
            {
                RemoveBuff(_slowBuff);
            }

            _slowBuff = null;
            RemoveAttachedVision();
            RemoveParticle(_orbit);
            RemoveParticle(_ring);
            bool ground = attached == null;
            _shark = AddMinion(
                _owner,
                "FizzShark",
                ground ? "OMNOMNOMNOMONOM" : "Omnomnomnom",
                position,
                _owner.Team,
                _owner.SkinID,
                ignoreCollision: true,
                targetable: false,
                isWard: ground,
                targetingFlags: 0,
                useSpells: false,
                visionRadius: 1350,
                visibilitySize: ground ? 100 : 0
            );
            HideHealthBar(_shark);
            PlayAnimation(_shark, "Spell4", 1, 0, 1, (AnimationFlags)197);
            _splash = AddParticlePos(_owner, "Fizz_SharkSplash.troy", position, position, lifetime: 1.3f, ignoreCasterVisibility: true);
            _groundSplash = AddParticlePos(_owner, "Fizz_SharkSplash_ground.troy", position, position, lifetime: 1.3f, ignoreCasterVisibility: true);
            foreach (var target in GetUnitsInRange(_owner, position, 350, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes))
            {
                if (!FizzHelpers.Eligible(_owner, target))
                {
                    continue;
                }

                target.TakeDamage(_owner, _damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, _spell);
                if (target.IsDead)
                {
                    continue;
                }

                var slow = new BuffVariables();
                slow.Set("slow", _slow);
                AddBuff("FizzMarinerDoomSlow", 1.5f, 1, _spell, target, _owner, buffVariables: slow);
                var lift = new BuffVariables();
                lift.Set("center", position);
                AddBuff(target == attached ? "FizzKnockup" : "FizzMoveback", target == attached ? 0.75f : 0.25f, 1, _spell, target, _owner,
                    buffVariables: lift);
            }
        }

        public void Cancel(bool removeMissile = true, bool removeGroundVision = true)
        {
            if (Finished)
            {
                return;
            }

            Finished = true;
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveAttachedVision();
            if (removeGroundVision)
            {
                foreach (var region in _groundVision)
                {
                    region.SetToRemove();
                }
            }

            _groundVision.Clear();
            if (removeMissile && !_missile.IsToRemove())
            {
                _missile.SetToRemove();
            }

            if (_bomb != null)
            {
                RemoveBuff(_bomb);
            }

            if (_slowBuff != null)
            {
                RemoveBuff(_slowBuff);
            }

            RemoveParticle(_orbit);
            RemoveParticle(_ring);
            RemoveParticle(_splash);
            RemoveParticle(_groundSplash);
            if (_shark != null)
            {
                DespawnMinion(_shark);
            }
        }
    }
}
