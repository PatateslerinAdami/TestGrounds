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
    public class KatarinaR : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            TriggersSpellCasts = true,
            ChannelDuration = 2.5f,
        };
        ObjAIBase _owner;
        bool once = false;
        Spell _spell;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        }
        private void OnLevelUpSpell(Spell spell)
        {
            once = true;
            ApiEventManager.OnLevelUpSpell.RemoveListener(this, spell);
        }
        public void OnUpdate(float diff)
        {
            if(once)
            {
                AddBuff("KatarinaRChecker", 1f, 1, _spell, _owner, _owner, true);
                once = false;
            }
        }
        public void OnSpellChannel(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            AddBuff("KatarinaR", 2.5f, 1, spell, owner, owner);
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            var owner = spell.CastInfo.Owner;
            owner.RemoveBuffsWithName("KatarinaR");
        }
    }

    public class KatarinaRMis : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            }
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            var AP = owner.Stats.AbilityPower.Total * 0.35f;
            var AD = owner.Stats.AttackDamage.FlatBonus * 0.50f;
            float damage = 45f + (95f * spell.CastInfo.SpellLevel) + AP + AD;
            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, spell);
            AddParticleTarget(owner, target, "katarina_deathLotus_tar.troy", target);
        }
    }
}
