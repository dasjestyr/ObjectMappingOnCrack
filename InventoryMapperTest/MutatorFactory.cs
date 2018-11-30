using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace InventoryMapperTest
{
    public static class MutatorFactory
    {
        private static readonly Dictionary<string, Action<JObject>> FlyweightInitializer 
            = new Dictionary<string, Action<JObject>>();
        
        private static readonly Dictionary<string, Func<string, string>> Mutators 
            = new Dictionary<string, Func<string, string>>();

        static MutatorFactory()
        {
            Register<DateTimeMutator>("date");
            Register<ValueMutator>("enum");
        }

        public static Func<string, string> GetMutator(string label, JObject mutatorInfo)
        {
            // flyweight
            if (Mutators.TryGetValue(label, out var mutator))
                return mutator;

            if(!FlyweightInitializer.ContainsKey(label))
                throw new Exception($"Could not find an initializer for '{label}' mutator. Did you forget to register it?");
            
            FlyweightInitializer[label](mutatorInfo);
            return Mutators[label];
        }

        public static void Register<T>(string label)
            where T : IMutator
        {
            FlyweightInitializer.Add(
                label, 
                j => Mutators.Add(label, j.ToObject<T>().Mutate));
        }
    }
}