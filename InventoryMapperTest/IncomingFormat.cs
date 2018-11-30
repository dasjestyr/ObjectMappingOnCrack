using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace InventoryMapperTest
{
    public class IncomingFormat
    {
        public bool IsMapped { get; set; }

        public string Type { get; set; }

        public bool ExpectNull { get; set; }

        public string Destination { get; set; }

        public Dictionary<string, JObject> MutatorInfo { get; set; }

        public override string ToString()
        {
            return IsMapped ? $"-->{Destination}" : "unmapped";
        }
    }
}