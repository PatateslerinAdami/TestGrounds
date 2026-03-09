using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class VelkozE : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            var ownerPos = owner.Position;
            var distance = Vector2.Distance(ownerPos, targetPos);
            var range = spell.GetCurrentCastRange();

            Vector2 finalPos;
            if (distance > range)
            {
                var direction = Vector2.Normalize(targetPos - ownerPos);
                finalPos = ownerPos + direction * range;
            }
            else
            {
                finalPos = targetPos;
            }

            CreateCustomMissile(owner, "VelkozEMissile", ownerPos, finalPos, new MissileParameters { Type = MissileType.Circle });

            AddParticlePos(owner, "Velkoz_Base_E_AOE_green.troy", finalPos, finalPos, lifetime: 1.0f, teamOnly: owner.Team);
            AddParticlePos(owner, "Velkoz_Base_E_AOE_red.troy", finalPos, finalPos, lifetime: 1.0f, teamOnly: CustomConvert.GetEnemyTeam(owner.Team), ignoreCasterVisibility: true);
        }
    }

    public class VelkozEMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle
            },
            IsDamagingSpell = true
        };

        public SpellMissile ActiveMissile;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }

        public void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ActiveMissile = missile;
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, false);
        }

        public void OnMissileEnd(SpellMissile missile)
        {
            var owner = missile.SpellOrigin.CastInfo.Owner;
            AddParticle(owner, default, "velkoz_base_e_explo.troy", missile.Position);
            owner.RegisterTimer(new GameScriptTimer(0.25f, () =>
            {
            }));
        }
    }
}