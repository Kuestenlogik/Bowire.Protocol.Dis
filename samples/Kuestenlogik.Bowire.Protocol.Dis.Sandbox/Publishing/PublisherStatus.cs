// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

/// <summary>What the status bar and the entity list show about sending.</summary>
internal sealed class PublisherStatus(IOptions<SandboxOptions> options)
{
    private readonly ConcurrentDictionary<ushort, string> _encodeFailures = new();
    private long _sent;
    private long _errors;

    public string Target { get; } = options.Value.Target;

    public long Sent => Interlocked.Read(ref _sent);

    public long Errors => Interlocked.Read(ref _errors);

    public string? LastError { get; private set; }

    public DateTimeOffset? LastErrorAt { get; private set; }

    public IReadOnlyDictionary<ushort, string> EncodeFailures => _encodeFailures;

    public event Action? Changed;

    public void RecordSent()
    {
        Interlocked.Increment(ref _sent);
        Changed?.Invoke();
    }

    public void RecordSendError(Exception error, DateTimeOffset at)
    {
        Interlocked.Increment(ref _errors);
        LastError = error.Message;
        LastErrorAt = at;
        Changed?.Invoke();
    }

    public void MarkEncodeFailed(ushort number, string message)
    {
        _encodeFailures[number] = message;
        Changed?.Invoke();
    }

    public void ClearEncodeFailure(ushort number)
    {
        if (_encodeFailures.TryRemove(number, out _))
            Changed?.Invoke();
    }
}
