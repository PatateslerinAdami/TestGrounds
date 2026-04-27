using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
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

public class Pantheon_Aegis_Counter : IBuffGameScript
{
    private Buff _buff;
    private ObjAIBase _pantheon;
    private Spell _spell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
        MaxStacks = 4
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _pantheon = ownerSpell.CastInfo.Owner;
        _buff = buff;
        _spell = ownerSpell;
        LogInfo("buff count: " + buff.StackCount);
        if (_buff.StackCount != 4) return;
        AddBuff("Pantheon_AegisShield", 15000f, 1, _spell, _pantheon, _pantheon, true);
        if (!_pantheon.HasBuff("Pantheon_AegisShieldVisual"))
        {
            AddBuff("Pantheon_AegisShieldVisual", 100000f, 1, ownerSpell, _pantheon, _pantheon, true);
        }

        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
    }
}