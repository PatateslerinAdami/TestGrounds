using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

    public class KatarinaQ : ISpellScript
    {
        Spell _qMis;
        ObjAIBase _katarina;
        private readonly Dictionary<uint, HashSet<AttackableUnit>> _chainHitUnits = new();
        
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target,
            },
            TriggersSpellCasts  = true,
            IsDamagingSpell     = true,
            CastTime            = 0.25f,
            AutoCooldownByLevel = [10, 9.5f, 9f, 8.5f, 8f]
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _qMis     = spell;
            _katarina = owner;
            ApiEventManager.OnSpellHit.AddListener(this, _qMis, TargetExecute, false);
            ApiEventManager.OnUpdateStats.AddListener(this, _katarina, OnStatsUpdate);
        }

        public void OnSpellPostCast(Spell spell)
        {
        }

        private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var ap          = _katarina.Stats.AbilityPower.Total * 0.45f;
            var dmg         = 60 + 25 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1 - 1) + ap;
            target.TakeDamage(_katarina, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                              false);
            
            ConsumeDaggered(target);
            AddBuff("KatarinaDaggered",              4.0f, 1, spell, target, _katarina);
            
            /*switch (_katarina.SkinId) {
                case 9: AddParticleTarget(_katarina, target, "katarina_Skin09_Q_Cast", target); break; 
                default: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_cas", target); break;  
            }*/
            
            switch (_katarina.SkinID) {
                case 9: AddParticleTarget(_katarina, target, "Katarina_Skin09_Q_tar", target); break;
                case 7: AddParticleTarget(_katarina, target, "katarina_XMas_bouncingBlades_tar", target);; break;
                case 6: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_tar_sand", target);; break;
                default: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_tar",      target); break;
            }

            var chainId = GetChainId(spell, missile);
            var hitUnits = new HashSet<AttackableUnit> { target };
            _chainHitUnits[chainId] = hitUnits;
            
            /*var nextTarget = GetUnitsInRange(_katarina, target.Position, 400f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)
                .Where(x => x != _katarina && !hitUnits.Contains(x))
                .OrderBy(x => Vector2.Distance(target.Position, x.Position))
                .FirstOrDefault();

            if (nextTarget != null)
            {
                SpellCast(_katarina, 2, SpellSlotType.ExtraSlots, false, nextTarget, target.Position);
                LinkLastQMisCastToChain(chainId);
            }
            else
            {
                EndChain(chainId);
            }*/
        }

        private void OnStatsUpdate(AttackableUnit unit, float diff) {
            var bonusAp = _katarina.Stats.AbilityPower.Total * 0.45f;
            SetSpellToolTipVar(_katarina, 0, bonusAp, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
        }

        internal uint GetChainId(Spell spell, SpellMissile missile)
        {
            return missile?.CastInfo?.SpellNetID ?? spell.CastInfo.SpellNetID;
        }

        internal HashSet<AttackableUnit> GetOrCreateHitUnits(uint chainId)
        {
            if (_chainHitUnits.TryGetValue(chainId, out var hitUnits)) return hitUnits;
            hitUnits                = new HashSet<AttackableUnit>();
            _chainHitUnits[chainId] = hitUnits;

            return hitUnits;
        }

        internal void LinkLastQMisCastToChain(uint parentChainId)
        {
            if (!_chainHitUnits.TryGetValue(parentChainId, out var hitUnits)) return;

            var qMisSpell = _katarina.GetSpell("KatarinaQMis");
            if (qMisSpell == null) return;

            var lastCastId = qMisSpell.CastInfo.SpellNetID;
            if (lastCastId == 0) return;

            _chainHitUnits[lastCastId] = hitUnits;
        }

        internal void EndChain(uint chainId)
        {
            _chainHitUnits.Remove(chainId);
        }

        private void ConsumeDaggered(AttackableUnit target)
        {
            if (!target.HasBuff("KatarinaDaggered")) return;

            var markApRatio = _katarina.Stats.AbilityPower.Total * 0.2f;
            var markDamage  = 15 + 15 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1) + markApRatio;

            target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
            RemoveBuff(target, "KatarinaDaggered");
        }
    }

    public class KatarinaQMis : ISpellScript
    {
        private ObjAIBase _katarina;
        private KatarinaQ _mainQ;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target,
            },
            TriggersSpellCasts = false,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
            _katarina = spell.CastInfo.Owner as Champion;

            var mainSpell = owner.GetSpell("KatarinaQ");
            _mainQ = mainSpell.Script as KatarinaQ;
        }

        private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (_mainQ == null) return;
            

            var chainId = _mainQ.GetChainId(spell, missile);
            var hitUnits = _mainQ.GetOrCreateHitUnits(chainId);

            var ap          = _katarina.Stats.AbilityPower.Total * 0.45f;
            var dmg         = 60 + 25 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1 - 1) + ap;

            switch (_katarina.SkinID) {
                default: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_cas", target); break;   
            }

            switch (_katarina.SkinID) {
                case 7:  AddParticleTarget(_katarina, target, "katarina_XMas_bouncingBlades_tar", target);; break;
                default: AddParticleTarget(_katarina, target, "katarina_bouncingBlades_tar",      target); break;  
            }

            switch (hitUnits.Count) {
                case 1: dmg *= 0.9f; break;
                case 2: dmg *= 0.8f; break;
                case 3: dmg *= 0.7f; break;
                case 4: dmg *= 0.6f; break;
            }

            target.TakeDamage(_katarina, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                              false);
            if (target.HasBuff("KatarinaDaggered")) {
                var markApRatio = _katarina.Stats.AbilityPower.Total * 0.2f;
                var markDamage  = 15 + 15 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1) + markApRatio;
                target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                RemoveBuff(target, "KatarinaDaggered");
            }
            AddBuff("KatarinaDaggered",              4.0f, 1, spell, target, _katarina);
            

            hitUnits.Add(target);
            if (hitUnits.Count >= 5)
            {
                _mainQ.EndChain(chainId);
                return;
            }

            var nextTarget = GetUnitsInRange(target.Position, 400f, true)
                .Where(x => x is not ObjBuilding && x != _katarina && !hitUnits.Contains(x) && x.Team !=_katarina.Team)
                .OrderBy(x => Vector2.Distance(target.Position, x.Position))
                .FirstOrDefault();

            if (nextTarget != null)
            {
                SpellCast(_katarina, 2, SpellSlotType.ExtraSlots, false, nextTarget, target.Position);
                _mainQ.LinkLastQMisCastToChain(chainId);
            }
            else
            {
                _mainQ.EndChain(chainId);
            }
        }
    }
