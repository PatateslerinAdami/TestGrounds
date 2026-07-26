using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4151 : ITalentScript
    {
        private ObjAIBase _owner;
        private int _frenzyStacks = 0;
        private StatsModifier _frenzyModifier;
        private int _frenzyTimerId = 0;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnHitUnit.AddListener(this, owner, OnHitUnit);
        }

        private void OnHitUnit(DamageData damageData)
        {
            if (damageData.Attacker == _owner && damageData.DamageResultType == DamageResultType.RESULT_CRITICAL)
            {
                if (_frenzyStacks < 3)
                {
                    _frenzyStacks++;
                    UpdateFrenzyModifier();
                }
                ResetFrenzyTimer();
            }
        }

        private void UpdateFrenzyModifier()
        {
            if (_frenzyModifier != null)
            {
                _owner.RemoveStatModifier(_frenzyModifier);
            }
            _frenzyModifier = new StatsModifier();
            _frenzyModifier.AttackSpeed.PercentBonus = 0.05f * _frenzyStacks;
            _owner.AddStatModifier(_frenzyModifier);
        }

        private void ResetFrenzyTimer()
        {
            int currentTimerId = ++_frenzyTimerId;
            ApiFunctionManager.CreateTimer(3.0f, () =>
            {
                if (currentTimerId == _frenzyTimerId)
                {
                    _frenzyStacks = 0;
                    UpdateFrenzyModifier();
                }
            });
        }
    }
}