using System;
using System.IO;
using System.Xml;

namespace BinaryStringReplacement
{
    class Program
    {
        static byte[] data;
        static void Main(string[] args)
        {
            if (args.Length <= 1)
            {
                printUsage();
                return;
            }

            // Load whole binary file into memory
            FileStream fs = new FileStream(args[1], FileMode.Open, FileAccess.Read);
            data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Close();

            Console.WriteLine("Loaded binary file.");
            String dataAsString = System.Text.Encoding.ASCII.GetString(data);
            
            XmlDocument replacementDoc = new XmlDocument();
            replacementDoc.Load(args[0]);
            Console.WriteLine("Loaded text file.");

            Console.WriteLine("Beginning replacements.");
            XmlNodeList replacementNodes = replacementDoc.SelectNodes("//node()[local-name() = 'replacement']");
            foreach (XmlNode replacementNode in replacementNodes)
            {
                XmlNode from = replacementNode.SelectNodes("node()[local-name() = 'from']")[0];
                XmlNode to = replacementNode.SelectNodes("node()[local-name() = 'to']")[0];
                replaceString(dataAsString, from.InnerText.Trim(), to.InnerText.Trim());
            }
            Console.WriteLine("Writing file.");

            fs = new FileStream(args[1], FileMode.Open, FileAccess.Write);
            fs.Write(data, 0, data.Length);
            fs.Close();

            Console.WriteLine("Complete.  Press Enter to close.");
            Console.Read();
        }

        private static void replaceString(string inThisStr, string replaceThisStr, string replacementStr)
        {
            if (replacementStr.Length > replaceThisStr.Length)
            {
                Console.WriteLine("Warning: truncating because replacement string \"" + replacementStr + "\" is too long.  Expected " + replaceThisStr.Length + " characters to replace \"" + replaceThisStr + "\"." );
                replacementStr = replacementStr.Substring(0, replaceThisStr.Length);
            }
            if (replacementStr.Length < replaceThisStr.Length)
            {
                replacementStr = replacementStr.PadRight(replaceThisStr.Length, ' ');
            }

            int foundIdx = inThisStr.IndexOf(replaceThisStr);
            if (foundIdx < 0)
            {
                Console.WriteLine("\"" + replaceThisStr + "\" not found in binary.  Skipping.");
                return;
            }
            for (int idx = 0; idx < replacementStr.Length; idx++)
            {
                data[foundIdx + idx] = (byte)replacementStr.ToCharArray()[idx];
            }

            Console.WriteLine("Replaced \"" + replaceThisStr + "\" with \"" + replacementStr + "\"");

        }

        static void printUsage()
        {
            Console.WriteLine("Usage: BinaryStringReplacement.exe stringlist.txt filetomod.exe");
        }
    }
}
