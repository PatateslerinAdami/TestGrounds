using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerCore.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Microsoft.CodeAnalysis.Operations;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeaguePackets.Game;

namespace Buffs
{
    internal class SummonerBarrier : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SPELL_SHIELD,
            IsHidden = false,
            BuffAddType = BuffAddType.STACKS_AND_OVERLAPS
        };
        private ObjAIBase owner;
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            owner = ownerSpell.CastInfo.Owner;
            AddParticleTarget(owner, unit, "Global_SS_Barrier", unit, buff.Duration,bone: "C_BUFFBONE_GLB_CHEST_LOC");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}