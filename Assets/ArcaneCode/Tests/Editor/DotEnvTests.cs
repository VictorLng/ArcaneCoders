using System;
using System.Collections.Generic;
using System.IO;
using ArcaneCode.Core;
using NUnit.Framework;

namespace ArcaneCode.Tests
{
    public sealed class DotEnvTests
    {
        [Test]
        public void ParserAcceptsCommentsWhitespaceQuotesAndEmptyValues()
        {
            var values=DotEnv.Parse("# perfil local\nARCANE_PROFILE_DIR = \"/tmp/arcane profile\"\nEMPTY=\nSINGLE='value'\nsem-separador\n");

            Assert.That(values["ARCANE_PROFILE_DIR"],Is.EqualTo("/tmp/arcane profile"));
            Assert.That(values["EMPTY"],Is.EqualTo(string.Empty));
            Assert.That(values["SINGLE"],Is.EqualTo("value"));
            Assert.That(values.ContainsKey("sem-separador"),Is.False);
        }

        [Test]
        public void LoadFileUsesTheDotEnvOnlyForUnsetVariables()
        {
            string path=Path.Combine(Path.GetTempPath(),"arcane-dotenv-"+Guid.NewGuid().ToString("N"));
            File.WriteAllText(path,"SYSTEM_WINS=from-file\nLOADED=from-file\n");
            var environment=new Dictionary<string,string> { { "SYSTEM_WINS","from-system" } };
            var applied=new Dictionary<string,string>();
            try
            {
                bool loaded=DotEnv.LoadFile(path,key=>environment.TryGetValue(key,out string value)?value:null,(key,value)=>applied[key]=value);

                Assert.That(loaded,Is.True);
                Assert.That(applied.ContainsKey("SYSTEM_WINS"),Is.False);
                Assert.That(applied["LOADED"],Is.EqualTo("from-file"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
