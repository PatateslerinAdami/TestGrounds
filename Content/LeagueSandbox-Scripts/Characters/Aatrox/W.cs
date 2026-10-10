using GameServerCore.Enums;
using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class AatroxW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false,
            CooldownIsAffectedByCDR = false
        };

        public float? GetCooldown(Spell spell, byte rank)
        {
            return 0.5f;
        }

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, _ => owner.CharScript.OnUpdate(0));
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        public virtual void OnSpellPostCast(Spell spell)
        {
            Toggle(spell, true);
        }

        protected static void Toggle(Spell spell, bool power)
        {
            var owner = spell.CastInfo.Owner;
            RemoveBuff(owner, power ? "AatroxWLife" : "AatroxWPower");
            AddBuff(power ? "AatroxWPower" : "AatroxWLife", 25000, 1, spell, owner, owner, true);
            SetSpell(owner, power ? "AatroxW2" : "AatroxW", SpellSlotType.SpellSlots, 1);
            owner.Spells[1].SetSpellToggle(power);
            owner.Spells[1].SetCooldown(0.5f, true);
            if (owner.GetBuffWithName("AatroxWONHBuff") is { Remove: false, BuffScript: Buffs.AatroxWONHBuff w })
            {
                w.RefreshIndicator();
            }
        }
    }

    public class AatroxW2 : AatroxW
    {
        public override void OnSpellPostCast(Spell spell)
        {
            Toggle(spell, false);
        }
    }

    public class AatroxWHeal : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters()
            {
                Type = MissileType.Target,
                IsVisualOnly = true
            }
        };
    }
}