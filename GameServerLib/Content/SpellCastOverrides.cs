using System;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace LeagueSandbox.GameServer.Content
{
    /// <summary>
    /// Server corrections for native cast carriers. Unspecified fields keep the
    /// shared casting defaults, client fields never change the learned rank or windup.
    /// </summary>
    public sealed class SpellCastOverrides
    {
        private float? _castTime, _totalTime, _attackSpeed, _cooldown;
        private float? _clientCastTime, _clientTotalTime;
        private int? _clientSlot, _clientLevel, _clientLevelOffset, _clientAmmo;
        private int? _clientStopBeforeCastFlags;
        private int? _clientStopAttackWindupFlags;
        private bool? _secondAttack, _attackCastDelay, _forceCast, _overridePosition;

        public static SpellCastOverrides Load(ContentFile file)
        {
            float? Number(string key) => file.GetObject("ServerCast", key) == null
                ? null : file.GetFloat("ServerCast", key);
            int? Integer(string key) => file.GetObject("ServerCast", key) == null
                ? null : file.GetInt("ServerCast", key);
            bool? Boolean(string key) => file.GetObject("ServerCast", key) == null
                ? null : file.GetBool("ServerCast", key);
            return new SpellCastOverrides
            {
                _castTime = Number("DesignerCastTime"),
                _totalTime = Number("DesignerTotalTime"),
                _attackSpeed = Number("AttackSpeedModifier"),
                _cooldown = Number("Cooldown"),
                _clientCastTime = Number("ClientDesignerCastTime"),
                _clientTotalTime = Number("ClientDesignerTotalTime"),
                _clientSlot = Integer("ClientSpellSlot"),
                _clientLevel = Integer("ClientSpellLevel"),
                _clientLevelOffset = Integer("ClientSpellLevelOffset"),
                _clientAmmo = Integer("ClientAmmoUsed"),
                _clientStopBeforeCastFlags = Integer("ClientStopBeforeCastFlags"),
                _clientStopAttackWindupFlags = Integer("ClientStopAttackWindupFlags"),
                _secondAttack = Boolean("IsSecondAutoAttack"),
                _attackCastDelay = Boolean("UseAttackCastDelay"),
                _forceCast = Boolean("IsForceCastingOrChannel"),
                _overridePosition = Boolean("IsOverrideCastPosition")
            };
        }

        public void Apply(CastInfo cast)
        {
            if (_castTime.HasValue) cast.DesignerCastTime = _castTime.Value;
            if (_totalTime.HasValue) cast.DesignerTotalTime = _totalTime.Value;
            if (_attackSpeed.HasValue) cast.AttackSpeedModifier = _attackSpeed.Value;
            if (_cooldown.HasValue) cast.Cooldown = _cooldown.Value;
            if (_clientCastTime.HasValue) cast.ClientDesignerCastTime = _clientCastTime;
            if (_clientTotalTime.HasValue) cast.ClientDesignerTotalTime = _clientTotalTime;
            if (_clientSlot.HasValue) cast.ClientSpellSlot = checked((byte)_clientSlot.Value);
            if (_clientLevel.HasValue) cast.ClientSpellLevel = checked((byte)_clientLevel.Value);
            else if (_clientLevelOffset.HasValue)
                cast.ClientSpellLevel = (byte)Math.Clamp(cast.SpellLevel + _clientLevelOffset.Value, 0, byte.MaxValue);
            if (_clientAmmo.HasValue) cast.ClientAmmoUsed = _clientAmmo;
            if (_clientStopBeforeCastFlags.HasValue)
            {
                cast.ClientStopBeforeCastFlags = checked((byte)_clientStopBeforeCastFlags.Value);
                cast.ClientStopBeforeCastMissileNetID = 0;
            }
            if (_clientStopAttackWindupFlags.HasValue)
                cast.ClientStopAttackWindupFlags = checked((byte)_clientStopAttackWindupFlags.Value);
            if (_secondAttack.HasValue) cast.IsSecondAutoAttack = _secondAttack.Value;
            if (_attackCastDelay.HasValue) cast.UseAttackCastDelay = _attackCastDelay.Value;
            if (_forceCast.HasValue) cast.IsForceCastingOrChannel = _forceCast.Value;
            if (_overridePosition.HasValue) cast.IsOverrideCastPosition = _overridePosition.Value;
        }
    }
}
