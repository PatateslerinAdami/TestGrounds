using System;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class MordekaiserCreepingDeathCast : ISpellScript
    {
        private ObjAIBase _mordekaiser;

        public SpellScriptMetadata ScriptMetadata { get; } = new()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            },
            TriggersSpellCasts = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _mordekaiser = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.Stats.CurrentHealth =
                Math.Max(1, owner.Stats.CurrentHealth - (26f + 6 * (spell.CastInfo.SpellLevel - 1)));

            AddParticleTarget(owner, owner, "mordekaiser_creepingDeath_cas", owner);
            if (owner == target)
                AddBuff("MordekaiserCreepingDeath", 6f, 1, spell, target, spell.CastInfo.Owner);
            else
                SpellCast(owner, 1, SpellSlotType.ExtraSlots, true, target, owner.Position);
        }
    }

    public class MordekaiserCreepingDeath : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            },
            TriggersSpellCasts = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            AddBuff("MordekaiserCreepingDeath", 6f, 1, spell, target, spell.CastInfo.Owner);
        }
    }
}