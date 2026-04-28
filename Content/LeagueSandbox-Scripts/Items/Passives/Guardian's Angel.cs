using Buffs;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3026 : IItemScript
    {
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private ObjAIBase owner;
        private Particle p;
        private bool onCooldown = false;

        public void OnActivate(ObjAIBase owner)
        {
            this.owner = owner;
            owner.AddStatModifier(StatsModifier);
            ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage, false);
            AddBuff("WillRevive", float.MaxValue, 1, null, owner, owner);
        }

        public void OnTakeDamage(DamageData data)
        {
            if (onCooldown) return;

            if (owner.Stats.CurrentHealth <= data.PostMitigationDamage)
            {
                onCooldown = true;
                data.PostMitigationDamage = 0f;
                AddBuff("GuardianAngel", 4f, 1, null, owner, owner);
                RemoveParticle(p);
            }
        }
       
        public void OnUpdate(float diff)
        {
            onCooldown = owner.HasBuff("HasBeenRevived");

            if (!onCooldown && !owner.HasBuff("WillRevive"))
            {
                AddBuff("WillRevive", float.MaxValue, 1, null, owner, owner);
            }
            else if (onCooldown && owner.HasBuff("WillRevive"))
            {
                RemoveBuff(owner, "WillRevive");
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(p);
        }
    }
}