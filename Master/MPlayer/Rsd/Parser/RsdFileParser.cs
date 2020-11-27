using EltraCommon.Logger;
using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using MPlayerMaster.Rsd.Models;
using EltraCommon.Helpers;

namespace MPlayerMaster.Rsd.Parser
{
    class RsdFileParser
    {
        private RadioStationEntriesModel _output;

        public RsdFileParser()
        {
            SerializeToJsonFile = true;
        }

        public RadioStationEntriesModel Output => _output ?? (_output = new RadioStationEntriesModel());

        public bool SerializeToJsonFile { get; set; }

        public bool ConvertRsdZipFileToJson(string filePath)
        {
            bool result = false;

            try
            {
                using (FileStream zipToOpen = new FileStream(filePath, FileMode.Open))
                {
                    result = ConvertZipArchiveToJson(zipToOpen);
                }
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - ConvertRsdZipFileToJson", e);
            }

            return result;
        }

        private bool ConvertZipArchiveToJson(FileStream zipToOpen)
        {
            bool result = false;

            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith(".rsd"))
                    {
                        result = ConvertRsdFileToJson(entry, zipToOpen.Name);
                    }
                }
            }

            return result;
        }

        private bool ConvertRsdFileToJson(ZipArchiveEntry entry, string zipFileName)
        {
            bool result = false;

            try
            {
                var fileName = Path.ChangeExtension(zipFileName, "json");

                var processor = new RsdEntryZipParser();

                using (var stream = entry.Open())
                {
                    var streamReader = new StreamReader(stream);

                    result = processor.GetRsdEntries(new StringReader(streamReader.ReadToEnd()), out var entries);

                    if (result)
                    {
                        Output.AddEntries(entries);

                        result = DoPostProcessing(fileName);
                    }
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - ConvertRsdFileToJson", e);
            }

            return result;
        }

        private bool DoPostProcessing(string fileName)
        {
            bool result = false;

            try
            {
                if (Output!=null && Output.Count > 0)
                {
                    CalculateMd5(fileName);

                    if (SerializeToJsonFile)
                    {
                        //serialize object
                        var json = JsonConvert.SerializeObject(Output);

                        File.WriteAllText(fileName, json);
                    }
                    
                    result = true;
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - DoPostProcessing", e);
            }

            return result;
        }

        private void CalculateMd5(string fileName)
        {
            //calculate md5
            var json = JsonConvert.SerializeObject(Output.Entries);

            Output.Md5 = CryptHelpers.ToMD5(json);
            Output.Name = new FileInfo(fileName).Name;
        }
    }
}
