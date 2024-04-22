using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using Xunit;

namespace StepWise.IO.Pipelines.Test;

public class HashPipeTest {

    [Theory]
    [InlineData(512)] // Hits single segment path in AdvanceTo
    [InlineData(4098)] // Hits multiple segment path in AdvanceTo
    public async void ComputesCorrectHashes(int size) {
        var buffer = new byte[size];
        var rnd = new Random();
        rnd.NextBytes(buffer);

        var expected = Convert.ToHexString(SHA256.Create().ComputeHash(buffer));

        var pipe = new Pipe();
        var writer = pipe.Writer.AsHashPipe(HashAlgorithmName.SHA256);
        var writeStream = writer.AsStream();
        var reader = pipe.Reader.AsHashPipe(HashAlgorithmName.SHA256);
        var streamReader = new StreamReader(reader.AsStream());

        await writeStream.WriteAsync(buffer);
        await writeStream.FlushAsync();
        writeStream.Close();
        string writerHash = Convert.ToHexString(writer.GetHash());

        await streamReader.ReadToEndAsync();
        string readerHash = Convert.ToHexString(reader.GetHash());

        Assert.Equal(expected, writerHash);
        Assert.Equal(expected, readerHash);
    }
}