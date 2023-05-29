using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace YuzuEAUpdater
{
    public class Release
    {
        public Release(string releaseVersion)
        {

            this.version = Regex.Match(releaseVersion, @""">(.*)</h2>").Groups[1].Value;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Linux-Yuzu-" + this.version + ".AppImage";
            else
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Windows-Yuzu-" + this.version + ".zip";


            this.releaseDate = DateTime.Parse(Regex.Match(releaseVersion, @"datetime=""(.*)"">").Groups[1].Value);

        }

        public Release(string version, bool none)
        {
            if (version == null)
            {
                this.version = "EA-0";
            }
            else
            {
                this.version = "EA-" + version;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Linux-Yuzu-" + this.version + ".AppImage";
            else
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Windows-Yuzu-" + this.version + ".zip";

            this.releaseDate = DateTime.MinValue;
        }

        public string version;
        public string downloadUrl;
        public DateTime releaseDate;
    }

}
