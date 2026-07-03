using System.Collections.ObjectModel;
using System.IO.Pipes;

namespace BdslValidator
{
    internal class SyntaxAnalyzer
    {
        public SyntaxAnalyzer(List<Token> tokens)
        {
            _tokens = tokens;
        }
        private List<Token> _tokens;
        private int _currentIndex = 0;

        public void Analyze()
        {
            // 第一步：检查是否有且仅有一个[skillpackagedefinition]块
            ValidateSPDBlockCount();

            // 第二步：检查[skillpackagedefinition]之前是否有无关内容
            int spdIndex = _tokens.FindIndex(t => t.Type == TokenType.Keyword && t.Value == "[skillpackagedefinition]");
            if (spdIndex > 0)
            {
                ThrowError(_tokens.GetRange(0, spdIndex), "[skillpackagedefinition]之前不允许出现无关内容");
            }
            _currentIndex = spdIndex;

            // 第三步：递归下降解析
            ParseSkillPackageDefinition();

            // 第四步：检查[skillpackagedefinition]的'}'之后是否有无关内容
            if (!Out())
            {
                ThrowError(_tokens.GetRange(_currentIndex, _tokens.Count - _currentIndex), "[skillpackagedefinition]的'}'之后不允许出现无关内容");
            }
        }

        #region 辅助方法

        private Token? Peek()
        {
            if (Out()) return null;
            return _tokens[_currentIndex];
        }
        private void Expect(TokenType type, string? value = null)
        {
            if (Out())
                throw new ErrorException(new(ErrorSpans: new()
                {
                    new(Line: _tokens.Last().Line, StartColumn: _tokens.Last().StartColumn, EndColumn: _tokens.Last().StartColumn + _tokens.Last().Value.Length - 1)
                }, Message: $"期望{TokenTypeName(type, value)}，但文件已结束"));

            var t = Read();
            if (t.Type != type || (value != null && t.Value != value))
            {
                string expected = value ?? TokenTypeName(type, null);
                ThrowError(t, $"期望{expected}，但遇到了\"{t.Value}\"");
            }
            Advance();
        }
        private bool Match(TokenType type, string? value = null)
        {
            if (Out()) return false;
            var t = _tokens[_currentIndex];
            if (t.Type == type && (value == null || t.Value == value))
            {
                Advance();
                return true;
            }
            return false;
        }
        private static string TokenTypeName(TokenType type, string? value)
        {
            if (value != null) return value;
            return type switch
            {
                TokenType.Keyword => "关键字",
                TokenType.LeftParenthesis => "'('",
                TokenType.RightParenthesis => "')'",
                TokenType.LeftBrace => "'{'",
                TokenType.RightBrace => "'}'",
                TokenType.Arrow => "'->'",
                TokenType.String => "字符串",
                TokenType.Int or TokenType.Float => "数字",
                TokenType.Bool => "布尔值",
                TokenType.LogicalOperator => "比较算符(==, !=, >=, <=)",
                TokenType.ArithmaticOperator => "算数算符",
                TokenType.DSLSentenceMark => "DSL语句标记",
                TokenType.Require => "<require>",
                TokenType.ResourceType => "资源类型(Iron, GoldIron, Space, Time, Magic)",
                TokenType.HPType => "生命类型(HP, MHP)",
                TokenType.AttackType => "攻击类型(Physical, Magical, Real)",
                TokenType.DefenseType => "防御类型",
                TokenType.DefenseAnalyzer => "防御分析器",
                TokenType.ParamName => "参数名",
                TokenType.SkillParam => "__skillparam__",
                _ => type.ToString()
            };
        }

        #endregion

        #region 顶层结构

