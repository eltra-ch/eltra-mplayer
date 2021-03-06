﻿using EltraCommon.Helpers;
using EltraCommon.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;

namespace MPlayerCommon.Contracts.Media
{
    [DataContract]
    public class MediaStore
    {
        #region Private fields

        private List<Artist> _artists;
        private string _json;
        private byte[] _cache;

        #endregion

        #region Properties

        [DataMember]
        public List<Artist> Artists
        {
            get => _artists ?? (_artists = new List<Artist>());
            set
            {
                _artists = value;
            }
        }

        #endregion

        #region Methods

        public byte[] Serialize(bool compress = true)
        {
            byte[] result = null;

            try
            {
                var json = JsonSerializer.Serialize(this);

                if (_json != json)
                {
                    _json = json;

                    if (compress)
                    {
                        result = ZipHelper.Compress(json);

                        _cache = result;
                    }
                    else
                    {
                        result = Encoding.UTF8.GetBytes(json);

                        _cache = result;
                    }
                }
                else
                {
                    result = _cache;
                }
            }
            catch(Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Serialize", e);
            }

            return result;
        }

        public static MediaStore Deserialize(byte[] data, bool compressed = true)
        {
            MediaStore result = null;

            try
            {
                if (compressed)
                {
                    var json = ZipHelper.Deflate(data);
                    var store = JsonSerializer.Deserialize<MediaStore>(json);

                    if (store != null)
                    {
                        result = store;
                    }
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"MediaStore - Deserialize", e);
            }

            return result;
        }

        private void Add(Artist artist)
        {
            artist.Build();

            var artists = new List<Artist>();
            
            Artists.Add(artist);

            Artists = Artists.OrderBy(o => o.Name).ToList();
        }

        public bool Build(string musicPath)
        {
            bool result = false;

            try
            {
                Artists.Clear();
                    
                var files = Directory.GetDirectories(musicPath);

                foreach (var path in files)
                {
                    var artist = new Artist(path);

                    Add(artist);
                }

                result = true;
            }
            catch (Exception e)
            {
                MsgLogger.Exception($"{GetType().Name} - Build", e);
            }

            return result;
        }

        #endregion
    }
}
