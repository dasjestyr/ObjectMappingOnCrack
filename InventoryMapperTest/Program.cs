using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DAS.Core.Parsing.Csv;

namespace InventoryMapperTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var map = new MapDefinition();
            map.Load("test_map.json");
            

            var stream = File.OpenRead("test_data.csv");
            var reader = new CsvParser(stream)
                .UseHeaders()
                .UseQuotedFields();

            var outStream = File.Create("out_file.csv");
            
            var timer = new Stopwatch();
            timer.Start();
            map.Transform(reader, ',', outStream).Wait();
            timer.Stop();
            Console.WriteLine($"Finished in {timer.Elapsed}");
            Console.WriteLine(stream.Length);

            Console.ReadKey();
        }
    }
}
