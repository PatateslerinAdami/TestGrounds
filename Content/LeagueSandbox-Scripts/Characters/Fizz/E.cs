using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FizzJump : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            CastingBreaksStealth = true
        };

        public void OnSpellPostCast(Spell spell)
        {
            AddBuff("FizzPlayful", 3, 1, spell, spell.CastInfo.Owner, spell.CastInfo.Owner);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            FizzHelpers.Playful(owner)?.SpellRemoved(spell);
        }
    }

    public class FizzJumpBuffer : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            AutoFaceDirection = false
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (FizzHelpers.Playful(owner) == null)
            {
                spell.CancelCast();
            }
        }

        public void OnSpellPostCast(Spell spell)
        {
            FizzHelpers.Playful(spell.CastInfo.Owner)?.Buffer(FizzHelpers.Aim(spell));
            spell.SetCooldown(0.15f, true);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            FizzHelpers.Playful(owner)?.SpellRemoved(spell);
        }
    }

    public class FizzJumpTwo : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (FizzHelpers.Playful(owner)?.CanRecast != true)
            {
                spell.CancelCast();
            }
            else
            {
                spell.CastInfo.Cooldown = FizzHelpers.Playful(owner).NativeCooldown;
            }
        }

        public void OnSpellPostCast(Spell spell)
        {
            FizzHelpers.Playful(spell.CastInfo.Owner)?.Recast(FizzHelpers.Aim(spell));
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            FizzHelpers.Playful(owner)?.SpellRemoved(spell);
        }
    }

    public class FizzJumpBuffered : FizzJumpBuffer
    {
    }

    public class FizzJumpDelay : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata();
    }

    public class FizzTrickSlam : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true
        };
    }

    public class FizzTrickSlamSoundDummy : FizzTrickSlam
    {
    }

    public class FizzUnlockAnimation : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata();
    }
}
