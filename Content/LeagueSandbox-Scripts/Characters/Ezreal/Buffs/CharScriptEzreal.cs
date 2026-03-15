using GameServerCore.Packets.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptEzreal : ICharScript {
    private ObjAIBase _ezreal;
    private float     _timer       = 0f;
    private bool      _enableTimer = false;

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ezreal = owner;
        ApiEventManager.OnEmote.AddListener(this, _ezreal, OnEmote);
    }

    private void OnEmote(ObjAIBase owner, Emotions emotions) {
        if (emotions != Emotions.TAUNT) return;
        _timer       = 0f;
        _enableTimer = true;
    }

    public void OnUpdate(float diff) {
        _timer += diff;
        if (!_enableTimer) return;
        if (!(_timer >= 1000f)) return;
        AddParticleTarget(_ezreal, _ezreal, "Ezreal_bow_taunt", _ezreal, bone: "L_hand");
        _enableTimer = false;
    }
}