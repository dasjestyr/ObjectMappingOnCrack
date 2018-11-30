using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DAS.Core.Parsing.Csv;
using Newtonsoft.Json;

namespace InventoryMapperTest
{
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
        public IDictionary<int, string> GetIndexToNameMap(IEnumerable<string> incomingHeaders, bool throwOnMissingMap = false)
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

        // reuse this for performance reasons
        private readonly Dictionary<string, string> _newObject = new Dictionary<string, string>();

        public IDictionary<string, string> Remap(IDictionary<string, string> source)
        {
            _newObject.Clear();
            return Remap(source.Keys.ToArray(), source.Values.ToArray());
        }

        public IDictionary<string, string> Remap(string[] sourceHeaders, string[] sourceValues)
        {
            _newObject.Clear();
            if(sourceHeaders.Length != sourceValues.Length)
                throw new Exception("Uneven headers/fields.");

            for (var i = 0; i < sourceHeaders.Length; i++)
            {
                if (!_configuration.TryGetValue(sourceHeaders[i], out var format) || !format.IsMapped) continue;
                _newObject.Add(format.Destination, Mutate(sourceValues[i], format));
            }

            return _newObject;
        }

        public async Task Transform(CsvParser parser, char outputDelimiter, Stream outStream)
        {
            var writer = new StreamWriter(outStream);
            var currentLine = new List<string>();
            var wroteHeader = false;
            var outHeaders = new List<string>();
            var captureIndexes = new List<int>();
            while (parser.Read()) // each record
            {
                if (!wroteHeader) // TODO: expose 'use headers' and delimiter value on parser
                {
                    
                    for (var i = 0; i < parser.Headers.Length; i++)
                    {
                        // only get the headers for which we have mappings
                        if(!_configuration.TryGetValue(parser.Headers[i], out var format) || !format.IsMapped) continue;
                        outHeaders.Add(format.Destination);
                        captureIndexes.Add(i);
                    }
                    
                    await writer.WriteLineAsync(string.Join(outputDelimiter, outHeaders)).ConfigureAwait(false);
                    wroteHeader = true;
                }
                
                
                currentLine.Clear();
                for (var i = 0; i < parser.CurrentRecord.Length; i++) // each value
                {
                    // TODO: based on value of i, only capture value if we have a header for it
                    if (!_configuration.TryGetValue(parser.Headers[i], out var format) || 
                        !format.IsMapped ||
                        !captureIndexes.Contains(i)) continue;
                    
                    currentLine.Add(
                        // TODO: if parser is using quotes, this should too
                        $"\"{Mutate(parser.CurrentRecord[i], format)}\"");
                }

                await writer.WriteLineAsync(string.Join(outputDelimiter, currentLine)).ConfigureAwait(false);
            }
        }

        private static string Mutate(string value, IncomingFormat format)
        {
            if (format.MutatorInfo == null || !format.MutatorInfo.TryGetValue(format.Type, out var mutatorSettings))
                return value;

            var mutateFunc = MutatorFactory.GetMutator(format.Type, mutatorSettings);
            return mutateFunc == null ? value : mutateFunc(value);
        }
    }
}