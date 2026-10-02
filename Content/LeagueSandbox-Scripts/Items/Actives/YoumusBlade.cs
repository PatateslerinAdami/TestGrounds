using System.Numerics;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class YoumusBlade : ItemActiveSpell
    {
        protected override float Cooldown => 45;

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            AddParticleTarget(
                owner,
                owner,
                "spectral_fury_cas.troy",
                owner,
                1f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            AddBuff("SpectralFury", 6, 1, spell, owner, owner);
            return true;
        }
    }
}
