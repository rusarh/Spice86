﻿namespace Spice86.Core.Emulator.Devices.Sound.Blaster;

using System;

/// <summary>
/// 2-bit ADPCM decoder.
/// </summary>
public class ADPCM2 : ADPCMDecoder {
    /// <summary>
    /// The limit value.
    /// </summary>
    private const int Limit = 1;
    /// <summary>
    /// The shift value.
    /// </summary>
    private const int Shift = 2;

    /// <summary>
    /// Initializes a new instance of the ADPCM2 class.
    /// </summary>
    public ADPCM2()
        : base(4) {
    }
    /// <summary>
    /// Initializes a new instance of the ADPCM2 class.
    /// </summary>
    /// <param name="factor">The compression factor.</param>
    protected ADPCM2(int factor)
        : base(factor) {
    }

    /// <summary>
    /// Decodes a block of ADPCM compressed data.
    /// </summary>
    /// <param name="source">Source array containing ADPCM data to decode.</param>
    /// <param name="sourceOffset">Offset in source array to start decoding.</param>
    /// <param name="count">Number of bytes to decode.</param>
    /// <param name="destination">Destination array to write decoded PCM data.</param>
    public override void Decode(byte[] source, int sourceOffset, int count, Span<byte> destination) {
        byte current = Reference;

        for (int i = 0; i < count; i++) {
            for (int j = 0; j < 4; j++) {
                int sourceByte = source[sourceOffset + i] >> j & 0x03;
                current = DecodeSample(current, sourceByte);
                destination[(i * 4) + j] = current;
            }
        }

        Reference = current;
    }

    /// <summary>
    /// Decodes a 2-bit sample into an 8-bit sample.
    /// </summary>
    /// <param name="current">Current prediction value.</param>
    /// <param name="sample">2-bit sample to decode.</param>
    /// <returns>Decoded 8-bit sample.</returns>
    protected byte DecodeSample(byte current, int sample) {
        if ((sample & 0x02) == 0) {
            current += (byte)(sample << _step + Shift);
        } else {
            current -= (byte)((sample & 0x01) << _step + Shift);
        }

        if (current >= Limit) {
            _step++;
            if (_step > 3) {
                _step = 3;
            }
        } else if (current == 0) {
            _step--;
            if (_step < 0) {
                _step = 0;
            }
        }

        return current;
    }
}
