using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace BinaryStringReplacement
{
    class Program
    {
        static byte[] data;
        static bool eclMode = false;
        static void Main(string[] args)
        {
            string binFile = null;
            string transFile = null;
            if (args.Length <= 1)
            {
                printUsage();
                return;
            }

            if (args[0].Trim().StartsWith("-"))
            {
                if (args[0].Trim().ToUpper() == "-E" && args.Length == 3)
                {
                    eclMode = true;
                    transFile = args[1];
                    binFile = args[2];
                } else
                {
                    printUsage();
                    return;
                }
            } else
            {
                transFile = args[0];
                binFile = args[1];
            }

            // Load whole binary file into memory
            FileStream fs = new FileStream(binFile, FileMode.Open, FileAccess.Read);
            data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Close();

            Console.WriteLine("Loaded binary file.");
            
            XmlDocument replacementDoc = new XmlDocument();
            replacementDoc.Load(transFile);
            Console.WriteLine("Loaded text file.");

            Console.WriteLine("Beginning replacements.");
            XmlNodeList replacementNodes = replacementDoc.SelectNodes("//node()[local-name() = 'replacement']");
            foreach (XmlNode replacementNode in replacementNodes)
            {
                string from = replacementNode.SelectNodes("node()[local-name() = 'from']")[0].InnerText.Trim();
                string to = replacementNode.SelectNodes("node()[local-name() = 'to']")[0].InnerText.Trim();
                byte[] bFrom = System.Text.Encoding.ASCII.GetBytes(from);
                byte[] bTo = System.Text.Encoding.ASCII.GetBytes(to);
                if (eclMode)
                {
                    bFrom = CompressString(from);
                    bTo = CompressString(to);
                }
                /* Debugging
                string bytes = "";
                foreach (byte b in bFrom)
                {
                    bytes += ((int)b).ToString("X").PadLeft(2, '0') + "/";
                }
                Console.WriteLine(bytes);
                */
                bool result = replaceString(bFrom, bTo, !eclMode);
                
                if (result)
                {
                    Console.WriteLine("Replaced \"" + from + "\" with \"" + to + "\"");
                }
                else
                {
                    Console.WriteLine("\"" + from + "\" not found in binary.  Skipping.");
                }
            }
            Console.WriteLine("Writing file.");

            fs = new FileStream(binFile, FileMode.Open, FileAccess.Write);
            fs.Write(data, 0, data.Length);
            fs.Close();

            Console.WriteLine("Complete.  Press Enter to close.");
            Console.Read();
        }

        private static bool replaceString(byte[] replaceThisStr, byte[] replacementStr, bool checkBoundary)
        {
            if (replacementStr.Length > replaceThisStr.Length)
            {
                Console.WriteLine("Warning: truncating because replacement string \"" + replacementStr + "\" is too long.  Expected " + replaceThisStr.Length + " characters to replace \"" + replaceThisStr + "\"." );
                //replacementStr = replacementStr.Substring(0, replaceThisStr.Length);
                byte[] tmp = new byte[replaceThisStr.Length];
                Array.Copy(replacementStr, tmp, tmp.Length);
                replacementStr = tmp;

            }
            if (replacementStr.Length < replaceThisStr.Length)
            {
                //replacementStr = replacementStr.PadRight(replaceThisStr.Length, ' ');
                byte[] tmp = new byte[replaceThisStr.Length];
                Array.Copy(replacementStr, tmp, replacementStr.Length);
                for (int idx = replacementStr.Length; idx < replaceThisStr.Length; idx++)
                {
                    tmp[idx] = (byte)' ';
                }
                replacementStr = tmp;
            }

            bool goodMatch = true;
            int foundIdx = 0;
            do {
                foundIdx = IndexOf(data, replaceThisStr, foundIdx);
                if (foundIdx > 0 && checkBoundary)
                {
                    if (data[foundIdx - 1] > 64 && data[foundIdx - 1] < 122) {
                        // This match is no good if it has a letter (ish) right before it.  String should have their length right before it so this may be a string that is inside another string
                        // Yes it is possible for a string to have a length that is between 64 & 122, but I'm not concerned about that because then it just won't get swapped
                        goodMatch = false;
                        // Advance the pointer and search again
                        foundIdx++;
                    } else {
                        goodMatch = true;
                    }
                }
            } while (!goodMatch && foundIdx >= 0 && foundIdx < data.Length) ;


            if (foundIdx < 0)
            {
                return false;
            }

            for (int idx = 0; idx < replacementStr.Length; idx++)
            {
                data[foundIdx + idx] = (byte)replacementStr[idx];
            }

            return true;

        }

        static void printUsage()
        {
            Console.WriteLine("Usage: BinaryStringReplacement.exe [-E (for ECL/compression mode)] stringlist.txt filetomod.exe");
        }

        static int IndexOf(byte[] src, byte[] pattern, int startIdx)
        {
            int c = src.Length - pattern.Length + 1;
            int j;
            for (int i = startIdx; i < c; i++)
            {
                if (src[i] != pattern[0]) continue;
                for (j = pattern.Length - 1; j >= 1 && src[i + j] == pattern[j]; j--) ;
                if (j == 0) return i;
            }
            return -1;
        }

        /** Next 4 functions stolen from Simeon Pilgrim at https://github.com/simeonpilgrim/coab **/
        internal string DecompressString(byte[] data)
        {
            var sb = new System.Text.StringBuilder();
            int state = 1;
            uint lastByte = 0;

            foreach (uint thisByte in data)
            {
                uint curr = 0;
                switch (state)
                {
                    case 1:
                        curr = (thisByte >> 2) & 0x3F;
                        if (curr != 0) sb.Append(inflateChar(curr));
                        state = 2;
                        break;

                    case 2:
                        curr = ((lastByte << 4) | (thisByte >> 4)) & 0x3F;
                        if (curr != 0) sb.Append(inflateChar(curr));
                        state = 3;
                        break;

                    case 3:
                        curr = ((lastByte << 2) | (thisByte >> 6)) & 0x3F;
                        if (curr != 0) sb.Append(inflateChar(curr));

                        curr = thisByte & 0x3F;
                        if (curr != 0) sb.Append(inflateChar(curr));
                        state = 1;
                        break;
                }
                lastByte = thisByte;
            }

            return sb.ToString();
        }


        internal static byte[] CompressString(string input)
        {
            byte[] data = new byte[((input.Length * 3) / 4) + 1];
            int state = 1;
            int last = 0;
            int curr = 0;

            foreach (char ch in input)
            {
                uint bits = deflateChar(ch) & 0x3F;
                if (state == 1)
                {
                    data[curr] = (byte)(bits << 2);
                    last = curr++;
                    state = 2;
                }
                else if (state == 2)
                {
                    data[last] |= (byte)(bits >> 4);
                    data[curr] = (byte)(bits << 4);
                    last = curr++;
                    state = 3;
                }
                else if (state == 3)
                {
                    data[last] |= (byte)(bits >> 2);
                    data[curr] = (byte)(bits << 6);
                    last = curr++;
                    state = 4;
                }
                else //if (state == 4)
                {
                    data[last] |= (byte)(bits);
                    state = 1;
                }

            }

            // Drop the null character off the end if it is put on there - this was not in the original Simeon Pilgrim code
            if (data[data.Length - 1] == 0)
            {
                byte[] tmp = new byte[data.Length - 1];
                Array.Copy(data, tmp, tmp.Length);
                data = tmp;
            }

            return data;
        }


        internal static uint deflateChar(char ch)
        {
            uint output = (uint)ch;

            if (output >= 0x40)
            {
                output -= 0x40;
            }
            return output;
        }

        internal char inflateChar(uint arg_0)
        {
            if (arg_0 <= 0x1f)
            {
                arg_0 += 0x40;
            }

            return (char)arg_0;
        }
    }
}

