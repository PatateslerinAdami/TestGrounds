using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    // The Jack-in-the-Box's basic attack. The box auto-attacks with this spell (it's the box's Spell1,
    // tooltipped "JackintheBoxBasicAttack", Attack1 animation, ApplyAttackDamage 0) — ShacoBoxAI points
    // the engine's auto-attack at it. The Target missile homes the acquired enemy; on hit this applies
    // the magic damage, scaling with Shaco's Jack-in-the-Box (W = champion spell slot 1) rank + AP.
    // Values byte-exact from S1 ShacoBoxSpell.lua / ShacoBoxBasicAttack.lua (both identical).
    public class ShacoBoxSpell : ISpellScript
    {
        // Per-attack magic damage by W rank (1-5) + 0.25 AP (S1: SrcValueByLevel + SpellDamageRatio 0.25).
        private static readonly float[] DamageByRank = { 35f, 55f, 75f, 95f, 115f };
        private const float APRatio = 0.25f;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            },
            IsDamagingSpell = true,
            CastingBreaksStealth = true,
        };


        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        }

        // The box reveals when it attacks (it arms hidden, then breaks stealth on the first swing).
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            owner.ExitStealth();
        }

        private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
        {
            var box = spell.CastInfo.Owner as Minion;
            if (box == null || target == null)
            {
                return;
            }

            // The box's damage scales with the Shaco that planted it: Jack-in-the-Box is Shaco's W,
            // i.e. champion spell slot 1 (S1 ShacoBoxSpell read GetSlotSpellLevel(Shaco, 1)).
            var shaco = box.Owner;
            int rank = shaco != null
                ? System.Math.Clamp(shaco.Spells[1].CastInfo.SpellLevel, 1, DamageByRank.Length)
                : 1;
            float ap = shaco?.Stats.AbilityPower.Total ?? 0f;
            float damage = DamageByRank[rank - 1] + APRatio * ap;

            // S1 attributes the damage to Shaco (CallForHelp to the box); SourceDamageType = PROC.
            target.TakeDamage(box, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
            missile?.SetToRemove();
        }
    }
}
