using System.Collections;
using System.Text;

namespace Verifex.CodeGen;

public static class RuntimeUtils
{
    public static string PrettyPrint(object obj)
    {
        var visited = new HashSet<object>();
        return PrettyPrintInternal(obj, visited);
    }

    private static string PrettyPrintInternal(object obj, HashSet<object> visited)
    {
        // Prevent infinite recursion on circular references
        if (obj is not string && visited.Contains(obj))
            return "(circular ref)";

        if (obj is not string)
            visited.Add(obj);

        switch (obj)
        {
            case string s:
                return $"\"{s}\"";

            case IDictionary dict:
                var dictSb = new StringBuilder();
                dictSb.Append("{ ");
                bool firstDict = true;
                foreach (DictionaryEntry entry in dict)
                {
                    if (!firstDict) dictSb.Append(", ");
                    dictSb.Append($"{PrettyPrintInternal(entry.Key, visited)}: {PrettyPrintInternal(entry.Value, visited)}");
                    firstDict = false;
                }
                dictSb.Append(" }");
                return dictSb.ToString();

            case IEnumerable enumerable when !(obj is string):
                var listSb = new StringBuilder();
                listSb.Append("[ ");
                bool firstList = true;
                foreach (var item in enumerable)
                {
                    if (!firstList) listSb.Append(", ");
                    listSb.Append(PrettyPrintInternal(item, visited));
                    firstList = false;
                }
                listSb.Append(" ]");
                return listSb.ToString();

            default:
                return obj.ToString();
        }
    }
}