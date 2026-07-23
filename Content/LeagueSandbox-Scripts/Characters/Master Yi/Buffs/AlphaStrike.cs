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
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class AlphaStrike : IBuffGameScript
    {
        private ObjAIBase _masterYi;
        private Spell _spell;
        private Fade _fade;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; private set; }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _masterYi = ownerSpell.CastInfo.Owner;
            SpellEffectCreate("MasterYi_Base_Q_Cas.troy", _masterYi, null, _masterYi,
                boneName: "C_Buffbone_Glb_Center_Loc",
                flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven);
            _fade = PushCharacterFade(_masterYi, 0.0f, 0.25f);
            SealSpellSlot(_masterYi, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
            unit.StopMovement();
            HideHealthBar(unit, null, true);
            unit.SetStatus(StatusFlags.CanAttack, false);
            unit.SetStatus(StatusFlags.CanCast, false);
            unit.SetStatus(StatusFlags.CanMove, false);
            unit.SetStatus(StatusFlags.Ghosted, true);
            unit.SetStatus(StatusFlags.Targetable, false); //Todo targetetability refactor
            unit.SetStatus(StatusFlags.DodgePiercing, true);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            PopCharacterFade(_masterYi, _fade);
            SpellEffectCreate("MasterYi_Base_Q_End.troy", _masterYi, _masterYi, _masterYi, boneName: "Root",
                flags: FXFlags.SimulateWhileOffScreen);
            SealSpellSlot(_masterYi, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
            HideHealthBar(unit, null, false);
            unit.SetStatus(StatusFlags.CanAttack, true);
            unit.SetStatus(StatusFlags.CanCast, true);
            unit.SetStatus(StatusFlags.CanMove, true);
            unit.SetStatus(StatusFlags.Ghosted, false);
            unit.SetStatus(StatusFlags.Targetable, true);
            unit.SetStatus(StatusFlags.DodgePiercing, false);
        }
    }
}