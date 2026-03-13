using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SionQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
            ChannelDuration = 2f,
        };

        ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.StopMovement(networked: false);
            InstantStopTest(owner, true, false);
        }
        public void OnSpellChannel(Spell spell)
        {
            _owner.SetStatus(StatusFlags.CanMove, false);
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            _owner.SetStatus(StatusFlags.CanMove, true);

            LetGo();

            if (reason == ChannelingStopSource.PlayerCommand)
            {

            }
        }

        public void OnSpellPostChannel(Spell spell)
        {
            _owner.SetStatus(StatusFlags.CanMove, true);

            LetGo();
        }

        private void LetGo()
        {

        }
    }
}