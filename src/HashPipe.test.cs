using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using Xunit;

namespace StepWise.IO.Pipelines.Test;

public class HashPipeTest {
    [Theory]
    [InlineData(512)] // Hits single segment path in AdvanceTo
    [InlineData(4098)] // Hits multiple segment path in AdvanceTo
    public async void HashValueMatchesExpected(int size) {
        var buffer = new byte[size];
        var rnd = new Random();
        rnd.NextBytes(buffer);

        var expected = Convert.ToHexString(SHA256.Create().ComputeHash(buffer));

        var pipe = new Pipe();
        pipe.Writer.Write(buffer);
        await pipe.Writer.FlushAsync();

        var hashPipe = pipe.Reader.HashPipe(HashAlgorithmName.SHA256);
        var result = await hashPipe.ReadAtLeastAsync(size);
        hashPipe.AdvanceTo(result.Buffer.End, result.Buffer.End);
        string hash = Convert.ToHexString(hashPipe.GetHash());

        Assert.Equal(expected, hash);
    }
}