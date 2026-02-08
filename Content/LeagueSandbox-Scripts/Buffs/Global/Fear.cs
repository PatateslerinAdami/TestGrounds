using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class Fear : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.FEAR,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
        };
        Particle p;
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = buff.SourceUnit;
            p = AddParticleTarget(owner, unit, "LOC_fear", unit, buff.Duration, bone: "head");
            unit.SetStatus(StatusFlags.Feared, true);

            var dir = (unit.Position - owner.Position).Normalized();
            var path = GetPath(unit.Position,unit.Position + dir * 500); 
            unit.SetWaypoints(path);
            if (unit is ObjAIBase ai)
            {
                ai.UpdateMoveOrder(OrderType.MoveTo);
            }
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            p.SetToRemove();
            unit.SetStatus(StatusFlags.Feared, false);
        }
    }
}

