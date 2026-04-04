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

internal class JudicatorHolyFervorDebuff : IBuffGameScript {
    private ObjAIBase _kayle;
    private Spell     _spell;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SHRED,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = 5
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _kayle                        =  ownerSpell.CastInfo.Owner;
        _spell                        =  ownerSpell;
        StatsModifier.Armor.BaseBonus -= unit.Stats.Armor.Total * 0.03f;
        StatsModifier.MagicResist.BaseBonus -= unit.Stats.MagicResist.Total * 0.03f;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        
    }
}