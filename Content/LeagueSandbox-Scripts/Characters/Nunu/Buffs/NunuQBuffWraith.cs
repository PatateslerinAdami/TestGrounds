using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
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
    internal class NunuQBuffWraith : IBuffGameScript
    {

        private ObjAIBase _nunu;
        private Spell _spell;
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
            _nunu = ownerSpell.CastInfo.Owner;
            _spell = ownerSpell;
            ApiEventManager.OnKillUnit.AddListener(this, _nunu, OnUnitKill);
        }

        private void OnUnitKill(DeathData data)
        {
            AddBuff("NunuQBuffWolf", 3f, 1, _spell, _nunu, _nunu);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            
        }
    }
}