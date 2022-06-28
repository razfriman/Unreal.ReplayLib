using Unreal.ReplayLib.Exceptions;

namespace Unreal.ReplayLib.Compression.OozSharp
{
    public unsafe class Kracken
    {
        public void Decompress(byte* compressedInput, int compressedSize, byte* uncompressed, int uncompressedSize)
        {
            using var decoder = new KrakenDecoder();

            var remainingBytes = uncompressedSize;
            var sourceLength = compressedSize;
            var destinationOffset = 0;

            var sourceStart = compressedInput;
            var decompressBufferStart = uncompressed;

            while (remainingBytes != 0)
            {
                if (!DecodeStep(decoder, uncompressed, destinationOffset, remainingBytes, sourceStart, sourceLength,
                    decoder.Scratch))

                {
                    throw new ReplayException("Failed DecodeStep method");
                }

                sourceStart += decoder.SourceUsed;
                sourceLength -= decoder.SourceUsed;

                destinationOffset += decoder.DestinationUsed;
                remainingBytes -= decoder.DestinationUsed;
            }
        }

        private bool DecodeStep(KrakenDecoder decoder, byte* destination, int destinationOffset,
            int remainingDestinationBytes, byte* source, int sourceBytesleft, byte* scratch)
        {
            var sourceIn = source;
            var sourceEnd = source + sourceBytesleft;
            if ((destinationOffset & 0x3FFFF) == 0)
            {
                decoder.Header = ParseHeader(source);

                source += 2;
            }

            //Only need Mermaid for Fortnite
            //"Oodle initializing compressor with Mermaid, level Normal, SpaceSpeed tradeoff 256"
            var isKrakenDecoder = decoder.Header.DecoderType == DecoderTypes.Mermaid;
            var destinationBytesLeft = Math.Min(isKrakenDecoder ? 0x40000 : 0x4000, remainingDestinationBytes);

            if (decoder.Header.Uncompressed)
            {
                if (sourceEnd - source < destinationBytesLeft)
                {
                    throw new CompressionException(
                        $"DecodeStep: sourceEnd - source ({sourceEnd - source}) < destinationBytesLeft ({destinationBytesLeft})");
                }

                //throw new NotImplementedException($"memmove(dst_start + offset, src, dst_bytes_left);");
                Buffer.MemoryCopy(source, destination + destinationOffset, sourceBytesleft, sourceBytesleft);

                decoder.SourceUsed = (int) (source - sourceIn + destinationBytesLeft);
                decoder.DestinationUsed = destinationBytesLeft;

                return true;
            }

            KrakenQuantumHeader quantumHeader;

            if (isKrakenDecoder)
            {
                quantumHeader = ParseQuantumHeader(source, decoder.Header.UseChecksums, out var bytesRead);

                source += bytesRead;
            }
            else
            {
                throw new CompressionException($"Decoder type {decoder.Header.DecoderType} not supported");
            }

            if (source > sourceEnd)
            {
                throw new CompressionException("Index out of range of source array");
            }

            // Too few bytes in buffer to make any progress?
            if (sourceEnd - source < quantumHeader.CompressedSize)
            {
                decoder.SourceUsed = 0;
                decoder.DestinationUsed = 0;

                return true;
            }

            if (quantumHeader.CompressedSize > remainingDestinationBytes)
            {
                throw new CompressionException(
                    $"Invalid compression size CompressedSize > RemainingDestinationLength. {quantumHeader.CompressedSize} > {remainingDestinationBytes}");
            }

            if (quantumHeader.CompressedSize == 0)
            {
                if (quantumHeader.WholeMatchDistance != 0)
                {
                    if (quantumHeader.WholeMatchDistance > destinationOffset)
                    {
                        throw new CompressionException(
                            $"WholeMatchDistance > destinationOffset. {quantumHeader.WholeMatchDistance} > {destinationOffset}");
                    }

                    throw new NotImplementedException(
                        "Kraken_CopyWholeMatch(dst_start + offset, qhdr.whole_match_distance, dst_bytes_left);");
                }
                else
                {
                    var val = quantumHeader.Checksum;

                    Buffer.MemoryCopy(&val, destination + destinationOffset, destinationBytesLeft,
                        destinationBytesLeft);
                }

                decoder.SourceUsed = (int) (source - sourceIn);
                decoder.DestinationUsed = destinationBytesLeft;

                return true;
            }

            if (decoder.Header.UseChecksums)
            {
                var checksum = GetCrc() & 0xFFFFFF;

                if (checksum != quantumHeader.Checksum)
                {
                    throw new CompressionException($"Invalid checksum. Found {checksum} need {quantumHeader.Checksum}");
                }
            }

            if (quantumHeader.CompressedSize == destinationBytesLeft)
            {
                decoder.SourceUsed = (int) (source - sourceIn + destinationBytesLeft);
                decoder.DestinationUsed = destinationBytesLeft;

                throw new NotImplementedException("memmove(dst_start + offset, src, dst_bytes_left);");
            }

            var numBytes = decoder.Header.DecoderType switch
            {
                DecoderTypes.Mermaid => MermaidDecodeQuantum(destination + destinationOffset,
                    destination + destinationOffset + destinationBytesLeft, destination, source,
                    source + quantumHeader.CompressedSize, scratch, decoder.ScratchSize),
                _ => throw new CompressionException($"Decoder type {decoder.Header.DecoderType} currently not supported")
            };

            if (numBytes != quantumHeader.CompressedSize)
            {
                throw new CompressionException(
                    $"Invalid number of bytes decompressed. {numBytes} != {quantumHeader.CompressedSize}");
            }


            decoder.SourceUsed = (int) (source - sourceIn) + numBytes;
            decoder.DestinationUsed = destinationBytesLeft;

            return true;
        }

