using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml;

namespace BinaryStringReplacement
{
    class Program
    {
        static byte MAX_STRING_DIFFERENCE = 5;
        static byte[] data;
        static bool eclMode = false;
        static void Main(string[] args)
        {

            //foreach (byte b in writeEGABlock(new Bitmap(@"c:\Users\Shadow\Desktop\Title2.png"))) { System.Console.Write(b.ToString("X").PadLeft(2, '0')); }
            //System.Console.WriteLine();
            //System.Console.ReadLine();
            //return;

            string binFile = null;
            string transFile = null;
            if (args.Length <= 1)
            {
                printUsage();
                return;
            }

            if (args[0].Trim().StartsWith(" - "))
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
            bool goodMatch = true;
            int foundIdx = 0;
            do {
                foundIdx = IndexOf(data, replaceThisStr, foundIdx);
                if (foundIdx > 0 && checkBoundary)
                {
                    byte preceedingByte = data[foundIdx - 1];
                    if (preceedingByte > 64 && preceedingByte < 122) {
                        // This match is no good if it has a letter (ish) right before it.  String should have their length right before it so this may be a string that is inside another string
                        // Yes it is possible for a string to have a length that is between 64 & 122, but I'm not concerned about that because then it just won't get swapped
                        goodMatch = false;
                        // Advance the pointer and search again
                        foundIdx++;
                    } else {
                        goodMatch = true;
                    }

                    if (Math.Abs(preceedingByte - replaceThisStr.Length) <= MAX_STRING_DIFFERENCE)
                    {
                        // Most strings should start with their length immediately prior.  Allow for a little wiggle room.  If that is 
                        // the purpose of the byte then change it to be the new length
                        data[foundIdx - 1] = (byte)replacementStr.Length;
                        goodMatch = true;
                    } else
                    {
                        // Advance the pointer and search again because I don't want to mess with strings that aren't preceeded by their length
                        foundIdx++;
                        goodMatch = false;
                    }
                }
            } while (!goodMatch && foundIdx >= 0 && foundIdx < data.Length) ;


            if (foundIdx < 0)
            {
                return false;
            }

            // Now, find the new allowable length for the string by counting the null bytes after the string.
            int availableLength = replaceThisStr.Length;
            while (data[foundIdx + availableLength] == 0)
            {
                availableLength++;
            }

            if (replacementStr.Length > availableLength)
            {
                Console.WriteLine("Warning: truncating because replacement string \"" + System.Text.Encoding.ASCII.GetString(replacementStr) + "\" is too long.  " + availableLength + " characters are available to replace \"" + System.Text.Encoding.ASCII.GetString(replaceThisStr) + "\".");
                byte[] tmp = new byte[availableLength];
                Array.Copy(replacementStr, tmp, tmp.Length);
                replacementStr = tmp;

            }
            if (replacementStr.Length < replaceThisStr.Length)
            {
                // String is shorter than the original - pad it with spaces - leave the length the same
                byte[] tmp = new byte[replaceThisStr.Length];
                Array.Copy(replacementStr, tmp, replacementStr.Length);
                for (int idx = replacementStr.Length; idx < replaceThisStr.Length; idx++)
                {
                    tmp[idx] = (byte)' ';
                }
                replacementStr = tmp;
            }


            for (int idx = 0; idx < replacementStr.Length; idx++)
            {
                data[foundIdx + idx] = (byte)replacementStr[idx];
            }

            // Assign the new length to the preceeding byte
            data[foundIdx - 1] = (byte)replacementStr.Length;

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


        // The next three functions are used to substitute the images in the TITLE.DAX file.  They are one-offs and are not 
        // meant to be end-usable features.  Just code I used to get the data in there.  I ended up writing it all out
        // to the console and just copying and pasting the hex into my hex editor overtop the data that was already there X`D

        static byte[] writeEGABlock(Bitmap img)
        {

            ushort xPos = 8;
            ushort yPos = 0;

            MemoryStream buf = new MemoryStream();
            // Byte [0] & [1] are height
            buf.Write(UShortToArray((ushort)img.Height), 0, 2);
            // Byte [2] & [3] are width / 8
            buf.Write(UShortToArray((ushort)(img.Width / 8)), 0, 2);
            // Byte [4] & [5] are xpos / 8
            buf.Write(UShortToArray((ushort)(xPos / 8)), 0, 2);
            // Byte [6] & [7] are ypos / 8
            buf.Write(UShortToArray((ushort)(yPos / 8)), 0, 2);

            // Write the number of items in this DAX
            byte itemCount = 1;  // I only need one image per block in the the TITLE.DAX
            buf.Write(new byte[] { itemCount }, 0, 1);

            // Copied these 8 from the original TITLE.DAX - don't know what it is.  Maybe the palette
            buf.Write(new byte[] { 0x0, 0x11, 0x22, 0x13, 0x02, 0x21, 0x23, 0x33 }, 0, 8);

            for (var i = 0; i < itemCount; i++)
            {
                for (var y = 0; y < img.Height; y++)
                {
                    for (var x = 0; x < img.Width; x += 2)
                    {
                        // Note: this will not work for images with an odd width, unless GetPixel is smart enough to wrap around, which I doubt
                        Color c1 = img.GetPixel(x, yPos + y);
                        Color c2 = img.GetPixel(x + 1, yPos + y);
                        int px1Color = paletteTranslate(c1);
                        int px2Color = paletteTranslate(c2);



                        // 1st 4 bits is the first pixel's color 
                        // 2nd 4 bits is the second pixel's color
                        // Construct a byte number out of those bits that represent those two pixels
                        byte b = System.Convert.ToByte((px1Color << 4) + px2Color);

                        buf.Write(new byte[] { b }, 0, 1);
                    }
                }
            }

            return buf.ToArray();
        }


        static byte[] UShortToArray(ushort shrt)
        {
            // 16 - bit little endian
            byte secondByte = System.Convert.ToByte(shrt >> 8);   // leftmost two bits
            byte firstByte = System.Convert.ToByte(shrt & 0xFF);   // rightmost two bits
            byte[] data = new byte[2];
            data[0] = firstByte;
            data[1] = secondByte;
            return data;
        }

        static byte paletteTranslate(Color c)
        {

            // These are hard-coded colors that I stole from GBE.  They may not match all games
            uint[] EgaColors = new uint[] {
                        4278190080,
                        4278190250,
                        4278233600,
                        4278233770,
                        4289331200,
                        4289331370,
                        4289352960,
                        4289374890,
                        4283782485,
                        4283782655,
                        4283826005,
                        4283826175,
                        4294923605,
                        4294923775,
                        4294967125,
                        4294967295
            };

            for (byte idx = 0; idx < EgaColors.Length; idx++)
            {
                Color palColor = Color.FromArgb((int)EgaColors[idx]);
                // I do not know why this is, but the bitmaps that I have seen that come from GBE have
                // a slight difference in their color.  They seem to be off by 3 in the R,G,B values unless
                // the value is 255 or 0.  Idk what is going on, but I give a little leeway in choosing the
                // color from the image
                if (System.Math.Abs(palColor.R - c.R) <= 3 &&
                    System.Math.Abs(palColor.G - c.G) <= 3 &&
                    System.Math.Abs(palColor.B - c.B) <= 3)
                {
                    return idx;
                }
            }
            throw new System.Exception("Unknown Color " + (uint)c.ToArgb() + " used.");
        }

    }
}

