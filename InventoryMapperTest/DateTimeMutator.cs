using System;
using System.Globalization;

namespace InventoryMapperTest
{
    public class DateTimeMutator : IMutator
    {
        public string SourceFormat { get; set; }

        public string DestinationFormat { get; set; }

        public string Mutate(string input)
        {
            var source = DateTimeOffset.ParseExact(input, SourceFormat, CultureInfo.InvariantCulture);
            return source.ToString(DestinationFormat);
        }
    }
}