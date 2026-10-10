using GameServerCore.Enums;
using System.Numerics;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FioraFlurry : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true
        };

        private Spell _spell;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _spell = spell;
            ApiEventManager.OnHitUnit.AddListener(this, owner, OnHit);
            ApiEventManager.OnKillUnit.AddListener(this, owner, OnKill);
            ApiEventManager.OnAssist.AddListener(this, owner, OnAssist);
        }

        public void OnSpellPostCast(Spell spell)
        {
            var variables = new BuffVariables();
            variables.Set("Rank", spell.CastInfo.SpellLevel);
            AddBuff("FioraFlurry", spell.SpellData.GetEffectAmount(2, spell.CastInfo.SpellLevel), 1, spell, spell.CastInfo.Owner,
                spell.CastInfo.Owner, buffVariables: variables);
            spell.CastInfo.Owner.CancelAutoAttack(true);
        }

        private void OnHit(DamageData data)
        {
            if (data.IsAutoAttack && data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK && !data.IsHitBlocked)
            {
                ApplyLungeHit(_spell, data.Target);
            }
        }

        public static void ApplyLungeHit(Spell source, AttackableUnit target)
        {
            var owner = source.CastInfo.Owner;
            var active = owner.GetBuffWithName("FioraFlurry");
            if (owner.IsDead)
            {
                return;
            }

            if (active == null)
            {
                return;
            }

            if (active.Remove)
            {
                return;
            }

            if (target == null)
            {
                return;
            }

            if (target.Team == owner.Team)
            {
                return;
            }

            var variables = new BuffVariables();
            variables.Set("Speed", active.OriginSpell.SpellData.GetEffectAmount(3, active.Variables.GetInt("Rank")) / 100f);
            var speed = AddBuff("FioraFlurryDummy", 3, 1, active.OriginSpell, owner, owner, buffVariables: variables);
            (speed?.BuffScript as Buffs.FioraFlurryDummy)?.SyncSpeed();
        }

        private void OnKill(DeathData data)
        {
            if (data.Unit is Champion && _spell.CastInfo.SpellLevel > 0)
            {
                _spell.SetCooldown(0, true);
            }
        }

        private void OnAssist(ObjAIBase owner, DeathData data)
        {
            if (data.Unit is Champion && _spell.CastInfo.SpellLevel > 0)
            {
                _spell.LowerCooldown(_spell.SpellData.Cooldown[_spell.CastInfo.SpellLevel] * 0.5f);
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            owner.RemoveBuffsWithName("FioraFlurry");
            owner.RemoveBuffsWithName("FioraFlurryDummy");
        }
    }

    public class FioraFlurryDummy : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata();
    }
}