        private void ValidateSPDBlockCount()
        {
            var ts = _tokens.FindAll(t => t.Type == TokenType.Keyword && t.Value == "[skillpackagedefinition]");
            if (ts.Count <= 0)
            {
                throw new ErrorException(new(ErrorSpans: new()
                {
                    new(Line: 1, StartColumn: 1, EndColumn: 1)
                }, Message: "每个.bdsl文件至少需要有一个[skillpackagedefinition]块"));
            }
            if (ts.Count > 1)
            {
                bool met = false;
                for (int i = 0; i < _tokens.Count; ++i)
                {
                    var t = _tokens[i];
                    if (t.Type == TokenType.Keyword && t.Value == "[skillpackagedefinition]")
                    {
                        if (met)
                        {
                            throw new ErrorException(new(ErrorSpans: new()
                            {
                                new(Line: t.Line, StartColumn: t.StartColumn, EndColumn: t.StartColumn + t.Value.Length - 1)
                            }, Message: "每个.bdsl文件只能有一个[skillpackagedefinition]块"));
                        }
                        if (!met)
                        {
                            met = true;
                        }
                    }
                }
            }
        }

        private void ParseSkillPackageDefinition()
        {
            Expect(TokenType.Keyword, "[skillpackagedefinition]");
            Expect(TokenType.LeftParenthesis);
            Expect(TokenType.String);  // profession name
            Expect(TokenType.RightParenthesis);
            Expect(TokenType.Arrow);
            Expect(TokenType.LeftBrace);

            // 解析内部技能列表
            while (!Out() && Peek()?.Type != TokenType.RightBrace)
            {
                var t = Peek();
                if (t?.Type == TokenType.Keyword && t.Value == "[skill]")
                {
                    ParseSkill();
                }
                else
                {
                    ThrowError(t!, "[skillpackagedefinition]内部只允许[skill]块");
                }
            }

            Expect(TokenType.RightBrace);
        }

        #endregion

        #region Skill Block

        private void ParseSkill()
        {
            var skillToken = Peek();
            Expect(TokenType.Keyword, "[skill]");

            Expect(TokenType.LeftParenthesis);
            var nameToken = Peek();
            Expect(TokenType.String);
            string skillName = nameToken!.Value;
            Expect(TokenType.RightParenthesis);

            Expect(TokenType.Arrow);
            Expect(TokenType.LeftBrace);

            bool isAuto = skillName == "\"auto\"";
            bool hasCheck = false;
            bool hasDeclare = false;

            while (!Out() && Peek()?.Type != TokenType.RightBrace)
            {
                var t = Peek();
                if (t?.Type == TokenType.Keyword)
                {
                    if (t.Value == "[check]")
                    {
                        if (isAuto)
                            ThrowError(t, "被动技能(auto)不允许有[check]块");
                        if (hasCheck)
                            ThrowError(t, "每个[skill]只能有一个[check]块");
                        hasCheck = true;
                        ParseCheck();
                    }
                    else if (t.Value == "[declare]")
                    {
                        if (hasDeclare)
                            ThrowError(t, "每个[skill]只能有一个[declare]块");
                        hasDeclare = true;
                        ParseDeclare();
                    }
                    else
                    {
                        ThrowError(t, $"[skill]内部不允许出现{t.Value}");
                    }
                }
                else
                {
                    ThrowError(t!, "[skill]内部只允许[check]和[declare]块");
                }
            }

            if (!isAuto && !hasCheck)
                ThrowError(skillToken!, "非auto的[skill]必须有一个[check]块");
            if (!hasDeclare)
                ThrowError(skillToken!, "每个[skill]必须有一个[declare]块");

            Expect(TokenType.RightBrace);
        }

        #endregion

        #region Check Block

        private void ParseCheck()
        {
            Expect(TokenType.Keyword, "[check]");

            Expect(TokenType.LeftParenthesis);
            // 可选取反标记 !
            if (!Match(TokenType.ExclamationMark))
            {
                // 如果下一个不是 )，说明括号内有非法内容
                if (Peek()?.Type != TokenType.RightParenthesis)
                {
                    ThrowError(Peek()!, "[check]的参数只能是'!'或空");
                }
            }
            Expect(TokenType.RightParenthesis);

            Expect(TokenType.Arrow);
            Expect(TokenType.LeftBrace);

            while (!Out() && Peek()?.Type != TokenType.RightBrace)
            {
                ParseRequireStatement();
            }

            Expect(TokenType.RightBrace);
        }

        private void ParseRequireStatement()
        {
            Expect(TokenType.Require);     // <require>
            ParseArithmeticExpr();          // 左表达式
            Expect(TokenType.LogicalOperator); // == != >= <=
            ParseArithmeticExpr();          // 右表达式
        }

