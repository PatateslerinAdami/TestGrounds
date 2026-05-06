using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
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
        private ObjAIBase _katarina;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            TriggersSpellCasts = false,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _katarina = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AddBuff("KatarinaRSound", 2.5f, 1, spell, _katarina, _katarina);
        }
    }

    public class KatarinaR : ISpellScript
    {
        private ObjAIBase _katarina;
        private Spell _spell;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            TriggersSpellCasts = true,
            ChannelDuration = 2.5f
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _katarina = owner;
            _spell = spell;
        }

        public void OnSpellChannel(Spell spell)
        {
            SpellCast(_katarina, 1, SpellSlotType.ExtraSlots, true, _katarina, _katarina.Position);
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
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
            var ap = _katarina.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient / 10;
            var ad = _katarina.Stats.AttackDamage.FlatBonus * mainSpell.SpellData.Coefficient2 / 10;
            var knifeDamage = 30 + 20 * (mainSpell.CastInfo.SpellLevel - 1) + ad + ap;

            AddBuff("GrievousWounds", 3.0f, 1, spell, target, _katarina);
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