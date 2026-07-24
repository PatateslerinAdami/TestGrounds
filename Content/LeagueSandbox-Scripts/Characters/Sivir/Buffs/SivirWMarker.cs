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

namespace Buffs;

internal class SivirWMarker : IBuffGameScript
{
    private ObjAIBase _sivir;
    private Buff _buff;
    private Particle _p1;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks = 3
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _sivir = buff.SourceUnit;
        _buff = buff;
        OverrideAutoAttack(_sivir, "SivirWAttack", true);
        ApiEventManager.OnLaunchAttack.AddListener(this, _sivir, OnLaunchAttack);
    }

    private void OnLaunchAttack(Spell spell)
    {
        if (!spell.CastInfo.IsAutoAttack) return;
        if (GetBuffStackCount(_sivir, "SivirWMarker", _sivir) == 1)
        {
            RemoveBuff(_sivir, "SivirWMarker", _sivir);
        }
        else
        {
            EditBuff(_buff, GetBuffStackCount(_sivir, "SivirWMarker", _sivir) - 1);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_p1);
        RemoveOverrideAutoAttack(_sivir);
    }
}