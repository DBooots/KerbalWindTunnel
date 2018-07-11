using System.Collections.Generic;
using KerbalWindTunnel.Graphing;

namespace KerbalWindTunnel.DataGenerators
{
    public interface IGraphableProvider
    {
        List<IGraphable> Graphables { get; }
        IGraphable GetGraphableByName(string graphName);
    }
}
