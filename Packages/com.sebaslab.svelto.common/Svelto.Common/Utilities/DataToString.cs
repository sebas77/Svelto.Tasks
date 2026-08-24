using System.Collections.Generic;

namespace Svelto.Utilities
{
    public static class DataToString
    {
        public static string DetailString(Dictionary<string, string> data)
        {
            string detailString = string.Empty;

            {
                int index = 0;
                foreach (var value in data)
                {
                    if (index++ < data.Count - 1)
                        detailString = detailString.FastConcat("<color=teal>\"").FastConcat(value.Key, "\"")
                                                   .FastConcat(":\"", value.Value, "\",</color>");
                    else
                        detailString = detailString.FastConcat("<color=teal>\"").FastConcat(value.Key, "\"")
                                                   .FastConcat(":\"", value.Value, "\"</color>");
                }
            }

            return detailString;
        }
    }
}