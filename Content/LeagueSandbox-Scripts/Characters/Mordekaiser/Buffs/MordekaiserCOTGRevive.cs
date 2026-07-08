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
    private AttackableUnit _unit;
    private Buff _buff;
    private Spell _ownerSpell;
    private bool _ghostSpawned;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _mordekaiser = ownerSpell.CastInfo.Owner;
        _unit = unit;
        _buff = buff;
        _ownerSpell = ownerSpell;
    }

    public void OnUpdate(Buff buff, float diff) {
        // S1 MordekaiserCOTGRevive.BuffOnUpdateActionsBuildingBlocks: poll until the holder is DEAD
        // and no longer a ZOMBIE — i.e. wait out any Sion-passive / Yorick-R revive phase — then
        // spawn Morde's ghost clone of them and deactivate. The buff persists through death
        // (PersistsThroughDeath) so it keeps updating after the holder dies.
        if (_ghostSpawned || _unit == null) return;
        if (!_unit.IsDead || _unit.IsZombie) return;

        _ghostSpawned = true;
        // Guard from S1: only a non-(enemy-hero) holder becomes a ghost.
        if (!IsValidTarget(_mordekaiser, _unit, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)) {
            var pet = CreateClonePet(_mordekaiser as Champion, _ownerSpell, _unit as ObjAIBase, Vector2.Zero, "", 0.0f);
            pet.SetTeam(_mordekaiser.Team);
            AddBuff("MordekaiserCOTGPet", 30.0f, 1, _ownerSpell, pet, _mordekaiser);
        }

        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}