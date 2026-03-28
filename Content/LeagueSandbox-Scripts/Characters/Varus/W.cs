using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VarusW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,

        };
        ObjAIBase _owner;
        Spell _spell;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell, true); 
        }
        private void OnLevelUpSpell(Spell spell)
        {
            ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHitUnit, false);
            spell.SetSpellToggle(true);
            _owner.RegisterTimer(new GameScriptTimer(0.01f, () =>
            {
                SealSpellSlot(_owner, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
            }));
        }
        public void OnHitUnit(DamageData damageData)
        {
            var target = damageData.Target;
            AddBuff("VarusWDebuff", 5f, 1, _spell , target, _owner);
        }
    }
}
