using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class AbsoluteZeroSlow : IBuffGameScript
    {

        private ObjAIBase _nunu;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            // RENEW_EXISTING (non-stackable): a flat 50% slow that refreshes on re-apply. STACKS_AND_OVERLAPS
            // requires MaxStacks >= 2 (engine throws otherwise -> the buff never applied), and per-instance
            // additive -50% would over-slow anyway. For the S1 ramping slow, use MaxStacks>=2 + a small
            // per-stack value + periodic re-apply.
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1,
        };
        
        public StatsModifier StatsModifier { get; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _nunu = buff.SourceUnit;
            StatsModifier.MoveSpeed.PercentBonus -= ownerSpell.SpellData.EffectLevelAmount[2][ownerSpell.CastInfo.SpellLevel]/100;
            StatsModifier.AttackSpeed.PercentBonus -= ownerSpell.SpellData.EffectLevelAmount[3][ownerSpell.CastInfo.SpellLevel]/100;
            unit.AddStatModifier(StatsModifier);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}