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
        // Fixed-period ticker (struct, default-initialized) used to expire the on-kill speed after 3s.
        private PeriodicTicker _periodicTicker;
        // Whether the on-kill speed modifier is currently applied. Guards every RemoveStatModifier:
        // Stat.RemoveStatModifier does PercentBonus -= value with NO membership check, so removing
        // while not applied permanently drains MoveSpeed. Add/Remove must stay strictly balanced.
        private bool _applied;
        // Dedicated modifier for the on-kill speed — NOT the StatsModifier property, which the buff
        // framework auto-removes on deactivate (Buff.DeactivateBuff); since our value persists at 15%
        // that auto-remove would double-subtract. Keeping the property empty makes it a no-op.
        private readonly StatsModifier _moveSpeedModifier = new();
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            // Marker only — the movement speed is a separate, dynamically applied stat modifier
            // granted on kill (Consume "Animals or Undead": killing a unit grants MS for 3 seconds).
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        // Required by IBuffGameScript. Kept empty: the on-kill speed lives on _moveSpeedModifier so the
        // framework's auto-remove of this property on deactivate is a harmless no-op (PercentBonus 0).
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = ownerSpell.CastInfo.Owner;
            _spell = ownerSpell;
            // OnKillUnit fires for ANY unit this champion kills (minions/monsters/champions).
            // OnKill only fires when a CHAMPION victim dies — wrong event for "killing a unit".
            ApiEventManager.OnKillUnit.AddListener(this, _nunu, OnKill);
        }

        private void OnKill(DeathData data)
        {
            // Refresh: remove ONLY if currently applied (else we'd subtract an un-applied modifier
            // and permanently drain MoveSpeed), then re-apply the bonus and restart the 3s window.
            if (_applied) _nunu.RemoveStatModifier(_moveSpeedModifier);
            _moveSpeedModifier.MoveSpeed.PercentBonus = _spell.SpellData.EffectLevelAmount[6][_spell.CastInfo.SpellLevel];
            _nunu.AddStatModifier(_moveSpeedModifier);
            _applied = true;
            _periodicTicker.Reset();
        }

        public void OnUpdate(Buff buff, float diff)
        {
            if (!_applied) return;
            if (_periodicTicker.ConsumeTicks(diff, 3000f, false, 1, 1) != 1) return;
            _nunu.RemoveStatModifier(_moveSpeedModifier);
            _applied = false;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_applied) _nunu.RemoveStatModifier(_moveSpeedModifier);
            _applied = false;
            ApiEventManager.OnKillUnit.RemoveListener(this, _nunu, OnKill);
        }
    }
}