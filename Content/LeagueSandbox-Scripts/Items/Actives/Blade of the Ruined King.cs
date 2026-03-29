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
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemSwordOfFeastAndFamine : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            // TODO
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            SpellCastItem(owner, "ItemSwordOfFeastAndFamineTransfuse", true, target, Vector2.Zero);
        }
    }
}

namespace Spells
{
    public class ItemSwordOfFeastAndFamineTransfuse : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            }
            // TODO
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, true);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            var DealTargetHealthPercentDamage = target.Stats.HealthPoints.Total * 0.10f;
            AddBuff("BladeOfTheRuinedKing", 3.0f, 1, spell, target, owner);
            AddBuff("BladeOfTheRuinedKingSelf", 3.0f, 1, spell, owner, owner);
            target.TakeDamage(owner, DealTargetHealthPercentDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_DEFAULT, false);
            owner.TakeHeal(owner, DealTargetHealthPercentDamage, HealType.SelfHeal);
            missile.SetToRemove();
        }
    }
}


namespace Buffs
{
    internal class BladeOfTheRuinedKing : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        Particle _transfuseParticle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var StolenMovementSpeed = 0.25f;
            StatsModifier.MoveSpeed.PercentBonus -= StolenMovementSpeed;
            unit.AddStatModifier(StatsModifier);
            _transfuseParticle = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "VladTransfusionHeal_mis_bloodless", unit, buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_transfuseParticle);
        }
    }
}

namespace Buffs
{
    internal class BladeOfTheRuinedKingSelf : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();


        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var StolenMovementSpeed = 0.25f;
            StatsModifier.MoveSpeed.PercentBonus += StolenMovementSpeed;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {

        }
    }
}