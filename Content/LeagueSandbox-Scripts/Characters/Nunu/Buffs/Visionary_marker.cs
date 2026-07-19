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
    internal class Visionary_marker : IBuffGameScript
    {

        private ObjAIBase _nunu;
        private Spell _spell;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true,
            IsNonDispellable = true
        };
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = buff.SourceUnit;
            _spell = ownerSpell;
            ApiEventManager.OnHitUnit.AddListener(this, _nunu, OnHit);
        }
        
        private void OnHit(DamageData data)
        {
            if (_nunu.HasBuff("Visionary"))return;
            AddBuff("Visionary_Counter", 25000f, 1, _spell, _nunu, _nunu, true);
        }
    }
}