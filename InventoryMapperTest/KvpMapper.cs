using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace InventoryMapperTest
{
    /* TODO:
     * - Scan config file to make sure there are mutators for all mutatorInfos
     * - Create a way to register more IMutator implementations
     */

    public class KvpMapper
    {
    }

    public class MapDefinition
    {
        private readonly Dictionary<string, int> _namedIndexes = new Dictionary<string, int>();
        private Dictionary<string, IncomingFormat> _configuration;

        public void Load(string path)
        {
            var json = File.ReadAllText(path);
            _configuration = JsonConvert.DeserializeObject<Dictionary<string, IncomingFormat>>(json);
            var asList = _configuration.ToList();
            for (var i = 0; i < _configuration.Count; i++)
                _namedIndexes.Add(asList[i].Key, i);
        }

        /// <summary>
        /// Gets the index map. Key = Source Index, Value = Destination Index
        /// </summary>
        /// <param name="incomingHeaders">The incoming headers.</param>
        /// <param name="throwOnMissingMap">if set to <c>true</c> [throw on missing map].</param>
        /// <returns></returns>
        /// <exception cref="FormatException">Could not find a mapping for '{header}</exception>
        public IDictionary<int, string> GetIndexMap(IEnumerable<string> incomingHeaders, bool throwOnMissingMap = false)
        {
            var map = new Dictionary<int, string>();
            foreach (var header in incomingHeaders)
            {
                if (!_namedIndexes.TryGetValue(header, out var index))
                {
                    if(throwOnMissingMap) throw new FormatException($"Could not find a mapping for '{header}' in the configuration.");
                    continue;
                };

                if(!_configuration[header].IsMapped) continue;
                map.Add(index, _configuration[header].Destination);
            }

            return map;
        }

        // reuse this for performance
        private readonly Dictionary<string, string> _newObject = new Dictionary<string, string>();

        public IDictionary<string, string> Remap(IDictionary<string, string> source)
        {
            _newObject.Clear();
            foreach (var entry in source)
            {
                if (!_configuration.TryGetValue(entry.Key, out var map)) continue;
                _newObject.Add(map.Destination, entry.Value);
            }

            return _newObject;
        }

        public IDictionary<string, string> Remap(string[] sourceHeaders, string[] sourceValues)
        {
            _newObject.Clear();
            if(sourceHeaders.Length != sourceValues.Length)
                throw new Exception("Uneven headers/fields");

            for (var i = 0; i < sourceHeaders.Length; i++)
            {
                if (!_configuration.TryGetValue(sourceHeaders[i], out var format) || !format.IsMapped) continue;
                _newObject.Add(format.Destination, Mutate(sourceValues[i], format));
            }

            return _newObject;
        }

        private static string Mutate(string value, IncomingFormat format)
        {
            // maybe not make this an enum
            if (format.MutatorInfo == null || !format.MutatorInfo.TryGetValue(format.Type, out var mutator))
                return value;

            var mutateFunc = MutatorFactory.GetMutator(format.Type, mutator);
            return mutateFunc == null ? value : mutateFunc(value);
        }
    }

    public struct IndexMapItem
    {
        public int SourceIndex;
        public int DestinationIndex;

        public IndexMapItem(int sourceIndex, int destinationIndex)
        {
            SourceIndex = sourceIndex;
            DestinationIndex = destinationIndex;
        }
    }

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

    public class MutatorFactory
    {
        private static readonly Dictionary<string, Action<JObject>> FlyweightInitializer = new Dictionary<string, Action<JObject>>();
        private static readonly Dictionary<string, Func<string, string>> Mutators = new Dictionary<string, Func<string, string>>();

        public MutatorFactory()
        {
            Register<DateTimeMutator>("date");
            Register<EnumMap>("enum");
        }

        public static Func<string, string> GetMutator(string type, JObject mutatorInfo)
        {
            // flyweight
            if (Mutators.TryGetValue(type, out var mutator))
                return mutator;

            FlyweightInitializer[type](mutatorInfo);
            return Mutators[type];
        }

        public static void Register<T>(string type)
            where T : IMutator
        {
            FlyweightInitializer.Add(type, j => Mutators.Add(type, j.ToObject<T>().Mutate));
        }
    }

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

    public class EnumMap : Dictionary<string, string>, IMutator
    {
        public string Mutate(string sourceValue) => this[sourceValue];
    }

    public interface IMutator
    {
        string Mutate(string sourceValue);
    }
}
