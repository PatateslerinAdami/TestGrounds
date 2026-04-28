using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class RivenMartyr : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner, Spell spell) 
        {
            _owner = owner;
        }
        public void OnSpellPostCast(Spell spell)
        {
            AddBuff("RivenPassiveAABoost", 5f, 1, spell, _owner, _owner, false);
            AddParticleTarget(_owner, _owner, "Riven_Base_W_Cast.troy", _owner);
            AddParticleTarget(_owner, _owner, "exile_W_weapon_cas.troy", _owner, bone: "weapon");

            var units = GetUnitsInRange(_owner, _owner.Position, 250, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);

            float baseDmg = 25f + (30f * spell.CastInfo.SpellLevel);
            float bonusAD = _owner.Stats.AttackDamage.FlatBonus * 1.0f;
            float damage = baseDmg + bonusAD;

            foreach (var unit in units)
            {
                    unit.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, spell);
                    AddBuff("RivenMartyr", 0.75f, 1, spell, unit, _owner);
                    AddParticleTarget(_owner, unit, "exile_W_tar_02.troy", unit, 1f);
            }
        }
    }
}