using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace LeagueSandbox.GameServer.Augments
{
    public class PanicButtonAugment : Augment
    {
        private bool _triggered = false;

        public PanicButtonAugment() : base(
            "Augment_PanicButton",
            "Panic Button",
            "Once per life falling below a certain HP threshold grants a 500 HP shield and movement speed for 3s.",
            "")
        {
        }

        public override void Apply(Champion target)
        {
            ApiEventManager.OnTakeDamage.AddListener(this, target, OnTakeDamage, false);

            ApiEventManager.OnDeath.AddListener(this, target, OnDeath, false);
        }

        private void OnTakeDamage(DamageData data)
        {
            if (_triggered) return;

            var unit = data.Target;

            if (unit.Stats.CurrentHealth / unit.Stats.HealthPoints.Total <= 0.3f)
            {
                _triggered = true;

                ApiFunctionManager.AddShield((ObjAIBase)unit, unit, 500f, true, true);

                var speedMod = new StatsModifier();
                speedMod.MoveSpeed.PercentBonus = 0.5f;
                unit.AddStatModifier(speedMod);

                unit.RegisterTimer(new GameScriptTimer(3.0f, () =>
                {
                    unit.RemoveStatModifier(speedMod);
                }));
            }
        }

        private void OnDeath(DeathData data)
        {
            _triggered = false;
        }
    }
}