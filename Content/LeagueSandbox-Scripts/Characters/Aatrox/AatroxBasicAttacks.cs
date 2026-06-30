using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class AatroxBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false,
            CastingBreaksStealth = true,
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack, true);
        }

        public void OnLaunchAttack(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            if (owner.HasBuff("AatroxWONHPowerBuff") || owner.HasBuff("AatroxWONHLifeBuff")) return;

            owner.SetAutoAttackSpell(owner.HasBuff("AatroxR") ? "AatroxBasicAttack5" : "AatroxBasicAttack2", false);
        }
    }

    public class AatroxBasicAttack2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false,
            CastingBreaksStealth = true,
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack, true);
        }

        public void OnLaunchAttack(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            if (owner.HasBuff("AatroxWONHPowerBuff") || owner.HasBuff("AatroxWONHLifeBuff")) return;

            owner.SetAutoAttackSpell(owner.HasBuff("AatroxR") ? "AatroxBasicAttack4" : "AatroxBasicAttack", false);
        }
    }

    public class AatroxBasicAttack3 : ISpellScript
    {
        AttackableUnit _target;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            AutoFaceDirection = false,
            CastingBreaksStealth = true,
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack, true);
        }

        public void OnLaunchAttack(Spell spell)
        {
            var owner = spell.CastInfo.Owner as Champion;
            if (owner != null)
            {
                if (owner.HasBuff("AatroxWPower"))
                {
                    float mBlood = owner.Stats.CurrentHealth * 0.1f;
                    owner.Stats.CurrentMana += mBlood;
                    owner.TakeDamage(owner, mBlood, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PERIODIC, false);

                    var adRatio = owner.Stats.AttackDamage.Total * 1f;
                    var damage = 60 + (35 * (owner.Spells[1].CastInfo.SpellLevel - 1) + adRatio);

                    _target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PERIODIC, false);

                    AddParticleTarget(owner, _target, "Aatrox_Base_W_Active_Hit_Power.troy", _target, 6f);
                }
                else if (owner.HasBuff("AatroxWLife"))
                {
                    float heal = 25 + (5 * (owner.Spells[1].CastInfo.SpellLevel - 1));
                    float bBlood = owner.Stats.HealthPoints.Total * 0.5f;
                    float blood = owner.Stats.HealthPoints.Total * 0.025f;
                    float ap = owner.Stats.AbilityPower.Total * 0.7f;
                    float xBlood = owner.Stats.CurrentHealth;

                    if (bBlood >= xBlood)
                    {
                        owner.Stats.CurrentHealth += (heal + ap + blood) * 2;
                    }
                    else
                    {
                        owner.Stats.CurrentHealth += heal + ap + blood;
                    }

                    AddParticleTarget(owner, owner, "Aatrox_Base_W_Life_Self.troy", owner);
                }

                owner.SetAutoAttackSpell(owner.HasBuff("AatroxR") ? "AatroxBasicAttack4" : "AatroxBasicAttack", false);
            }
        }
    }

    public class AatroxBasicAttack4 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false,
            CastingBreaksStealth = true,
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack, true);
        }

        public void OnLaunchAttack(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            if (owner.HasBuff("AatroxWONHPowerBuff") || owner.HasBuff("AatroxWONHLifeBuff")) return;

            owner.SetAutoAttackSpell(owner.HasBuff("AatroxR") ? "AatroxBasicAttack5" : "AatroxBasicAttack2", false);
        }
    }

    public class AatroxBasicAttack5 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false,
            CastingBreaksStealth = true,
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack, true);
        }

        public void OnLaunchAttack(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            if (owner.HasBuff("AatroxWONHPowerBuff") || owner.HasBuff("AatroxWONHLifeBuff")) return;

            owner.SetAutoAttackSpell(owner.HasBuff("AatroxR") ? "AatroxBasicAttack4" : "AatroxBasicAttack", false);
        }
    }

    public class AatroxBasicAttack6 : ISpellScript
    {
        AttackableUnit _target;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            AutoFaceDirection = false,
            CastingBreaksStealth = true,
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack, true);
        }

        public void OnLaunchAttack(Spell spell)
        {
            var owner = spell.CastInfo.Owner as Champion;
            if (owner != null)
            {
                if (owner.HasBuff("AatroxWPower"))
                {
                    float mBlood = owner.Stats.CurrentHealth * 0.1f;
                    owner.Stats.CurrentMana += mBlood;
                    owner.TakeDamage(owner, mBlood, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PERIODIC, false);

                    var adRatio = owner.Stats.AttackDamage.Total * 1f;
                    var damage = 60 + (35 * (owner.Spells[1].CastInfo.SpellLevel - 1) + adRatio);

                    _target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PERIODIC, false);

                    AddParticleTarget(owner, _target, "Aatrox_Base_W_Active_Hit_Power.troy", _target, 6f);
                }
                else if (owner.HasBuff("AatroxWLife"))
                {
                    float heal = 25 + (5 * (owner.Spells[1].CastInfo.SpellLevel - 1));
                    float bBlood = owner.Stats.HealthPoints.Total * 0.5f;
                    float blood = owner.Stats.HealthPoints.Total * 0.025f;
                    float ap = owner.Stats.AbilityPower.Total * 0.7f;
                    float xBlood = owner.Stats.CurrentHealth;

                    if (bBlood >= xBlood)
                    {
                        owner.Stats.CurrentHealth += (heal + ap + blood) * 2;
                    }
                    else
                    {
                        owner.Stats.CurrentHealth += heal + ap + blood;
                    }

                    AddParticleTarget(owner, owner, "Aatrox_Base_W_Life_Self.troy", owner);
                }
                owner.SetAutoAttackSpell(owner.HasBuff("AatroxR") ? "AatroxBasicAttack4" : "AatroxBasicAttack", false);
            }
        }
    }
}