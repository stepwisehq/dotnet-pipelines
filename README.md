StepWise.IO.Piplines
====================

This project contains useful pipeline wrappers and utilities.

## Getting Started

```
dotnet add package StepWise.IO.Pipelines
```

### HashPipe

`HashPipeReader` and `HashPipeWriter` compute incremental hash sums on the data passing through the wrapped pipe.

`HashPipeReader` incrementally computes the hash on _examined_ data.

This can be useful for computing etags or other data fingerprints.

```csharp
var buffer = new byte[4098];
var rnd = new Random();
rnd.NextBytes(buffer);

var pipe = new Pipe();
pipe.Writer.Write(buffer);
await pipe.Writer.FlushAsync();

// ♫ I've got my hash pipe ♫
var hashPipe = pipe.Reader.HashPipe(HashAlgorithmName.SHA256);

var result = await hashPipe.ReadAtLeastAsync(size);
hashPipe.AdvanceTo(result.Buffer.End, result.Buffer.End);

string hash = Convert.ToHexString(hashPipe.GetHash());
```