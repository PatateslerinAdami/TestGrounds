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
    public class FiddlesticksDarkWind : ISpellScript
    {
        private ObjAIBase _fiddlesticks;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Chained,
                MaximumHits = 5,
                CanHitSameTarget = true,
                BounceSpellName = "FiddleSticksDarkWindMissile",
            },
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _fiddlesticks = owner;
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void OnSpellCast(Spell spell)
        {
        }

        private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            AddBuff("Silence", 1.2f, 1, spell, target, _fiddlesticks, false);
            AddParticleTarget(_fiddlesticks, target, spell.SpellData.HitEffectName, target);
            target.TakeDamage(_fiddlesticks,
                (65f + 15f * (spell.CastInfo.SpellLevel - 1) +
                 _fiddlesticks.Stats.AbilityPower.Total * spell.SpellData.Coefficient) *
                (IsValidTarget(_fiddlesticks, target,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions)
                    ? 1.5f
                    : 1f), DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                DamageResultType.RESULT_NORMAL);
        }
    }

    public class FiddleSticksDarkWindMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
        };
    }
}