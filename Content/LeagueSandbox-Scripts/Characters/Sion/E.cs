using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
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
            CreateCustomMissile(_owner, "SionEMissile", _owner.Position, GetPointFromUnit(_owner, 800f), new MissileParameters { Type = MissileType.Circle });
        }
    }

    public class SionEMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            ObjAIBase caster = spell.CastInfo.Owner;

            if (target is Minion || target is Monster)
            {
                Vector2 direction = Vector2.Normalize(target.Position - caster.Position);

                float distFromCaster = Vector2.Distance(caster.Position, target.Position);
                float pushDist = 1350 - distFromCaster;
                if (pushDist < 0) pushDist = 0;

                Vector2 endPos = target.Position;
                float step = 20f; 

                for (float d = 0; d <= pushDist; d += step)
                {
                    Vector2 testPos = target.Position + direction * d;

                    if (!IsWalkable(testPos.X, testPos.Y, target.PathfindingRadius))
                    {
                        endPos = testPos - direction * target.PathfindingRadius;
                        break;
                    }
                    endPos = testPos;
                }

                AddBuff("SionEKnockback", 0.75f, 1, spell, target, caster);
                ForceMovement(target, "Run", endPos, 2500f, 0, 0, 0, true, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION);
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