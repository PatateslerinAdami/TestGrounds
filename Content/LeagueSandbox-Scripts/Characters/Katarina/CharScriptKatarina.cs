using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptKatarina : ICharScript
{
    private ObjAIBase _owner;

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        _owner = owner;
        ApiEventManager.OnKill.AddListener(this, owner, OnKill);
        ApiEventManager.OnAssist.AddListener(this, owner, OnAssist);
    }

    private void OnKill(DeathData data)
    {
        if (data.Unit is not Champion)
        {
            return;
        }

        RefreshSpellCooldowns();
    }

    private void OnAssist(ObjAIBase assistant, DeathData data)
    {
        if (data.Unit is not Champion)
        {
            return;
        }

        RefreshSpellCooldowns();
    }

    private void RefreshSpellCooldowns()
    {
        _owner.GetSpell("KatarinaQ")?.LowerCooldown(15.0f);
        _owner.GetSpell("KatarinaW")?.LowerCooldown(15.0f);
        _owner.GetSpell("KatarinaE")?.LowerCooldown(15.0f);
        _owner.GetSpell("KatarinaR")?.LowerCooldown(15.0f);

        switch (_owner.SkinID)
        {
            case 9:
                AddParticle(_owner, _owner, "Katarina_P_Cast", _owner.Position);
                break;
            default:
                AddParticle(_owner, _owner, "katarina_spell_refresh_indicator", _owner.Position);
                break;
        }
    }
}
