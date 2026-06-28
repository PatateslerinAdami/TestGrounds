using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SionE : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
        }
        public void OnSpellPostCast(Spell spell)
        {
            CreateCustomMissile(_owner, "SionEMissile", _owner.Position, GetPointFromUnit(_owner, 800f), new MissileParameters { Type = MissileType.Arc });
        }
    }

    public class SionEMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile)
        {
            ObjAIBase caster = spell.CastInfo.Owner;

            if (target is Minion || target is Monster)
            {
                float distFromCaster = Vector2.Distance(caster.Position, target.Position);
                float pushDist = 1350 - distFromCaster;
                if (pushDist < 0) pushDist = 0;

                AddBuff("SionEKnockback", 0.75f, 1, spell, target, caster);
                // BBMoveAway: push the target away from Sion by pushDist; FIRST_WALL_HIT clamps at terrain
                // (replaces the manual walkable-cell stepping).
                ForceMoveAway(target, caster, pushDist, 2500f,
                    resolve: ForceMovementType.FIRST_WALL_HIT);
            }
            else
            {
                AddParticleTarget(caster, target, "sion_base_e_buf_champ.troy", target, lifetime: 4f);

                // damages buffs etc
            }

            missile.SetToRemove();
        }
    }
}