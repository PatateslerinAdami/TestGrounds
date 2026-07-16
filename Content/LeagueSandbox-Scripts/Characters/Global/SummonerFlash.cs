using GameServerCore.Enums;
using GameServerCore.Packets.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SummonerFlash : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
            CastingBreaksStealth = false,
            NotSingleTargetSpell = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {

            var current = owner.Position;
            var dist = Vector2.Distance(current, start);
            var cursor = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);

            FaceDirection(start, owner, true);

            if (dist > spell.SpellData.CastRangeDisplayOverride && !AreEmpoweredSumsEnabled())
            {
                start = GetPointFromUnit(owner, spell.SpellData.CastRangeDisplayOverride);
            }
            else if (AreEmpoweredSumsEnabled())
            {
                start = cursor;
            }

            StopChanneling(owner, ChannelingStopCondition.Cancel, ChannelingStopSource.Move);

            AddParticle(owner, null, "global_ss_flash", owner.Position);
            AddParticleTarget(owner, owner, "global_ss_flash_02", owner);

            TeleportToPosition(owner, start.X, start.Y);
        }
    }
}