        private static uint GetCrc() => throw new NotImplementedException();

        private KrackenHeader ParseHeader(byte* source)
        {
            var header = new KrackenHeader();
            var firstByte = source[0];
            var secondByte = source[1];

            if ((firstByte & 0xF) != 0xC)
            {
                throw new CompressionException("Failed to decode header. (source[0] & 0xF) != 0xC");
            }

            if (((firstByte >> 4) & 3) != 0)
            {
                throw new CompressionException("Failed to decode header. ((source[0] >> 4) & 3) != 0");
            }

            header.RestartDecoder = ((firstByte >> 7) & 0x1) == 0x01;
            header.Uncompressed = ((firstByte >> 6) & 0x1) == 0x01;

            header.DecoderType = (DecoderTypes) (secondByte & 0x7F);
            header.UseChecksums = ((secondByte >> 7) & 0x1) == 0x01;

            return header;
        }

        private KrakenQuantumHeader ParseQuantumHeader(byte* source, bool useChecksums, out int bytesRead)
        {
            var quantumHeader = new KrakenQuantumHeader();

            var v = (uint) ((source[0] << 16) | (source[1] << 8) | source[2]);
            var size = v & 0x3FFFF;

            if (size != 0x3FFFF)
            {
                quantumHeader.CompressedSize = size + 1;
                quantumHeader.Flag1 = (byte) ((v >> 18) & 1);
                quantumHeader.Flag2 = (byte) ((v >> 19) & 1);

                if (useChecksums)
                {
                    quantumHeader.Checksum = (uint) ((source[3] << 16) | (source[4] << 8) | source[5]);

                    bytesRead = 6;
                }
                else
                {
                    bytesRead = 3;
                }

                return quantumHeader;
            }

            v >>= 18;

            if (v != 1)
            {
                throw new CompressionException("Failed to parse KrakenQuantumHeader");
            }

            quantumHeader.Checksum = source[3];
            quantumHeader.CompressedSize = 0;
            quantumHeader.WholeMatchDistance = 0;

            bytesRead = 4;

            return quantumHeader;
        }

