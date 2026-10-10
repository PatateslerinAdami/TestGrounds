using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using System.Linq;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class YasuoDashWrapper : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            AutoFaceDirection = true
        };

        internal Buffs.YasuoDashController Dash { get; private set; }

        private AttackableUnit _target;
        private Spell _dummy;

        public float? GetCooldown(Spell spell, byte rank)
        {
            return spell.SpellData.GetEffectAmount(3, rank);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (target == null || target.GetBuffs().Any(b => !b.Remove && b.Name == "YasuoEBlock" && b.SourceUnit == owner))
            {
                spell.CancelCast();
                return;
            }

            YasuoCastHelpers.StopComboAnimation(owner);
            YasuoCastHelpers.ResetSheath(spell);
            _target = target;
        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            _dummy = CreateSpell(owner, "YasuoDummySpell", 45);
            _dummy.CastInfo.SpellLevel = owner.Spells[0].CastInfo.SpellLevel;
            _dummy.Cast(owner.Position, owner.Position, owner);
            float targetCooldown = spell.SpellData.GetEffectAmount(2, spell.CastInfo.SpellLevel);
            AddBuff("YasuoEBlock", targetCooldown - 0.25f, 1, spell, _target, owner);
            var variables = new BuffVariables();
            variables.Set("target", _target);
            Dash = AddBuff("YasuoDashController",0,1, spell,owner,owner,infiniteduration: true,buffVariables: variables)?.BuffScript as Buffs.YasuoDashController;
            if (Dash?.IsActive != true)
            {
                return;
            }

            AddBuff("YasuoDashGhosted", 2, 1, spell, owner, owner);
            var scalar = owner.GetBuffWithName("YasuoDashScalar");
            int stacks = scalar != null && !scalar.Remove && scalar.TimeElapsed < scalar.Duration ? scalar.StackCount : 0;
            float damage = spell.SpellData.GetEffectAmount(1, spell.CastInfo.SpellLevel) * (1 + 0.25f * stacks) + spell.SpellData.Coefficient * owner.Stats.AbilityPower.Total;
            AddBuff("YasuoDashScalar", 5, 1, spell, owner, owner);
            _target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
        }
    }

    public class YasuoDummySpell : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            AutoFaceDirection = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            spell.CastInfo.TargetPositionEnd = Vector3.Zero;
        }
    }
}
