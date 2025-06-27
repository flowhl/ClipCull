using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTOOWA.AvalonDockTheme;

internal static class ResourceUtility {
    public static Uri GetUri(string assemblyName, string path) {
        return new Uri($"pack://application:,,,/{assemblyName};component/{path}", UriKind.Absolute);
    }
}