        private int DecodeBytes(byte** output, byte* source, byte* sourceEnd, int* decodedSize, uint outputSize,
            bool forceMemmove)
        {
            var sourceOrg = source;
            int sourceSize;

            if (sourceEnd - source < 2)
            {
                throw new CompressionException($"DecodeBytes: Too few bytes ({sourceEnd - source}) remaining");
            }

            var chunkType = (source[0] >> 4) & 0x7;

            if (chunkType != 0)
            {
                throw new NotImplementedException("DecodeBytes");
            }

            if (source[0] >= 0x80)
            {
                // In this mode, memcopy stores the length in the bottom 12 bits.
                sourceSize = ((source[0] << 8) | source[1]) & 0xFFF;
                source += 2;
            }
            else
            {
                if (sourceEnd - source < 3)
                {
                    throw new CompressionException($"DecodeBytes: Too few bytes ({sourceEnd - source}) remaining");
                }

                sourceSize = source[0] << 16 | source[1] << 8 | source[2];

                if ((sourceSize & ~0x3ffff) > 0)
                {
                    throw new CompressionException("Reserved bits must not be set");
                }

                source += 3;
            }

            if (sourceSize > outputSize || sourceEnd - source < sourceSize)
            {
                throw new CompressionException(
                    $"sourceSize ({sourceSize}) > outputSize ({outputSize}) || sourceEnd - source ({sourceEnd - source}) < sourceSize ({sourceSize})");
            }

            *decodedSize = sourceSize;

            if (forceMemmove)
            {
                throw new NotImplementedException("Memmove not implemented");
            }
            else
            {
                *output = source;

                return (int) (source + sourceSize - sourceOrg);
            }
        }

        private int MermaidDecodeQuantum(byte* destination, byte* destinationEnd, byte* destinationStart, byte* source,
            byte* sourceEnd, byte* temp, int tempSize)
        {
            var tempEnd = temp + tempSize;
            var sourceIn = source;

            while (destinationEnd - destination != 0)
            {
                var destinationCount = (int) (destinationEnd - destination);

                destinationCount = destinationCount > 0x20000 ? 0x20000 : destinationCount;

                if (sourceEnd - source < 4)
                {
                    throw new CompressionException(
                        $"Less than 4 bytes remaining in source. Remaining: {sourceEnd - source}");
                }

                var chunkHeader = source[2] | source[1] << 8 | source[0] << 16;

                int sourceUsed;
                if (!((chunkHeader & 0x800000) > 0))
                {
                    //Stored without any match copying.
                    var outDestination = destination;

                    throw new NotImplementedException(
                        "src_used = Kraken_DecodeBytes(&out, src, src_end, &written_bytes, dst_count, false, temp, temp_end);");

                    // if (sourceUsed < 0 || writtenBytes != destinationCount)
                    // {
                    //     return -1;
                    // }
                }
                else
                {
                    source += 3;
                    sourceUsed = chunkHeader & 0x7FFFF;
                    var mode = (chunkHeader >> 19) & 0xF;

                    if (sourceEnd - source < sourceUsed)
                    {
                        throw new CompressionException(
                            $"Not enough source bytes remaining. Have {sourceEnd - source}. Need {sourceUsed}");
                    }

                    if (sourceUsed < destinationCount)
                    {
                        var tempUsage = 2 * destinationCount + 32;

                        tempUsage = tempUsage > 0x40000 ? 0x40000 : tempUsage;

                        //Mermaid_ReadLzTable
                        if (!MermaidReadLzTable(mode,
                            source, source + sourceUsed,
                            destination, destinationCount, destination - destinationStart,
                            temp + sizeof(MermaidLzTable), temp + tempUsage, (MermaidLzTable*) temp))
                        {
                            throw new CompressionException("Failed to run MermaidReadLzTable");
                        }

                        //Mermaid_ProcessLzRuns
                        if (!MermaidProcessLzRuns(mode,
                            source + sourceUsed,
                            destination, destinationCount,
                            destination - destinationStart, destinationEnd,
                            (MermaidLzTable*) temp))
                        {
                            throw new CompressionException("Failed to run MermaidProcessLzRuns");
                        }
                    }
                    else if (sourceUsed > destinationCount || mode != 0)
                    {
                        throw new CompressionException(
                            $"Used bytes ({sourceUsed}) > destinationCount ({destinationCount} or Mode ({mode}) != 0");
                    }
                    else
                    {
                        Buffer.MemoryCopy(source, destination, destinationCount, destinationCount);
                    }
                }

                source += sourceUsed;
                destination += destinationCount;
            }

            return (int) (source - sourceIn);
        }

