using EltraCommon.Logger;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MPlayerMaster.Helpers
{
    static class AssembyHelpers
    {
        public static bool CreateFileFromResource(string embeddedFileName, string targetFile)
        {
            bool result = false;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames().First(s => s.EndsWith(embeddedFileName, StringComparison.CurrentCultureIgnoreCase));

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException("Could not load manifest resource stream.");
                    }

                    byte[] byteArray = new byte[stream.Length];

                    stream.Read(byteArray, 0, byteArray.Length);

                    File.WriteAllBytes(targetFile, byteArray);

                    result = true;
                }
            }
            catch (Exception e)
            {
                MsgLogger.Exception("AssembyHelpers - CreateFileFromResource", e);
            }

            return result;
        }
    }
}
