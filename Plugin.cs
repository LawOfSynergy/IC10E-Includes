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

namespace Includes
{
    [BepInPlugin("net.lawofsynergy.stationeers.trap", "[IC10E] Includes", "0.0.0.1")]
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
        internal const string Include = "@include";

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

        public static string GetFilename(string line, out string remainder)
        {
            if(!string.IsNullOrEmpty(line) && line.Contains(Include)) 
            { 
                var index = line.IndexOf(Include) + Include.Length;
                var filename = line.Substring(index).Trim();
                remainder = line.Substring(0, index);

                filename = Regexes.CleanInvalidXmlChars(filename);
                filename = filename.SanitizeFilename();
                return filename;
            }
            remainder = line;
            return null;
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
                var filename = GetFilename(line.Raw, out var remainder);
                var index = remainder.IndexOf(Include);
                if (index != -1)
                {
                    line.Raw = remainder.Substring(0, index);
                    line.Display = line.Raw;
                }

                if (filename == null) return new List<Line> { line };
                filename = Path.Combine(filename, "instruction.xml");

                var path = Path.Combine(StationSaveUtils.GetSavePathScriptsSubDir().FullName, filename);
                if (!File.Exists(path))
                {
                    throw new ProgrammableChipException(ProgrammableChipException.ICExceptionType.Unknown, line.LineNumber);
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
            public override string HighlightLine(string line)
            {
                int index = line.IndexOf("<color=darkgrey>#");

                var before = string.Empty;
                var comment = string.Empty;

                if (index != -1)
                {
                    before = line.Substring(0, index);
                    comment = ProgrammableChip.StripColorTags(line.Substring(index));
                }
                else
                {
                    comment = line;
                }

                index = comment.IndexOf(Include);
                if (index != -1)
                {
                    var include = comment.Substring(index);
                    comment = comment.Substring(0, index);
                    var filename = GetFilename(include, out var remainder);

                    var path = Path.Combine(filename, "instruction.xml");
                    path = Path.Combine(StationSaveUtils.GetSavePathScriptsSubDir().FullName, path);
                    var color = "green";
                    if (!File.Exists(path)) color = "red";

                    var result = line.Replace(Include, $"<color=green>{Include}</color>");
                    if (!string.IsNullOrEmpty(filename)) result = result.Replace(filename, $"<color={color}>{filename}</color>");

                    return result;
                }

                return line;
            }
        }
    }
}