        private bool MermaidReadLzTable(int mode, byte* source, byte* sourceEnd, byte* destination, int destinationSize,
            long offset, byte* scratch, byte* scratchEnd, MermaidLzTable* lz)
        {
            int decodeCount;

            if (mode > 1)
            {
                return false;
            }

            if (sourceEnd - source < 10)
            {
                return false;
            }

            if (offset == 0)
            {
                PointerUtil.Copy64(destination, source);

                destination += 8;
                source += 8;
            }

            //Decode lit stream
            var scratchOut = scratch;

            var numBytes = DecodeBytes(&scratchOut, source, sourceEnd, &decodeCount,
                (uint) Math.Min(scratchEnd - scratch, destinationSize), false);

            source += numBytes;
            lz->LitStream = scratchOut;
            lz->LitStreamEnd = scratchOut + decodeCount;
            scratch += decodeCount;

            //Decode flag stream
            scratchOut = scratch;
            numBytes = DecodeBytes(&scratchOut, source, sourceEnd, &decodeCount,
                (uint) Math.Min(scratchEnd - scratch, destinationSize), false);

            source += numBytes;
            lz->CmdStream = scratchOut;
            lz->CmdStreamEnd = scratchOut + decodeCount;
            scratch += decodeCount;

            lz->CmdStream2OffsetsEnd = (uint) decodeCount;

            if (destinationSize <= 0x10000)
            {
                lz->CmdStream2Offsets = (uint) decodeCount;
            }
            else
            {
                if (sourceEnd - source < 2)
                {
                    throw new CompressionException($"MermaidReadLzTable: Too few bytes ({sourceEnd - source}) remaining");
                }

                lz->CmdStream2Offsets = *(ushort*) source;

                source += 2;

                if (lz->CmdStream2Offsets > lz->CmdStream2OffsetsEnd)
                {
                    throw new CompressionException(
                        $"MermaidReadLzTable: lz->CmdStream2Offsets ({lz->CmdStream2Offsets}) > lz->CmdStream2OffsetsEnd ({lz->CmdStream2OffsetsEnd})");
                }
            }

            if (sourceEnd - source < 2)
            {
                throw new CompressionException($"MermaidReadLzTable: Too few bytes ({sourceEnd - source}) remaining");
            }

            int off16Count = *(ushort*) source;

            if (off16Count == 0xFFFF)
            {
                int offset16LowCount;
                int offset16HighCount;

                source += 2;
                var offset16High = scratch;
                numBytes = DecodeBytes(&offset16High, source, sourceEnd, &offset16HighCount,
                    (uint) Math.Min(scratchEnd - scratch, destinationSize >> 1), false);

                source += numBytes;
                scratch += offset16HighCount;

                var offset16Low = scratch;
                numBytes = DecodeBytes(&offset16Low, source, sourceEnd, &offset16LowCount,
                    (uint) Math.Min(scratchEnd - scratch, destinationSize >> 1), false);

                source += numBytes;
                scratch += offset16LowCount;

                if (offset16LowCount != offset16HighCount)
                {
                    throw new CompressionException(
                        $"MermaidReadLzTable: offset16LowCount ({offset16LowCount}) != offset16HighCount ({offset16HighCount})");
                }

                scratch = PointerUtil.AlignPointer(scratch, 2);
                lz->Offset16Stream = (ushort*) scratch;

                if (scratch + offset16LowCount * 2 > scratchEnd)
                {
                    throw new CompressionException("MermaidReadLzTable: scratch + offset16LowCount * 2 > scratchEnd");
                }

                scratch += offset16LowCount * 2;
                lz->Offset16StreamEnd = (ushort*) scratch;

                MermaidCombineOffset16(lz->Offset16Stream, offset16LowCount, offset16Low, offset16High);
            }
            else
            {
                lz->Offset16Stream = (ushort*) (source + 2);
                source += 2 + off16Count * 2;
                lz->Offset16StreamEnd = (ushort*) source;
            }

            if (sourceEnd - source < 3)
            {
                throw new CompressionException($"MermaidReadLzTable: Too few bytes ({sourceEnd - source}) remaining");
            }

            var temp = (uint) (source[0] | source[1] << 8 | source[2] << 16);

            source += 3;

            if (temp != 0)
            {
                var offset32Size1 = temp >> 12;
                var offset32Size2 = temp & 0xFFF;

                if (offset32Size1 == 4095)
                {
                    if (sourceEnd - source < 2)
                    {
                        throw new CompressionException(
                            $"MermaidReadLzTable: Too few bytes ({sourceEnd - source}) remaining");
                    }

                    offset32Size1 = *(ushort*) source;
                    source += 2;
                }

                if (offset32Size2 == 4095)
                {
                    if (sourceEnd - source < 2)
                    {
                        throw new CompressionException(
                            $"MermaidReadLzTable: Too few bytes ({sourceEnd - source}) remaining");
                    }

                    offset32Size2 = *(ushort*) source;
                    source += 2;
                }

                lz->Offset32Stream1Size = offset32Size1;
                lz->Offset32Stream2Size = offset32Size2;

                if (scratch + 4 * (offset32Size1 + offset32Size2) + 64 > scratchEnd)
                {
                    throw new CompressionException("MermaidReadLzTable: Not enough remaining scratch space");
                }

                scratch = PointerUtil.AlignPointer(scratch, 4);

                lz->Offset32Stream1 = (uint*) scratch;
                scratch += offset32Size1 * 4;

                // store dummy bytes after for prefetcher.
                ((ulong*) scratch)[0] = 0;
                ((ulong*) scratch)[1] = 0;
                ((ulong*) scratch)[2] = 0;
                ((ulong*) scratch)[3] = 0;
                scratch += 32;

                lz->Offset32Stream2 = (uint*) scratch;
                scratch += offset32Size2 * 4;

                // store dummy bytes after for prefetcher.
                ((ulong*) scratch)[0] = 0;
                ((ulong*) scratch)[1] = 0;
                ((ulong*) scratch)[2] = 0;
                ((ulong*) scratch)[3] = 0;
                scratch += 32;

                numBytes = MermaidDecodeFarOffsets(source, sourceEnd, lz->Offset32Stream1, lz->Offset32Stream1Size,
                    offset);

                source += numBytes;

                numBytes = MermaidDecodeFarOffsets(source, sourceEnd, lz->Offset32Stream2, lz->Offset32Stream2Size,
                    offset + 0x10000);

                source += numBytes;
            }
            else
            {
                if (scratchEnd - scratch < 32)
                {
                    throw new CompressionException($"MermaidReadLzTable: Too few bytes ({sourceEnd - source}) remaining");
                }


                lz->Offset32Stream1Size = 0;
                lz->Offset32Stream2Size = 0;
                lz->Offset32Stream1 = (uint*) scratch;
                lz->Offset32Stream2 = (uint*) scratch;

                // store dummy bytes after for prefetcher.
                ((ulong*) scratch)[0] = 0;
                ((ulong*) scratch)[1] = 0;
                ((ulong*) scratch)[2] = 0;
                ((ulong*) scratch)[3] = 0;
            }

            lz->LengthStream = source;

            return true;
        }

