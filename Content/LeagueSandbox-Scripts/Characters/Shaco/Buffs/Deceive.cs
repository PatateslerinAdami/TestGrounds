using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;


namespace Buffs
{
    internal class Deceive : IBuffGameScript
    {
        private ObjAIBase _shaco;
        private Fade _fade;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INVISIBILITY,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };
        
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _shaco = buff.SourceUnit;
            // Stealth self-tint ON — replay-verified from Shaco's OWN perspective (3f6b5739:
            // 56 Q casts, 225 tints; enable RGB(0,0,50)@0.2 exactly at cast, clear at stealth
            // end). The tint is unicast to the stealther, so only own-perspective replays can
            // show it.
            _shaco.SetStatus(StatusFlags.Stealthed, true);
            _fade = PushCharacterFade(_shaco, 0.2f, 0.5f);
            FadeInColorFadeEffect(_shaco, 0, 0, 50, 0.25f, 0.2f);
            SealSpellSlot(_shaco, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, true);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _shaco.SetStatus(StatusFlags.Stealthed, false);
            PopCharacterFade(_shaco, _fade);
            FadeOutColorFadeEffect(_shaco, 0.25f);
            SealSpellSlot(_shaco, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, false);
        }
    }
}
