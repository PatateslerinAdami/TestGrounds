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

public class UdyrPassiveBuff : IBuffGameScript {
    private ObjAIBase _udyr;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    private const short StanceSpellCount   = 4;
    private const float StanceSwapCooldown = 1.5f;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
         _udyr = ownerSpell.CastInfo.Owner;

        for (short i = 0; i < StanceSpellCount; i++) {
            var stanceSpell = _udyr.Spells[i];
            if (stanceSpell == null || stanceSpell == ownerSpell) continue;

            RemoveBuff(unit, stanceSpell.SpellName);
            stanceSpell.SetCooldown(StanceSwapCooldown, true);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        
    }
}