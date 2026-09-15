using System;

namespace AetherXIV.Core.Common
{
    /// <summary>
    /// Fixed-size bit field used by the legacy quest availability contract.
    /// Bits are addressed least-significant-bit first within each byte.
    /// </summary>
    public sealed class Bitstream
    {
        private readonly byte[] data;

        public Bitstream(uint numBits, bool setAllTrue = false)
        {
            if (numBits == 0 || numBits % 8 != 0)
                throw new ArgumentOutOfRangeException(nameof(numBits));

            data = new byte[numBits / 8];
            if (setAllTrue)
                SetAll(true);
        }

        private Bitstream(byte[] bytes)
        {
            data = bytes;
        }

        public void SetAll(bool value)
        {
            Array.Fill(data, value ? (byte)0xFF : (byte)0x00);
        }

        public void SetTo(Bitstream other)
        {
            EnsureSameSize(other);
            Array.Copy(other.data, data, data.Length);
        }

        public void SetTo(bool[] values)
        {
            if (values == null || values.Length != data.Length * 8)
                throw new ArgumentException("Boolean field size does not match the bitstream.", nameof(values));

            SetAll(false);
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index])
                    Set(index);
            }
        }

        public bool Get(uint index) => Get(checked((int)index));

        public bool Get(int index)
        {
            EnsureIndex(index);
            return (data[index / 8] & (1 << (index % 8))) != 0;
        }

        public void Set(uint index) => Set(checked((int)index));

        public void Set(int index)
        {
            EnsureIndex(index);
            data[index / 8] |= (byte)(1 << (index % 8));
        }

        public void Clear(uint index) => Clear(checked((int)index));

        public void Clear(int index)
        {
            EnsureIndex(index);
            data[index / 8] &= (byte)~(1 << (index % 8));
        }

        public void NOT()
        {
            for (int index = 0; index < data.Length; index++)
                data[index] = (byte)~data[index];
        }

        public void OR(Bitstream other)
        {
            EnsureSameSize(other);
            for (int index = 0; index < data.Length; index++)
                data[index] |= other.data[index];
        }

        public void NOTOR(Bitstream other)
        {
            EnsureSameSize(other);
            for (int index = 0; index < data.Length; index++)
                data[index] = (byte)~(data[index] | other.data[index]);
        }

        public void AND(Bitstream other)
        {
            EnsureSameSize(other);
            for (int index = 0; index < data.Length; index++)
                data[index] &= other.data[index];
        }

        public void XOR(Bitstream other)
        {
            EnsureSameSize(other);
            for (int index = 0; index < data.Length; index++)
                data[index] ^= other.data[index];
        }

        public Bitstream Copy()
        {
            byte[] copy = new byte[data.Length];
            Array.Copy(data, copy, data.Length);
            return new Bitstream(copy);
        }

        public byte[] GetBytes()
        {
            byte[] copy = new byte[data.Length];
            Array.Copy(data, copy, data.Length);
            return copy;
        }

        public byte[] GetSlice(ushort from, ushort to)
        {
            if (from > to || to >= data.Length * 8)
                throw new ArgumentOutOfRangeException(nameof(to));

            int bitCount = to - from + 1;
            byte[] result = new byte[(bitCount + 7) / 8 + 1];
            for (int offset = 0; offset < bitCount; offset++)
            {
                if (Get(from + offset))
                    result[offset / 8] |= (byte)(1 << (offset % 8));
            }

            result[result.Length - 1] = 0x03;
            return result;
        }

        private void EnsureIndex(int index)
        {
            if (index < 0 || index >= data.Length * 8)
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        private void EnsureSameSize(Bitstream other)
        {
            if (other == null || other.data.Length != data.Length)
                throw new ArgumentException("Bitstream sizes do not match.", nameof(other));
        }
    }
}
