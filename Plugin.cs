using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using BepInEx;
using BepInEx.Logging;
using IC10_Extender;
using IC10_Extender.Highlighters;
using IC10_Extender.Preprocessors;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Includes
{
    [BepInPlugin("net.lawofsynergy.stationeers.trap", "[IC10E] Includes", "0.0.0.1")]
    [BepInDependency("net.lawofsynergy.stationeers.ic10e")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            IC10Extender.Register(new IncludePreprocessor(), 0);
        }
    }

    public class IncludePreprocessor : Preprocessor
    {
        public static readonly Regex Pattern = new Regex(@"(?<remainder>^.*)\s+(?<command>@include\s+(?<filename>[^\s].*))$", RegexOptions.Compiled);

        public override string SimpleName => "include_preprocessor";

        public override string HelpEntryName => "<color=green>@include {steam library entry}</color>";

        public override string HelpEntryDescription => "Includes the contents of a script stored in your steam library into this ic10 script";

        public override PreprocessorOperation Create(ChipWrapper chip)
        {
            return new Instance(chip);
        }

        public override SyntaxHighlighter Highlighter()
        {
            return new HighlighterInstaance();
        }

        public class Instance : PreprocessorOperation
        {
            public Instance(ChipWrapper chip) : base(chip) { }

            public override IEnumerable<Line> DoPass(IEnumerable<Line> fullScript)
            {
                var result = fullScript.SelectMany(line => ExpandLine(line));
                result = ReNumber(result);
                return result;
            }

            public IEnumerable<Line> ExpandLine(Line line)
            {
                var match = Pattern.Match(line.Raw);

                if (!match.Success) return new List<Line> { line };

                var file = match.Groups["filename"];
                var remainder = match.Groups["remainder"];

                if (file.Success)
                {
                    line.Raw = remainder.Value;
                    line.Display = remainder.Value;
                }

                var filename = Path.Combine(file.Value, "instruction.xml");

                var path = Path.Combine(StationSaveUtils.GetSavePathScriptsSubDir().FullName, filename);
                if (!File.Exists(path))
                {
                    throw new ExtendedPCException(line.LineNumber, $"File not found: {path}");
                }

                var result = InstructionData.GetFromFile(path).Instructions.Split('\n').Select(l => new Line(l, line.OriginatingLineNumber));
                if (!string.IsNullOrWhiteSpace(line.Raw)) result = result.Prepend(line);

                return result;
            }

            public override Line? ProcessLine(Line line)
            {
                throw new System.NotImplementedException();
            }
        }

        public class HighlighterInstaance : SyntaxHighlighter
        {
            public override void HighlightLine(StyledLine line)
            {
                var match = Pattern.Match(line.Remainder());
                if (match.Success)
                {
                    line.Consume(match.Groups["command"].Value, line.Theme.Macro);
                }
            }
        }
    }
}
