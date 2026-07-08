
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using System.Collections.Generic;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerLib.GameObjects.AttackableUnits;

namespace Buffs
{
    internal class FioraFlurry : IBuffGameScript
    {
        private Buff _flurry;
        ObjAIBase _fiora;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _flurry = buff;
            _fiora = buff.SourceUnit as Champion;
            ApiEventManager.OnKill.AddListener(this, _fiora, OnKill, false);
            ApiEventManager.OnLaunchAttack.AddListener(this, _fiora, OnAttackAndDash, false);
            ApiEventManager.OnSpellPostCast.AddListener(this, _fiora.Spells[0], OnAttackAndDash, false);
            StatsModifier.AttackSpeed.PercentBonus = StatsModifier.AttackSpeed.PercentBonus += (45f + (_fiora.Spells[2].CastInfo.SpellLevel * 15f)) / 100f;
            unit.AddStatModifier(StatsModifier);
        }
        public void OnKill(DeathData deathData) { _fiora.Spells[2].SetCooldown(0); }
        public void OnAttackAndDash(Spell spell)
        {
            if (_flurry != null && _flurry.StackCount != 0 && !_flurry.Elapsed())
            {
                AddBuff("FioraFlurryDummy", 3.0f, 1, spell, _fiora, _fiora);
            }
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (buff.TimeElapsed >= buff.Duration)
            {
                ApiEventManager.OnKill.RemoveListener(this);
                ApiEventManager.OnLaunchAttack.RemoveListener(this);
                ApiEventManager.OnSpellPostCast.RemoveListener(this);
            }
        }
    }
}
