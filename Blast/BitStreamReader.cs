using System;
using System.IO;

namespace Blast
{
    public class BitStreamReader
    {
        private Stream _inputStream;

        private byte[] _inputBuffer = new byte[16384];
        private int _inputBufferPos = 0;
        private int _inputBufferRemaining = 0; // available input in buffer

        // FIXME these need to be private
        public int _bitBuffer = 0; // bit buffer 
        public int _bitBufferCount = 0; // number of bits in bit buffer 

        public BitStreamReader(Stream inputStream)
        {
            this._inputStream = inputStream;
        }

        /// <summary>
        /// Get the next <c>need</c> bits from the input stream.
        /// </summary>
        /// <returns>
        /// The requested number of bits.
        /// </returns>
        /// <param name='need'>
        /// The number of bits to read from the input stream.
        /// </param>
        public int GetBits(int need)
        {
            int val = _bitBuffer;

            while (_bitBufferCount < need)
            {
                val |= ((int)ConsumeByte()) << _bitBufferCount;
                _bitBufferCount += 8;
            }

            _bitBuffer = val >> need;
            _bitBufferCount -= need;

            return val & ((1 << need) - 1);
        }

        /// <summary>
        /// Flushes the internal bit buffer, so subsequent reads will pull from
        /// the input stream.
        /// </summary>
        public void Flush()
        {
            _bitBufferCount = 0;
        }

        public byte ConsumeByte()
        {
            if (_inputBufferRemaining == 0)
            {
                DoReadBuffer();

                if (_inputBufferRemaining == 0)
                {
                    throw new BlastException(BlastException.OutOfInputMessage);
                }
            }

            byte b = _inputBuffer[_inputBufferPos++];
            _inputBufferRemaining--;

            return b;
        }

        private void DoReadBuffer()
        {
            _inputBufferRemaining = _inputStream.Read(_inputBuffer, 0, _inputBuffer.Length);
            _inputBufferPos = 0;
        }

        /// <summary>
        /// Check for presence of more input without consuming it.
        /// May refill the input buffer.
        /// </summary>
        /// <returns></returns>
        public bool HasInput()
        {
            // is there any input in the buffer?
            if (_inputBufferRemaining > 0)
            {
                return true;
            }

            // try to fill it if not
            DoReadBuffer();

            // true if input now available
            return _inputBufferRemaining > 0;
        }

    }
}

