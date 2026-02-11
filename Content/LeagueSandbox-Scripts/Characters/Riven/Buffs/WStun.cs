using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Buffs
{
    internal class RivenMartyr : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.STUN,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SetStatus(unit, StatusFlags.CanMove, false);
            SetStatus(unit, StatusFlags.CanAttack, false);
            AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Global_Stun.troy", unit, buff.Duration, bone:"head");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            SetStatus(unit, StatusFlags.CanMove, true);
            SetStatus(unit, StatusFlags.CanAttack, true);
        }
    }
}