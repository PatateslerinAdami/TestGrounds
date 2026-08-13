using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;

namespace LeagueSandbox.GameServer.Augments
{
    public class BrushStalkerAugment : Augment
    {
        public BrushStalkerAugment() : base(
            "Augment_BrushStalker",
            "Brush Stalker",
            "Entering a brush grants Stealth Leaving the brush reveals you.",// Eh we should send a reference to fontconfig like in rengo vs kha quest for like multi lang and stuff but since this is for PoC its fine for me
            ""
            )
        {
        }

        public override void Apply(Champion target)
        {
            ApiEventManager.OnEnterGrass.AddListener(this, target, OnEnterGrass, false);
            ApiEventManager.OnLeaveGrass.AddListener(this, target, OnLeaveGrass, false);
        }

        private void OnEnterGrass(AttackableUnit unit)
        {
            //eh should do buff for spells causing stealth to break but for PoC etc.
            unit.EnterStealth();
        }

        private void OnLeaveGrass(AttackableUnit unit)
        {
            unit.ExitStealth();
        }
    }
}