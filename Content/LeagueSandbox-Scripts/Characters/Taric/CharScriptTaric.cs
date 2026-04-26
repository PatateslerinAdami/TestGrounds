using System;
using System.Linq;
using Buffs;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptTaric : ICharScript {
    private ObjAIBase _taric;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _taric = owner;
        AddBuff("TaricGemcraftBuff", 25000f, 1, spell, _taric, _taric);
    }
    
}