        #endregion

        #region Declare Block

        private void ParseDeclare()
        {
            Expect(TokenType.Keyword, "[declare]");

            Expect(TokenType.LeftParenthesis);
            // 参数必须为空
            if (Peek()?.Type != TokenType.RightParenthesis)
            {
                ThrowError(Peek()!, "[declare]的参数必须为空");
            }
            Expect(TokenType.RightParenthesis);

            Expect(TokenType.Arrow);
            Expect(TokenType.LeftBrace);

            while (!Out() && Peek()?.Type != TokenType.RightBrace)
            {
                ParseDSLStatement();
            }

            Expect(TokenType.RightBrace);
        }

        private void ParseDSLStatement()
        {
            var t = Peek();
            if (t?.Type == TokenType.DSLSentenceMark)
            {
                switch (t.Value)
                {
                    case "<attack>": Advance(); ParseAttack(); break;
                    case "<defense>": Advance(); ParseDefense(); break;
                    case "<resource>": Advance(); ParseResource(); break;
                    case "<use>": Advance(); ParseUse(); break;
                    case "<takemark>": Advance(); ParseTakeMark(); break;
                    default:
                        // <countmark>, <addmark>, <effect> 等未定义标记：宽松跳过
                        Advance();
                        SkipUnknownDSL();
                        break;
                }
            }
            else
            {
                ThrowError(t!, "declare块内只允许DSL语句(以'<>'标记包裹)");
            }
        }

        /// <summary>
        /// 宽松跳过未知 DSL 语句，消费 token 直到遇到 } 或下一个 DSL 标记或关键字
        /// </summary>
        private void SkipUnknownDSL()
        {
            while (!Out())
            {
                var t = Peek();
                if (t?.Type == TokenType.RightBrace ||
                    t?.Type == TokenType.Keyword ||
                    t?.Type == TokenType.DSLSentenceMark)
                    break;
                Advance();
            }
        }

        #endregion

        #region DSL 语句: <attack>

        private void ParseAttack()
        {
            var markToken = _tokens[_currentIndex - 1];  // <attack> token

            Expect(TokenType.LeftParenthesis);

            bool hasPower = false, hasType = false;
            while (!Out() && Peek()?.Type != TokenType.RightParenthesis)
            {
                var p = Peek();
                if (p?.Type == TokenType.ParamName)
                {
                    if (p.Value == "[power]")
                    {
                        Advance();
                        ParseArithmeticExpr();
                        hasPower = true;
                    }
                    else if (p.Value == "[type]")
                    {
                        Advance();
                        Expect(TokenType.AttackType);
                        hasType = true;
                    }
                    else
                    {
                        ThrowError(p, $"<attack>不支持参数{p.Value}");
                    }
                }
                else
                {
                    ThrowError(p!, "<attack>的参数必须是命名参数([paramname]value)");
                }
            }

            if (!hasPower)
                ThrowError(markToken, "<attack>必须有[power]参数");
            if (!hasType)
                ThrowError(markToken, "<attack>必须有[type]参数");

            Expect(TokenType.RightParenthesis);

            // 可选外部参数 + 可选尾部 require block
            ParseDSLExternalParamsAndBlock();
        }

        #endregion

        #region DSL 语句: <defense>

