using System;
using System.Collections.Generic;
using System.IO;


namespace LeaguePackets.Game.Common
{
    public abstract class ChangeSpellData
    {
        public abstract ChangeSlotSpellDataType ChangeSlotSpellDataType { get; }
        public byte SpellSlot { get; set; }
        public bool IsSummonerSpell { get; set; }
        /// <summary>
        /// Remaining bitfield bits beyond IsSummonerSpell (bit 0). DECODED 2026-07-05 from the
        /// 4.20 client exe (handler at 0x65bc10): the client does `and al, 0x1` — it reads ONLY
        /// bit 0; all other bits are IGNORED. Riot's wire still carries nonzero garbage here
        /// (Sion W replay: 0x6E arming the override spell, 0x0E restoring — presumably
        /// uninitialized server-side struct bytes, same phenomenon as the AnimationFlags
        /// Junk5-7 bits). We replicate the observed values purely for byte-exact wirecompare;
        /// they have zero client effect.
        /// </summary>
        public byte ExtraFlags { get; set; }

        public abstract void ReadBodyInternal(ByteReader reader);
        public abstract void WriteBodyInternal(ByteWriter writer);
    }

    public static class ChangeSpellDataExtension
    {
        public static ChangeSpellData ReadChangeSpellData(this ByteReader reader)
        {
            ChangeSpellData data;

            byte spellSlot = reader.ReadByte();

            byte bitfield = reader.ReadByte();
            bool isSummonerSpell = (bitfield & 0x01) != 0;
            byte extraFlags = (byte)(bitfield & ~0x01);

            ChangeSlotSpellDataType type = (ChangeSlotSpellDataType)reader.ReadUInt32();
            switch (type)
            {
                case ChangeSlotSpellDataType.TargetingType:
                    data = new ChangeSpellDataTargetingType();
                    break;
                case ChangeSlotSpellDataType.SpellName:
                    data = new ChangeSpellDataSpellName();
                    break;
                case ChangeSlotSpellDataType.Range:
                    data = new ChangeSpellDataRange();
                    break;
                case ChangeSlotSpellDataType.MaxGrowthRange:
                    data = new ChangeSpellDataMaxGrowthRange();
                    break;
                case ChangeSlotSpellDataType.RangeDisplay:
                    data = new ChangeSpellDataRangeDisplay();
                    break;
                case ChangeSlotSpellDataType.IconIndex:
                    data = new ChangeSpellDataIconIndex();
                    break;
                case ChangeSlotSpellDataType.OffsetTarget:
                    data = new ChangeSpellDataOffsetTarget();
                    break;
                default:
                    data = new ChangeSpellDataUnknown();
                    break;
            }

            data.SpellSlot = spellSlot;
            data.IsSummonerSpell = isSummonerSpell;
            data.ExtraFlags = extraFlags;
            data.ReadBodyInternal(reader);
            return data;
        }
        public static void WriteChangeSpellData(this ByteWriter writer, ChangeSpellData data)
        {
            writer.WriteByte(data.SpellSlot);

            byte bitfield = (byte)(data.ExtraFlags & ~0x01);
            if (data.IsSummonerSpell)
                bitfield |= 0x01;

            writer.WriteByte(bitfield);
            writer.WriteUInt32((uint)data.ChangeSlotSpellDataType);
            data.WriteBodyInternal(writer);
        }
    }

    public class ChangeSpellDataUnknown : ChangeSpellData
    {
        private ChangeSlotSpellDataType _changeSlotSpellDataType;
        public ChangeSpellDataUnknown() { }
        public ChangeSpellDataUnknown(ChangeSlotSpellDataType type) => _changeSlotSpellDataType = type;
        public ChangeSlotSpellDataType  ChangeSlotSpellDataTypeRaw
        {
            get  => _changeSlotSpellDataType;
            set => _changeSlotSpellDataType = value;
        }
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => _changeSlotSpellDataType;
        public override void ReadBodyInternal(ByteReader reader) { }
        public override void WriteBodyInternal(ByteWriter writer) { }
    }

    public class ChangeSpellDataTargetingType : ChangeSpellData
    {
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => ChangeSlotSpellDataType.TargetingType;
        public byte TargetingType { get; set; }
        public override void ReadBodyInternal(ByteReader reader)
        {
            TargetingType = reader.ReadByte();
        }
        public override void WriteBodyInternal(ByteWriter writer)
        {
            writer.WriteByte(TargetingType);
        }
    }

    public class ChangeSpellDataSpellName : ChangeSpellData
    {
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => ChangeSlotSpellDataType.SpellName;
        public string SpellName { get; set; } = "";
        public override void ReadBodyInternal(ByteReader reader)
        {
            SpellName = reader.ReadFixedStringLast(128);
        }
        public override void WriteBodyInternal(ByteWriter writer)
        {
            // Riot sends the name VARIABLE-length + single null terminator (replay: Sion W swap
            // packets are 25/17 bytes total), not padded to 128. The client reads a C-string.
            var bytes = System.Text.Encoding.UTF8.GetBytes(SpellName ?? "");
            writer.WriteBytes(bytes);
            writer.WriteByte(0);
        }
    }

    public class ChangeSpellDataRange : ChangeSpellData
    {
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => ChangeSlotSpellDataType.Range;
        public float CastRange { get; set; }
        public override void ReadBodyInternal(ByteReader reader)
        {
            CastRange = reader.ReadFloat();
        }
        public override void WriteBodyInternal(ByteWriter writer)
        {
            writer.WriteFloat(CastRange);
        }
    }

    public class ChangeSpellDataMaxGrowthRange : ChangeSpellData
    {
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => ChangeSlotSpellDataType.MaxGrowthRange;
        public float OverrideMaxCastRange { get; set; }
        public override void ReadBodyInternal(ByteReader reader)
        {
            OverrideMaxCastRange = reader.ReadFloat();
        }
        public override void WriteBodyInternal(ByteWriter writer)
        {
            writer.WriteFloat(OverrideMaxCastRange);
        }
    }

    public class ChangeSpellDataRangeDisplay : ChangeSpellData
    {
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => ChangeSlotSpellDataType.RangeDisplay;
        public float OverrideCastRangeDisplay { get; set; }
        public override void ReadBodyInternal(ByteReader reader)
        {
            OverrideCastRangeDisplay = reader.ReadFloat();
        }
        public override void WriteBodyInternal(ByteWriter writer)
        {
            writer.WriteFloat(OverrideCastRangeDisplay);
        }
    }

    public class ChangeSpellDataIconIndex : ChangeSpellData
    {
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => ChangeSlotSpellDataType.IconIndex;
        public byte IconIndex { get; set; }
        public override void ReadBodyInternal(ByteReader reader)
        {
            IconIndex = reader.ReadByte();
        }
        public override void WriteBodyInternal(ByteWriter writer)
        {
            writer.WriteByte(IconIndex);
        }
    }

    public class ChangeSpellDataOffsetTarget : ChangeSpellData
    {
        public override ChangeSlotSpellDataType ChangeSlotSpellDataType => ChangeSlotSpellDataType.OffsetTarget;
        public List<uint> Targets { get; set; } = new List<uint>();
        public override void ReadBodyInternal(ByteReader reader)
        {
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                Targets.Add(reader.ReadUInt32());
            }
        }
        public override void WriteBodyInternal(ByteWriter writer)
        {
            var count = Targets.Count;
            if (count > 0xFF)
            {
                throw new IOException("Too many targets!");
            }
            writer.WriteByte((byte)count);
            for (var i = 0; i < count; i++)
            {
                writer.WriteUInt32(Targets[i]);
            }
        }
    }

}
