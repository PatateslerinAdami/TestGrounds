using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class Alpha_Striking: IBuffGameScript
    {

        private ObjAIBase _masterYi;
        private Spell _spell;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _masterYi = ownerSpell.CastInfo.Owner;
            SealSpellSlot(_masterYi, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
            unit.StopMovement();
            unit.SetStatus(StatusFlags.CanMove, false);
            unit.SetStatus(StatusFlags.Targetable, false);
            unit.SetStatus(StatusFlags.NoRender, true);
            unit.SetStatus(StatusFlags.CanAttack, false);
            unit.SetStatus(StatusFlags.Ghosted, true);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SealSpellSlot(_masterYi, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
            unit.SetStatus(StatusFlags.CanMove, true);
            unit.SetStatus(StatusFlags.Targetable, true);
            unit.SetStatus(StatusFlags.NoRender, false);
            unit.SetStatus(StatusFlags.CanAttack, true);
            unit.SetStatus(StatusFlags.Ghosted, false);
        }
    }
}
