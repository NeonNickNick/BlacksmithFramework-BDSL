namespace BdslValidator
{
    internal static class StandardTokens
    {
        public static HashSet<string> LogicalOperators = new()
        {
            ">=", "<=", "==", "!="
        };
        public static HashSet<string> ArithmaticOperators = new()
        {
            "+", "-", "*", "/", "^"
        };
        public static HashSet<string> Bools = new()
        {
            "true", "false"
        };
        public static string SingleLineComment = "//";
        public static string MultipleLineCommentLeft = "/*";
        public static string MultipleLineCommentRight = "*/";
        public static string Arrow = "->";
        public static HashSet<string> Keywords = new()
        {
            "[skillpackagedefinition]", 
            "[skill]",
            "[check]",
            "[declare]"
        };
        public static HashSet<string> DSLSentenceMarks = new()
        {
            "<takemark>",
            "<countmark>",
            "<addmark>",
            "<attack>",
            "<defense>",
            "<resource>",
            "<effect>"
        };
        public static string Require = "<require>";
        public static HashSet<string> ResourceTypes = new()
        {
            "Iron",
            "GoldIron",
            "Space",
            "Time",
            "Magic"
        };
        public static HashSet<string> HPTypes = new()
        {
            "HP",
            "MHP"
        };
        public static HashSet<string> AttackTypes = new()
        {
            "Physical",
            "Magical",
            "Real"
        };
        public static string SkillParam = "__skillparam__";
        public static HashSet<string> DefenseTypes = new()
        {
            "CommonReduction"
        };
        public static HashSet<string> DefenseAnalyzers = new()
        {
            "DefaultReduction"
        };
    }
}