        private void ParseDefense()
        {
            var markToken = _tokens[_currentIndex - 1];  // <defense> token

            Expect(TokenType.LeftParenthesis);

            bool hasName = false, hasAnalyzer = false, hasType = false, hasPower = false, hasClock = false;

            while (!Out() && Peek()?.Type != TokenType.RightParenthesis)
            {
                var p = Peek();
                if (p?.Type == TokenType.ParamName)
                {
                    switch (p.Value)
                    {
                        case "[name]":
                            Advance();
                            Expect(TokenType.String);
                            hasName = true;
                            break;
                        case "[analyzer]":
                            Advance();
                            Expect(TokenType.DefenseAnalyzer);
                            hasAnalyzer = true;
                            break;
                        case "[type]":
                            Advance();
                            Expect(TokenType.DefenseType);
                            hasType = true;
                            break;
                        case "[power]":
                            Advance();
                            ParseArithmeticExpr();
                            hasPower = true;
                            break;
                        case "[clock]":
                            Advance();
                            ParseClockSubStructure();
                            hasClock = true;
                            break;
                        default:
                            ThrowError(p, $"<defense>不支持参数{p.Value}");
                            break;
                    }
                }
                else
                {
                    ThrowError(p!, "<defense>的参数必须是命名参数([paramname]value)");
                }
            }

            if (!hasName) ThrowError(markToken, "<defense>必须有[name]参数");
            if (!hasAnalyzer) ThrowError(markToken, "<defense>必须有[analyzer]参数");
            if (!hasType) ThrowError(markToken, "<defense>必须有[type]参数");
            if (!hasPower) ThrowError(markToken, "<defense>必须有[power]参数");
            if (!hasClock) ThrowError(markToken, "<defense>必须有[clock]参数");

            Expect(TokenType.RightParenthesis);

            // 可选外部参数 + 可选尾部 require block
            ParseDSLExternalParamsAndBlock();
        }

        private void ParseClockSubStructure()
        {
            var clockToken = _tokens[_currentIndex - 1];  // [clock] token

            Expect(TokenType.LeftParenthesis);

            bool hasDelay = false, hasRemain = false, hasIsinfinite = false;

            while (!Out() && Peek()?.Type != TokenType.RightParenthesis)
            {
                var p = Peek();
                if (p?.Type == TokenType.ParamName)
                {
                    switch (p.Value)
                    {
                        case "[delay]":
                            Advance();
                            ParseArithmeticExpr();
                            hasDelay = true;
                            break;
                        case "[remain]":
                            Advance();
                            ParseArithmeticExpr();
                            hasRemain = true;
                            break;
                        case "[isinfinite]":
                            Advance();
                            Expect(TokenType.Bool);
                            hasIsinfinite = true;
                            break;
                        default:
                            ThrowError(p, $"[clock]不支持参数{p.Value}");
                            break;
                    }
                }
                else
                {
                    ThrowError(p!, "[clock]的参数必须是命名参数");
                }
            }

            if (!hasDelay) ThrowError(clockToken, "[clock]必须有[delay]参数");
            if (!hasRemain) ThrowError(clockToken, "[clock]必须有[remain]参数");
            if (!hasIsinfinite) ThrowError(clockToken, "[clock]必须有[isinfinite]参数");

            Expect(TokenType.RightParenthesis);
        }

        #endregion

        #region DSL 语句: <resource>

        private void ParseResource()
        {
            Expect(TokenType.LeftParenthesis);
            ParseArithmeticExpr();
            Expect(TokenType.RightParenthesis);

            var t = Peek();
            if (t?.Type == TokenType.ResourceType)
            {
                Advance();
            }
            else
            {
                ThrowError(t!, "<resource>后期望资源类型(Iron, GoldIron, Space, Time, Magic)");
            }
        }

        #endregion

        #region DSL 语句: <use>

        private void ParseUse()
        {
            ParseArithmeticExpr();

            var t = Peek();
            if (t?.Type == TokenType.ResourceType || t?.Type == TokenType.HPType)
            {
                Advance();
            }
            else
            {
                ThrowError(t!, "<use>后期望资源类型或HP类型");
            }
        }

        #endregion

        #region DSL 语句: <takemark> (暂跳过)

        private void ParseTakeMark()
        {
            // <takemark> 暂不支持，宽松跳过后续 token
            SkipUnknownDSL();
        }

        #endregion

        #region 共享: DSL外部参数和尾部require block

