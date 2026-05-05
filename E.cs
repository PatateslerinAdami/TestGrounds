using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using System.Linq;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class AsheSpiritOfTheHawk : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle,
            }
        };
        ObjAIBase _owner;
        bool isEvolved = false;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile, false);
        }
        public void OnLaunchMissile(Spell spell, SpellMissile missile)
        {
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
        }
        public void OnMissileEnd(SpellMissile missile)
        {
            var endPosition = missile.Position;
            AddParticlePos(_owner, "ashe_base_e_tar_explode.troy", endPosition, endPosition, lifetime: 4f);
            AddPosPerceptionBubble(position: endPosition, radius: 1000f, duration: 5.0f, team: _owner.Team, revealStealthed: false, ignoresLoS: true);
            if (isEvolved)
            {
                Champion targetChamp = null;
                targetChamp = GetChampionsInRange(endPosition, 1000f, true).Where(c => c.Team != _owner.Team) 
                .OrderBy(c => Vector2.Distance(c.Position, endPosition)) 
                .FirstOrDefault();

                if (targetChamp != null)
                {
                    AddUnitPerceptionBubble(targetChamp, 1f, 20.0f, _owner.Team, false, targetChamp, ignoresLoS: true, onlyShowTarget: true);
                    AddUnitPerceptionBubble(_owner, 1f, 20.0f, targetChamp.Team, false, _owner, ignoresLoS: true, onlyShowTarget: true);
                    var missiles = GetMissiles();
                    foreach (var m in missiles)
                    {
                        if (m.SpellOrigin != null && m.SpellOrigin.SpellName == "EnchantedCrystalArrowMissile" && m.CastInfo.Owner == _owner)
                        {
                            Vector2 mPos = m.Position;
                            m.SetToRemove();
                            CreateCustomMissile(_owner, "EnchantedCrystalArrowMissile2", mPos, targetChamp.Position, new MissileParameters { Type = MissileType.Target }, target: targetChamp);
                        }
                    }
                }
            }
        }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            ScriptMetadata.MissileParameters.OverrideEndPosition = end;
        }
        public void OnSpellEvolve(Spell spell)
        {
            isEvolved = true;
        }
    }
}