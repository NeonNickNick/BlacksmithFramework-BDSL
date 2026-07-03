
using System.Text.RegularExpressions;

namespace BdslValidator
{
    internal enum TokenType
    {
        Arrow,
        LogicalOperator,
        ArithmaticOperator,
        Int,
        Float,
        Bool,
        Keyword,
        DSLSentenceMark,
        Require,
        LeftParenthesis,
        RightParenthesis,
        ExclamationMark,
        LeftBrace,
        RightBrace,
        String,
        ParamName,
        ResourceType,
        HPType,
        AttackType,
        DefenseType,
        DefenseAnalyzer,
        SkillParam,
        LazyLayerNum
    }
    internal record Token(TokenType Type, string Value, int Line, int StartColumn);
    internal class Tokenizer
    {
        public Tokenizer(string input)
        {
            _input = input;
        }
        private string _input;
        private int _currentPos = 0;
        private int _currentLine = 1;
        private int _currentCol = 1;
        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while(_currentPos < _input.Length)
            {
                var token = NextToken();
                if(token != null)
                {
                    tokens.Add(token);
                }
            }
            return tokens;
        }
        private Token? NextToken()
        {
            SkipWhitespace();
            if(_currentPos >= _input.Length)
            {
                return null;
            }

            var startLine = _currentLine;
            var startCol = _currentCol;

            // 单行注释
            {
                if (Match(StandardTokens.SingleLineComment))
                {
                    while (_currentPos < _input.Length && _input[_currentPos] != '\n')
                    {
                        Advance();
                    }
                    return null;
                }
            }

            // 多行注释
            {
                if (Match(StandardTokens.MultipleLineCommentLeft))
                {
                    while (_currentPos < _input.Length && !Match(StandardTokens.MultipleLineCommentRight))
                    {
                        Advance();
                    }
                    return null;
                }
            }

            // 箭头
            {
                if (Match(StandardTokens.Arrow))
                {
                    return new(Type: TokenType.Arrow, Value: StandardTokens.Arrow, Line: startLine, StartColumn: startCol);
                }
            }

            // SkillParam参数
            {
                if (Match(StandardTokens.SkillParam))
                {
                    return new(Type: TokenType.SkillParam, Value: StandardTokens.SkillParam, Line: startLine, StartColumn: startCol);
                }
            }

            // 资源类型
            {
                foreach (var r in StandardTokens.ResourceTypes)
                {
                    if (Match(r))
                    {
                        return new(Type: TokenType.ResourceType, Value: r, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // 生命类型
            {
                foreach (var h in StandardTokens.HPTypes)
                {
                    if (Match(h))
                    {
                        return new(Type: TokenType.HPType, Value: h, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // 攻击类型
            {
                foreach (var a in StandardTokens.AttackTypes)
                {
                    if (Match(a))
                    {
                        return new(Type: TokenType.AttackType, Value: a, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // 防御类型
            {
                foreach (var d in StandardTokens.DefenseTypes)
                {
                    if (Match(d))
                    {
                        return new(Type: TokenType.DefenseType, Value: d, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // 防御分析器类型
            {
                foreach (var d in StandardTokens.DefenseAnalyzers)
                {
                    if (Match(d))
                    {
                        return new(Type: TokenType.DefenseAnalyzer, Value: d, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // 逻辑运算符，值得注意的是全部是双字符运算符
            {
                if (_currentPos + 1 < _input.Length)
                {
                    var two = _input.Substring(_currentPos, 2);
                    if (StandardTokens.LogicalOperators.Contains(two))
                    {
                        Advance();
                        Advance();
                        return new Token(Type: TokenType.LogicalOperator, Value: two, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // 算数运算符，值得注意的是全部是单字符运算符
            {
                if (StandardTokens.ArithmaticOperators.Contains(_input[_currentPos].ToString()))
                {
                    var ch = _input[_currentPos].ToString();
                    Advance();
                    return new Token(Type: TokenType.ArithmaticOperator, ch, Line: startLine, StartColumn: startCol);
                }
            }

            // 数字
            {
                if (char.IsDigit(_input[_currentPos]))
                {
                    bool hasDot = false;
                    var start = _currentPos;
                    while (_currentPos < _input.Length && (char.IsDigit(_input[_currentPos]) || _input[_currentPos] == '.'))
                    {
                        if (_input[_currentPos] == '.')
                        {
                            if (hasDot)
                            {
                                break;
                            }
                            if (!hasDot)
                            {
                                hasDot = true;
                            }
                        }
                        Advance();
                    }
                    var res = _input[start.._currentPos];
                    if (res.EndsWith("."))
                    {
                        throw new ErrorException(new(ErrorSpans: new()
                    {
                        new(Line: startLine, StartColumn: startCol, EndColumn: startCol + res.Length - 1)
                    }, "小数不允许以小数点结尾，需要加上0。如果期望是整数，去除小数点"));
                    }
                    if (hasDot)
                    {
                        return new Token(Type: TokenType.Float, res, Line: startLine, StartColumn: startCol);
                    }
                    else
                    {
                        return new Token(Type: TokenType.Int, res, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // 布尔值
            {
                foreach (var b in StandardTokens.Bools)
                {
                    if (Match(b))
                    {
                        return new(Type: TokenType.Bool, Value: b, Line: startLine, StartColumn: startCol);
                    }
                }
            }

            // Lazy常数，只支持Mark层
            {
                var kt = LRMatch("@", "@", TokenType.LazyLayerNum, startLine, startCol);
                if (kt != null)
                {
                    return kt;
                }
            }

            // 关键字
            {
                var kt = LRMatch("[", "]", TokenType.Keyword, startLine, startCol);
                if (kt != null)
                {
                    // 如果不是几个既定关键字，说明是DSL中的参数名
                    if (!StandardTokens.Keywords.Contains(kt.Value))
                    {
                        return kt with { Type = TokenType.ParamName };
                    }
                    return kt;
                }
            }

            // DSL语句标志
            {
                var dt = LRMatch("<", ">", TokenType.DSLSentenceMark, startLine, startCol);
                if (dt != null)
                {
                    // <require>是逻辑语句标志
                    if (dt.Value == StandardTokens.Require)
                    {
                        return dt with { Type = TokenType.Require };
                    }
                    return dt;
                }
            }

            // 字符串
            {
                var st = LRMatch("\"", "\"", TokenType.String, startLine, startCol);
                if (st != null)
                {
                    return st;
                }
            }

            // 感叹号，小括号和大括号，包括非法字符的处理
            {
                var c = _input[_currentPos];
                if ("!(){}".Contains(c))
                {
                    Advance();
                    return c switch
                    {
                        '!' => new Token(Type: TokenType.ExclamationMark, "!", Line: startLine, StartColumn: startCol),
                        '(' => new Token(Type: TokenType.LeftParenthesis, "(", Line: startLine, StartColumn: startCol),
                        ')' => new Token(Type: TokenType.RightParenthesis, ")", Line: startLine, StartColumn: startCol),
                        '{' => new Token(Type: TokenType.LeftBrace, "{", Line: startLine, StartColumn: startCol),
                        '}' => new Token(Type: TokenType.RightBrace, "}", Line: startLine, StartColumn: startCol),
                        _ => null // 实际上不会到这里来
                    };
                }
                // 全部匹配不到，说明是非法字符
                throw new ErrorException(new(ErrorSpans: new()
                {
                    new(Line: startLine, StartColumn: startCol, EndColumn: startCol)
                }, $"非法的字符\"{c}\""));
            }
     
        }
        private Token? LRMatch(string left, string right, TokenType type, int startLine, int startCol)
        {
            if (Match(left))
            {
                var start = _currentPos;
                while(_currentPos < _input.Length && !Match(right))
                {
                    if(_input[_currentPos] == '\n')
                    {
                        throw new ErrorException(new(ErrorSpans: new()
                        {
                            new(Line: startLine, StartColumn: startCol, EndColumn: startCol + _currentPos - start)
                        }, $"必须具有闭合的{left}{right}"));
                    }
                    Advance();
                }
                var res =  _input[(start - left.Length).._currentPos];
                if (!res.EndsWith(right))
                {
                    throw new ErrorException(new(ErrorSpans: new()
                    {
                        new(Line: startLine, StartColumn: startCol, EndColumn: startCol + _currentPos - start)
                    }, $"必须具有闭合的{left}{right}"));
                }
                if(res.Length <= 2)
                {
                    throw new ErrorException(new(ErrorSpans: new()
                    {
                        new(Line: startLine, StartColumn: startCol, EndColumn: startCol + _currentPos - start)
                    }, $"{left}{right}中间必须有内容"));
                }
                var content = _input[(start - left.Length + 1)..(_currentPos - 1)];
                if(!Regex.IsMatch(content, @"^[a-zA-Z]+$"))
                {
                    throw new ErrorException(new(ErrorSpans: new()
                    {
                        new(Line: startLine, StartColumn: startCol, EndColumn: startCol + _currentPos - start)
                    }, $"{left}{right}中间的内容必须是英文字母"));
                }
                return new(Type: type, Value: res, Line: startLine, StartColumn: startCol);
            }
            return null;
        }
        private void SkipWhitespace()
        {
            while(_currentPos < _input.Length && char.IsWhiteSpace(_input[_currentPos]))
            {
                if(_input[_currentPos] == '\n')
                {
                    _currentLine++;
                    _currentCol = 1;
                }
                else
                {
                    _currentCol++;
                }
                _currentPos++;
            }
        }
        private bool Match(string s)
        {
            if(_currentPos + s.Length > _input.Length)
            {
                return false;
            }
            if(_input.Substring(_currentPos, s.Length) != s)
            {
                return false;
            }
            for(int i = 0; i < s.Length; ++i)
            {
                Advance();
            }
            return true;
        }
        private void Advance()
        {
            if (_currentPos < _input.Length && _input[_currentPos] == '\n')
            { 
                _currentLine++; 
                _currentCol = 1; 
            }
            else 
            {
                _currentCol++;
            }
            _currentPos++;
        }
    }
}