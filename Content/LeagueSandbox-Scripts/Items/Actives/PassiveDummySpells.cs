using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace ItemSpells
{
    public abstract class PassiveDummySpell : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata();

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnCanCast.AddListener(this, owner, (unit, cast) =>
            {
                return cast != spell;
            });
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class AtmasImpalerDummySpell : PassiveDummySpell
    {
    }

    public class ArchAngelsDummySpell : PassiveDummySpell
    {
    }

    public class ManamuneDummySpell : PassiveDummySpell
    {
    }

    public class RodOfAgesDummySpell : PassiveDummySpell
    {
    }

    public class Malady : PassiveDummySpell
    {
    }

    public class ZhonyasRing : PassiveDummySpell
    {
    }

    public class GuardianAngel : PassiveDummySpell
    {
    }
}
