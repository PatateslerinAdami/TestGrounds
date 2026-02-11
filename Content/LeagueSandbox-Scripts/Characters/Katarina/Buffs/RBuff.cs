using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Linq;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs
{
    class KatarinaR : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();


        private ObjAIBase Owner;
        float somerandomTick;
        AttackableUnit Target1;
        AttackableUnit Target2;
        AttackableUnit Target3;
        Particle p;

        Spell spell;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            Champion champion = unit as Champion;
            Owner = ownerSpell.CastInfo.Owner;
            spell = ownerSpell;
            var owner = ownerSpell.CastInfo.Owner;
            p = AddParticleTarget(owner, owner, "Katarina_deathLotus_cas.troy", owner, lifetime: 2.5f, bone: "C_BUFFBONE_GLB_CHEST_LOC");


            var champs = GetChampionsInRange(owner.Position, 500f, true).OrderBy(enemy => Vector2.Distance(enemy.Position, owner.Position)).ToList();
            if (champs.Count > 3)
            {
                foreach (var enemy in champs.GetRange(0, 4)
                     .Where(x => x.Team == CustomConvert.GetEnemyTeam(owner.Team)))
                {
                    SpellCast(owner, 0, SpellSlotType.ExtraSlots, true, enemy, Vector2.Zero);
                    if (Target1 == null) Target1 = enemy;
                    else if (Target2 == null) Target2 = enemy;
                    else if (Target3 == null) Target3 = enemy;
                }
            }
            else
            {
                foreach (var enemy in champs.GetRange(0, champs.Count)
                    .Where(x => x.Team == CustomConvert.GetEnemyTeam(owner.Team)))
                {
                    SpellCast(owner, 0, SpellSlotType.ExtraSlots, true, enemy, Vector2.Zero);
                    if (Target1 == null) Target1 = enemy;
                    else if (Target2 == null) Target2 = enemy;
                    else if (Target3 == null) Target3 = enemy;
                }
            }
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(p);
            StopAnimation(Owner, "Spell4");
        }

        public void OnUpdate(float diff)
        {
            somerandomTick += diff;
            if (somerandomTick >= 250f)
            {
                if (Target1 != null) SpellCast(Owner, 0, SpellSlotType.ExtraSlots, true, Target1, Vector2.Zero);
                if (Target2 != null) SpellCast(Owner, 0, SpellSlotType.ExtraSlots, true, Target2, Vector2.Zero);
                if (Target3 != null) SpellCast(Owner, 0, SpellSlotType.ExtraSlots, true, Target3, Vector2.Zero);
                somerandomTick = 0;
            }

        }
    }

    class KatarinaRChecker : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1,
            UpdateInfinite = true
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        ObjAIBase _owner;
        bool _isSlotSealed;
        bool shouldBeSealed;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = ownerSpell.CastInfo.Owner;
            _isSlotSealed = !CheckChampionsInRangeFromTeam(_owner.Position, 500f, CustomConvert.GetEnemyTeam(_owner.Team), true);
            if (_isSlotSealed)
            {
                SealSpellSlot(_owner, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION, true);
                _owner.Replication.Update();
            }
        }
        public void OnUpdate(float diff)
        {
            shouldBeSealed = !CheckChampionsInRangeFromTeam(_owner.Position, 500f, CustomConvert.GetEnemyTeam(_owner.Team), true);
            if (shouldBeSealed != _isSlotSealed)
            {
                _isSlotSealed = shouldBeSealed;
                SealSpellSlot(_owner, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION, _isSlotSealed);
            }
        }
    }
}
