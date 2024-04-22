using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;


namespace StepWise.IO.Pipelines;


public class HashPipeReader : PipeReader {
    private readonly PipeReader _reader;
    private readonly IncrementalHash _hasher;
    private ReadOnlySequence<byte>? _mem = null;

    public HashPipeReader(PipeReader reader, HashAlgorithmName algo) {
        _reader = reader;
        _hasher = IncrementalHash.CreateHash(algo);
    }

    public byte[] GetHash() => _hasher.GetCurrentHash();

    public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default) {
        var result = await _reader.ReadAsync(cancellationToken);
        _mem = result.Buffer;
        return result;
    }

    public override void AdvanceTo(SequencePosition consumed) => AdvanceTo(consumed, consumed);

    public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) {
        if (_mem == null) throw new InvalidOperationException("ReadAsync must be called before AdvanceTo");

        var buffer = _mem.Value;

        if (_mem.Value.IsSingleSegment) {
            // For a single segement sequence we can avoid a copy by slicing the underlying span
            var bytes = buffer.FirstSpan[..(int)buffer.GetOffset(examined)];
            _hasher.AppendData(bytes);
        } else {
            var sliced = buffer.Slice(0, examined);
            foreach (var segment in sliced) {
                _hasher.AppendData(segment.Span);
            }
        }
        _mem = null;

        _reader.AdvanceTo(consumed, examined);
    }

    public override bool TryRead(out ReadResult result) {
        var success = _reader.TryRead(out result);
        if (success) _hasher.AppendData(result.Buffer.ToArray());
        return success;
    }

    public override void CancelPendingRead() => _reader.CancelPendingRead();

    public override void Complete(Exception? exception = null) => _reader.Complete(exception);
}

public class HashPipeWriter : PipeWriter {
    private readonly PipeWriter _writer;
    private readonly IncrementalHash _hasher;
    private Memory<byte>? _mem = null;

    public HashPipeWriter(PipeWriter writer, HashAlgorithmName algo) {
        _writer = writer;
        _hasher = IncrementalHash.CreateHash(algo);
    }

    public byte[] GetHash() => _hasher.GetCurrentHash();

    public override Span<byte> GetSpan(int sizeHint = 0) =>
        throw new NotImplementedException("GetSpan is not implemented for HashPipeWriter");

    public override Memory<byte> GetMemory(int sizeHint = 0) {
        _mem = _writer.GetMemory(8192);
        return _mem.Value;
    }

    public override void Advance(int bytes) {
        if (_mem != null)
            _hasher.AppendData(_mem.Value.Span[..bytes]);

        _mem = null;
        _writer.Advance(bytes);
    }

    public override async ValueTask<FlushResult> WriteAsync(
        ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        var result = await _writer.WriteAsync(source, cancellationToken);
        _hasher.AppendData(source.Span);
        return result;
    }

    public override void CancelPendingFlush() => _writer.CancelPendingFlush();

    public override void Complete(Exception? exception = null) => _writer.Complete(exception);

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) =>
        _writer.FlushAsync(cancellationToken);
}

public static class PipelineExtensions {
    public static HashPipeReader AsHashPipe(this PipeReader reader, HashAlgorithmName algo) => new HashPipeReader(reader, algo);
    public static HashPipeWriter AsHashPipe(this PipeWriter writer, HashAlgorithmName algo) => new HashPipeWriter(writer, algo);
}