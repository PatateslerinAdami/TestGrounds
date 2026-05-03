using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    internal class OdinCaptureSoundFilling : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
    internal class OdinCaptureSoundEmptying : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
    internal class OdinCaptureChannelCooldownBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
    internal class OdinCaptureChannel : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
    }
}