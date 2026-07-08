using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class SionW : IBuffGameScript
    {
        private ObjAIBase _sion;
        private PeriodicTicker _periodicTicker;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _sion = buff.SourceUnit;
            // changeFlags: Riot wire sends bitfield 0x6E when arming the detonate override
            // (and 0x0E when restoring, see OnDeactivate) — never 0.
            SetSpell(_sion, "SionWDetonate", SpellSlotType.SpellSlots, 1, changeFlags: 0x6E);
            _sion.Spells[1].SetCooldown(0f, true);
            SealSpellSlot(_sion, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            // Soul Furnace can't be recast for the first 2s (wiki; replay min recast gap = 2.18s,
            // clean cluster from ~2.0s). Was 3000 here, which silently ate every 2-3s recast.
            var ticks = _periodicTicker.ConsumeTicks(diff, 2000f, false, 1, 1);
            if (ticks == 1)
            {
                SealSpellSlot(_sion, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SealSpellSlot(_sion, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
            SetSpell(_sion, "SionW", SpellSlotType.SpellSlots, 1);
            _sion.Spells[1].SetCooldown(_sion.Spells[1].CastInfo.Cooldown, false);
        }
    }
}