using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeaguePackets.Game.Common.CastInfo;

namespace LeagueSandbox.GameServer.Augments
{
    public class PredatorsMarkAugment : Augment
    {
        public PredatorsMarkAugment() : base(
            "Augment_PredatorsMark",
            "Predator's Mark",
            "Damaging a Hider reveals them through Stealth and Fog of War for 4 seconds.",
            "")
        {
        }

        public override void Apply(Champion target)
        {
            ApiEventManager.OnDealDamage.AddListener(this, target, OnDealDamage, false);
        }

        private void OnDealDamage(DamageData data)
        {
            if (data.Target != null && !data.Target.IsDead)
            {
                ApiFunctionManager.AddUnitPerceptionBubble(data.Target, 1f, 4.0f, data.Attacker.Team, revealStealthed:true, revealSpecificUnitOnly:data.Target, ignoresLoS: true, onlyShowTarget: true);
            }
        }
    }
}