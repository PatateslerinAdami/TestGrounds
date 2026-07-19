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
    internal class Visionary_Counter : IBuffGameScript
    {
        private ObjAIBase _nunu;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 5,
            IsNonDispellable = true
        };

        public StatsModifier StatsModifier { get; } = new();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = buff.SourceUnit;
            if (buff.StackCount != 5) return;
            AddBuff("Visionary", 25000f, 1, ownerSpell, _nunu, _nunu);
            _nunu.RemoveBuffsWithName("Visionary_Counter");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            
        }
    }
}