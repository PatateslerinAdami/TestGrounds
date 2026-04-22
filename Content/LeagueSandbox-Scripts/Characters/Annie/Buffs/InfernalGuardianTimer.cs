using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

internal class InfernalGuardianTimer : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER
    };

    public StatsModifier StatsModifier { get; }
}