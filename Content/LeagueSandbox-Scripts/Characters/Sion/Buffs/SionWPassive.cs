using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class SionWPassive : IBuffGameScript
    {
        private ObjAIBase _sion;
        private double _bonusHealth = 0;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _sion = buff.SourceUnit;
            ApiEventManager.OnKillUnit.AddListener(this, _sion, OnUnitKill);
            ApiEventManager.OnUpdateStats.AddListener(this, _sion, OnUpdateStats);
        }

        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            SetSpellToolTipVar(_sion, 0, _bonusHealth, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        }

        private void OnUnitKill(DeathData data)
        {
            // UnitTags is a [Flags] composite (a siege minion is Minion | Minion_Lane |
            // Minion_Lane_Siege), so equality patterns never match a live unit — query with
            // ContainsAny, specific tags first since those units also carry the Minion bit.
            var tags = data.Unit.UnitTags;
            if (tags.ContainsAny(UnitTag.Minion_Lane_Siege | UnitTag.Minion_Lane_Super | UnitTag.Champion
                                 | UnitTag.Champion_Clone | UnitTag.Monster_Large | UnitTag.Monster_Epic))
            {
                StatsModifier.HealthPoints.FlatBonus = 15f;
                _sion.AddStatModifier(StatsModifier);
                _bonusHealth += 15f;
            }
            else if (tags.ContainsAny(UnitTag.Minion | UnitTag.Minion_Lane | UnitTag.Ward | UnitTag.Minion_Summon))
            {
                StatsModifier.HealthPoints.FlatBonus = 4f;
                _sion.AddStatModifier                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            (StatsModifier);
                _bonusHealth += 4f;
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}