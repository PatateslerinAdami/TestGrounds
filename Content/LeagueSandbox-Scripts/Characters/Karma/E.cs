using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class KarmaSolKimShield : ISpellScript
    {
        private AttackableUnit _target;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
        }

        public void OnSpellPostCast(Spell spell)
        {
            AddBuff("KarmaSolKimShield", 5f, 1, spell, _target, spell.CastInfo.Owner);
        }

    }

}
