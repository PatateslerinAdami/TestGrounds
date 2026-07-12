using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class ToolTipData
    {
        // Wire model (S2C_ToolTipVars 0x7F, decomp GameClient.cpp:1727): each tooltip slot carries
        // 16 RAW FLOATS + 16 hide-from-enemy bools — the client stores them verbatim as float
        // arrays (Spell::SpellDataInst::SetToolTipVars(const float*) / Buff::BuffManagerClient::
        // SetBuffToolTipVars(uchar, const float*) / SetHideToolTipsToEnemyVars(const bool*)).
        // There is NO per-value type on the wire: ints and bools are simply sent as their float
        // representation (bool → 1.0f/0.0f). A former per-value IsFloat flag here was copy-paste
        // from the Replication encoder (where it selects float-vs-varint) and was never consumed.
        public class ToolTipValue
        {
            public bool Hide { get; set; } = false;
            public float Value { get; set; } = 0.0f;
            public bool Changed { get; set; } = false;

            public ToolTipValue() { }
        }

        private readonly AttackableUnit Owner;

        public uint NetID => Owner.NetId;
        public byte Slot { get; private set; }
        // 16 ToolTip slots per spell.
        public ToolTipValue[] Values { get; private set; } = new ToolTipValue[16];
        public bool Changed { get; private set; }

        public ToolTipData(AttackableUnit owner, Spell spell, Buff buff = null)
        {
            Populate(Values);

            Owner = owner;

            if (spell != null)
            {
                // The slots seem to start at 60 for spell tooltips.
                Slot = (byte)(spell.CastInfo.SpellSlot + 60);
            }
            else if (buff != null)
            {
                // Slots start at 0 for buff tooltips.
                Slot = buff.Slot;
            }

            // NOTE Client behavior with higher slots: Slot > 120 => Slot - 120 => 120 > Slot > 59
            // End result is counted as a spell tooltip as slot is read from 60.
        }

        // Values[] is fully populated in the constructor, so entries are never null here.
        private void DoUpdate(float value, int primary, bool isHidden)
        {
            var slot = Values[primary];
            if (slot.Value != value || slot.Hide != isHidden)
            {
                slot.Hide = isHidden;
                slot.Value = value;
                slot.Changed = true;
                Changed = true;
            }
        }

        private void UpdateUint(uint value, int primary, bool hide)
        {
            DoUpdate(value, primary, hide);
        }

        private void UpdateInt(int value, int primary, bool hide)
        {
            DoUpdate(value, primary, hide);
        }

        private void UpdateBool(bool value, int primary, bool hide)
        {
            // bool → 1.0f/0.0f is the wire form: the client reads all 16 slots as raw floats
            // (see wire-model note on ToolTipValue).
            DoUpdate(value ? 1f : 0f, primary, hide);
        }

        private void UpdateFloat(float value, int primary, bool hide)
        {
            DoUpdate(value, primary, hide);
        }

        // Dirty-flag reset, called by Game.Update's per-tick bulk flush after this data was sent
        // in the tick's single S2C_ToolTipVars (Riot batches all units' changes into one packet).
        public void MarkAsUnchanged()
        {
            foreach (var x in Values)
            {
                if (x != null)
                {
                    x.Changed = false;
                }
            }

            Changed = false;
        }

        /// <param name="hide">Wire "hide from enemy" bool for this slot (client
        /// SetHideToolTipsToEnemyVars) — obfuscates the value on enemy clients.</param>
        public void Update<T>(int tipIndex, T value, bool hide = false) where T : struct
        {
            if (value is bool boolVal)
            {
                UpdateBool(boolVal, tipIndex, hide);
            }
            else if (value is int intVal)
            {
                UpdateInt(intVal, tipIndex, hide);
            }
            else if (value is uint uintVal)
            {
                UpdateUint(uintVal, tipIndex, hide);
            }
            else if (value is float floatVal)
            {
                UpdateFloat(floatVal, tipIndex, hide);
            }
        }

        public void SetSlot(byte slot)
        {
            Slot = slot;
        }

        private static void Populate(ToolTipValue[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = new ToolTipValue();
            }
        }
    }
}
