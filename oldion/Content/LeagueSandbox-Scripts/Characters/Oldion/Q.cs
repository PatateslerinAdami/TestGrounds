using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class CrypticGaze : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    private static readonly float[] DamageByLevel = { 70, 125, 180, 240, 300 };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        int level = spell.CastInfo.SpellLevel;
        float damage = DamageByLevel[Math.Clamp(level - 1, 0, 4)];
        target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELL, false);
        AddBuff("Stun", 1.5f, 1, spell, target, _owner);
    }
}
