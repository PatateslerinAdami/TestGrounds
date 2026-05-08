using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;


namespace ItemPassives
{
    public class ItemID_1501 : IItemScript
    {
        private bool _disabled = false;
        private const float DamageReduction = 30f; //Blocks 30 damage as per the fontconfig file
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage, false);
        }

        public void OnUpdate(float diff)
        {
            if (_disabled) return;
            if (!(GameTime() / (1000f * 60f) < 7)) return; // Expires after 7 minutes as per the fontconfig file. Note: I'm applying it to champions only because if minions get this reduction, they start literally healing the towers with their basic attacks lol.
            ApiEventManager.RemoveAllListenersForOwner(this);
            _disabled = true;
        }

        private void OnPreTakeDamage(DamageData data)
        {
            var attacker = data.Attacker;
            if (data.DamageSource is DamageSource.DAMAGE_SOURCE_ATTACK or DamageSource.DAMAGE_SOURCE_PROC && data.DamageType is not DamageType.DAMAGE_TYPE_TRUE && attacker is Champion)
            {
                data.PostMitigationDamage -= DamageReduction;
            }
        }
    }
}
