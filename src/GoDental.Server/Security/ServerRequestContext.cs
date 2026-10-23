using GoDental.Application.DTOs;

namespace GoDental.Server.Security;

// Scoped: nunca compartido entre peticiones/usuarios. Sólo lo establece el handler autenticado.
public sealed class ServerRequestContext
{
    public ServerOperator? Operator { get; private set; }
    internal void Set(ServerOperator identity) => Operator = identity;
}
