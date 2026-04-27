using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class SwainMetaHealTracker : IBuffGameScript
{
    private ObjAIBase _swain;
    private Buff _buff;
    private float _timeout = 2000f;
    private Spell _metamorphismSpell;
    private Queue<float> _pendingHeals = new();
    private bool _disableRequest = false;
    private int _ravensSent = 0;
    private int _ravensReturned = 0;
    private PeriodicTicker _periodicTicker;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _swain = ownerSpell.CastInfo.Owner;
        _buff = buff;
        _metamorphismSpell = ownerSpell;
        ApiEventManager.OnSpellHit.AddListener(this, _swain.GetSpell("SwainMetaNuke"), OnSpellHitNuke);
        ApiEventManager.OnSpellHit.AddListener(this, _swain.GetSpell("SwainMetaHeal"), OnSpellHitHeal);
        ApiEventManager.OnSpellHit.AddListener(this, _swain.GetSpell("SwainMetaHealTorment"), OnSpellHitHeal);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _pendingHeals.Clear();
        LoggerProvider.GetLogger().Info("Removed Buff");
    }

    public void OnUpdate(float diff)
    {
        if (!_disableRequest) return;
        LogDebug("RemovingBuff");
        var ticks = _periodicTicker.ConsumeTicks(diff, _timeout, false, 1, 1);
        if (_ravensReturned < _ravensSent && ticks != 1) return;
        LogDebug("uwu starting to remove Buff: ravens sent: " + _ravensSent + " ravens returned:  " + _ravensReturned);
        _disableRequest = false;
        _buff.DeactivateBuff();
    }

    private void OnSpellHitNuke(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var dmg = 50f + 30f * (_swain.GetSpell("SwainMetamorphism").CastInfo.SpellLevel - 1f) +
                  _swain.Stats.AbilityPower.Total * 0.2f;
        target.TakeDamage(
            _swain, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
            DamageResultType.RESULT_NORMAL);
        var healAmmount = target.Stats.GetPostMitigationDamage(dmg, DamageType.DAMAGE_TYPE_MAGICAL, _swain);
        if (target is Champion)
        {
            healAmmount *= 0.75f;
        }
        else
        {
            healAmmount *= 0.25f;
        }

        _pendingHeals.Enqueue(healAmmount);
        _ravensSent++;
    }

    private void OnSpellHitHeal(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        if (_pendingHeals.Count == 0) return;
        _swain.TakeHeal(_swain, _pendingHeals.Dequeue(), HealType.SelfHeal);
        _ravensReturned++;
    }

    public void RequestDisable()
    {
        _timeout = _metamorphismSpell.CastInfo.Cooldown - 200f;
        _disableRequest = true;
    }
}