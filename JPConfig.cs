using System;
using System.IO;
using Newtonsoft.Json;

namespace JailPrison
{
    public class JPConfigFile
    {
        public bool jailmode = true;
        public bool prisonmode = true;
        public string jailcomm = "ireadtherules";
        public string groupname = "";
        public string guestgroupname = "";

        public static JPConfigFile Read(string path)
        {
            if (!File.Exists(path))
                return new JPConfigFile();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static JPConfigFile Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<JPConfigFile>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<JPConfigFile> ConfigRead;
    }
}