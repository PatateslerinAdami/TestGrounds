
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public class CHAR_SetCooldown : GamePacket // 0x85
    {
        public override GamePacketID ID => GamePacketID.CHAR_SetCooldown;
        public byte Slot { get; set; }
        public bool PlayVOWhenCooldownReady { get; set; }

        /// <summary>
        /// Wire bit 0x02 of the bitfield byte. Originally designed for Summoner Spell (D/F) cooldown
        /// broadcasts — its dual semantic effect is:
        /// <list type="bullet">
        /// <item><b>Server-side scope</b>: when <c>true</c>, Riot's server broadcasts the packet to
        ///   all teammates with vision (so allies see your Flash/Heal/Cleanse cooldown via the
        ///   tab/HUD); when <c>false</c>, it goes per-target via <c>SendPacket(ownerClientId)</c>
        ///   only. This is a server-side policy decision based on the flag, not part of the wire
        ///   format itself.</item>
        /// <item><b>Client-side spellbook routing</b>: per S4 decomp
        ///   <c>SpellbookRouter::SetTimeForCooldown</c> (`spells/SpellbookRouter.cpp:2516-2551`),
        ///   when <c>true</c> the slot indexes into the alternate spellbook obtained via
        ///   <c>caster->vptr[0xff]()</c> (= <c>GetAvatar()</c>); when <c>false</c> it indexes into
        ///   the regular <c>caster->mSpellbook</c>. Both spellbooks have 52 slots.</item>
        /// </list>
        ///
        /// <para><b>4.x reuse pattern (Katarina Voracity):</b> Riot's server reuses this flag to
        /// piggyback non-summoner-spell broadcast cooldowns. On Katarina champion-kills, the server
        /// emits <c>slot=3, IsSummonerSpell=true, cd=R_remaining, max=-1</c> to broadcast Kat's R
        /// cooldown change to all teammates (icon-refresh visual + HUD update). On her assists it
        /// uses <c>IsSummonerSpell=false</c> for per-target only — Q/W/E (slot=0/1/2) always use
        /// <c>false</c> since allies don't render her ability cooldowns. So in this packet
        /// specifically, treat the field as "broadcast-scope flag" rather than literally
        /// "is-this-a-summoner-spell".</para>
        ///
        /// <para>Other packets (<c>ChangeSlotSpellData</c>, <c>NPC_InstantStop_Attack</c>, etc.)
        /// have their own <c>IsSummonerSpell</c> fields that are NOT subject to this reuse — they
        /// genuinely mean "this spell is a summoner spell". Don't extrapolate the Voracity dual
        /// semantics to those packets.</para>
        /// </summary>
        public bool IsSummonerSpell { get; set; }

        public float Cooldown { get; set; }
        public float MaxCooldownForDisplay { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            this.Slot = reader.ReadByte();

            byte bitfield = reader.ReadByte();
            this.PlayVOWhenCooldownReady = (bitfield & 0x01) != 0;
            this.IsSummonerSpell = (bitfield & 0x02) != 0;

            this.Cooldown = reader.ReadFloat();
            this.MaxCooldownForDisplay = reader.ReadFloat();
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteByte(Slot);

            byte bitfield = 0;
            if (PlayVOWhenCooldownReady)
                bitfield |= 0x01;
            if (IsSummonerSpell)
                bitfield |= 0x02;
            writer.WriteByte(bitfield);

            writer.WriteFloat(Cooldown);
            writer.WriteFloat(MaxCooldownForDisplay);
        }
    }
}
