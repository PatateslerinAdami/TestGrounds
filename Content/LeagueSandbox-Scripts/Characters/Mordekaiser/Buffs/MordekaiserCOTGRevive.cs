using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class MordekaiserCOTGRevive : IBuffGameScript {
    private ObjAIBase _mordekaiser;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _mordekaiser = ownerSpell.CastInfo.Owner;
        //It should check if the unit is a zombie (Sion passive / Yorick R) and wait until the unit isn't anymore for then spawn the ghost and then deactivate itself.
        buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (IsValidTarget(_mordekaiser, unit, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)) return;
        var pet = CreateClonePet(_mordekaiser as Champion, ownerSpell, unit as ObjAIBase, Vector2.Zero, "", 0.0f);
        pet.SetTeam(_mordekaiser.Team);

        AddBuff("MordekaiserCOTGPet", 30.0f, 1, ownerSpell, pet, _mordekaiser);
    }
}