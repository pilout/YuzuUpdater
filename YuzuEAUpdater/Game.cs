using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YuzuEAUpdater
{
    public class Game
    {
        public string id;
        public string name;
        public string pathApp;
        public List<BananaMod> bananaMods = new List<BananaMod>();

        public string pathMods
        {
            get
            {
                return pathApp + "\\load\\" + this.id;
            }
        }


        public List<BananaMod> validBananaMods => bananaMods.Where(x => x._sModelName == "Mod" && !x._bIsObsolete && x._bHasFiles).ToList();
 

        public Game(string id, string name, string pathApp)
        {
            this.id = id;
            this.name = name;
            this.pathApp = pathApp + "\\load\\" + id + "\\";
        }

        public void loadMods(IProgress<float> progess =  null)
        {
            if (bananaMods.Count == 0)
            {
                HttpClient _httpClient = new HttpClient();

                String src = _httpClient.GetAsync("https://gamebanana.com/apiv11/Util/Game/NameMatch?_sName=" + name.Replace(" ", "+").Replace("™","") + "&_nPerpage=10&_nPage=1").Result.Content.ReadAsStringAsync().Result;
                String idGameBanana = Regex.Match(src, @"""_idRow"": (\d+)").Groups[1].Value;
                BananaResponse bananaResponse = null;
                int p = 1;
                while (bananaResponse == null || bananaResponse._aRecords.Count > 0)
                {
                    src = _httpClient.GetAsync("https://gamebanana.com/apiv11/Game/" + idGameBanana + "/Subfeed?_nPage=" + p + "&_sSort=default").Result.Content.ReadAsStringAsync().Result;
                    bananaResponse = JsonSerializer.Deserialize<BananaResponse>(src);
                    bananaResponse._aRecords.ForEach((m) =>
                    {
                        m.pathApp = this.pathApp;
                    });

                    bananaMods.AddRange(bananaResponse._aRecords);
                    p++;

                    if(progess != null)
                    {
                        progess.Report(0.1f);
                    }
                }
            }

            if(progess != null)
                progess.Report(1f);
        }
    }


}
