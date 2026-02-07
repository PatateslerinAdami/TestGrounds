using Buffs;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic; 
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells
{
    public class JackInTheBox : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        private List<GameScriptTimer> _activationTimers = new List<GameScriptTimer>();
        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var castrange = spell.GetCurrentCastRange();
            var ownerPos = owner.Position;
            var spellPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);

            if (Extensions.IsVectorWithinRange(ownerPos, spellPos, castrange))
            {
                Minion newBox = AddMinion((Champion)owner, "ShacoBox", "ShacoBox", spellPos, owner.Team);
                newBox.FaceDirection(owner.Direction);
                AddParticle(owner, null, "JackintheboxPoof", spellPos, lifetime: 5f, ignoreCasterVisibility: true);

                var boxTimer = new GameScriptTimer(2.0f, () =>
                {
                    AddBuff("BoxTime", 62.0f, 1, spell, newBox, owner);
                    newBox.SetStatus(StatusFlags.Invulnerable, true);
                    newBox.SetStatus(StatusFlags.Ghosted, true);
                    // Dont know what TEAM_ALL and TEAM_UNKNOWN and TEAM_MAX does so to be safe.
                    TeamId[] validTeams = { TeamId.TEAM_BLUE, TeamId.TEAM_PURPLE, TeamId.TEAM_NEUTRAL };

                    foreach (var team in validTeams)
                    {
                        if (team != newBox.Team)
                        {
                            newBox.SetIsTargetableToTeam(team, false);
                        }
                    }
                    newBox.EnterStealth();
                });
                _activationTimers.Add(boxTimer);
            }
        }
        public void OnUpdate(float diff)
        {
            for (int i = _activationTimers.Count - 1; i >= 0; i--)
            {
                var timer = _activationTimers[i];
                timer.Update(diff);

                if (timer.IsDead())
                {
                    _activationTimers.RemoveAt(i);
                }
            }
        }
    }
}