        private bool MermaidProcessLzRuns(int mode, byte* sourceEnd, byte* destination,
            int destinationSize, long offset, byte* destinationEnd, MermaidLzTable* lz)
        {
            var destinationStart = destination - offset;
            var savedDist = -8;
            byte* sourceCurrent = null;

            for (var iteration = 0; iteration != 2; iteration++)
            {
                var destinationSizeCurrent = destinationSize;

                destinationSizeCurrent = destinationSizeCurrent > 0x10000 ? 0x10000 : destinationSizeCurrent;

                if (iteration == 0)
                {
                    lz->Offset32Stream = lz->Offset32Stream1;
                    lz->Offset32StreamEnd = lz->Offset32Stream1 + lz->Offset32Stream1Size * 4;
                    lz->CmdStreamEnd = lz->CmdStream + lz->CmdStream2Offsets;
                }
                else
                {
                    lz->Offset32Stream = lz->Offset32Stream2;
                    lz->Offset32StreamEnd = lz->Offset32Stream2 + lz->Offset32Stream2Size * 4;
                    lz->CmdStreamEnd = lz->CmdStream + lz->CmdStream2OffsetsEnd;

                    lz->CmdStream += lz->CmdStream2Offsets;
                }

                if (mode == 0)
                {
                    throw new NotImplementedException("MermaidProcessLzRuns: Mode 0 not implemented currently");
                }
                else
                {
                    sourceCurrent = MermaidMode1(destination, destinationSizeCurrent,
                        sourceEnd, lz, &savedDist, offset == 0 && iteration == 0 ? 8 : 0);
                }

                destination += destinationSizeCurrent;
                destinationSize -= destinationSizeCurrent;

                if (destinationSize == 0)
                {
                    break;
                }
            }

            if (sourceCurrent != sourceEnd)
            {
                throw new CompressionException("MermaidProcessLzRuns: Failed to read decompress source bytes");
            }

            return true;
        }

