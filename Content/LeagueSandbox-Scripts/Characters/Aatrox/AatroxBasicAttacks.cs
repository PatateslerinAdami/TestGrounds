using System.Numerics;
using Buffs;
using CharScripts;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class AatroxBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            spell.CastInfo.IsAutoAttack = true;
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, OnLaunch);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (spell.CastInfo.SpellSlot is 48 or 49)
            {
                var timing = owner.CharData.BasicAttacks[0];
                float speed = spell.CastInfo.AttackSpeedModifier;
                spell.CastInfo.SpellNetID = 0;
                spell.CastInfo.ClientDesignerCastTime = spell.SpellData.GetCharacterAttackCastDelay(speed, timing.AttackDelayOffsetPercent,
                    timing.AttackDelayCastOffsetPercent, timing.AttackDelayCastOffsetPercentAttackSpeedRatio);
                spell.CastInfo.ClientDesignerTotalTime = spell.SpellData.GetCharacterAttackDelay(speed, timing.AttackDelayOffsetPercent);
            }

            if (owner.GetBuffWithName("AatroxWONHBuff") is { Remove: false, BuffScript: AatroxWONHBuff w })
            {
                w.PrepareAttack(target);
            }
        }

        private void OnLaunch(Spell spell)
        {
            if (spell.Script == this)
            {
                (spell.CastInfo.Owner.CharScript as CharScriptAatrox)?.AdvanceAttack();
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class AatroxBasicAttack2 : AatroxBasicAttack
    {
    }

    public class AatroxBasicAttack3 : AatroxBasicAttack
    {
    }

    public class AatroxBasicAttack4 : AatroxBasicAttack
    {
    }

    public class AatroxBasicAttack5 : AatroxBasicAttack
    {
    }

    public class AatroxBasicAttack6 : AatroxBasicAttack
    {
    }

    public class AatroxCritAttack : AatroxBasicAttack
    {
    }

    public class AatroxCritAttack2 : AatroxBasicAttack
    {
    }

    public class AatroxWONHAttackLife : AatroxBasicAttack
    {
    }

    public class AatroxWONHAttackPower : AatroxBasicAttack
    {
    }
}
