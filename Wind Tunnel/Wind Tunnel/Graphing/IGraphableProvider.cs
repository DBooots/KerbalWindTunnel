using System.Collections.Generic;

namespace KerbalWindTunnel.Graphing
{
    public interface IGraphableProvider
    {
        List<IGraphable> Graphables { get; }
        IGraphable GetGraphableByName(string graphName);
    }
}
