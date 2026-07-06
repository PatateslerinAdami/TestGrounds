using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class VelkozE : ISpellScript
    {
        private ObjAIBase _velkoz;
        private Vector2 _endPos;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            NotSingleTargetSpell = false,
            DoesntBreakShields = true,
            TriggersSpellCasts = true,
            SpellToggleSlot = 3,
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _velkoz = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _endPos = end;
        }

        public void OnSpellPostCast(Spell spell)
        {
            
            var hiddenMinion = AddMinion(_velkoz, "TestCubeRender10Vision", "hiddenMinion", _endPos, _velkoz.Team, 0, true, true, isVisible: false);
            HideHealthBar(hiddenMinion);
            SpellCast(_velkoz, 2, SpellSlotType.ExtraSlots, true, hiddenMinion, Vector2.Zero);
            AddParticlePos(_velkoz, "Velkoz_Base_E_AOE_green.troy", _endPos, _endPos, lifetime: 0.8f, enemyParticle: "Velkoz_Base_E_AOE_red.troy");
        }
    }

    public class VelkozEMissile : ISpellScript
    {
        private ObjAIBase _velkoz;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            },
            IsDamagingSpell = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _velkoz = owner;
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        }

        private void OnSpellHit(Spell spell, AttackableUnit target,SpellMissile missile)
        {
            AddParticle(_velkoz, default, "velkoz_base_e_explo.troy", missile.Position);
            var unitsInRange = GetUnitsInRange(_velkoz, missile.Position, 120f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions);

            foreach (var unit in unitsInRange)
            {
                AddBuff("VelkozEStun", 0.75f, 1, spell, unit, _velkoz);
            }
            target.Die(CreateDeathData(false, 0, target, target, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0));
        }
        
    }
}