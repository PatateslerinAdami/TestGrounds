using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ElixirOfSorcery : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            // TODO
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AddBuff("ElixirOfSorcery", 180f, 1, spell, owner, owner);
        }
    }
}


namespace Buffs
{
    public class ElixirOfSorcery : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = false
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private ObjAIBase _owner;
        private Spell _spell;


        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = ownerSpell.CastInfo.Owner;
            _spell = ownerSpell;

            StatsModifier.AbilityPower.FlatBonus += 40f;
            StatsModifier.ManaRegeneration.FlatBonus += 3f;
            ApiEventManager.OnHitUnit.AddListener(this, _owner, ChampionTarget, false);
            ApiEventManager.OnHitUnit.AddListener(this, _owner, TowerTarget, false);

            unit.AddStatModifier(StatsModifier);
        }

        private bool AttackedAChampion = false;
        float ChampionTick;


        public void ChampionTarget(DamageData damageData)
        {
            var Target = damageData.Target;
            var attacker = damageData.Attacker;
            if (damageData.Target is Champion)
            {
                if (ChampionTick < 5000f)
                {
                    return;
                }
                else
                {
                    Target.TakeDamage(attacker, 25f, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_SPELL, false);
                    AttackedAChampion = true;
                    ChampionTick = 0f;
                }
            }
        }


        public void TowerTarget(DamageData damageData)
        {
            var Target = damageData.Target;
            var attacker = damageData.Attacker;
            if (damageData.Target is ObjBuilding)
            {
                Target.TakeDamage(attacker, 25f, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_SPELL, false);
            }
        }



        public void OnUpdate(float diff)
        {
            ChampionTick += diff;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnHitUnit.RemoveListener(this);
            ApiEventManager.OnHitUnit.RemoveListener(this);
            unit.RemoveStatModifier(StatsModifier);
        }
    }
}