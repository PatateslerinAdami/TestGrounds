using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    internal class CharScriptOdinCenterRelic : ICharScript
    {
        private const float RewardDuration = 50f;

        private Minion _owner;
        private Spell _captureSpell;
        private bool _consumed;

        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            _owner = owner as Minion;
            SetStatus(owner, StatusFlags.MagicImmune, true);
            SetStatus(owner, StatusFlags.PhysicalImmune, true);
            SetStatus(owner, StatusFlags.CanAttack, false);
            SetStatus(owner, StatusFlags.CanMove, false);
            owner.Stats.CurrentMana = owner.Stats.ManaPoints.Total;
            AddBuff("OdinBombBuff", 25000f, 1, null, owner, owner, false);
        }

        public bool CanCapture(Champion champion)
        {
            return champion != null && !champion.IsDead && _owner != null
                && !_consumed && !_owner.IsDead && !_owner.IsToRemove()
                && (champion.Team == TeamId.TEAM_BLUE || champion.Team == TeamId.TEAM_PURPLE)
                && (_owner.Team == TeamId.TEAM_BLUE || _owner.Team == TeamId.TEAM_PURPLE)
                && champion.Team != _owner.Team && _captureSpell == null;
        }

        public bool BeginCapture(Champion champion, Spell spell)
        {
            if (!CanCapture(champion) || spell == null || spell.CastInfo.Owner != champion
                || champion.HasBuff("OdinCaptureChannelCooldownBuff"))
            {
                return false;
            }

            _captureSpell = spell;
            _owner.Stats.CurrentMana = _owner.Stats.ManaPoints.Total;
            return true;
        }

        public bool IsCapturing(Champion champion, Spell spell)
        {
            return _captureSpell == spell && spell != null && champion != null
                && spell.CastInfo.Owner == champion && !champion.IsDead
                && !_consumed && !_owner.IsDead && !_owner.IsToRemove();
        }

        public void SetCaptureRemaining(Spell spell, float fraction)
        {
            if (spell != null && _captureSpell == spell && !_consumed)
            {
                _owner.Stats.CurrentMana = _owner.Stats.ManaPoints.Total * Math.Clamp(fraction, 0, 1);
            }
        }

        public void EndCapture(Spell spell)
        {
            if (spell == null || _captureSpell != spell)
            {
                return;
            }

            _captureSpell = null;
            if (!_consumed && !_owner.IsDead && !_owner.IsToRemove())
            {
                _owner.Stats.CurrentMana = _owner.Stats.ManaPoints.Total;
            }
        }

        public void CompleteCapture(Champion champion, Spell spell)
        {
            if (!IsCapturing(champion, spell) || _owner.Stats.CurrentMana > 0)
            {
                return;
            }

            _consumed = true;
            _owner.Stats.CurrentMana = 0;
            AddParticle(champion, null, "OdinBombSuccessParticle.troy", _owner.Position, 2f);
            AddBuff("OdinCenterRelic", RewardDuration, 1, spell, champion, champion);
            AddBuff("OdinCenterRelicDamage", RewardDuration, 1, spell, champion, champion);

            // A pickup must notify Crystal's respawn listener without awarding a minion kill.
            RemoveBuff(_owner, "OdinBombBuff");
            DespawnMinion(_owner);
            ApiEventManager.OnDeath.Publish(_owner, new DeathData
            {
                Unit = _owner,
                DamageType = DamageType.DAMAGE_TYPE_TRUE,
                DamageSource = DamageSource.DAMAGE_SOURCE_INTERNALRAW
            });
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell = null)
        {
            var capture = _captureSpell;
            if (capture != null && capture.CastInfo.Owner.ChannelSpell == capture)
            {
                capture.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
            }

            _captureSpell = null;
        }
    }
}
