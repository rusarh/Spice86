﻿namespace Spice86.Core.Backend.Audio;

using System;

/// <summary>
/// Implements a background audio playback stream.
/// </summary>
public abstract partial class AudioPlayer : IDisposable {
    private readonly InternalBufferWriter _writer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPlayer"/> class.
    /// </summary>
    /// <param name="format">Format of the audio stream.</param>
    protected AudioPlayer(AudioFormat format) {
        Format = format ?? throw new ArgumentNullException(nameof(format));

        _writer = format.SampleFormat switch {
            SampleFormat.UnsignedPcm8 => new InternalBufferWriter<byte>(this),
            SampleFormat.SignedPcm16 => new InternalBufferWriter<short>(this),
            SampleFormat.IeeeFloat32 => new InternalBufferWriter<float>(this),
            _ => throw new ArgumentException("Invalid sample format.")
        };
    }

    /// <summary>
    /// Gets the playback audio format.
    /// </summary>
    public AudioFormat Format { get; }
    /// <summary>
    /// Gets a value indicating whether the player is active.
    /// </summary>
    public bool Playing { get; private set; }

    /// <summary>
    /// Begins playback of the background stream.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stream is already playing.</exception>
    /// <exception cref="ObjectDisposedException">The <see cref="AudioPlayer"/> instance has been disposed.</exception>
    public void BeginPlayback() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(AudioPlayer));
        }

        if (Playing) {
            throw new InvalidOperationException("Playback has already started.");
        }

        Playing = true;
    }

    /// <summary>
    /// Writes 32-bit IEEE floating point data to the output buffer.
    /// </summary>
    /// <param name="data">Buffer containing data to write.</param>
    /// <returns>Number of samples actually written to the buffer.</returns>
    public int WriteData(Span<float> data) => _writer.WriteData(data);
    /// <summary>
    /// Writes 16-bit PCM data to the output buffer.
    /// </summary>
    /// <param name="data">Buffer containing data to write.</param>
    /// <returns>Number of samples actually written to the buffer.</returns>
    public int WriteData(Span<short> data) => _writer.WriteData(data);

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        _disposed = true;
    }
    protected abstract int WriteDataInternal(Span<byte> data);
}