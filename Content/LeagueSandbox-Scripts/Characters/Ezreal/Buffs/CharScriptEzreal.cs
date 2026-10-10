using System.Collections.Generic;
using GameServerCore.Packets.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    public class CharScriptEzreal : ICharScript
    {
        private ObjAIBase _owner;
        private float _tauntTime;
        private bool _taunting;
        private int _armorRank;
        private bool _fastRun;
        private readonly List<Particle> _particles = new List<Particle>();

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnEmote.AddListener(this, owner, OnEmote);
            ApiEventManager.OnMinionKill.AddListener(this, owner, OnMinionKill);
            for (short slot = 0; slot < 4; slot++)
            {
                ApiEventManager.OnLevelUpSpell.AddListener(this, owner.Spells[slot], OnSpellUpgrade);
            }
        }

        public void OnPostActivate(ObjAIBase owner, Spell spell)
        {
            UpdateArmor(false);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            foreach (var particle in _particles)
            {
                RemoveParticle(particle);
            }
            _particles.Clear();
            owner.RemoveModelOverride(this);
            owner.RemoveAnimStates(this);
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        private void OnEmote(ObjAIBase owner, Emotions emotion)
        {
            if (emotion != Emotions.TAUNT)
            {
                return;
            }

            _tauntTime = 0;
            _taunting = true;
        }

        private void OnSpellUpgrade(Spell spell)
        {
            if (_owner.SkinID != 5)
            {
                return;
            }

            if (spell.CastInfo.SpellSlot == 3)
            {
                UpdateArmor(true);
                return;
            }

            if (spell.CastInfo.SpellLevel != 1)
            {
                return;
            }

            string effect = spell.CastInfo.SpellSlot switch
            {
                0 => "Ezreal_Pulsefire_MythicShot_Activate",
                1 => "Ezreal_Pulsefire_EssenceFlux_Activate",
                _ => "Ezreal_Pulsefire_ArcaneShift_Activate"
            };
            AddOwnedParticle(effect, _owner, "gun_base2");
        }

        private void UpdateArmor(bool effects)
        {
            if (_owner.SkinID != 5)
            {
                return;
            }

            int rank = System.Math.Clamp((int)_owner.Spells[3].CastInfo.SpellLevel, 0, 3);
            if (rank == _armorRank)
            {
                return;
            }

            _armorRank = rank;
            if (rank == 0)
            {
                _owner.RemoveModelOverride(this);
                return;
            }

            _owner.SetModelOverride("Ezreal_cyber_" + rank, this);
            if (!effects)
            {
                return;
            }

            AddOwnedParticle("Ezreal_Pulsefire_Upgrayedd", _owner);
            AddOwnedParticle("Ezreal_Pulsefire_UpGraeddd_Model" + rank, _owner);
            AddOwnedParticle("Ezreal_Pulsefire_TrueShot_Activate", _owner, "gun_base2");
            AddOwnedParticle("Ezreal_Pulsefire_Model1_Flare", _owner, "gun_base2");
            AddOwnedParticle("Ezreal_Pulsefire_Shldr_Flare" + rank, _owner, "BUFFBONE_ShoulderTube1");
            AddOwnedParticle("Ezreal_Pulsefire_Shldr_Flare" + rank, _owner, "BUFFBONE_ShoulderTube2");
        }

        private void OnMinionKill(DeathData death)
        {
            if (_owner.SkinID != 5)
            {
                return;
            }

            AddOwnedParticle("Ezreal_Pulsefire_Minion_Death", death.Unit);
        }

        private void AddOwnedParticle(string effect, GameObject bindObject, string bone = "")
        {
            var particle = AddParticleTarget(
                _owner,
                bindObject,
                effect,
                _owner,
                bone: bone
            );
            _particles.Add(particle);
        }

        public void OnUpdate(float diff)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                if (_particles[i].IsToRemove())
                {
                    _particles.RemoveAt(i);
                }
            }
            UpdateArmor(false);
            bool fastRun = _owner.SkinID == 5 && !_owner.IsDead && _owner.Stats.GetTrueMoveSpeed() > 400;
            if (fastRun != _fastRun)
            {
                _fastRun = fastRun;
                if (fastRun)
                {
                    _owner.SetAnimStates(new Dictionary<string, string> { { "RUN", "RUN2" } }, this);
                }
                else
                {
                    _owner.RemoveAnimStates(this);
                }
            }

            if (!_taunting)
            {
                return;
            }

            _tauntTime += diff;
            if (_tauntTime < 1000)
            {
                return;
            }

            _taunting = false;
            AddOwnedParticle("Ezreal_bow_taunt", _owner, "L_hand");
        }
    }
}