using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace Buffs
{
    public class ItemPhantomDancer : ItemLifelineCooldown, IBuffGameScript
    {
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            buff.SetStatusEffect(StatusFlags.Ghosted, true);
        }
    }
}
