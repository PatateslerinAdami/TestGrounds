using System;
using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class ZedPBAOEDummy : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        private ObjAIBase _owner;

        private Minion _wShadow;
        private Minion wShadow
        {
            get
            {
                if (_wShadow == null)
                {
                    var sp = _owner.GetSpell("ZedShadowDashMissile");
                    if (sp?.Script is ZedShadowDashMissile dashScript)
                    {
                        _wShadow = dashScript.shadow;
                    }
                }
                return _wShadow;
            }
        }

        private ZedShadowDashMissile GetInFlightMissile()
        {
            var sp = _owner.GetSpell("ZedShadowDashMissile");
            if (sp?.Script is ZedShadowDashMissile dashScript && dashScript.MissileInFlight)
            {
                return dashScript;
            }
            return null;
        }

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellCast(Spell spell)
        {
            _owner.PlayAnimation("Spell3", timeScale: 0.6f);
            AddParticle(_owner, _owner, "Zed_Base_E_cas.troy", default);

            var dashScript = GetInFlightMissile();

            if (dashScript != null)
            {
                dashScript.PendingShadowCasts.Add(() =>
                {
                    dashScript.shadow.PlayAnimation("Spell3", timeScale: 0.6f);
                    AddParticle(_owner, dashScript.shadow, "Zed_Base_E_cas.troy", default);
                });
            }
            else if (_owner.HasBuff("ZedWHandler2"))
            {
                if (wShadow != null)
                {
                    wShadow.PlayAnimation("Spell3", timeScale: 0.6f);
                    AddParticle(_owner, wShadow, "Zed_Base_E_cas.troy", default);
                }
            }
        }

        public void OnSpellPostCast(Spell spell)
        {
            float bonusAd = _owner.Stats.AttackDamage.Total - _owner.Stats.AttackDamage.BaseValue;
            float damage = 30.0f + (spell.CastInfo.SpellLevel * 30.0f) + (bonusAd * 0.8f);

            foreach (var target in GetUnitsInRange(_owner.Position, 290f, true).OfType<ObjAIBase>())
            {
                if (target.Team != _owner.Team && target.Status.HasFlag(StatusFlags.Targetable) && !target.IsDead)
                {
                    target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
                    AddParticle(_owner, target, "Zed_E_Tar.troy", target.Position);
                    if (target is Champion)
                    {
                        var wSpell = _owner.GetSpell("ZedShadowDash");
                        if (wSpell != null && wSpell.State == SpellState.STATE_COOLDOWN)
                        {
                            wSpell.LowerCooldown(2.0f);
                        }
                    }
                }
            }

            var dashScript = GetInFlightMissile();

            if (dashScript != null)
            {
                var capturedDamage = damage;
                var capturedSpell = spell;
                dashScript.PendingShadowCasts.Add(() =>
                {
                    foreach (var shadowTarget in GetUnitsInRange(dashScript.shadow.Position, 290f, true).OfType<ObjAIBase>())
                    {
                        if (shadowTarget.Team != _owner.Team && shadowTarget.Status.HasFlag(StatusFlags.Targetable) && !shadowTarget.IsDead)
                        {
                            shadowTarget.TakeDamage(_owner, capturedDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, capturedSpell);
                            AddParticle(_owner, shadowTarget, "Zed_E_Tar.troy", shadowTarget.Position);
                            if (shadowTarget is Champion)
                            {
                                var wSpell = _owner.GetSpell("ZedShadowDash");
                                if (wSpell != null && wSpell.State == SpellState.STATE_COOLDOWN)
                                {
                                    wSpell.LowerCooldown(2.0f);
                                }
                            }
                        }
                    }
                });
            }
            else if (_owner.HasBuff("ZedWHandler2"))
            {
                var sh = wShadow;
                if (sh != null)
                {
                    foreach (var shadowTarget in GetUnitsInRange(sh.Position, 290f, true).OfType<ObjAIBase>())
                    {
                        if (shadowTarget.Team != _owner.Team && shadowTarget.Status.HasFlag(StatusFlags.Targetable) && !shadowTarget.IsDead)
                        {
                            shadowTarget.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
                            AddParticle(_owner, shadowTarget, "Zed_E_Tar.troy", shadowTarget.Position);
                            if (shadowTarget is Champion)
                            {
                                var wSpell = _owner.GetSpell("ZedShadowDash");
                                if (wSpell != null && wSpell.State == SpellState.STATE_COOLDOWN)
                                {
                                    wSpell.LowerCooldown(2.0f);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}