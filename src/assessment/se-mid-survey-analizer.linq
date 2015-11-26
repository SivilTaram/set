<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Web.dll</Reference>
  <Reference>&lt;ProgramFilesX86&gt;\Microsoft ASP.NET\ASP.NET MVC 4\Assemblies\System.Web.Http.dll</Reference>
  <NuGetReference>SharpZipLib</NuGetReference>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net.Http.Headers</Namespace>
  <Namespace>ICSharpCode.SharpZipLib.Zip</Namespace>
</Query>

void Main() {
    var dir = @"D:\dev\lab\fzu2015-SE-半程问卷调查\se-half-survey";
    var fileNames = new DirectoryInfo(dir).GetFiles().Select(f => f.FullName);
    var list = new List<List<string>>();

    var textAnswers = new Dictionary<string, int> {
        {"有",1},{"没有",2},
        {"坚决不推荐",1},{"不推荐",2},{"一般",3},{"可能推荐",4},{"强烈推荐",5},
        {"很不好",1},{"不好",2},{"高于平均水平",4},{"非常好",5},
        {"不知道",0},{"母鸡啊",0}
    };
    
    foreach (var fileName in fileNames) {
        var reader = new DocxTextReader(fileName);
        var value = reader.getDocumentText();
        
        var start = value.IndexOf("你有没有修实践课");
        var content = value.Substring(start);

        var lines = content.Split(new[] { '\r', '\n' });
        var validLinse = new List<string>();
        var lastLine = "";
        for (int i = 0; i < lines.Length; i++) {
            var l = lines[i];
            if (i == 0) {
                l = l.Substring(0,l.IndexOf("）")+1);
            }
            l = l.Replace("_","").Replace(")","）").Replace("（ ","（").Replace(" ）","）").Trim();
            
            if (!string.IsNullOrWhiteSpace(l)) {
                if (lastLine.Contains("事情？")&&(l.Contains("你觉得"))) {
                    validLinse.Add("无");
                }
                validLinse.Add(l);
                lastLine = l;
            }
        }
        if (validLinse.Last().Contains("你觉得")) {
            validLinse.Add("无");
        }

        var normalLines = new List<string>();
        var oneLineAnswer = new List<string>();
        var beginAnswer = false;
        foreach (var l in validLinse) {
            if (l.Contains("你觉得")) {
                beginAnswer = true;
                if (oneLineAnswer.Any()) {
                    normalLines.Add(oneLineAnswer.Aggregate((a,b)=>a+b));
                    oneLineAnswer.Clear();
                }
                normalLines.Add(l);
                continue;
            }

            if (!beginAnswer) {
                normalLines.Add(l);
                continue;
            }
            oneLineAnswer.Add(l);
        }
        if (oneLineAnswer.Any()) {
            normalLines.Add(oneLineAnswer.Aggregate((a, b) => a + b));
            oneLineAnswer.Clear();
        }

        for (int i = 0; i < 16; i++) {
            if (i % 2 == 0) {
                foreach (var textAnswer in textAnswers) {
                    if (normalLines[i].Contains(textAnswer.Key)) {
                        normalLines[i] = normalLines[i].Replace(textAnswer.Key,textAnswer.Value.ToString());
                    }
                }
            }
        }
        
        list.Add(normalLines);
    }
    //list.Dump();

    var noPracticeAnswers = new List<Survey>();
    var hasPracticeAnswers = new List<Survey>();
    var regex = @"（(\d)\s*）";
    int index = 1;
    foreach (var items in list) {
        var survey = new Survey();
        survey.Index = index++;
        
        // answer1-4
        bool hasPractice = items[0].ValueAt(regex).ToBool();
        survey.RecommandSEPractice = items[2].ValueAt(regex).ToInt();
        survey.TeachingMethod = items[4].ValueAt(regex).ToInt();
        survey.AssistentScore = items[6].ValueAt(regex).ToInt();
        survey.RecommandThisCourse = items[8].ValueAt(regex).ToInt();
        
        survey.RecommandContinueWork = items[11];
        survey.RecommandStopWork = items[13];
        survey.RecommandNewerWork = items[15];

        if (hasPractice) {
            hasPracticeAnswers.Add(survey);
        } else {
            noPracticeAnswers.Add(survey);
        }
    }
    
    var sb = new StringBuilder();
    sb.AppendLine(noPracticeAnswers.MarkDown("本班**没有选择软件工程实践课**的学生问卷调查"));
    sb.AppendLine(hasPracticeAnswers.MarkDown("本班**选择软件工程实践课**的学生问卷调查"));
    sb.ToString().Dump();
    File.WriteAllText(@"D:\dev\lab\se-half-survey-report.md",sb.ToString());
}

class DocxTextReader {
    private string file = "";
    private string location = "";

    // constructor, with the fileName you want to extract the text from
    public DocxTextReader(string theFile) { file = theFile; }

    // Here the do it all method, call it after the constructor
    // it will try to find and parse document.xml from the zipped file
    // and return the docx's text in a string
    public string getDocumentText() {
        if (string.IsNullOrEmpty(file)) {
            throw new Exception("No Input file");
        }

        location = getDocumentXmlFile_FromZipFile();

        if (string.IsNullOrEmpty(location)) {
            //throw new Exception("Invalid Docx");

            location = "word/document.xml";
            var value =  ReadDocumentText();
            return value;
        } else {
            return ReadDocumentText();
        }
    }

