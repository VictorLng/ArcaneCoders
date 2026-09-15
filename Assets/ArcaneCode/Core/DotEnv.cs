using System;
using System.Collections.Generic;
using System.IO;

namespace ArcaneCode.Core
{
    public static class DotEnv
    {
        public static IReadOnlyDictionary<string,string> Parse(string source)
        {
            var values=new Dictionary<string,string>(StringComparer.Ordinal);
            if (source==null) return values;
            string[] lines=source.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
            foreach (string raw in lines)
            {
                string line=raw.Trim();
                if (line.Length==0 || line.StartsWith("#",StringComparison.Ordinal)) continue;
                int separator=line.IndexOf('=');
                if (separator<=0) continue;
                string key=line.Substring(0,separator).Trim();
                string value=line.Substring(separator+1).Trim();
                if (key.Length==0) continue;
                if (value.Length>=2 && (value[0]=='\"' && value[value.Length-1]=='\"' || value[0]=='\'' && value[value.Length-1]=='\'')) value=value.Substring(1,value.Length-2);
                values[key]=value;
            }
            return values;
        }

        public static bool LoadFile(string path)
        {
            return LoadFile(path,Environment.GetEnvironmentVariable,Environment.SetEnvironmentVariable);
        }

        public static bool LoadFile(string path,Func<string,string> getEnvironmentVariable,Action<string,string> setEnvironmentVariable)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            foreach (KeyValuePair<string,string> value in Parse(File.ReadAllText(path)))
                if (getEnvironmentVariable(value.Key)==null) setEnvironmentVariable(value.Key,value.Value);
            return true;
        }
    }
}
