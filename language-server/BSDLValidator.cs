using System.ComponentModel;
using System.Text.Json;

namespace BdslValidator
{
    internal class GrammarAnalyzer
    {
        public GrammarAnalyzer(string input)
        {
            _input = input;
        }
        private readonly string _input;
        public Error? Analyze()
        {
            try
            {
                var tokenizer = new Tokenizer(_input);
                // 词法分析
                var tokens = tokenizer.Tokenize();

                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(basePath, "tokens.json");
                string jsonString = JsonSerializer.Serialize(tokens, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 可选：不转义中文
                });
                File.WriteAllText(filePath, jsonString);
                
                // 语法分析和少量语义分析
                var syntaxAnalyzer = new SyntaxAnalyzer(tokens);
                syntaxAnalyzer.Analyze();
                
                return null;
            }catch( ErrorException ee)
            {
                return ee.Error;
            }
        }
    }
}
