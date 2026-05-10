using System.Numerics;
using GameServerCore;
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
    public class KatarinaRTrigger : ISpellScript
    {
        private ObjAIBase _katarina;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = false
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _katarina = owner;
            SealSpellSlot(_katarina, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION, true);
        }

        public void OnUpdate(float diff)
        {
            var shouldSeal = GetUnitsInRange(_katarina, _katarina.Position, 550f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes).Count == 0;
            SealSpellSlot(_katarina, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION, shouldSeal);
        }
    }
    
    public class KatarinaRTriggerSound : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            TriggersSpellCasts = false,
        };
    }

    public class KatarinaR : ISpellScript
    {
        private ObjAIBase _katarina;
        private Spell _spell;
        private PeriodicTicker _periodicTicker;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            TriggersSpellCasts = true,
            ChannelDuration = 2.4f
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _katarina = owner;
            _spell = spell;
        }

        public void OnSpellChannel(Spell spell)
        {
            _periodicTicker.Reset();
            SpellCast(_katarina, 1, SpellSlotType.ExtraSlots, true, _katarina, _katarina.Position);
            AddBuff("KatarinaRSound", 4f, 1, spell, _katarina, _katarina);

            const AnimationFlags spell4Flags = AnimationFlags.UniqueOverride | AnimationFlags.Override | AnimationFlags.Unknown8;
            switch (_katarina.SkinID)
            {
                default: PlayAnimation(_katarina, "Spell4", timeScale: 0f, speedScale: 1f, flags: spell4Flags); break;
                case 7:  PlayAnimation(_katarina, "Spell4", timeScale: 0.2f, speedScale: 1f, flags: spell4Flags); break;
            }
        }
        
        public void OnSpellChannelUpdate(Spell spell, float diff)
        {
            var ticks = _periodicTicker.ConsumeTicks(diff, 250f, true, 1, 10);
            if (ticks != 1) return;

            var closestUnits = GetUnitsInRange(_katarina, _katarina.Position, 550, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes).ToArray();
            var length = closestUnits.Length > 3 ? 3 : closestUnits.Length;
            for (var i = 0; i < length; i++)
            {
                SpellCast(_katarina, 0, SpellSlotType.ExtraSlots, true, closestUnits[i], _katarina.Position);
            }
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            _katarina.RemoveBuffsWithName("KatarinaRSound");
            AddParticleTarget(_katarina, _katarina, "Katarina_deathLotus_empty", _katarina, 1.0f);
        }

        public void OnSpellPostChannel(Spell spell)
        {
            _katarina.RemoveBuffsWithName("KatarinaRSound");
        }
    }

    public class KatarinaRMis : ISpellScript
    {
        private ObjAIBase _katarina;

        public SpellScriptMetadata ScriptMetadata => new()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            },
            PersistsThroughDeath = true,
            IsDamagingSpell = true,
            CooldownIsAffectedByCDR = false
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _katarina = owner;
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        }

        private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var mainSpell = _katarina.GetSpell("KatarinaR");
            var ap = _katarina.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient;
            var ad = _katarina.Stats.AttackDamage.FlatBonus * mainSpell.SpellData.Coefficient2;
            var knifeDamage = 30 + 20 * (mainSpell.CastInfo.SpellLevel - 1) + ad + ap;

            AddBuff("GrievousWound", 3.0f, 1, spell, target, _katarina);
            switch (_katarina.SkinID)
            {
                case 9:
                    AddParticleTarget(_katarina, target, "Katarina_Skin09_DeathLotus_Tar", target,
                        bone: "BUFFBONE_GLB_GROUND_LOC"); break;
                default:
                    AddParticleTarget(_katarina, target, "katarina_deathLotus_tar", target,
                        bone: "BUFFBONE_GLB_GROUND_LOC"); break;
            }

            target.TakeDamage(_katarina, knifeDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_SPELLAOE,
                false);

            if (target.HasBuff("KatarinaQMark"))
            {
                var markApRatio = _katarina.Stats.AbilityPower.Total * 0.2f;
                var markDamage = 15 + 15 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1) + markApRatio;
                target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELL, false);
                RemoveBuff(target, "KatarinaQMark");
            }

            missile.SetToRemove();
        }
    }
}