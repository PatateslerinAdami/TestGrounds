using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class Meditate : IBuffGameScript
    {

        private ObjAIBase _masterYi;
        private Spell _spell;
        private Particle _p1, _p2;
        private PeriodicTicker _periodicTicker;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HEAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = true
        };
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _masterYi = buff.SourceUnit;
            _spell = ownerSpell;
            _p1 = SpellEffectCreate("MasterYi_Base_W_Buf.troy",_masterYi, _masterYi,  _masterYi, lifetime: buff.Duration, flags: FXFlags.SimulateWhileOffScreen);
            _p2 = SpellEffectCreate("MasterYi_Base_W_Cas.troy", _masterYi, _masterYi, _masterYi, flags: FXFlags.SimulateWhileOffScreen); 
            ApiEventManager.OnPreTakeDamage.AddListener(this, _masterYi, OnPreTakeDamage);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            data.PostMitigationDamage -= data.PostMitigationDamage * (_spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel]/100);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            
            var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, false, 1, 4);
            if (ticks != 1) return;
            _masterYi.TakeHeal(_masterYi, _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + _masterYi.Stats.AbilityPower.Total * _spell.SpellData.Coefficient, HealType.SelfHeal);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_p1);
            RemoveParticle(_p2);
            //master yi heals when finishing channeling
            _masterYi.TakeHeal(_masterYi, _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + _masterYi.Stats.AbilityPower.Total * _spell.SpellData.Coefficient, HealType.SelfHeal);
        }
    }
}