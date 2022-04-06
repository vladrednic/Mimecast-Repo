using A2A.MC.Kernel.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Common {
    public class ScenarioHelper {
        public string ScenarioFilePath { get; set; }

        public List<Search> GetSearches() {
            var list = new List<Search>();
            var first = true;
            using (var sr = File.OpenText(ScenarioFilePath)) {
                var line = string.Empty;
                while ((line = sr.ReadLine()) != null) {
                    if (first) {
                        first = false;
                        continue;
                    }
                    if (string.IsNullOrEmpty(line))
                        continue;
                    var search = ParseSearch(line);
                    list.Add(search);
                }
            }
            return list;
        }

        public Search ParseSearch(string line) {
            var fields = line.Split('\t');
            var search = new Search();
            search.ParseLine(fields);
            return search;
        }
    }
}