        private byte* MermaidMode1(byte* destination, int destinationSize, byte* sourceEnd, MermaidLzTable* lz,
            int* savedDist, int startOff)
        {
            var destinationEnd = destination + destinationSize;
            var cmdStream = lz->CmdStream;
            var cmdStreamEnd = lz->CmdStreamEnd;
            var lengthStream = lz->LengthStream;
            var litStream = lz->LitStream;
            var litStreamEnd = lz->LitStreamEnd;
            var off16Stream = lz->Offset16Stream;
            var off16StreamEnd = lz->Offset16StreamEnd;
            var off32Stream = lz->Offset32Stream;
            var off32StreamEnd = lz->Offset32StreamEnd;
            var recentOffs = *savedDist;
            int length;
            var destinationBegin = destination;

            destination += startOff;

            var test = cmdStreamEnd - cmdStream;

            while (cmdStream < cmdStreamEnd)
            {
                uint flag = *cmdStream++;

                byte* match;
                switch (flag)
                {
                    case >= 24:
                    {
                        uint newDist = *off16Stream;
                        var useDistance = (flag >> 7) - 1;
                        var litLen = flag & 7;

                        PointerUtil.Copy64(destination, litStream);

                        destination += litLen;
                        litStream += litLen;

                        recentOffs ^= (int) (useDistance & (uint) (recentOffs ^ -newDist));

                        off16Stream = (ushort*) ((byte*) off16Stream + (useDistance & 2));
                        match = destination + recentOffs;

                        PointerUtil.Copy64(destination, match);
                        PointerUtil.Copy64(destination + 8, match + 8);

                        destination += (flag >> 3) & 0xF;
                        break;
                    }
                    case > 2:
                    {
                        length = (int) (flag + 5);

                        if (off32Stream == off32StreamEnd)
                        {
                            throw new CompressionException("MermaidMode1: off32Stream == off32StreamEnd");
                        }

                        match = destinationBegin - *off32Stream++;
                        recentOffs = (int) (match - destination);

                        if (destinationEnd - destination < length)
                        {
                            throw new CompressionException("MermaidMode1: destinationEnd - destination < length");
                        }

                        PointerUtil.Copy64(destination, match);
                        PointerUtil.Copy64(destination + 8, match + 8);
                        PointerUtil.Copy64(destination + 16, match + 16);
                        PointerUtil.Copy64(destination + 24, match + 24);

                        destination += length;
                        break;
                    }
                    case 0 when sourceEnd - lengthStream == 0:
                        throw new CompressionException(
                            $"MermaidMode1: Not enough source bytes remaining. Have {sourceEnd - lengthStream}");
                    case 0:
                    {
                        length = *lengthStream;

                        if (length > 251)
                        {
                            if (sourceEnd - lengthStream < 3)
                            {
                                throw new CompressionException(
                                    $"MermaidMode1: Not enough source bytes remaining. Have {sourceEnd - lengthStream}. Need 3");
                            }

                            length += *(ushort*) (lengthStream + 1) * 4;
                            lengthStream += 2;
                        }

                        lengthStream += 1;

                        length += 64;

                        if (destinationEnd - destination < length || litStreamEnd - litStream < length)
                        {
                            throw new CompressionException(
                                "MermaidMode1: destinationEnd - destination < length || litStreamEnd - litStream < length");
                        }

                        do
                        {
                            PointerUtil.Copy64(destination, litStream);
                            PointerUtil.Copy64(destination + 8, litStream + 8);

                            destination += 16;
                            litStream += 16;
                            length -= 16;
                        } while (length > 0);

                        destination += length;
                        litStream += length;
                        break;
                    }
                    case 1 when sourceEnd - lengthStream == 0:
                        throw new CompressionException(
                            $"MermaidMode1: Not enough source bytes remaining. Have {sourceEnd - lengthStream}");
                    case 1:
                    {
                        length = *lengthStream;

                        if (length > 251)
                        {
                            if (sourceEnd - lengthStream < 3)
                            {
                                throw new CompressionException(
                                    $"MermaidMode1: Not enough source bytes remaining. Have {sourceEnd - lengthStream}. Need 3");
                            }

                            length += *(ushort*) (lengthStream + 1) * 4;
                            lengthStream += 2;
                        }

                        lengthStream += 1;
                        length += 91;

                        if (off16Stream == off16StreamEnd)
                        {
                            throw new CompressionException("MermaidMode1: off16Stream == off16StreamEnd");
                        }

                        match = destination - *off16Stream++;
                        recentOffs = (int) (match - destination);

                        do
                        {
                            PointerUtil.Copy64(destination, match);
                            PointerUtil.Copy64(destination + 8, match + 8);

                            destination += 16;
                            match += 16;
                            length -= 16;
                        } while (length > 0);

                        destination += length;
                        break;
                    }
                    default:
                    {
                        if (sourceEnd - lengthStream == 0)
                        {
                            throw new CompressionException("MermaidMode1: sourceEnd - lengthStream == 0");
                        }

                        length = *lengthStream;

                        if (length > 251)
                        {
                            if (sourceEnd - lengthStream < 3)
                            {
                                throw new CompressionException(
                                    $"MermaidMode1: Not enough source bytes remaining. Have {sourceEnd - lengthStream}. Need 3");
                            }

                            length += *(ushort*) (lengthStream + 1) * 4;
                            lengthStream += 2;
                        }

                        lengthStream += 1;
                        length += 29;

                        if (off32Stream == off32StreamEnd)
                        {
                            throw new CompressionException("MermaidMode1: off32Stream == off32StreamEnd");
                        }

                        match = destinationBegin - *off32Stream++;
                        recentOffs = (int) (match - destination);

                        do
                        {
                            PointerUtil.Copy64(destination, match);
                            PointerUtil.Copy64(destination + 8, match + 8);

                            destination += 16;
                            match += 16;
                            length -= 16;
                        } while (length > 0);

                        destination += length;
                        break;
                    }
                }
            }

            length = (int) (destinationEnd - destination);

            if (length >= 8)
            {
                do
                {
                    PointerUtil.Copy64(destination, litStream);

                    destination += 8;
                    litStream += 8;
                    length -= 8;
                } while (length >= 8);
            }

            if (length > 0)
            {
                do
                {
                    *destination++ = *litStream++;
                } while (--length > 0);
            }

            *savedDist = recentOffs;
            lz->LengthStream = lengthStream;
            lz->Offset16Stream = off16Stream;
            lz->LitStream = litStream;

            return lengthStream;
        }

