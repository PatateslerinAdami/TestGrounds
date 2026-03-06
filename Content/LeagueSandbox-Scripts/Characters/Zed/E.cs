using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
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

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }

        public void OnSpellCast(Spell spell)
        {
            _owner.PlayAnimation("Spell3", timeScale: 0.6f);
            AddParticle(_owner, _owner, "Zed_Base_E_cas.troy", default);

            if (_owner.HasBuff("ZedWQue") || _owner.HasBuff("ZedWHandler2"))
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

            var units = GetUnitsInRange(_owner.Position, 290f, true).ToList();

            if (wShadow != null && (_owner.HasBuff("ZedWQue") || _owner.HasBuff("ZedWHandler2")))
            {
                var shadowUnits = GetUnitsInRange(wShadow.Position, 290f, true);
                units = units.Union(shadowUnits).ToList();
            }

            foreach (var target in units.OfType<ObjAIBase>())
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
        }
    }
}