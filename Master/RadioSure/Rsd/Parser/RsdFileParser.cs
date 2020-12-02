using EltraCommon.Logger;
using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using RadioSureMaster.Rsd.Models;
using EltraCommon.Helpers;

namespace RadioSureMaster.Rsd.Parser
{
    class RsdFileParser
    {
        #region Private fields

        private RadioStationEntriesModel _output;
        private string _rsdFileName;

        #endregion

        #region Constructors

        public RsdFileParser()
        {
            SerializeToJsonFile = true;
        }

        #endregion

        #region Properties

        public RadioStationEntriesModel Output => _output ?? (_output = new RadioStationEntriesModel());

        public bool SerializeToJsonFile { get; set; }

        public string RsdFileName
        {
            get => _rsdFileName;
            set
            {
                _rsdFileName = value;
                OnRsdFileNameChanged();
            }
        }

        #endregion

        #region Events

        public event EventHandler<string> RsdFileNameChanged;

        private void OnRsdFileNameChanged()
        {
            RsdFileNameChanged?.Invoke(this, RsdFileName);
        }

        #endregion

        #region Methods

        public bool ConvertRsdZipFileToJson(string filePath)
        {
            bool result = false;

            try
            {
                using (FileStream zipToOpen = new FileStream(filePath, FileMode.Open))
                {
                    result = ConvertZipArchiveToJson(zipToOpen, zipToOpen.Name);
                }
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - ConvertRsdZipFileToJson", e);
            }

            return result;
        }

        public bool ConvertZipArchiveToJson(Stream zipToOpen, string fileName)
        {
            bool result = false;

            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith(".rsd"))
                    {
                        result = ConvertRsdFileToJson(entry, fileName);

                        if(result)
                        {
                            RsdFileName = entry.Name;
                        }
                    }
                }
            }

            return result;
        }

        private bool ConvertRsdFileToJson(ZipArchiveEntry entry, string rsdFileName)
        {
            bool result = false;

            try
            {
                var fileName = Path.ChangeExtension(rsdFileName, "json");

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

        #endregion
    }
}