        /// <summary>
        /// 解析 [delay]expr [undo](!) 和可选的 ->{require*...}
        /// 用于 <attack> 和 <defense>
        /// </summary>
        private void ParseDSLExternalParamsAndBlock()
        {
            // 可选外部参数: [delay] 和 [undo]
            while (true)
            {
                var p = Peek();
                if (p?.Type == TokenType.ParamName)
                {
                    if (p.Value == "[delay]")
                    {
                        Advance();
                        ParseArithmeticExpr();
                    }
                    else if (p.Value == "[undo]")
                    {
                        Advance();
                        Expect(TokenType.LeftParenthesis);
                        Expect(TokenType.ExclamationMark);
                        Expect(TokenType.RightParenthesis);
                    }
                    else
                    {
                        // 不是 [delay] 或 [undo]，停止
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            // 可选尾部 require block: ->{ <require>* }
            if (Match(TokenType.Arrow))
            {
                Expect(TokenType.LeftBrace);
                while (!Out() && Peek()?.Type != TokenType.RightBrace)
                {
                    ParseRequireStatement();
                }
                Expect(TokenType.RightBrace);
            }
        }

        #endregion

        #region 表达式解析（优先级攀登法）

        // expr   ::= term (('+' | '-') term)*
        // term   ::= factor (('*' | '/') factor)*
        // factor ::= primary ('^' factor)?    -- ^ 右结合，最高优先级
        // primary ::= Int | Float | ResourceType | HPType | SkillParam | LazyLayerNum
        //           | '(' expr ')'
        //           | ('+' | '-') primary     -- 一元正负号

        private void ParseArithmeticExpr()
        {
            ParseTerm();
            while (true)
            {
                var t = Peek();
                if (t?.Type == TokenType.ArithmaticOperator && (t.Value == "+" || t.Value == "-"))
                {
                    Advance();
                    ParseTerm();
                }
                else break;
            }
        }

        private void ParseTerm()
        {
            ParseFactor();
            while (true)
            {
                var t = Peek();
                if (t?.Type == TokenType.ArithmaticOperator && (t.Value == "*" || t.Value == "/"))
                {
                    Advance();
                    ParseFactor();
                }
                else break;
            }
        }

        private void ParseFactor()
        {
            ParsePrimary();
            var t = Peek();
            if (t?.Type == TokenType.ArithmaticOperator && t.Value == "^")
            {
                Advance();
                ParseFactor();  // 右结合
            }
        }

        private void ParsePrimary()
        {
            if (Out())
            {
                throw new ErrorException(new(ErrorSpans: new()
                {
                    new(Line: _tokens.Last().Line, StartColumn: _tokens.Last().StartColumn, EndColumn: _tokens.Last().StartColumn + _tokens.Last().Value.Length - 1)
                }, Message: "表达式不完整，文件意外结束"));
            }

            var t = Peek()!;

            // 一元正负号
            if (t.Type == TokenType.ArithmaticOperator && (t.Value == "+" || t.Value == "-"))
            {
                Advance();
                ParsePrimary();
                return;
            }

            // 括号表达式
            if (t.Type == TokenType.LeftParenthesis)
            {
                Advance();
                ParseArithmeticExpr();
                Expect(TokenType.RightParenthesis);
                return;
            }

            // 操作数：数字、资源类型、HP类型、SkillParam、LazyLayerNum
            if (t.Type == TokenType.Int || t.Type == TokenType.Float ||
                t.Type == TokenType.ResourceType || t.Type == TokenType.HPType ||
                t.Type == TokenType.SkillParam || t.Type == TokenType.LazyLayerNum)
            {
                Advance();
                return;
            }

            ThrowError(t, $"期望数字、资源类型、HP、__skillparam__或@lazynum@，但遇到了\"{t.Value}\"");
        }

        #endregion

        #region 错误处理

        private void ThrowError(List<Token> tokens, string message)
        {
            var lookup = tokens.ToLookup(t => t.Line);
            var errorSpans = new List<ErrorSpan>();
            foreach (var group in lookup)
            {
                var tf = group.First();
                var tl = group.Last();
                errorSpans.Add(new(Line: group.Key, StartColumn: tf.StartColumn, EndColumn: tl.StartColumn + tl.Value.Length - 1));
            }
            throw new ErrorException(new(errorSpans, message));
        }
        private void ThrowError(Token token, string message)
        {
            var errorSpans = new List<ErrorSpan>()
            {
                new(
                Line: token.Line,
                StartColumn: token.StartColumn,
                EndColumn: token.StartColumn + token.Value.Length - 1)
            };
            throw new ErrorException(new(errorSpans, message));
        }
        private void Advance() => _currentIndex++;
        private bool Out() => _currentIndex >= _tokens.Count;
        private Token Read() => _tokens[_currentIndex];

        #endregion
    }
}