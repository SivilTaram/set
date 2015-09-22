using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ExcelToolKit
{
	internal class Program
	{
		private static void Main(string[] args)
		{
            Xls2MarkDown(args);
		}

        private static void Xls2MarkDown(string[] args)
        {
            int ret = 1;
            var cmd = new CommandParser(args);

            do
            {
                if (cmd["t"] != null && cmd["t"] == "md")
                {
                    if (cmd["xls"] == null)
                    {
                        break;
                    }

                    var xls = cmd["xls"];
                    var sheet = cmd["sheet"];
                    var output = Path.Combine(Path.GetDirectoryName(xls), Path.GetFileNameWithoutExtension(xls));

                    if (!File.Exists(xls))
                    {
                        Console.WriteLine("xls file is not exist:{0}", xls);
                        break;
                    }

                    if (sheet != null)
                    {
                        var table = xls.ToMd(sheet);
                        var tableFile = output + table.Name + ".md";
                        File.WriteAllText(tableFile, table.Value);
                        Console.WriteLine("Output File: {0}", tableFile);
                    }
                    else
                    {
                        var tables = xls.ToMd();
                        foreach (var table in tables)
                        {
                            var tableFile = output + table.Name + ".md";
                            File.WriteAllText(tableFile, table.Value);
                            Console.WriteLine("Output File: {0}", tableFile);
                        }
                    }

                    ret = 0;
                }
            } while (false);

            if (ret != 0)
            {
                Console.WriteLine();
                Console.WriteLine("Usecase:exceltk -t md -xls xlsfile [-sheet sheetname]");
            }

            //Console.Read();
        }
	}
}