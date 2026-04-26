using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class UdyrPhoenixStance : IBuffGameScript {
    private ObjAIBase _udyr;
    private Spell     _spell;
    private Buff      _buff;
    private Particle  _particle1,  _particle2;
    private int       _attackCount = 0;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        _udyr.ChangeModel("UdyrPhoenix");
        _udyr.SetAutoAttackSpell("UdyrPhoenixAttack", false);
        ApiEventManager.OnHitUnit.AddListener(this, _udyr, OnHit);
        _particle1 = AddParticleTarget(_udyr,_udyr,"phoenixpelt",_udyr, bone: "head", lifetime: buff.Duration);
    }

    private void OnHit(DamageData data) {
        if (_attackCount == 2) {
         _particle2 = AddParticleTarget(_udyr,_udyr,"Udyr_PhoenixBreath_cas",data.Target);
         Vector2 targetPos = GetPointFromUnit(_udyr, 400f, 0);
         SpellCast(_udyr, 0, SpellSlotType.ExtraSlots, targetPos, targetPos, true, Vector2.Zero);
         _attackCount = 0;
        } else {
            _attackCount++;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr.ResetAutoAttackSpell();
        RemoveParticle(_particle1);
    }
}