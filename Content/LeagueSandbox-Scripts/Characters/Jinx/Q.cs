using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class JinxQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };
        Champion _owner;
        Spell _spell;
        bool switchToRocket = true;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner as Champion;
            _spell = spell;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell, true);
        }
        private void OnLevelUpSpell(Spell spell)
        {
            AddBuff("JinxQIcon", 1f, 1, spell, _owner, _owner, true);
        }
        public void OnSpellCast(Spell spell)
        {
            if (switchToRocket)
            {
                _owner.RemoveBuffsWithName("JinxQIcon");
                NotifyChangeSlotSpellData(_owner.ClientId, _owner, 0, ChangeSlotSpellDataType.IconIndex, newIconIndex: 1);
            }
            else
            {
                _owner.RemoveBuffsWithName("JinxQ");
                NotifyChangeSlotSpellData(_owner.ClientId, _owner, 0, ChangeSlotSpellDataType.IconIndex, newIconIndex: 0);
            }
            switchToRocket = !switchToRocket;
        }
    }
}