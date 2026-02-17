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
using System.Numerics;
using System.Collections.Generic;

using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs
{
    internal class PowerBall : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };
        public SpellSector QHit;
        private List<GameScriptTimer> _timers = new List<GameScriptTimer>();
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public StatsModifier StatsModifier2 { get; private set; } = new StatsModifier();
        ObjAIBase owner;
        Spell sp;
        float second = 0f; int count = 0;
        Buff buf;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            buf = buff;
            sp = ownerSpell;
            owner = ownerSpell.CastInfo.Owner;
            owner.ChangeModelTo("RammusPB");
            StatsModifier.MoveSpeed.PercentBonus = 0.03f;
            owner.SetStatus(StatusFlags.Ghosted, true);
            owner.AddStatModifier(StatsModifier);

            ApiEventManager.OnSpellHit.AddListener(this, ownerSpell, TargetExecute, true);
            QHit = ownerSpell.CreateSpellSector(new SectorParameters
            {
                BindObject = owner,
                Length = owner.CollisionRadius + 60f,
                Tickrate = 30,
                CanHitSameTargetConsecutively = true,
                OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes,
                Type = SectorType.Area
            });
        }
        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            float AP = owner.Stats.AbilityPower.Total * 0.01f;

            float damage = target.Stats.CurrentHealth * 0.015f * (spell.CastInfo.SpellLevel) + AP;

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, sp);
            AddParticleTarget(owner, owner, "powerballhit.troy", owner);
            var goTo = owner.Position + (new Vector2(owner.Direction.X, owner.Direction.Z)) * 20f;
            ForceMovement(owner, "RUN", goTo, 100, 0, 0, 0);
            //AddParticleTarget(owner, owner, "powerballstop.troy", owner);
            var deactivationTimer = new GameScriptTimer(0.15f, () =>
            {
                if (owner.HasBuff(buf))
                {
                    buf.DeactivateBuff();
                }
            });
            _timers.Add(deactivationTimer);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            AddParticleTarget(owner, owner, "powerballstop.troy", owner);
            for (int i = 0; i < count; i++)
            {
                owner.RemoveStatModifier(StatsModifier2);
            }
            owner.ChangeModelTo("Rammus");
            owner.SetStatus(StatusFlags.Ghosted, false);
            ownerSpell.SetCooldown(ownerSpell.GetCooldown(), true);
        }
        public void OnUpdate(float diff)
        {
            second += diff;
            if (second >= 1000f)
            {
                StatsModifier2.MoveSpeed.PercentBonus = 0.04f;
                owner.AddStatModifier(StatsModifier2);
                count += 1;
                second = 0f;
            }
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                var timer = _timers[i];
                timer.Update(diff);

                if (timer.IsDead())
                {
                    _timers.RemoveAt(i);
                }
            }
        }
    }
}