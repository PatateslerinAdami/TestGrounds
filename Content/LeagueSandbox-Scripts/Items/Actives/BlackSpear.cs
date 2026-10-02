using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class KalistaPSpellCast : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnCanCast.AddListener(this, owner, (unit, cast) =>
            {
                if (cast != spell)
                {
                    return true;
                }

                return !owner.HasBuff("KalistaOathsworn");
            });
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            bool isInvalidOwner = owner.Model != "Kalista" || owner.IsDead || owner.HasBuff("KalistaOathsworn");
            bool isInvalidTarget = target is not Champion || target == owner || target.IsDead
                || target.Team != owner.Team
                || target.HasBuff("KalistaOathsworn")
                || Vector2.DistanceSquared(owner.Position, target.Position) > 1200f * 1200f;
            if (isInvalidOwner || isInvalidTarget)
            {
                spell.CancelCast();
                return;
            }

            var selfVariables = new BuffVariables();
            selfVariables.Set("partner", target);
            var allyVariables = new BuffVariables();
            allyVariables.Set("partner", owner);
            AddBuff("KalistaOathsworn", 0, 1, spell, owner, owner, true, buffVariables: selfVariables);
            AddBuff("KalistaOathsworn", 0, 1, spell, target, owner, true, buffVariables: allyVariables);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
