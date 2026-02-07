
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class KarmaSpiritBind : ISpellScript
    {
        AttackableUnit _target;
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _target = target;
        }
        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;

            AddParticleTarget(owner, _target, "karma_base_w_beam.troy", owner, 2f, bone: "root", targetBone: "l_hand");
            AddUnitPerceptionBubble(_target, 1f, 2.0f, owner.Team, false, _target, ignoresLoS: true, onlyShowTarget: true);
        }
    }
}

