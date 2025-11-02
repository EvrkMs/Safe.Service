using System.Threading;
using System.Threading.Tasks;

namespace Safe.Host.Introspection;

public interface ITokenIntrospector
{
    Task<TokenIntrospectionResult> IntrospectAsync(string token, CancellationToken cancellationToken = default);
}
