using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class ZedWHandler : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };

        public Minion shadow;
        public StatsModifier StatsModifier { get; private set; }
        private ObjAIBase _owner;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = ownerSpell.CastInfo.Owner;

            if (buff.Variables != null)
            {
                shadow = buff.Variables.Get<Minion>("Shadow");
            }

            byte wLevel = _owner.Spells[1].CastInfo.SpellLevel;
            var w2 = _owner.GetSpell("ZedW2");
            w2?.SetLevel(wLevel);

            _owner.SwapSpells(1, 48);
            SealSpellSlot(_owner, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);

            if (shadow != null)
                shadow.SetStatus(StatusFlags.NoRender, false);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            byte currentLevel = _owner.Spells[1].CastInfo.SpellLevel;
            _owner.SwapSpells(1, 48);
            _owner.Spells[1].SetLevel(currentLevel);

            bool isShadowStillActive = _owner.HasBuff("ZedWHandler2");
            SealSpellSlot(_owner, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, isShadowStillActive);

            if (_owner.Spells[1].CurrentCooldown > 0)
                _owner.Spells[1].SetCooldown(_owner.Spells[1].CurrentCooldown, true);
        }
    }

    internal class ZedWHandler2 : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            MaxStacks = 1
        };

        public Minion shadow;
        public StatsModifier StatsModifier { get; private set; }
        private ObjAIBase _owner;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = ownerSpell.CastInfo.Owner;

            if (buff.Variables != null)
            {
                shadow = buff.Variables.Get<Minion>("Shadow");
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (shadow != null)
                shadow.SetStatus(StatusFlags.NoRender, true);

            SealSpellSlot(_owner, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
        }
    }
}