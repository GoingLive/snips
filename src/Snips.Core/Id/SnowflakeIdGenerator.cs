using System.Threading;

namespace Snips.Core.Id;

/// <summary>
/// Generates monotonically increasing 63-bit snowflake IDs, per SPEC.md §4.1:
/// 41 bits milliseconds since 2024-01-01T00:00:00Z, 10 bits instance id, 12 bits sequence.
/// Stored as TEXT zero-padded to 19 characters so lexicographic order equals numeric order.
/// </summary>
public sealed class SnowflakeIdGenerator
{
    private static readonly DateTimeOffset Epoch = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const int InstanceBits = 10;
    private const int SequenceBits = 12;
    private const int MaxInstanceId = (1 << InstanceBits) - 1;
    private const int MaxSequence = (1 << SequenceBits) - 1;

    private readonly long _instanceId;
    private readonly Lock _lock = new();
    private long _lastTimestampMs = -1;
    private long _sequence;

    public SnowflakeIdGenerator(long? instanceId = null)
    {
        _instanceId = instanceId ?? Random.Shared.NextInt64(0, MaxInstanceId + 1);
        if (_instanceId is < 0 or > MaxInstanceId)
            throw new ArgumentOutOfRangeException(nameof(instanceId), $"Instance id must fit in {InstanceBits} bits.");
    }

    /// <summary>Returns the next ID as a zero-padded 19-character decimal string.</summary>
    public string NextId()
    {
        lock (_lock)
        {
            var timestampMs = CurrentTimestampMs();

            if (timestampMs < _lastTimestampMs)
                throw new InvalidOperationException("Clock moved backwards; refusing to generate a duplicate id.");

            if (timestampMs == _lastTimestampMs)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                    timestampMs = WaitForNextMillis(_lastTimestampMs);
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestampMs = timestampMs;

            var value = (timestampMs << (InstanceBits + SequenceBits))
                        | (_instanceId << SequenceBits)
                        | _sequence;

            return value.ToString("D19");
        }
    }

    private static long CurrentTimestampMs() =>
        (long)(DateTimeOffset.UtcNow - Epoch).TotalMilliseconds;

    private static long WaitForNextMillis(long lastTimestampMs)
    {
        long timestamp;
        do
        {
            timestamp = CurrentTimestampMs();
        } while (timestamp <= lastTimestampMs);

        return timestamp;
    }
}
