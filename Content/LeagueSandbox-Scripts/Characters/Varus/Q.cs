using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static GameServerCore.Content.HashFunctions;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VarusQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            ChannelDuration = 4f,
            AutoFaceDirection = false
        };

        private ObjAIBase _owner;
        private Spell _spell;
        Buff soundBuff;
        Particle p1, p2, p3;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
        }
        public void OnSpellChannel(Spell spell)
        {
            soundBuff = AddBuff("VarusQ", 4.0f, 1, spell, _owner, _owner);
            var timerAnm = new GameScriptTimer(0.2f, () =>
            {
                // idk either couldnt find the right bone to attach to, or something wrong with the system
                if(_owner.ChannelSpell != null && _owner.ChannelSpell.SpellName == "VarusQ") p1 = AddParticleTarget(_owner, _owner, "varusqchannel.troy", _owner, 4f, 1, "Weapon", "R_PARENTING_HAND_LOC"); // AddParticleTarget(_owner, _owner, "varusqchannel.troy", _owner, 4f, 1, "BUFFBONE_GLB_CHANNEL_LOC", "R_finger_b");
            });
            _owner.RegisterTimer(timerAnm);
            p2 = AddParticle(_owner, _owner, "varusqchannel2", default, 4f, bone:"HEAD");//C_BUFFBONE_GLB_CENTER_LOC
        }

        public void OnSpellChannelUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            if (!forceStop)
            {
                FaceDirection(new Vector2(position.X, position.Z), _owner, false);
            }
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            LetGo();
            if (reason == ChannelingStopSource.PlayerCommand)
            {

                float maxChannelTime = ScriptMetadata.ChannelDuration;
                float timeChanneled = maxChannelTime - spell.CurrentChannelDuration;

                float minRange = 895;
                float maxRange = 1595;
                float growthDuration = 1.5f;

                float currentRange = minRange;
                if (timeChanneled > 0)
                {
                    float progress = Math.Min(1.0f, timeChanneled / growthDuration);
                    currentRange = minRange + ((maxRange - minRange) * progress);
                }
                Vector2 ownerPos = _owner.Position;
                Vector2 mousePos = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

                Vector2 direction = Vector2.Normalize(mousePos - ownerPos);
                if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
                {
                    direction = new Vector2(1, 0);
                }
                Vector2 castPos = ownerPos + (direction * currentRange);

                CreateCustomMissile(_owner, "VarusQMissile", ownerPos, castPos, new MissileParameters { Type = MissileType.Circle });
                PlayAnimation(_owner, "Spell1_Fire");
                if(_owner.IsPathEnded()) FaceDirection(castPos, _owner, true);
                //SpellCast(_owner, 0, SpellSlotType.ExtraSlots, castPos, castPos, false, Vector2.Zero);
                //_owner.GetSpell("VarusQMissile").Cast(ownerPos, castPos);
            }
            else
            {
                ManaRefund();
            }
        }

        public void OnSpellPostChannel(Spell spell)
        {
            ManaRefund();
            AddParticle(_owner, _owner, "varusqexpire", default);
            LetGo();
        }
        private void LetGo()
        {
            p1?.SetToRemove();
            p2?.SetToRemove();
            if(_owner.HasBuff(soundBuff)) _owner.RemoveBuff(soundBuff);
        }
        private void ManaRefund()
        {
            
        }
    }

    public class VarusQMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle
            },
            IsDamagingSpell = true
        };
        ObjAIBase _owner;
        IEventSource source;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
            _owner = owner;
            source = new AbilityInfo(HashString("VarusQ"), HashString("VarusQ"));
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            if (spell.SpellData.IsValidTarget(_owner, target))
            {

                target.TakeDamage(_owner, 100f, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false, source);
                switch (target)
                {
                    case Minion minion:
                        AddParticleTarget(_owner, target, "VarusQHitMinion_amber.troy", target);
                        AddParticleTarget(_owner, target, "VarusQHitMinion.troy", target);
                        break;
                    default:
                        AddParticleTarget(_owner, target, "VarusQHit_amber.troy", target);
                        AddParticleTarget(_owner, target, "VarusQHit.troy", target);
                        break;
                }
            }
        }
    }
}