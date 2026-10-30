// Genera una pagina di Bitfocus Companion (.companionconfig) con i tasti di LiveCount.
// Formato "page" versione 9: Companion lo aggiorna da solo all'importazione.
// Ogni tasto usa il modulo "Generic HTTP" per chiamare l'API di LiveCount (RemoteApi).
using System;
using System.Collections.Generic;
using System.Text;

namespace LiveCount
{
    public static class CompanionExport
    {
        const string ConnectionId = "livecount_http";

        // Colori nella palette di LiveCue/LiveCount (RGB come intero, come li salva Companion).
        const int White = 0xFFFFFF, Ink = 0x0D0C22;
        const int Green = 0x22A046, Amber = 0xFFB300, Grey = 0x626A7A;
        const int PlusBg = 0x1E5E36, MinusBg = 0x7A2A2A, PresetBg = 0x2E3440;

        class Key
        {
            public string Text, Url;
            public int Bg, Fg = White;
        }

        // columns x rows: dimensione del deck (5x3 = Stream Deck classico, 8x4 = XL).
        public static string Build(IList<Preset> presets, string baseUrl, int columns, int rows, out int hiddenPresets)
        {
            var grid = new Dictionary<int, Dictionary<int, Key>>();
            Action<int, int, Key> put = delegate(int r, int c, Key k)
            {
                if (!grid.ContainsKey(r)) grid[r] = new Dictionary<int, Key>();
                grid[r][c] = k;
            };

            // Prime tre colonne: trasporto e minuti.
            put(0, 0, new Key { Text = "AVVIA", Url = "start", Bg = Green });
            put(0, 1, new Key { Text = "PAUSA", Url = "pause", Bg = Amber, Fg = Ink });
            put(0, 2, new Key { Text = "RESET", Url = "reset", Bg = Grey });
            int[] steps = { 1, 5, 10 };
            for (int i = 0; i < 3; i++)
            {
                put(1, i, new Key { Text = "+" + steps[i] + "\\nMIN", Url = "add?min=" + steps[i], Bg = PlusBg });
                put(2, i, new Key { Text = "-" + steps[i] + "\\nMIN", Url = "add?min=-" + steps[i], Bg = MinusBg });
            }

            // Preset: riempiono i tasti liberi riga per riga, poi proseguono sotto.
            var free = new List<int[]>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    if (!(grid.ContainsKey(r) && grid[r].ContainsKey(c))) free.Add(new[] { r, c });
            int extraRow = rows, extraCol = 0;
            hiddenPresets = 0;
            foreach (var p in presets)
            {
                int r, c;
                if (free.Count > 0) { r = free[0][0]; c = free[0][1]; free.RemoveAt(0); }
                else
                {
                    r = extraRow; c = extraCol; hiddenPresets++;
                    if (++extraCol >= columns) { extraCol = 0; extraRow++; }
                }
                put(r, c, new Key
                {
                    Text = Escape(p.Name) + "\\n" + Duration(p.Seconds),
                    Url = "preset?name=" + Uri.EscapeDataString(p.Name),
                    Bg = PresetBg
                });
            }
            int maxRow = Math.Max(rows - 1, extraCol > 0 ? extraRow : extraRow - 1);

            var sb = new StringBuilder();
            sb.Append("{\n\"version\": 9,\n\"type\": \"page\",\n\"companionBuild\": \"LiveCount\",\n");
            sb.Append("\"page\": {\n\"id\": \"livecount\",\n\"name\": \"LiveCount\",\n\"controls\": {");
            bool firstRow = true;
            foreach (var row in grid)
            {
                sb.Append(firstRow ? "\n" : ",\n").Append('"').Append(row.Key).Append("\": {");
                firstRow = false;
                bool firstCol = true;
                foreach (var col in row.Value)
                {
                    sb.Append(firstCol ? "\n" : ",\n").Append('"').Append(col.Key).Append("\": ");
                    firstCol = false;
                    AppendButton(sb, col.Value);
                }
                sb.Append("\n}");
            }
            sb.Append("\n},\n\"gridSize\": {\"minColumn\": 0, \"maxColumn\": ").Append(Math.Max(7, columns - 1))
              .Append(", \"minRow\": 0, \"maxRow\": ").Append(Math.Max(3, maxRow)).Append("}\n},\n");

            sb.Append("\"instances\": {\n\"").Append(ConnectionId).Append("\": {");
            sb.Append("\"label\": \"LiveCount\", \"moduleId\": \"generic-http\", \"enabled\": true, \"sortOrder\": 0, ");
            sb.Append("\"isFirstInit\": false, \"lastUpgradeIndex\": 3, ");
            sb.Append("\"config\": {\"prefix\": ").Append(RemoteApi.JsonString(baseUrl))
              .Append(", \"proxyAddress\": \"\", \"authType\": \"none\", \"authUser\": \"\", \"rejectUnauthorized\": true, \"insecureHTTPParser\": false}");
            sb.Append("}\n},\n\"connectionCollections\": [],\n\"oldPageNumber\": 1\n}\n");
            return sb.ToString();
        }

        static void AppendButton(StringBuilder sb, Key k)
        {
            sb.Append("{\"type\": \"button\", \"style\": {\"text\": \"").Append(k.Text)
              .Append("\", \"textExpression\": false, \"size\": \"auto\", \"png64\": null, \"alignment\": \"center:center\", \"pngalignment\": \"center:center\", ")
              .Append("\"color\": ").Append(k.Fg).Append(", \"bgcolor\": ").Append(k.Bg).Append(", \"show_topbar\": \"default\"}, ");
            sb.Append("\"options\": {\"stepProgression\": \"auto\", \"stepExpression\": \"\", \"rotaryActions\": false}, ");
            sb.Append("\"feedbacks\": [], ");
            sb.Append("\"steps\": {\"0\": {\"action_sets\": {\"down\": [{\"id\": \"").Append(NewId())
              .Append("\", \"definitionId\": \"get\", \"connectionId\": \"").Append(ConnectionId)
              .Append("\", \"type\": \"action\", \"upgradeIndex\": 3, \"children\": {}, \"options\": {")
              // Companion 5 salva le opzioni dei moduli come { isExpression, value }.
              .Append("\"url\": ").Append(Opt(RemoteApi.JsonString(k.Url)))
              .Append(", \"header\": ").Append(Opt("\"\""))
              .Append(", \"jsonResultDataVariable\": ").Append(Opt("\"\""))
              .Append(", \"result_stringify\": ").Append(Opt("true"))
              .Append(", \"statusCodeVariable\": ").Append(Opt("\"\""))
              .Append("}}], \"up\": []}, ")
              .Append("\"options\": {\"runWhileHeld\": []}}}, ");
            sb.Append("\"localVariables\": []}");
        }

        static string Opt(string jsonValue)
        {
            return "{\"isExpression\": false, \"value\": " + jsonValue + "}";
        }

        // Testo gia' pronto per l'interno di una stringa JSON.
        static string Escape(string s)
        {
            string j = RemoteApi.JsonString(s);
            return j.Substring(1, j.Length - 2);
        }

        static string Duration(int secs)
        {
            int h = secs / 3600, m = (secs % 3600) / 60, s = secs % 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + s.ToString("00") : m.ToString("00") + ":" + s.ToString("00");
        }

        static string NewId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 21);
        }
    }
}