    // we go to the xml file location
    // load it
    // and return the extracted text
    private string ReadDocumentText() {
        StringBuilder result = new StringBuilder();

        string bodyXPath = "/w:document/w:body";

        ZipFile zipped = new ZipFile(file);
        foreach (ZipEntry entry in zipped) {
            if (string.Compare(entry.Name, location, true) == 0) {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.Load(zipped.GetInputStream(entry));

                XmlNamespaceManager xnm = new XmlNamespaceManager(xmlDoc.NameTable);
                xnm.AddNamespace("w", @"http://schemas.openxmlformats.org/wordprocessingml/2006/main");

                XmlNode node = xmlDoc.DocumentElement.SelectSingleNode(bodyXPath, xnm);

                if (node == null) { return ""; }
                result.Append(ReadNode(node));
                break;
            }
        }
        zipped.Close();

        return result.ToString();
    }

    // Xml node reader helper :D
    private string ReadNode(XmlNode node) {
        // not a good node ?
        if (node == null || node.NodeType != XmlNodeType.Element) { return ""; }

        StringBuilder result = new StringBuilder();
        foreach (XmlNode child in node.ChildNodes) {
            // not an element node ?
            if (child.NodeType != XmlNodeType.Element) { continue; }

            // lets get the text, or replace the tags for the actua text's characters
            switch (child.LocalName) {
                case "tab": result.Append("\t"); break;
                case "p": result.Append(ReadNode(child)); result.Append("\r\n\r\n"); break;
                case "cr":
                case "br": result.Append("\r\n"); break;

                case "t": // its Text !
                    result.Append(child.InnerText.TrimEnd());
                    string space = ((XmlElement)child).GetAttribute("xml:space");
                    if (!string.IsNullOrEmpty(space) && space == "preserve") { result.Append(' '); }
                    break;

                default: result.Append(ReadNode(child)); break;
            }
        }

        return result.ToString();
    }

    // lets open the zip file and look up for the
    // document.xml file
    // and save its zip location into the location variable
    private string getDocumentXmlFile_FromZipFile() {
        // ICsharpCode helps here to open the zipped file
        ZipFile zip = new ZipFile(file);

        // lets take a look to the file entries inside the zip file
        // up to we get
        foreach (ZipEntry entry in zip) {

            if (string.Compare(entry.Name, "[Content_Types].xml", true) == 0) {
                Stream contentTypes = zip.GetInputStream(entry);

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.Load(contentTypes);

                contentTypes.Close();

                // we need a XmlNamespaceManager for resolving namespaces
                XmlNamespaceManager xnm = new XmlNamespaceManager(xmlDoc.NameTable);
                xnm.AddNamespace("t", @"http://schemas.openxmlformats.org/package/2006/content-types");

                // lets find the location of document.xml
                XmlNode node = xmlDoc.DocumentElement.SelectSingleNode("/t:Types/t:Override[@ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"]", xnm);

                if (node != null) {
                    string location = ((XmlElement)node).GetAttribute("PartName");
                    return location.TrimStart(new char[] { '/' });
                }
                break;
            }
        }

        // close the zip
        zip.Close();

        return null;
    }

}

public class Survey {
    public int Index { get; set; }
    public int RecommandSEPractice { get; set;}
    public int TeachingMethod { get; set; }
    public int AssistentScore { get; set; }
    public int RecommandThisCourse { get; set; }
    public string RecommandContinueWork { get; set; }
    public string RecommandStopWork { get; set; }
    public string RecommandNewerWork { get; set;}
}

public static class Extension {
    public static string ValueAt(this string s, string regex) {
        var match = Regex.Match(s, regex);
        if (match.Success) {
            string v =  match.Groups[1].Value;
            return v;
        }
        return "";
    }
    public static bool ToBool(this string s) {
        if (s == "1") {
            return true;
        } else {
            return false;
        }
    }
    public static int ToInt(this string s) {
        int v = Convert.ToInt32(s);
        return v;
    }
    public static string MarkDown(this List<Survey> list,string head) {
        var sb = new StringBuilder();
        sb.Append("#### ").Append(head).Append("\n");
        sb.AppendLine();
        sb.AppendLine(string.Format("- 总数:{0}",list.Count));
        list.Select(i=>i.RecommandSEPractice).Statistic(sb,"建议低年级同学修软工实践课");
        list.Select(i=>i.TeachingMethod).Statistic(sb,"对这门课的教学方法的评价");
        list.Select(i=>i.AssistentScore).Statistic(sb,"对这门课助教的评价");
        list.Select(i=>i.RecommandThisCourse).Statistic(sb,"推荐这个老师班级的课给你的低年级同学");
        sb.AppendLine();

        sb.AppendLine("|建议低年级修SE|教学方法评价|助教评价|推荐该老师班级课程给低年级|建议继续|建议停止|建议新增|");
        sb.AppendLine("|:--|:--|:--|:--|:--|:--|:--|");
        foreach (var i in list) {
            sb.AppendLine(string.Format("|{0}|{1}|{2}|{3}|{4}|{5}|{6}|",
                i.RecommandSEPractice,
                i.TeachingMethod,
                i.AssistentScore,
                i.RecommandThisCourse,
                i.RecommandContinueWork,
                i.RecommandStopWork,
                i.RecommandNewerWork));
        }
        return sb.ToString();
    }
    public static void Statistic(this IEnumerable<int> values, StringBuilder sb,string description) {
        sb.AppendLine(string.Format("- {0}（平均星数）：{1:#.#}",description,values.Average()));
    }
}