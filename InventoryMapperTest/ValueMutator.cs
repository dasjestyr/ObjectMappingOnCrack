using System.Collections.Generic;

namespace InventoryMapperTest
{
    public class ValueMutator : Dictionary<string, string>, IMutator
    {
        public string Mutate(string sourceValue) => this[sourceValue];
    }
}