        private void MermaidCombineOffset16(ushort* destination, int size, byte* lo, byte* hi)
        {
            for (var i = 0; i != size; i++)
            {
                destination[i] = (ushort) (lo[i] + hi[i] * 256);
            }
        }

        private int MermaidDecodeFarOffsets(byte* source, byte* sourceEnd, uint* output, uint outputSize, long offset)
        {
            var sourceCurrent = source;
            uint i;
            uint off;

            if (offset < 0xC00000 - 1)
            {
                for (i = 0; i != outputSize; i++)
                {
                    if (sourceEnd - sourceCurrent < 3)
                    {
                        throw new CompressionException(
                            $"MermaidDecodeFarOffsets: Too few bytes ({sourceEnd - source}) remaining");
                    }

                    off = (uint) (sourceCurrent[0] | sourceCurrent[1] << 8 | sourceCurrent[2] << 16);
                    sourceCurrent += 3;

                    output[i] = off;

                    if (off > offset)
                    {
                        throw new CompressionException($"MermaidDecodeFarOffsets: off ({off}) > offset ({offset})");
                    }
                }

                return (int) (sourceCurrent - source);
            }

            for (i = 0; i != outputSize; i++)
            {
                if (sourceEnd - sourceCurrent < 3)
                {
                    throw new CompressionException(
                        $"MermaidDecodeFarOffsets: Too few bytes ({sourceEnd - source}) remaining");
                }

                off = (uint) (sourceCurrent[0] | sourceCurrent[1] << 8 | sourceCurrent[2] << 16);
                sourceCurrent += 3;

                if (off >= 0xc00000)
                {
                    if (sourceCurrent == sourceEnd)
                    {
                        throw new CompressionException("MermaidDecodeFarOffsets: No remaining bytes");
                    }

                    off += (uint) (*sourceCurrent++ << 22);
                }

                output[i] = off;

                if (off > offset)
                {
                    throw new CompressionException($"MermaidDecodeFarOffsets: off ({off}) > offset ({offset})");
                }
            }

            return (int) (sourceCurrent - source);
        }
    }
}