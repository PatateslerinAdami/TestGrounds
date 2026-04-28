using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs
{
    internal class NamiRVision: IBuffGameScript {
        private ObjAIBase _nami;
        private Particle  _rTar, _rSplash;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
            _nami   = ownerSpell.CastInfo.Owner;
            CancelDash(unit);
            ForceMovement(unit, "RUN", new Vector2(unit.Position.X + 10f, unit.Position.Y + 10f), 28f, 0, 16.5f, 0);
            _rSplash = AddParticleTarget(_nami, unit, "Nami_Base_R_splash", unit, buff.Duration,default, bone: "C_BUFFBONE_GLB_CENTER_LOC");
            _rTar = AddParticleTarget(_nami, unit, "Nami_Base_R_tar", unit, buff.Duration,default, bone: "C_BUFFBONE_GLB_CENTER_LOC");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_rSplash);
            RemoveParticle(_rTar);
        }
    }
}
