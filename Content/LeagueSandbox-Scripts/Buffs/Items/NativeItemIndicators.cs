using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public abstract class NativeItemIndicator : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 254
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected Buff IndicatorBuff;
        private Particle _particle;
        protected virtual string ParticleName => null;

        protected virtual string ParticleBone => "C_BUFFBONE_GLB_CHEST_LOC";

        public virtual void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            IndicatorBuff = buff;
            if (ParticleName != null)
            {
                _particle = AddParticleTarget(
                    buff.SourceUnit,
                    unit,
                    ParticleName,
                    unit,
                    0,
                    bone: ParticleBone,
                    ignoreCasterVisibility: true
                );
            }

            if (_particle != null)
            {
                _particle.isInfinite = true;
            }

            NativeItemPresentation.Sync(buff);
        }

        public virtual void OnUpdate(float diff)
        {
            NativeItemPresentation.Sync(IndicatorBuff, diff);
        }

        public virtual void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_particle);
        }
    }

    public class WitsEndBuff : NativeItemIndicator
    {
    }

    public class MaladyCounter : NativeItemIndicator
    {
        public MaladyCounter()
        {
            BuffMetaData.BuffType = BuffType.COMBAT_DEHANCER;
        }
    }

    public class BlackCleaver : NativeItemIndicator
    {
        public BlackCleaver()
        {
            BuffMetaData.BuffType = BuffType.COMBAT_DEHANCER;
        }
    }

    public class ItemSlow : NativeItemIndicator
    {
        public ItemSlow()
        {
            BuffMetaData.BuffType = BuffType.COMBAT_DEHANCER;
        }
    }

    public class LichBane : NativeItemIndicator
    {
        public LichBane() => BuffMetaData.ClientPackageHash = 0;
    }

    public class ItemFrozenFist : NativeItemIndicator
    {
        public ItemFrozenFist() => BuffMetaData.ClientPackageHash = 0;
    }

    public class Rageblade : NativeItemIndicator
    {
    }

    public class RagebladeEnrage : NativeItemIndicator
    {
    }

    public class Hexdrinker : NativeItemIndicator
    {
    }

    public class HexdrinkerMaw : NativeItemIndicator
    {
    }

    public class GrievousWound : NativeItemIndicator
    {
        public GrievousWound()
        {
            BuffMetaData.BuffType = BuffType.COMBAT_DEHANCER;
        }
    }

    public class BilgewaterCutlass : NativeItemIndicator
    {
        public BilgewaterCutlass()
        {
            BuffMetaData.BuffType = BuffType.COMBAT_DEHANCER;
        }
    }

    public class HextechGunbladeSpell : NativeItemIndicator
    {
        public HextechGunbladeSpell()
        {
            BuffMetaData.BuffType = BuffType.COMBAT_DEHANCER;
        }
    }

    public class BootsDeathmarchSpeed : NativeItemIndicator
    {
    }

    public class BootsDistortion : NativeItemIndicator
    {
    }

    public class ElixirOfIronTrailBuff : NativeItemIndicator
    {
    }

    public class RengarRevive : NativeItemIndicator
    {
    }

    public class ItemAegisoftheLegionAuraSelf : NativeItemIndicator
    {
    }

    public class ItemAegisoftheLegionAuraFriend : NativeItemIndicator
    {
    }

    public class RallyingBannerAuraSelf : NativeItemIndicator
    {
        protected override string ParticleName => "Rallyingbanner_itm.troy";

        protected override string ParticleBone => "C_BUFFBONE_GLB_GROUND_LOC";
    }

    public class RallyingBannerAuraFriend : NativeItemIndicator
    {
    }

    public class IronStylusAura : NativeItemIndicator
    {
    }

    public class AbyssalScepterAuraSelf : NativeItemIndicator
    {
    }

    public class FrozenHeartAuraCosmetic : NativeItemIndicator
    {
        protected override string ParticleName => "Frozen_heart_overhead.troy";

        protected override string ParticleBone => "C_BUFFBONE_GLB_OVERHEAD_LOC";
    }

    public class ItemSunfireCapeParticle : NativeItemIndicator
    {
        protected override string ParticleName => "global_item_sunfirecape_aura.troy";

        protected override string ParticleBone => "C_BUFFBONE_GLB_GROUND_LOC";
    }

    public class ZephyrsLamentBurn : NativeItemIndicator
    {
        public ZephyrsLamentBurn()
        {
            BuffMetaData.BuffType = BuffType.DAMAGE;
        }

        protected override string ParticleName => "brandblaze_hotfoot.troy";

        protected override string ParticleBone => "C_BUFFBONE_GLB_GROUND_LOC";
    }

    public class BootsCaptainSpeedVFX : NativeItemIndicator
    {
        public BootsCaptainSpeedVFX()
        {
            BuffMetaData.IsHidden = true;
        }

        protected override string ParticleName => "boots_captain.troy";

        protected override string ParticleBone => "C_BUFFBONE_GLB_GROUND_LOC";
    }

    public class ItemSpectreCowlRegen : NativeItemIndicator
    {
    }

    public class ItemAngelHandBuff : NativeItemIndicator
    {
    }

    public class ShurelyaMinionHaste : NativeItemIndicator
    {
    }

    public class HexdrinkerTimerCD : NativeItemIndicator
    {
    }

    public class ItemAuraPresentation : NativeItemIndicator
    {
        public ItemAuraPresentation()
        {
            BuffMetaData.IsHidden = true;
            BuffMetaData.BuffAddType = BuffAddType.STACKS_AND_OVERLAPS;
            BuffMetaData.MaxStacks = 100;
        }

        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Show(buff, buff.Variables.Get<string>("nativeName"), true);
        }

        public override void OnUpdate(float diff)
        {
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public class ItemStatikShankCharge : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COUNTER,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 100
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private Particle _charged;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
        }

        public void OnUpdate(float diff)
        {
            var owner = _buff.SourceUnit;
            if (!owner.Inventory.HasItemWithID(3087))
            {
                RemoveBuff(_buff);
                return;
            }

            int charge = (int)_buff.Variables.GetFloat("charge");
            if (_buff.StackCount != charge)
            {
                EditBuff(_buff, (byte)charge);
            }

            if (charge == 100 && !owner.IsDead && _charged == null)
            {
                _charged = AddParticleTarget(
                    owner,
                    owner,
                    "tempitemshank_3.troy",
                    owner,
                    0,
                    bone: "BUFFBONE_GLB_WEAPON_1"
                );
                _charged.isInfinite = true;
            }

            if ((charge < 100 || owner.IsDead) && _charged != null)
            {
                RemoveParticle(_charged);
                _charged = null;
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_charged);
        }
    }

    public class ItemSunfireCapeAura : NativeItemIndicator
    {
        public ItemSunfireCapeAura()
        {
            BuffMetaData.BuffType = BuffType.DAMAGE;
        }
    }

    public class SheenDelay : NativeItemIndicator
    {
    }
}
