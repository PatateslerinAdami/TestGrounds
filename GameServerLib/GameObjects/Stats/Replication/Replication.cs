using System;
using System.Collections.Generic;
using GameServerCore.Enums;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public abstract class Replication
    {
        public class Replicate
        {
            public uint Value { get; set; }
            public bool IsFloat { get; set; }
            public bool Changed { get; set; }
        }

        protected Replication(AttackableUnit owner)
        {
            Owner = owner;
            Update();
        }

        protected readonly AttackableUnit Owner;
        protected Stats Stats => Owner.Stats;

        public uint NetID => Owner.NetId;
        public Replicate[,] Values { get; private set; } = new Replicate[6, 32];
        public bool Changed { get; private set; }

        private void DoUpdate(uint value, ReplicationBucket primary, int secondary, bool isFloat)
        {
            int p = (int)primary;
            if (Values[p, secondary] == null)
            {
                Values[p, secondary] = new Replicate
                {
                    Value = value,
                    IsFloat = isFloat,
                    Changed = true
                };
                Changed = true;
            }
            else if (Values[p, secondary].Value != value)
            {
                Values[p, secondary].IsFloat = isFloat;
                Values[p, secondary].Value = value;
                Values[p, secondary].Changed = true;
                Changed = true;
            }
        }

        protected void UpdateUint(uint value, ReplicationBucket primary, int secondary)
        {
            DoUpdate(value, primary, secondary, false);
        }

        protected void UpdateInt(int value, ReplicationBucket primary, int secondary)
        {
            DoUpdate((uint)value, primary, secondary, false);
        }

        protected void UpdateBool(bool value, ReplicationBucket primary, int secondary)
        {
            DoUpdate(value ? 1u : 0u, primary, secondary, false);
        }

        protected void UpdateFloat(float value, ReplicationBucket primary, int secondary)
        {
            DoUpdate(BitConverter.ToUInt32(BitConverter.GetBytes(value), 0), primary, secondary, true);
        }

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

        public abstract void Update();

        /// <summary>
        /// Serializes the replication state into wire form.
        /// </summary>
        /// <param name="partial">Only include vars marked Changed since the last <see cref="MarkAsUnchanged"/>.</param>
        /// <param name="includeOwnerOnly">When false, the <see cref="ReplicationBucket.ClientOnly"/> bucket
        /// (gold / cooldowns / mana costs) is omitted — used when the recipient is NOT the unit's owner, so
        /// owner-secret data never leaks to allies or enemies. Replay-verified scoping (only the owning
        /// player ever receives CLIENT_ONLY). See docs/REPLICATION_VISIBILITY_SCOPING_PLAN.md.</param>
        public ReplicationData GetData(bool partial = true, bool includeOwnerOnly = true)
        {
            var data = new ReplicationData(){
                UnitNetID = Owner.NetId
            };

            for (byte primaryId = 0; primaryId < 6; primaryId++)
            {
                if (!includeOwnerOnly && primaryId == (byte)ReplicationBucket.ClientOnly)
                {
                    continue;
                }
                uint secondaryIdArray = 0;
                List<byte> bytes = new List<byte>(8);
                for (byte secondaryId = 0; secondaryId < 32; secondaryId++)
                {
                    var rep = Values[primaryId, secondaryId];
                    if (rep != null && (!partial || rep.Changed))
                    {
                        secondaryIdArray |= 1u << secondaryId;

                        if (rep.IsFloat)
                        {
                            var source = BitConverter.GetBytes(rep.Value);

                            if (source[0] >= 0xFE)
                            {
                                bytes.Add((byte)0xFE);
                            }

                            bytes.AddRange(source);
                        }
                        else
                        {
                            var num = rep.Value;
                            while (num >= 0x80)
                            {
                                bytes.Add((byte)(num | 0x80));
                                num >>= 7;
                            }

                            bytes.Add((byte)num);
                        }
                    }
                }

                if(bytes.Count > 0)
                {
                    data.Data[primaryId] = new Tuple<uint, byte[]>(secondaryIdArray, bytes.ToArray());
                }
            }

            return data;
        }
    }
}