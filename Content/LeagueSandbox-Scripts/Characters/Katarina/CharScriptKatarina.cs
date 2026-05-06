using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptKatarina : ICharScript
{
    private ObjAIBase _katarina;

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        _katarina = owner;
        ApiEventManager.OnKill.AddListener(this, owner, OnKill);
        ApiEventManager.OnAssist.AddListener(this, owner, OnAssist);
    }

    private void OnKill(DeathData data)
    {
        if (data.Unit is not Champion) return;

        RefreshSpellCooldowns();
    }

    private void OnAssist(ObjAIBase assistant, DeathData data)
    {
        if (data.Unit is not Champion) return;

        RefreshSpellCooldowns();
    }

    private void RefreshSpellCooldowns()
    {
        _katarina.GetSpell("KatarinaQ")?.LowerCooldown(15.0f);
        _katarina.GetSpell("KatarinaW")?.LowerCooldown(15.0f);
        _katarina.GetSpell("KatarinaE")?.LowerCooldown(15.0f);
        _katarina.GetSpell("KatarinaR")?.LowerCooldown(15.0f);

        switch (_katarina.SkinID)
        {
            case 9:
                AddParticle(_katarina, _katarina, "Katarina_P_Cast", _katarina.Position);
                break;
            default:
                AddParticle(_katarina, _katarina, "katarina_spell_refresh_indicator", _katarina.Position);
                break;
        }
    }
}
