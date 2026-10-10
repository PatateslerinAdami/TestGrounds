using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    public class CharScriptYasuo : ICharScript
    {
        private static readonly float[] ShieldByLevel =
        {
            60,
            65,
            70,
            75,
            80,
            90,
            100,
            110,
            125,
            140,
            160,
            185,
            215,
            250,
            290,
            340,
            400,
            470
        };
        private readonly StatsModifier _intent = new StatsModifier();
        private ObjAIBase _owner;
        private Spell _passive;
        private Buff _charge, _passiveBuff, _movementBuff;
        private Vector2 _lastPosition;
        private byte _teleportId;
        private float _capacity, _originalCapacity;
        private bool _wasDead;
        private int _attackStage;

        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            if (_owner != null)
            {
                return;
            }

            _owner = owner;
            _passive = spell;
            _originalCapacity = owner.Stats.ManaPoints.BaseValue;
            _capacity = Capacity(owner.Stats.Level);
            owner.Stats.ManaPoints.BaseValue = _capacity;
            owner.Stats.CurrentMana = 0;
            _intent.CriticalChance.PercentBonus = 1;
            _intent.CriticalDamage.PercentBonus = -0.1f;
            owner.AddStatModifier(_intent);
            _passiveBuff = AddBuff("YasuoPassive", 25000, 1, spell, owner, owner, infiniteduration: true);
            _movementBuff = AddBuff("YasuoPassiveMovementShield", 25000, 1, spell, owner, owner, infiniteduration: true);
            _charge = AddBuff("YasuoPassiveMSCharge", 25000, 1, spell, owner, owner, infiniteduration: true);
            Anchor();
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage);
            ApiEventManager.OnDeath.AddListener(this, owner, OnDeath);
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunchAttack);
            UpdateCounter();
        }

        private static float Capacity(byte level)
        {
            return ShieldByLevel[Math.Clamp((int)level, 1, 18) - 1];
        }

        public Spell SelectAutoAttack(ObjAIBase owner, bool isCritical)
        {
            string suffix = _attackStage == 0 ? "" : (_attackStage + 1).ToString();
            return owner.GetAutoAttackSpell((isCritical ? "YasuoCritAttack" : "YasuoBasicAttack") + suffix);
        }

        private void OnLaunchAttack(Spell spell)
        {
            if (spell.SpellName.StartsWith("YasuoBasicAttack") || spell.SpellName.StartsWith("YasuoCritAttack"))
            {
                _attackStage = (_attackStage + 1) % 4;
            }
        }

        private void Anchor()
        {
            _lastPosition = _owner.Position;
            _teleportId = _owner.TeleportID;
        }

        public void OnUpdate(float diff)
        {
            if (_owner == null)
            {
                return;
            }

            Spells.YasuoCastHelpers.RefreshQTooltip(_owner.Spells[0]);
            float capacity = Capacity(_owner.Stats.Level);
            if (capacity != _capacity)
            {
                float fraction = Math.Clamp(_owner.Stats.CurrentMana / _capacity, 0, 1);
                _capacity = capacity;
                _owner.Stats.ManaPoints.BaseValue = capacity;
                _owner.Stats.CurrentMana = capacity * fraction;
            }

            if (!_owner.IsDead && !_wasDead && _owner.TeleportID == _teleportId)
            {
                float distance = Vector2.Distance(_lastPosition, _owner.Position);
                if (float.IsFinite(distance))
                {
                    _owner.Stats.CurrentMana = Math.Min(_capacity, _owner.Stats.CurrentMana + distance * _capacity / 4600f);
                }
            }

            if (_owner.IsDead)
            {
                _owner.Stats.CurrentMana = 0;
            }

            _wasDead = _owner.IsDead;
            Anchor();
            UpdateCounter();
        }

        private void UpdateCounter()
        {
            int percent = (int)MathF.Floor(Math.Clamp(_owner.Stats.CurrentMana / _capacity, 0, 1) * 100f + 0.0001f);
            if (_charge != null && !_charge.Remove && _charge.StackCount != percent)
            {
                _charge.SetStacks(percent);
            }
        }

        private void OnPreTakeDamage(DamageData damage)
        {
            var activeShield = _owner.GetBuffWithName("YasuoPassiveMSShieldOn");
            if (_owner.IsDead)
            {
                return;
            }

            if (damage.PostMitigationDamage <= 0)
            {
                return;
            }

            if (!_owner.CanTakeDamage(damage.DamageType))
            {
                return;
            }

            if (damage.DamageResultType is DamageResultType.RESULT_MISS or DamageResultType.RESULT_DODGE or DamageResultType.RESULT_INVULNERABLE or DamageResultType.RESULT_INVULNERABLENOMESSAGE)
            {
                return;
            }

            if (damage.Attacker == null)
            {
                return;
            }

            if (damage.Attacker.Team == _owner.Team)
            {
                return;
            }

            if (!(damage.Attacker is Champion or Monster || damage.Attacker is Minion { Owner: Champion }))
            {
                return;
            }

            if (_owner.Stats.CurrentMana < _capacity - 0.0001f)
            {
                return;
            }

            if ((activeShield != null && !activeShield.Remove))
            {
                return;
            }

            var shield = AddBuff("YasuoPassiveMSShieldOn", 1, 1, _passive, _owner, _owner);
            if (shield == null || shield.Remove)
            {
                return;
            }

            _owner.Stats.CurrentMana = 0;
            UpdateCounter();
        }

        private void OnDeath(DeathData data)
        {
            _owner.RemoveBuffsWithName("YasuoSwordOut");
            _owner.Stats.CurrentMana = 0;
            _owner.RemoveBuffsWithName("YasuoPassiveMSShieldOn");
            _wasDead = true;
            Anchor();
            UpdateCounter();
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell = null)
        {
            if (_owner == null)
            {
                return;
            }

            ApiEventManager.RemoveAllListenersForOwner(this);
            owner.RemoveBuffsWithName("YasuoSwordOut");
            owner.RemoveBuffsWithName("YasuoPassiveMSShieldOn");
            foreach (var buff in new[] { _charge, _movementBuff, _passiveBuff }

            )
            {
                if (buff != null)
                {
                    owner.RemoveBuff(buff);
                }
            }

            owner.RemoveStatModifier(_intent);
            owner.Stats.ManaPoints.BaseValue = _originalCapacity;
            owner.Stats.CurrentMana = 0;
            _owner = null;
        }
    }
}
