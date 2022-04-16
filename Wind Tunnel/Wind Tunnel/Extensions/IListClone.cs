using System;
using System.Collections;
using System.Linq;

namespace KerbalWindTunnel.Extensions
{
    public static class IListClone
    {
        public static IList ToIList(this IList original)
        {
            IList result = (IList)Activator.CreateInstance(original.GetType());
            int length = original.Count;
            for (int i = 0; i < length; i++)
                result.Add(original[i]);
            return result;
        }
        public static IList ToIList(this IList original, Func<IList> constructor)
        {
            IList result = constructor();
            int length = original.Count;
            for (int i = 0; i < length; i++)
                result.Add(original[i]);
            return result;
        }
    }
}
