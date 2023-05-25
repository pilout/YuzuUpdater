using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace YuzuEAUpdater
{
    public class PR
    {
        public PR(string prVersion)
        {
            idIssue = Regex.Match(prVersion, @"<a id=""issue_(\d*)_link").Groups[1].Value;
            description = Regex.Match(prVersion, @"<a id=""issue_\d*_link"".*>(.*)</a>").Groups[1].Value;
            releaseDate = DateTime.Parse(Regex.Match(prVersion, @"datetime=""(.*)Z""").Groups[1].Value + "Z");

            var labels = Regex.Matches(prVersion, @"data-name=""(.*)"" style=");
            foreach (Match label in labels)
            {
                this.label += label.Groups[1].Value + " ";
            }
        }

        public string idIssue;
        public string description;
        public DateTime releaseDate;
        public string label = "";

    }

}
