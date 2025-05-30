// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace System.Formats.Asn1
{
    public sealed partial class AsnWriter
    {
        /// <summary>
        ///   Write an Object Identifier with a specified tag.
        /// </summary>
        /// <param name="oidValue">The object identifier to write.</param>
        /// <param name="tag">The tag to write, or <see langword="null"/> for the default tag (Universal 6).</param>
        /// <exception cref="ArgumentException">
        ///   <paramref name="tag"/>.<see cref="Asn1Tag.TagClass"/> is
        ///   <see cref="TagClass.Universal"/>, but
        ///   <paramref name="tag"/>.<see cref="Asn1Tag.TagValue"/> is not correct for
        ///   the method.
        ///
        ///   -or-
        ///
        ///   <paramref name="oidValue"/> is not a valid dotted decimal
        ///   object identifier.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="oidValue"/> is <see langword="null"/>.
        /// </exception>
        public void WriteObjectIdentifier(string oidValue, Asn1Tag? tag = null)
        {
            ArgumentNullException.ThrowIfNull(oidValue);

            WriteObjectIdentifier(oidValue.AsSpan(), tag);
        }

        /// <summary>
        ///   Write an Object Identifier with a specified tag.
        /// </summary>
        /// <param name="oidValue">The object identifier to write.</param>
        /// <param name="tag">The tag to write, or <see langword="null"/> for the default tag (Universal 6).</param>
        /// <exception cref="ArgumentException">
        ///   <paramref name="tag"/>.<see cref="Asn1Tag.TagClass"/> is
        ///   <see cref="TagClass.Universal"/>, but
        ///   <paramref name="tag"/>.<see cref="Asn1Tag.TagValue"/> is not correct for
        ///   the method.
        ///
        ///   -or-
        ///
        ///   <paramref name="oidValue"/> is not a valid dotted decimal
        ///   object identifier.
        /// </exception>
        public void WriteObjectIdentifier(ReadOnlySpan<char> oidValue, Asn1Tag? tag = null)
        {
            CheckUniversalTag(tag, UniversalTagNumber.ObjectIdentifier);

#if NET
            ReadOnlySpan<byte> wellKnownContents = WellKnownOids.GetContents(oidValue);

            if (!wellKnownContents.IsEmpty)
            {
                WriteTag(tag?.AsPrimitive() ?? Asn1Tag.ObjectIdentifier);
                WriteLength(wellKnownContents.Length);
                wellKnownContents.CopyTo(_buffer.AsSpan(_offset));
                _offset += wellKnownContents.Length;
                return;
            }
#endif

            WriteObjectIdentifierCore(tag?.AsPrimitive() ?? Asn1Tag.ObjectIdentifier, oidValue);
        }

        // T-REC-X.690-201508 sec 8.19
        private void WriteObjectIdentifierCore(Asn1Tag tag, ReadOnlySpan<char> oidValue)
        {
            // T-REC-X.690-201508 sec 8.19.4
            // The first character is in { 0, 1, 2 }, the second will be a '.', and a third (digit)
            // will also exist.
            if (oidValue.Length < 3)
                throw new ArgumentException(SR.Argument_InvalidOidValue, nameof(oidValue));
            if (oidValue[1] != '.')
                throw new ArgumentException(SR.Argument_InvalidOidValue, nameof(oidValue));

            // The worst case is "1.1.1.1.1", which takes 4 bytes (5 components, with the first two condensed)
            // Longer numbers get smaller: "2.1.127" is only 2 bytes. (81d (0x51) and 127 (0x7F))
            // So length / 2 should prevent any reallocations.
            byte[] tmp = CryptoPool.Rent(oidValue.Length / 2);
            int tmpOffset = 0;

            try
            {
                int firstComponent = oidValue[0] switch
                {
                    '0' => 0,
                    '1' => 1,
                    '2' => 2,
                    _ => throw new ArgumentException(SR.Argument_InvalidOidValue, nameof(oidValue)),
                };

                // The first two components are special:
                // ITU X.690 8.19.4:
                //   The numerical value of the first subidentifier is derived from the values of the first two
                //   object identifier components in the object identifier value being encoded, using the formula:
                //       (X*40) + Y
                //   where X is the value of the first object identifier component and Y is the value of the
                //   second object identifier component.
                //       NOTE - This packing of the first two object identifier components recognizes that only
                //          three values are allocated from the root node, and at most 39 subsequent values from
                //          nodes reached by X = 0 and X = 1.

                // skip firstComponent and the trailing .
                ReadOnlySpan<char> remaining = oidValue.Slice(2);

                BigInteger subIdentifier = ParseSubIdentifier(ref remaining);

                if (firstComponent <= 1 && subIdentifier >= 40)
                {
                    throw new ArgumentException(SR.Argument_InvalidOidValue, nameof(oidValue));
                }

                subIdentifier += 40 * firstComponent;

                int localLen = EncodeSubIdentifier(tmp.AsSpan(tmpOffset), ref subIdentifier);
                tmpOffset += localLen;

                while (!remaining.IsEmpty)
                {
                    subIdentifier = ParseSubIdentifier(ref remaining);
                    localLen = EncodeSubIdentifier(tmp.AsSpan(tmpOffset), ref subIdentifier);
                    tmpOffset += localLen;
                }

                Debug.Assert(!tag.IsConstructed);
                WriteTag(tag);
                WriteLength(tmpOffset);
                Buffer.BlockCopy(tmp, 0, _buffer, _offset, tmpOffset);
                _offset += tmpOffset;
            }
            finally
            {
                CryptoPool.Return(tmp, tmpOffset);
            }
        }

        private static BigInteger ParseSubIdentifier(ref ReadOnlySpan<char> oidValue)
        {
            int endIndex = oidValue.IndexOf('.');

            if (endIndex == -1)
            {
                endIndex = oidValue.Length;
            }
            else if (endIndex == 0 || endIndex == oidValue.Length - 1)
            {
                throw new ArgumentException(SR.Argument_InvalidOidValue, nameof(oidValue));
            }

            BigInteger value = BigInteger.Zero;

            for (int position = 0; position < endIndex; position++)
            {
                if (position > 0 && value == 0)
                {
                    // T-REC X.680-201508 sec 12.26
                    throw new ArgumentException(SR.Argument_InvalidOidValue, nameof(oidValue));
                }

                value *= 10;
                value += AtoI(oidValue[position]);
            }
            oidValue = oidValue.Slice(Math.Min(oidValue.Length, endIndex + 1));
            return value;
        }

        private static int AtoI(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            throw new ArgumentException(SR.Argument_InvalidOidValue, "oidValue");
        }

        // ITU-T-X.690-201508 sec 8.19.5
        private static int EncodeSubIdentifier(Span<byte> dest, ref BigInteger subIdentifier)
        {
            Debug.Assert(dest.Length > 0);

            if (subIdentifier.IsZero)
            {
                dest[0] = 0;
                return 1;
            }

            BigInteger unencoded = subIdentifier;
            int idx = 0;

            do
            {
                BigInteger cur = unencoded & 0x7F;
                byte curByte = (byte)cur;

                if (subIdentifier != unencoded)
                {
                    curByte |= 0x80;
                }

                unencoded >>= 7;
                dest[idx] = curByte;
                idx++;
            }
            while (unencoded != BigInteger.Zero);

            dest.Slice(0, idx).Reverse();
            return idx;
        }
    }
}
