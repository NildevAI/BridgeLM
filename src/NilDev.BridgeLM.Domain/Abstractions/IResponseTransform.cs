namespace NilDev.BridgeLM.Domain.Abstractions;

public interface IResponseTransform
{
    ValueTask<string> TransformAsync(string responseBody, CancellationToken cancellationToken);
}
