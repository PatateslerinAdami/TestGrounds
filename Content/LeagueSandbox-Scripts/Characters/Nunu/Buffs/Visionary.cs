using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class Visionary : IBuffGameScript
    {

        private ObjAIBase _nunu;
        private Buff _buff;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = buff.SourceUnit;
            _buff = buff;
            for (short i = 0; i < 4; i++)
            {
                ApiEventManager.OnSpellCast.AddListener(this, _nunu.Spells[i], OnSpellsCast);
                // Additive increment = -(current cost) -> net 0 mana. Riot's BBSetPARCostInc path:
                // drives the server deduction (Spell.GetManaCost) AND syncs the client tooltip /
                // cast-cost floating text. Restored to 0 (base cost) in OnDeactivate.
                var spell = _nunu.Spells[i];
                SetSpellPARCost(_nunu, i, SpellPARCostType.Additive,
                    -spell.SpellData.ManaCost[spell.CastInfo.SpellLevel]);
            }
        }

        private void OnSpellsCast(Spell spell)
        {
            RemoveBuff(_buff);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            for (short i = 0; i < 4; i++)
            {
                ApiEventManager.OnSpellCast.RemoveListener(this, _nunu.Spells[i], OnSpellsCast);
                SetSpellPARCost(_nunu, i, SpellPARCostType.Additive, 0f);
            }
        }
    }
}