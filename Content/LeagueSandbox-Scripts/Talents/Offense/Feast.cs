using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;
using GameServerCore.Enums;

namespace Talents
{
    internal class Talent_4124 : ITalentScript
    {
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnKillUnit.AddListener(this, owner, OnKillUnit);
        }

        private void OnKillUnit(DeathData deathData)
        {
            if (deathData.Killer == _owner)
            {
                _owner.TakeHeal(_owner, 3.0f, HealType.HealthRegeneration);
                _owner.IncreasePAR(_owner, 1.0f);
            }
        }
    }
}