using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptPantheon : ICharScript
{
    private ObjAIBase _pantheon;

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
        ApiEventManager.OnSpellCast.AddListener(this, owner.GetSpell("PantheonQ"), OnSpellsCast);
        ApiEventManager.OnSpellCast.AddListener(this, owner.GetSpell("PantheonW"), OnSpellsCast);
        ApiEventManager.OnSpellCast.AddListener(this, owner.GetSpell("PantheonE"), OnSpellsCast);
        ApiEventManager.OnSpellChannel.AddListener(this, owner.GetSpell("PantheonRJump"), OnSpellsCast);
        ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnSpellsCast);
        ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnTakeDamage);
    }

    public void OnUpdate(float diff)
    {
    }

    private void OnSpellsCast(Spell spell)
    {
        AddBuff("Pantheon_Aegis_Counter", 15000f, 1, spell, _pantheon, _pantheon, true);
    }

    private void OnTakeDamage(DamageData data)
    {
        if (_pantheon.HasBuff("Pantheon_AegisShield2") && data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK)
        {
            data.Damage = 0f;
            data.PostMitigationDamage = 0f;
            RemoveBuff(_pantheon, "Pantheon_AegisShield2");
            if (!_pantheon.HasBuff("Pantheon_AegisShield"))
            {
                RemoveBuff(_pantheon, "Pantheon_AegisShieldVisual");
            }

            FloatingTextData ftd = new FloatingTextData(_pantheon, "Blocked!", FloatTextType.Dodge, 1073741833);
            NotifyDisplayFloatingText(ftd, TeamId.TEAM_UNKNOWN);
        }
        else if (_pantheon.HasBuff("Pantheon_AegisShield") && data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK)
        {
            data.Damage = 0f;
            data.PostMitigationDamage = 0f;
            RemoveBuff(_pantheon, "Pantheon_AegisShield");
            if (!_pantheon.HasBuff("Pantheon_AegisShield2"))
            {
                RemoveBuff(_pantheon, "Pantheon_AegisShieldVisual");
            }

            FloatingTextData ftdTarget =
                new FloatingTextData(_pantheon, "Blocked!", FloatTextType.Invulnerable, 1073741833);
            NotifyDisplayFloatingText(ftdTarget, TeamId.TEAM_UNKNOWN);
        }
    }
}