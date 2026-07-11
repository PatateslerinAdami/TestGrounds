using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FiddlesticksDarkWind : ISpellScript
    {
        private ObjAIBase _fiddlesticks;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Chained,
                BounceSpellNameEnemy = "FiddleSticksDarkWindMissile",
                MaximumHits = 5,
                CanHitSameTarget = true,
                CanHitEnemies = true,
                CanHitFriends = false,
                CanHitCaster = false,
                BounceSelection =  BounceSelection.Random
            },
            NotSingleTargetSpell = false,
            DoesntBreakShields = false,
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _fiddlesticks = owner;
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit, false);
        }
        
        private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
        {
            
            var particleName = _fiddlesticks.SkinID switch
            {
                6 => "Party_DarkWind_tar.troy",
                _ => "DarkWind_tar.troy"
            };
            SpellEffectCreate(particleName, _fiddlesticks, target, target, scale: 1f, flags: FXFlags.SimulateWhileOffScreen, keywordObject: _fiddlesticks, fowVisibilityRadius: 10f);
            AddParticleTarget(_fiddlesticks, target, spell.SpellData.HitEffectName, target);
            
            AddBuff("Silence", 1.2f, 1, spell, target, _fiddlesticks, false);
            var ap = _fiddlesticks.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
            var dmg = spell.SpellData.EffectLevelAmount[3][spell.CastInfo.SpellLevel] + ap;
            dmg *= IsValidTarget(_fiddlesticks, target,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions) ? 1.5f : 1f;
            target.TakeDamage(_fiddlesticks, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
        }
    }

    public class FiddleSticksDarkWindMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
        };
    }
}