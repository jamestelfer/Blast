using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Utils {
    public class BlastException : Exception {
        public const string OutOfInputMessage = "Ran out of input before completing decompression";
        public const string OutputMessage = "Output error before completing decompression";
        public const string LiteralFlagMessage = "Literal flag not zero or one";
        public const string DictionarySizeMessage = "Dictionary size not in 4..6";
        public const string DistanceMessage = "Distance is too far back";

        public BlastException() : base() { }
        public BlastException(string message) : base(message) { }
        public BlastException(string message, Exception inner) : base(message, inner) { }
    }

    public class Blast {
        public const int MAXBITS = 13;
        public const int MAXWIN = 4096;

        // bit lengths of literal codes 
        private static readonly byte[] LITERAL_BIT_LENGTHS = { 
            11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23, 8,
            9, 7, 6, 7, 8, 7, 6, 55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7, 22, 5,
            7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7, 9, 8, 25, 11, 8, 11, 9, 12,
            8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24, 10, 27,
            44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61, 12, 45,
            44, 173};

        /// <summary>
        /// bit lengths of length codes 0..15
        /// </summary> 
        private static readonly byte[] LENGTH_BIT_LENGTHS = { 2, 35, 36, 53, 38, 23 };

        /// <summary>
        ///  bit lengths of distance codes 0..63
        /// </summary>
        private static readonly byte[] DISTANCE_BIT_LENGTHS = { 2, 20, 53, 230, 247, 151, 248 };

        /// <summary>
        /// base for length codes
        /// </summary>
        private static readonly short[] LENGTH_CODE_BASE = { 3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264 };

        /// <summary>
        /// extra bits for length codes
        /// </summary>
        private static readonly byte[] LENGTH_CODE_EXTRA = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };

        private static readonly HuffmanTable LITERAL_CODE = new HuffmanTable(256, LITERAL_BIT_LENGTHS);
        private static readonly HuffmanTable LENGTH_CODE = new HuffmanTable(16, LENGTH_BIT_LENGTHS);
        private static readonly HuffmanTable DISTANCE_CODE = new HuffmanTable(64, DISTANCE_BIT_LENGTHS);

        private BlastState state;

        /// <summary>
        /// <para>Decompress input to output using the provided infun() and outfun() calls.
        /// On success, the return value of blast() is zero.  If there is an error in
        /// the source data, i.e. it is not in the proper format, then a negative value
        /// is returned.  If there is not enough input available or there is not enough
        /// output space, then a positive error is returned.</para>
        /// 
        /// <para>The input function is invoked: len = infun(how, &buf), where buf is set by
        /// infun() to point to the input buffer, and infun() returns the number of
        /// available bytes there.  If infun() returns zero, then blast() returns with
        /// an input error.  (blast() only asks for input if it needs it.)  inhow is for
        /// use by the application to pass an input descriptor to infun(), if desired.</para>
        /// 
        /// <para>The output function is invoked: err = outfun(how, buf, len), where the bytes
        /// to be written are buf[0..len-1].  If err is not zero, then blast() returns
        /// with an output error.  outfun() is always called with len &lt;= 4096.  outhow
        /// is for use by the application to pass an output descriptor to outfun(), if
        /// desired.</para>
        /// 
        /// <para>The return codes are:</para>
        /// 
        ///   2:  ran out of input before completing decompression
        ///   1:  output error before completing decompression
        ///   0:  successful decompression
        ///  -1:  literal flag not zero or one
        ///  -2:  dictionary size not in 4..6
        ///  -3:  distance is too far back
        /// 
        /// <para>At the bottom of blast.c is an example program that uses blast() that can be
        /// compiled to produce a command-line decompression filter by defining TEST.</para>
        /// </summary>
        public Blast(Stream inputStream, Stream outputStream) {
            state = new BlastState() {
                inputStream = inputStream,
                left = 0,
                bitbuf = 0,
                bitcnt = 0,
                outputStream = outputStream,
                next = 0,
                first = 1
            };
        }

        /// <summary>
        /// Decode PKWare Compression Library stream.
        /// 
        /// Format notes:
        /// 
        /// - First byte is 0 if literals are uncoded or 1 if they are coded.  Second
        ///   byte is 4, 5, or 6 for the number of extra bits in the distance code.
        ///   This is the base-2 logarithm of the dictionary size minus six.
        /// 
        /// - Compressed data is a combination of literals and length/distance pairs
        ///   terminated by an end code.  Literals are either Huffman coded or
        ///   uncoded bytes.  A length/distance pair is a coded length followed by a
        ///   coded distance to represent a string that occurs earlier in the
        ///   uncompressed data that occurs again at the current location.
        /// 
        /// - A bit preceding a literal or length/distance pair indicates which comes
        ///   next, 0 for literals, 1 for length/distance.
        /// 
        /// - If literals are uncoded, then the next eight bits are the literal, in the
        ///   normal bit order in th stream, i.e. no bit-reversal is needed. Similarly,
        ///   no bit reversal is needed for either the length extra bits or the distance
        ///   extra bits.
        /// 
        /// - Literal bytes are simply written to the output.  A length/distance pair is
        ///   an instruction to copy previously uncompressed bytes to the output.  The
        ///   copy is from distance bytes back in the output stream, copying for length
        ///   bytes.
        /// 
        /// - Distances pointing before the beginning of the output data are not
        ///   permitted.
        /// 
        /// - Overlapped copies, where the length is greater than the distance, are
        ///   allowed and common.  For example, a distance of one and a length of 518
        ///   simply copies the last byte 518 times.  A distance of four and a length of
        ///   twelve copies the last four bytes three times.  A simple forward copy
        ///   ignoring whether the length is greater than the distance or not implements
        ///   this correctly.
        /// </summary>
        public void Decompress() {
            int codedLiteral;            // true if literals are coded 
            int dictSize;           // log2(dictionary size) - 6 
            int symbol;         // decoded symbol, extra bits for distance 
            int copyLength;            // length for copy 
            int copyDist;           // distance for copy 
            int copyCount;           // copy counter 

            int fromIndex;
            int toIndex;

            // read header 
            codedLiteral = bits(8);
            if (codedLiteral > 1) {
                throw new BlastException(BlastException.LiteralFlagMessage);
            }

            dictSize = bits(8);

            if (dictSize < 4 || dictSize > 6) {
                throw new BlastException(BlastException.DictionarySizeMessage);
            }

            // decode literals and length/distance pairs 
            do {
                if (bits(1) > 0) { // 0 == literal, 1 == length+distance
                    // get length 
                    symbol = decode(LENGTH_CODE);
                    copyLength = LENGTH_CODE_BASE[symbol] + bits(LENGTH_CODE_EXTRA[symbol]);

                    if (copyLength == 519) { // end code
                        break;
                    }

                    // get distance 
                    symbol = copyLength == 2 ? 2 : dictSize;
                    copyDist = decode(DISTANCE_CODE) << symbol;
                    copyDist += bits(symbol);
                    copyDist++;

                    if (state.first > 0 && copyDist > state.next) {
                        throw new BlastException(BlastException.DistanceMessage);
                    }

                    // copy length bytes from distance bytes back 
                    do {
                        toIndex = state.next;
                        fromIndex = toIndex - copyDist;
                        copyCount = MAXWIN;

                        if (state.next < copyDist) {
                            fromIndex += copyCount;
                            copyCount = copyDist;
                        }
                        copyCount -= state.next;

                        if (copyCount > copyLength) {
                            copyCount = copyLength;
                        }

                        copyLength -= copyCount;
                        state.next += copyCount;

                        Buffer.BlockCopy(state.outputBuffer, fromIndex, state.outputBuffer, toIndex, copyCount);

                        if (state.next == MAXWIN) {
                            state.outputStream.Write(state.outputBuffer, 0, state.next);

                            state.next = 0;
                            state.first = 0;
                        }
                    } while (copyLength != 0);
                } else {
                    // get literal and write it 
                    symbol = codedLiteral != 0 ? decode(LITERAL_CODE) : bits(8);
                    state.outputBuffer[state.next++] = (byte)symbol;
                    if (state.next == MAXWIN) {
                        state.outputStream.Write(state.outputBuffer, 0, state.next);

                        state.next = 0;
                        state.first = 0;
                    }
                }
            } while (true);

            // write remaining bytes
            if (state.next > 0) {
                state.outputStream.Write(state.outputBuffer, 0, state.next);
            }
        }

        /*
         * Decode a code from the stream s using huffman table h.  Return the symbol or
         * a negative value if there is an error.  If all of the lengths are zero, i.e.
         * an empty code, or if the code is incomplete and an invalid code is received,
         * then -9 is returned after reading MAXBITS bits.
         *
         * Format notes:
         *
         * - The codes as stored in the compressed data are bit-reversed relative to
         *   a simple integer ordering of codes of the same lengths.  Hence below the
         *   bits are pulled from the compressed data one at a time and used to
         *   build the code value reversed from what is in the stream in order to
         *   permit simple integer comparisons for decoding.
         *
         * - The first code for the shortest length is all ones.  Subsequent codes of
         *   the same length are simply integer decrements of the previous code.  When
         *   moving up a length, a one bit is appended to the code.  For a complete
         *   code, the last code of the longest length will be all zeros.  To support
         *   this ordering, the bits pulled during decoding are inverted to apply the
         *   more "natural" ordering starting with all zeros and incrementing.
         */
        private int decode(HuffmanTable h) {
            int len = 1;            // current number of bits in code 
            int code = 0;           // len bits being decoded 
            int first = 0;          // first code of length len 
            int count;          // number of codes of length len 
            int index = 0;          // index of first code of length len in symbol table 
            int bitbuf;         // bits from stream 
            int left;           // bits left in next or left to process 
            int next = 1;           // next number of codes 

            bitbuf = state.bitbuf;
            left = state.bitcnt;

            while (true) {
                while (left-- > 0) {
                    code |= (bitbuf & 1) ^ 1;
                    bitbuf >>= 1;
                    count = h.count[next++];
                    if (code < first + count) {
                        state.bitbuf = bitbuf;
                        state.bitcnt = (state.bitcnt - len) & 7;

                        return h.symbol[index + (code - first)];
                    }
                    index += count;
                    first += count;
                    first <<= 1;
                    code <<= 1;
                    len++;
                }
                left = (MAXBITS + 1) - len;

                if (left == 0)
                    break;
                
                if (state.left == 0) {
                    state.left = state.inputStream.Read(state.inputBuffer, 0, state.inputBuffer.Length);
                    if (state.left == 0) {
                        throw new BlastException(BlastException.OutOfInputMessage);
                    }
                }
                
                bitbuf = state.consume();
                if (left > 8)
                    left = 8;
            }

            return -9;
        }

        private int bits(int need) {
            int val;
            val = state.bitbuf;
            while (state.bitcnt < need) {
                if (state.left == 0) {
                    state.left = state.inputStream.Read(state.inputBuffer, 0, state.inputBuffer.Length);
                    if (state.left == 0)
                        throw new BlastException(BlastException.OutOfInputMessage);
                }
                val |= ((int)state.consume()) << state.bitcnt;
                state.bitcnt += 8;
            }
            state.bitbuf = val >> need;
            state.bitcnt -= need;
            return val & ((1 << need) - 1);
        }

        private class BlastState {
            public Stream inputStream;

            public byte[] inputBuffer = new byte[16384];
            public int inputBufferPos;
            public int left;
            public byte consume() {
                byte b = inputBuffer[inputBufferPos++];
                left--;
                return b;
            }

            public int bitbuf;
            public int bitcnt;

            public Stream outputStream;

            public int next;
            public int first;
            public byte[] outputBuffer = new byte[MAXWIN];
            public int outputBufferPos;
            public void writeBuffer(byte b) {
                inputBuffer[inputBufferPos++] = b;
            }
        }

        /*
         * Huffman code decoding tables.  count[1..MAXBITS] is the number of symbols of
         * each length, which for a canonical code are stepped through in order.
         * symbol[] are the symbol values in canonical order, where the number of
         * entries is the sum of the counts in count[].  The decoding process can be
         * seen in the function decode() below.
         */
        public class HuffmanTable {
            public readonly short[] count;
            public readonly short[] symbol;

            public HuffmanTable(int symbolSize, byte[] compacted) {
                 count = new short[MAXBITS + 1];
                 symbol = new short[symbolSize];

                 construct(compacted);
            }

            /*
             * Given a list of repeated code lengths rep[0..n-1], where each byte is a
             * count (high four bits + 1) and a code length (low four bits), generate the
             * list of code lengths.  This compaction reduces the size of the object code.
             * Then given the list of code lengths length[0..n-1] representing a canonical
             * Huffman code for n symbols, construct the tables required to decode those
             * codes.  Those tables are the number of codes of each length, and the symbols
             * sorted by length, retaining their original order within each length.  The
             * return value is zero for a complete code set, negative for an over-
             * subscribed code set, and positive for an incomplete code set.  The tables
             * can be used if the return value is zero or positive, but they cannot be used
             * if the return value is negative.  If the return value is zero, it is not
             * possible for decode() using that table to return an error--any stream of
             * enough bits will resolve to a symbol.  If the return value is positive, then
             * it is possible for decode() using that table to return an error for received
             * codes past the end of the incomplete lengths.
             */
            private int construct(byte[] rep) {
                short symbol;// current symbol when stepping through length[] 
                int len;// current length when stepping through h->count[] 
                int left;// number of possible codes left of current length 
                short[] offs = new short[MAXBITS + 1]; // offsets in symbol table for each length 
                short[] length = new short[256]; // code lengths 

                int n;

                // convert compact repeat counts into symbol bit length list 
                symbol = 0;
                for (int ri = 0; ri < rep.Length; ri++) {
                    len = rep[ri];
                    left = (len >> 4) + 1;
                    len &= 0xf;
                    do {
                        length[symbol++] = (short)len;
                    } while (--left > 0);
                }

                // count number of codes of each length 
                n = symbol;
                for (len = 0; len <= MAXBITS; len++)
                    this.count[len] = 0;

                for (symbol = 0; symbol < n; symbol++)
                    (this.count[length[symbol]])++;// assumes lengths are within bounds 
                
                if (this.count[0] == n)// no codes! 
                    return 0;   // complete, but decode() will fail 

                // check for an over-subscribed or incomplete set of lengths 
                left = 1; // one possible code of zero length 
                for (len = 1; len <= MAXBITS; len++) {
                    left <<= 1; // one more bit, double codes left 
                    left -= this.count[len]; // deduct count from possible codes 
                    if (left < 0)
                        return left; // over-subscribed--return negative 
                } // left > 0 means incomplete 

                // generate offsets into symbol table for each length for sorting 
                offs[1] = 0;

                for (len = 1; len < MAXBITS; len++) {
                    offs[len + 1] = (short)(offs[len] + this.count[len]);
                }

                // 
                // put symbols in table sorted by length, by symbol order within each
                // length
                // 
                for (symbol = 0; symbol < n; symbol++) {
                    if (length[symbol] != 0) {
                        this.symbol[offs[length[symbol]]++] = symbol;
                    }
                }

                // return zero for complete set, positive for incomplete set 
                return left;
            }
        }
    }

}