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
        ObjAIBase owner;
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            this.owner = owner;
            owner.AddStatModifier(StatsModifier);
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage, false);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            float minutes = GameTime() / (1000f * 60f);
            var attacker = data.Attacker;
            float reducedDamage = 30f; //Blocks 30 damage as per the fontconfig file

            if (data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK && minutes < 7f && attacker is Champion) // Expires after 7 minutes as per the fontconfig file. Note: I'm applying it to champions only because if minions get this reduction, they start literally healing the towers with their basic attacks lol.
            {
                data.PostMitigationDamage -= reducedDamage;
            }
        }
    }
}
