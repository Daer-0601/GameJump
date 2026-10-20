using Unity.Collections;
using Unity.Netcode;

namespace Gamejam2026
{
    public struct LanPose : INetworkSerializable
    {
        public float x, y, z, yaw, pitch;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);
            serializer.SerializeValue(ref yaw);
            serializer.SerializeValue(ref pitch);
        }
    }

    public struct LanMech : INetworkSerializable
    {
        public bool done, unstable;
        public float cooperation, hold;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref done);
            serializer.SerializeValue(ref unstable);
            serializer.SerializeValue(ref cooperation);
            serializer.SerializeValue(ref hold);
        }
    }

    public struct LanStationNet : INetworkSerializable
    {
        public float progress, secondsLeft;
        public byte flags;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref progress);
            serializer.SerializeValue(ref secondsLeft);
            serializer.SerializeValue(ref flags);
        }
    }

    public struct LanGrowthNet : INetworkSerializable
    {
        public ushort id;
        public float burn;
        public bool consumed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref id);
            serializer.SerializeValue(ref burn);
            serializer.SerializeValue(ref consumed);
        }
    }

    public struct LanInput : INetworkSerializable
    {
        public float moveX, moveY, lookX, lookY;
        public bool run, useHeld, reading;
        public ushort sequence;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref moveX);
            serializer.SerializeValue(ref moveY);
            serializer.SerializeValue(ref lookX);
            serializer.SerializeValue(ref lookY);
            serializer.SerializeValue(ref run);
            serializer.SerializeValue(ref useHeld);
            serializer.SerializeValue(ref reading);
            serializer.SerializeValue(ref sequence);
        }
    }

    // Estado caliente. Las frases largas viajan en RPC fiables solo cuando cambian.
    public struct LanSnapshot : INetworkSerializable
    {
        public const int MaxGrowth = 12;
        public byte version;
        public ushort sequence;
        public float danger, candle, containment, elapsed, disturbance;
        public bool containing, paused, finished, won;
        public FixedString128Bytes defeat;
        public byte completed, contacts, evidence;
        public bool contrasted, revealed;
        public sbyte altered;
        public bool bodyMoved, shadowOn, shadowMoving;
        public float sx, sy, sz, syaw;
        public LanPose p0, p1;
        public byte candleAnchor, crankAnchor;
        public float cax, cay, caz, cayaw, crx, cry, crz, cryaw;
        public LanMech m0, m1, m2;
        public LanStationNet s0, s1, s2, s3, s4, s5;
        public byte growthCount;
        public LanGrowthNet g0, g1, g2, g3, g4, g5, g6, g7, g8, g9, g10, g11;
        public FixedString512Bytes hint;
        public byte focusMech, focusStation;
        public bool working, stabilizing, needsHand;

        public LanGrowthNet GrowthAt(int index)
        {
            switch (index)
            {
                case 0: return g0;
                case 1: return g1;
                case 2: return g2;
                case 3: return g3;
                case 4: return g4;
                case 5: return g5;
                case 6: return g6;
                case 7: return g7;
                case 8: return g8;
                case 9: return g9;
                case 10: return g10;
                default: return g11;
            }
        }

        public void SetGrowth(int index, LanGrowthNet value)
        {
            switch (index)
            {
                case 0: g0 = value; break;
                case 1: g1 = value; break;
                case 2: g2 = value; break;
                case 3: g3 = value; break;
                case 4: g4 = value; break;
                case 5: g5 = value; break;
                case 6: g6 = value; break;
                case 7: g7 = value; break;
                case 8: g8 = value; break;
                case 9: g9 = value; break;
                case 10: g10 = value; break;
                default: g11 = value; break;
            }
        }

        public LanStationNet StationAt(int index)
        {
            switch (index)
            {
                case 0: return s0;
                case 1: return s1;
                case 2: return s2;
                case 3: return s3;
                case 4: return s4;
                default: return s5;
            }
        }

        public void SetStation(int index, LanStationNet value)
        {
            switch (index)
            {
                case 0: s0 = value; break;
                case 1: s1 = value; break;
                case 2: s2 = value; break;
                case 3: s3 = value; break;
                case 4: s4 = value; break;
                default: s5 = value; break;
            }
        }

        public LanMech MechAt(int index)
        {
            if (index == 0) return m0;
            return index == 1 ? m1 : m2;
        }

        public void SetMech(int index, LanMech value)
        {
            if (index == 0) m0 = value;
            else if (index == 1) m1 = value;
            else m2 = value;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref version);
            serializer.SerializeValue(ref sequence);
            serializer.SerializeValue(ref danger);
            serializer.SerializeValue(ref candle);
            serializer.SerializeValue(ref containment);
            serializer.SerializeValue(ref elapsed);
            serializer.SerializeValue(ref disturbance);
            serializer.SerializeValue(ref containing);
            serializer.SerializeValue(ref paused);
            serializer.SerializeValue(ref finished);
            serializer.SerializeValue(ref won);
            serializer.SerializeValue(ref defeat);
            serializer.SerializeValue(ref completed);
            serializer.SerializeValue(ref contacts);
            serializer.SerializeValue(ref evidence);
            serializer.SerializeValue(ref contrasted);
            serializer.SerializeValue(ref revealed);
            serializer.SerializeValue(ref altered);
            serializer.SerializeValue(ref bodyMoved);
            serializer.SerializeValue(ref shadowOn);
            serializer.SerializeValue(ref shadowMoving);
            serializer.SerializeValue(ref sx);
            serializer.SerializeValue(ref sy);
            serializer.SerializeValue(ref sz);
            serializer.SerializeValue(ref syaw);
            p0.NetworkSerialize(serializer);
            p1.NetworkSerialize(serializer);
            serializer.SerializeValue(ref candleAnchor);
            serializer.SerializeValue(ref crankAnchor);
            serializer.SerializeValue(ref cax);
            serializer.SerializeValue(ref cay);
            serializer.SerializeValue(ref caz);
            serializer.SerializeValue(ref cayaw);
            serializer.SerializeValue(ref crx);
            serializer.SerializeValue(ref cry);
            serializer.SerializeValue(ref crz);
            serializer.SerializeValue(ref cryaw);
            m0.NetworkSerialize(serializer);
            m1.NetworkSerialize(serializer);
            m2.NetworkSerialize(serializer);
            s0.NetworkSerialize(serializer);
            s1.NetworkSerialize(serializer);
            s2.NetworkSerialize(serializer);
            s3.NetworkSerialize(serializer);
            s4.NetworkSerialize(serializer);
            s5.NetworkSerialize(serializer);
            serializer.SerializeValue(ref growthCount);
            int count = growthCount;
            if (count > MaxGrowth) count = MaxGrowth;
            for (int i = 0; i < count; i++)
            {
                LanGrowthNet growth = GrowthAt(i);
                growth.NetworkSerialize(serializer);
                if (serializer.IsReader) SetGrowth(i, growth);
            }
            if (serializer.IsReader) growthCount = (byte)count;
            serializer.SerializeValue(ref hint);
            serializer.SerializeValue(ref focusMech);
            serializer.SerializeValue(ref focusStation);
            serializer.SerializeValue(ref working);
            serializer.SerializeValue(ref stabilizing);
            serializer.SerializeValue(ref needsHand);
        }
    }
}
