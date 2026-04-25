using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs
{
    internal class NamiEbuff : IBuffGameScript {
        private ObjAIBase      _nami;
        private AttackableUnit _unit;
        private Spell          _tideCallersBlessing;
        private Particle       _tidecallersBlessingParticle, _hitParticle, _1, _2, _3;
        private Buff           _buff;
        private int            _count             = 0;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _nami = ownerSpell.CastInfo.Owner;
            _unit = unit;
            _tideCallersBlessing = ownerSpell;
            _buff = buff;
            ApplyAssistMarker(unit, ownerSpell.CastInfo.Owner, 10.0f);
            ApiEventManager.OnHitUnit.AddListener(this, unit as ObjAIBase, OnHit);
            _tidecallersBlessingParticle = AddParticleTarget(_nami, unit, "Nami_E_buf", unit, buff.Duration);
            _3 = AddParticleTarget(_nami, unit, "Nami_E_Counter3", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC");
        }

        private void OnHit(DamageData data) {
            var ap  = _nami.Stats.AbilityPower.Total * 0.2f;
            var dmg = 20f + 10f * (_tideCallersBlessing.CastInfo.SpellLevel - 1) + ap;
            if (_count < 3 && IsValidTarget(_nami, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) {
                data.Target.TakeDamage(_unit, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
                
                //slow
                var variables      = new BuffVariables();
                variables.Set("slowAmount", 0.15f + 0.05f * (_tideCallersBlessing.CastInfo.SpellLevel - 1) + 0.05f * (_nami.Stats.AbilityPower.Total % 100f));
                AddBuff("Slow", 1f, 1, _tideCallersBlessing, data.Target, _nami, buffVariables: variables);
                
                
                AddBuff("NamiEHitParticle", 1f, 1, _tideCallersBlessing, data.Target, _nami);
                _count++;
            }
            switch (_count) {
                case 1: RemoveParticle(_3); _2 = AddParticleTarget(_nami, _unit, "Nami_E_Counter2", _unit, _buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC"); break;
                case 2: RemoveParticle(_2); _1 = AddParticleTarget(_nami, _unit, "Nami_E_Counter1", _unit, _buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC"); break;
                case 3: RemoveParticle(_1);  break;
            }
            if (_count >= 3) {
                _buff.DeactivateBuff();
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_tidecallersBlessingParticle);
            RemoveParticle(_hitParticle);
            RemoveParticle(_1);
            RemoveParticle(_2);
            RemoveParticle(_3);
        }
    }
}