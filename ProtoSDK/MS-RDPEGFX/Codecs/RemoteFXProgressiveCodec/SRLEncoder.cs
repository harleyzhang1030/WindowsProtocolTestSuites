// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Microsoft.Protocols.TestTools.StackSdk.RemoteDesktop.Rdpegfx
{
    /// <summary>
    /// Simplified Run-Length (SRL) Eecoder 
    /// </summary>
    public class SRLEncoder
    {
        // Constants used within the RLGR1/RLGR3 algorithm

        const int KPMAX = 80;  // max value for kp or krp
        const int LSGR = 3;  // shift count to convert kp to k
        const int UP_GR = 4;  // increase in kp after a zero run in RL mode
        const int DN_GR = 6;  // decrease in kp after a nonzero symbol in RL mode
        const int UQ_GR = 3;   // increase in kp after nonzero symbol in GR mode
        const int DQ_GR = 3;   // decrease in kp after zero symbol in GR mode

        short[] inputData;
        int[] InputDataBitLen;
        int nextInputIdx;
        int bufferOffset = 0;
        byte[] pBuffer;

        /// <summary>
        /// Do ALGR encode to the input data.
        /// </summary>
        /// <param name="inputArr">Input data to be encoded.</param>
        /// <param name="mode">The ALGR mode, can be RLGR1 or RLGR3.</param>
        /// <returns>The encoded data.</returns>
        public byte[] Encode(short[] inputArr, int[] bitLenArr)
        {
            inputData = inputArr;
            InputDataBitLen = bitLenArr;
            nextInputIdx = 0;
            bufferOffset = 0; //offset&0xFFFFFFF8 = byte offset, offset&0x7 = bit offset
            pBuffer = new byte[inputArr.Length];
            //bitStrm = new BitStream();

            SRL_Encode();


            int numbytes = bufferOffset >> 3;
            int bitOffset = bufferOffset & 7;
            if (bitOffset != 0) numbytes++;

            byte[] encodedBytes = new byte[numbytes];
            Array.Copy(pBuffer, encodedBytes, encodedBytes.Length);
            return encodedBytes;

            //return bitStrm.ToBytes();
        }

        //
        // Returns the next coefficient (a signed int) to encode, from the input stream
        //
        int GetNextInput(out int bitLen)
        {
            bitLen = InputDataBitLen[nextInputIdx];
            return (int)inputData[nextInputIdx++];
        }

        bool hasMoreData()
        {
            return (nextInputIdx <= inputData.Length - 1);
        }

        //
        // Emit bitPattern to the output bitstream.
        // The bitPattern value represents a bit sequence that is generated by shifting 
        // new bits in from the right. If we take the binary representation of bitPattern, 
        // with N(numBits-1) being the leftmost bit position and 0 being the rightmost bit position, 
        // the mapping of bitPattern to the output bytes is as follows:
        //
        //     bitPattern[N..0] -> byte[MSB..LSB] .. byte[MSB..LSB]
        //
        public void OutputBits(
            int numBits,      // number of bits in bitPattern
            int bitPattern   // bit pattern
            )
        {
            int patternOffset = numBits - 1;

            while (patternOffset >= 0)
            {
                int bit = ((bitPattern & (1 << patternOffset)) != 0) ? 1 : 0;
                OutputBit(1, bit);
                patternOffset--;
            }

        }

        //
        // Emit a bit (0 or 1), count number of times, to the output bitstream
        //
        void OutputBit(
            int count,     // number of times to emit the bit
            int bit        // 0 or 1
            )
        {
            if (count == 0) return;

            //bitStrm.WriteBits(count, bit == 1 ? true : false);


            for (int i = 0; i < count; i++)
            {
                int bitOffset = bufferOffset & 7;
                int bufferBoundary = bufferOffset >> 3;
                if (bit != 0) // bit 1
                {
                    pBuffer[bufferBoundary] |= (byte)(1 << (7 - bitOffset));
                }
                else
                {
                    pBuffer[bufferBoundary] &= (byte)(0xFF - ((byte)(1 << (7 - bitOffset))));
                }
                bufferOffset++;
            }

        }

        //
        // Update the passed parameter and clamp it to the range [0,KPMAX]
        // Return the value of parameter right-shifted by LSGR
        //
        int UpdateParam(
            ref int param,    // parameter to update
            int deltaP    // update delta
            )
        {
            param += deltaP;
            if (param > KPMAX) param = KPMAX;
            if (param < 0) param = 0;
            return (param >> LSGR);
        }

        //
        // Routine that outputs a stream of RLGR1/RLGR3-encoded bits
        //
        void SRL_Encode(  
            )
        {
            // initialize the parameters
            int k = 1;
            int kp = 1 << LSGR;
            //int kr = 1;
            //int krp = 1 << LSGR;

            // process all the input coefficients
            while (hasMoreData())
            {
                int input;
                int bitLen;

                //if (k != 0)
                {
                    // RUN-LENGTH MODE

                    // collect the run of zeros in the input stream
                    int numZeros = 0;
                    while ((input = GetNextInput(out bitLen)) == 0)
                    {
                        ++numZeros;
                        if (!hasMoreData()) break;
                    }

                    // emit output zebros
                    int runmax = 1 << k;
                    while (numZeros >= runmax)
                    {
                        OutputBit(1, 0);             // output a zero bit
                        numZeros -= runmax;
                        k = UpdateParam(ref kp, UP_GR);  // update kp, k 
                        runmax = 1 << k;
                    }

                    // output a 1 to terminate runs
                    OutputBit(1, 1);

                    // output the remaining run length using k bits
                    OutputBits(k, numZeros);

                    k = UpdateParam(ref kp, -DN_GR);

                    if (input != 0)
                    {
                        // encode the nonzero value using Unary Encoding

                        int mag = Math.Abs(input);            // absolute value of input coefficient
                        int sign = (input < 0 ? 1 : 0);  // sign of input coefficient
                        int maxM = (1 << bitLen) - 1;

                        OutputBit(1, sign);      // output the sign bit

                        //a sequence of "magnitude - 1" zeros is written.
                        OutputBit(mag - 1, 0);

                        if (mag < maxM)
                        {
                            OutputBit(1, 1);
                        }
                    }
                }
            }
        }

    }
}
