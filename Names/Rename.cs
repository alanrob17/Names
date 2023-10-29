// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Rename.cs" company="Software Inc.">
//   A.Robson
// </copyright>
// <summary>
//   Defines the Renamer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Names
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;

    /// <summary>
    /// The rename class.
    /// </summary>
    public class Rename
    {
        /// <summary>
        /// The main method.
        /// </summary>
        /// <param name="args">The argument list.</param>
        public static void Main(string[] args)
        {
            var argList = GetArguments(args);

            var fileDirectory = Environment.CurrentDirectory + @"\";

            if (argList.ChangeFolderName)
            {
                CreateFolderList(fileDirectory);
            }

            var fileList = GetFileList(fileDirectory, argList.SubFolder, argList.ChangeFolderName);
            var count = 0;
            
            var items = fileList.Select(file => new Item { ItemId = count++, Name = file, ChangeName = file, Changed = false }).ToList();

            ChangeWordSeparators(items);  // change word separators . - _

            ModifyStatus(items);

            if (argList.ChangeFileName)
            {
                ChangeFileNames(items);
            }

            WriteReport(items);
        }

        /// <summary>
        /// Create a list for renaming folders.
        /// </summary>
        /// <param name="folder">The directory.</param>
        private static void CreateFolderList(string folder)
        {
            var searchPattern = "*.*";

            var di = new DirectoryInfo(folder);
            var directories = di.GetDirectories(searchPattern, SearchOption.AllDirectories);

            var outFile = Environment.CurrentDirectory + "\\alan.cmd.log";
            var outStream = File.Create(outFile);
            var sw = new StreamWriter(outStream);

            foreach (var dir in directories)
            {
                var dirName = CleanUpFolderName(dir.Name);
                sw.WriteLine("ren \"{0}\" \"{1}\"", dir.FullName, dirName);
            }

            // flush and close
            sw.Flush();
            sw.Close();
        }

        /// <summary>
        /// Clean up the folder name.
        /// </summary>
        /// <param name="name">The folder name.</param>
        /// <returns>
        /// The <see cref="string"/>corrected folder name.</returns>
        private static string CleanUpFolderName(string name)
        {
            if (name.Contains('.'))
            {
                name = name.Replace('.', ' ');
            }

            if (name.Contains('_'))
            {
                name = name.Replace('_', ' ');
            }

            name = FixCase(name);

            return name;
        }

        /// <summary>
        /// Write report on what needs to be changed.
        /// </summary>
        /// <param name="items">The items.</param>
        public static void WriteReport(List<Item> items)
        {
            var outFile = Environment.CurrentDirectory + "\\alan.log";
            var outStream = File.Create(outFile);
            var sw = new StreamWriter(outStream);
            
            // TODO: delete the log file if it exists
            foreach (var item in items.Where(item => item.Changed))
            {
                sw.WriteLine("{0}\nto\n{1}\n\n", item.Name, item.ChangeName);
            }

            // flush and close
            sw.Flush();
            sw.Close();
        }

        /// <summary>
        /// Change filenames.
        /// </summary>
        /// <param name="items">The items.</param>
        private static void ChangeFileNames(IEnumerable<Item> items)
        {
            foreach (var item in items.Where(item => item.Changed))
            {
                try
                {
                  File.Move(item.Name, item.ChangeName);
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex + "\n{0}", item.ChangeName);
                }
            }
        }

        /// <summary>
        /// Check a string for a phrase and remove if found.
        /// </summary>
        /// <param name="filename">The filename as a string.</param>
        /// <param name="phrases">The phrase list.</param>
        /// <returns>The <see cref="string"/>cleaned filename.</returns>
        private static string RemovePhrase(string filename, IEnumerable<string> phrases)
        {
            foreach (var phrase in phrases.Where(phrase => filename.ToLowerInvariant().Contains(phrase)))
            {
                filename = ReplaceEx(filename, phrase, string.Empty);
            }

            return filename.Trim();
        }

        /// <summary>
        /// Modify status of changed items.
        /// </summary>
        /// <param name="items">The items.</param>
        private static void ModifyStatus(IEnumerable<Item> items)
        {
            foreach (var item in items.Where(item => item.Name != item.ChangeName))
            {
                item.Changed = true;
            }
        }

        /// <summary>
        /// The change word separators.
        /// </summary>
        /// <param name="items">The file objects.</param>
        private static void ChangeWordSeparators(IEnumerable<Item> items)
        {
            var phrases = new List<string>();
            CreatePhraseList(phrases);

            foreach (var item in items)
            {
                var line = item.ChangeName;
                var path = Path.GetDirectoryName(line);
                var filename = Path.GetFileNameWithoutExtension(line);
                var extension = Path.GetExtension(line);

                if (!string.IsNullOrEmpty(extension) && extension == extension.ToUpperInvariant())
                {
                    extension = extension.Replace(extension, extension.ToLowerInvariant());
                }

                // modify program here to add spaces into a line that has upper and lower case and no spaces
                // if the name has more than three upper case chars and more lower case chars then I will add spaces to it.
                // APracticalGuideToUbuntuLinuxMarkGSobell
                // TheSamsungGalaxyBookVol3RevisedEdition2014_softarchive.net

                filename = RemovePhrase(filename, phrases);

                // I don't want to change characters in .rar and .zip filenames (except for removing phrases)
                if (!item.Name.ToLowerInvariant().Contains(".rar") && !item.Name.ToLowerInvariant().Contains(".zip"))
                {
                    filename = ModifyName(filename, "\\.");
                    filename = ModifyName(filename, @"-");
                    filename = ModifyName(filename, @"_");
                    filename = AddSpaces(filename);

                    if (!string.IsNullOrEmpty(filename) && filename == filename.ToUpperInvariant())
                    {
                        filename = filename.ToLowerInvariant(); // .ToTitleCase() won't change uppercase filenames
                    }

                    if (!filename.Contains(".") && !filename.Contains(" "))
                    {
                        filename = FixSpaces(filename);
                    }

                    filename = ModifyName(filename, "\\s+");

                    filename = FixTerms(filename);

                    filename = FixCase(filename);

                    filename = FixTerms(filename);
                }
                
                // TODO: change this to a regular expression
                if (filename.StartsWith(".") || filename.EndsWith(".") || filename.StartsWith("_") || filename.EndsWith("_") || filename.StartsWith("-") || filename.EndsWith("-"))
                {
                    filename = FixStartEnd(filename);
                    filename = FixStartEnd(filename);
                    // TODO: some files have a couple of special characters - once again this should be fixed with Regex
                }

                var pattern = "\\s+";
                filename = Regex.Replace(filename, pattern, " ");
                
                item.ChangeName = path + "\\" + filename.Trim() + extension;

                if (item.Name != item.ChangeName)
                {
                    item.Changed = true;
                }
            }
        }

        /// <summary>
        /// Add spaces to a string of upper and lower characters.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The <see cref="string"/>cleaned string.</returns>
        private static string AddSpaces(string filename)
        {
            // var s = "TheSamsungGalaxyBookVol3RevisedEdition2014";
            var iposn = filename.IndexOf(" ", StringComparison.InvariantCulture);

            if (iposn < 0)
            {
                filename = Regex.Replace(filename, "[A-Z]", " $&");                
            }
            
            return filename;
        }

        /// <summary>
        /// Modify file name.
        /// </summary>
        /// <param name="filename">The file name.</param>
        /// <param name="pattern">The pattern.</param>
        /// <returns>The <see cref="string"/>stripped text.</returns>
        private static string ModifyName(string filename, string pattern)
        {
            var replacement = " ";

            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            
            if (!string.IsNullOrEmpty(filename))
            {
                var number = regex.Matches(filename).Count;

                if (number > 1)
                {
                    filename = Regex.Replace(filename, pattern, replacement);    
                }
                
                if (pattern == "\\s+")
                {
                    filename = Regex.Replace(filename, pattern, replacement);
                }
            }

            return filename;
        }

        /// <summary>
        /// Add spaces to a filename.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The <see cref="string"/> corrected filename.</returns>
        private static string FixSpaces(string filename)
        {
            filename = string.Concat(filename.Select(letter => char.IsUpper(letter) ? " " + letter : letter.ToString(CultureInfo.InvariantCulture))).TrimStart();

            return filename;
        }

        /// <summary>
        /// Fix the case for all words.
        /// </summary>
        /// <param name="filename">The filename string.</param>
        /// <returns>The <see cref="string"/>proper case.</returns>
        private static string FixCase(string filename)
        {
            var textInfo = new CultureInfo("en-AU", false).TextInfo;
            filename = textInfo.ToTitleCase(filename);

            return filename;
        }

        /// <summary>
        /// Remove certain characters from start and end of a string.
        /// </summary>
        /// <param name="filename">The filename string.</param>
        /// <returns>The <see cref="string"/>corrected string.</returns>
        private static string FixStartEnd(string filename)
        {
            if (filename.StartsWith(".") || filename.StartsWith("_") || filename.StartsWith("-"))
            {
                filename = filename.Substring(1);
            }

            if (filename.EndsWith(".") || filename.EndsWith("_") || filename.EndsWith("-"))
            {
                filename = filename.Substring(0, filename.Length - 1);
            }

            return filename;
        }

        /// <summary>
        /// Get a list of all files in a folder structure.
        /// </summary>
        /// <param name="folder">The folder name.</param>
        /// <param name="subFolders">The sub Folders.</param>
        /// <param name="changeFolders">The change Folders.</param>
        /// <returns>A list of text files.</returns>
        private static IEnumerable<string> GetFileList(string folder, bool subFolders, bool changeFolders)
        {
            var dir = new DirectoryInfo(folder);
            var fileList = new List<string>();

            if (subFolders)
            {
                GetFiles(dir, fileList);    
            }
            else
            {
                GetFiles(fileList);
            }
            
            return fileList;
        }

        /// <summary>
        /// Recursive list of files.
        /// </summary>
        /// <param name="d">Directory name.</param>
        /// <param name="fileList">The file List.</param>
        private static void GetFiles(DirectoryInfo d, ICollection<string> fileList)
        {
            var files = d.GetFiles("*.*");

            foreach (var fileName in files.Select(file => file.FullName))
            {
                // TODO: remove .rar and .zip extensions once I figure out how to change these filenames
                if (Path.GetExtension(fileName.ToLowerInvariant()) != ".exe" && Path.GetExtension(fileName.ToLowerInvariant()) != ".bak" && Path.GetExtension(fileName.ToLowerInvariant()) != ".log")
                {
                    fileList.Add(fileName);
                }
            }

            // get sub-folders for the current directory
            var dirs = d.GetDirectories("*.*");

            // recurse
            foreach (var dir in dirs)
            {
                // Console.WriteLine(dir.FullName);
                GetFiles(dir, fileList);
            }
        }

        /// <summary>
        /// Get list of files.
        /// </summary>
        /// <param name="fileList">The image List.</param>
        private static void GetFiles(ICollection<string> fileList)
        {
            var imageDirectory = Environment.CurrentDirectory + @"\";
            var d = new DirectoryInfo(imageDirectory);

            var files = d.GetFiles("*.*");

            foreach (var fileName in files.Select(file => file.FullName))
            {
                if (Path.GetExtension(fileName.ToLowerInvariant()) != ".exe" && Path.GetExtension(fileName.ToLowerInvariant()) != ".bak")
                {
                    fileList.Add(fileName);    
                }
            }
        }
        
        /// <summary>
        /// Get command line arguments.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The <see cref="bool"/>subfolder status.</returns>
        private static ArgList GetArguments(IList<string> args)
        {
            // TODO: add multiple arguments S = subdir, W = write only, F = change folders           
            var subFolders = false;
            var changeFileNames = false;
            var changeFolders = false;

            if (args.Count == 1)
            {
                if (args[0].ToLowerInvariant().Contains("s"))
                {
                    subFolders = true;
                }

                if (args[0].ToLowerInvariant().Contains("w"))
                {
                    changeFileNames = true;
                }

                if (args[0].ToLowerInvariant().Contains("f"))
                {
                    changeFolders = true;
                }
            }

            var argList = new ArgList(subFolders, changeFileNames, changeFolders);

            return argList;
        }

        /// <summary>
        /// ReplaceEX: a case insensitive replace method.
        /// </summary>
        /// <param name="original">original string</param>
        /// <param name="pattern">pattern to replace</param>
        /// <param name="replacement">replacement text</param>
        /// <returns>the modified string</returns>
        private static string ReplaceEx(string original, string pattern, string replacement)
        {
            int position0, position1;
            var count = position0 = position1 = 0;
            var upperString = original.ToUpper();
            var upperPattern = pattern.ToUpper();
            var inc = (original.Length / pattern.Length) * (replacement.Length - pattern.Length);
            var chars = new char[original.Length + Math.Max(0, inc)];
            while ((position1 = upperString.IndexOf(upperPattern, position0, StringComparison.Ordinal)) != -1)
            {
                for (var i = position0; i < position1; ++i)
                {
                    chars[count++] = original[i];
                }

                for (var i = 0; i < replacement.Length; ++i)
                {
                    chars[count++] = replacement[i];
                }

                position0 = position1 + pattern.Length;
            }

            if (position0 == 0)
            {
                return original;
            }

            for (var i = position0; i < original.Length; ++i)
            {
                chars[count++] = original[i];
            }

            return new string(chars, 0, count);
        } 

        /// <summary>
        /// Create a phrase list.
        /// </summary>
        /// <param name="phrases">The phrases.</param>
        private static void CreatePhraseList(ICollection<string> phrases)
        {
            // must be lowercase phrases
            phrases.Add("www.sanet.st");
            phrases.Add("softarchive.net");
            phrases.Add("softarchive.la");
            phrases.Add("sanet.st");
            phrases.Add("sanet..st");
            phrases.Add("sanet.cd");
            phrases.Add("sanet..cd");
            phrases.Add("sanet.me");
            phrases.Add("sanet..me");
            phrases.Add("snorgared");
            phrases.Add("avaxhome");
            phrases.Add("avxhom");
            phrases.Add("ebook");
        }

        /// <summary>
        /// Fix terms I have broken.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The <see cref="string"/>corrected terms.</returns>
        private static string FixTerms(string filename)
        {
            if (filename.ToLowerInvariant().Contains("asp net"))
            {
                filename = ReplaceEx(filename, "asp net", "ASP.Net");
            }

            if (filename.ToLowerInvariant().Contains("4th"))
            {
                filename = ReplaceEx(filename, "4th", "4th");
            }

            if (filename.ToLowerInvariant().Contains("5th"))
            {
                filename = ReplaceEx(filename, "5th", "5th");
            }

            if (filename.ToLowerInvariant().Contains("6th"))
            {
                filename = ReplaceEx(filename, "6th", "6th");
            }

            if (filename.ToLowerInvariant().Contains("2nd"))
            {
                filename = ReplaceEx(filename, "2nd", "2nd");
            }

            if (filename.ToLowerInvariant().Contains("3rd"))
            {
                filename = ReplaceEx(filename, "3rd", "3rd");
            }

            if (filename.ToLowerInvariant().Contains("iphone"))
            {
                filename = ReplaceEx(filename, "iphone", "iPhone");
            }

            if (filename.ToLowerInvariant().Contains("ipad"))
            {
                filename = ReplaceEx(filename, "ipad", "iPad");
            }

            if (filename.ToLowerInvariant().Contains("ipod"))
            {
                filename = ReplaceEx(filename, "ipod", "iPod");
            }

            if (filename.ToLowerInvariant().Contains("dotnet"))
            {
                filename = ReplaceEx(filename, "dotnet", ".Net");
            }

            if (filename.ToLowerInvariant().Contains("javascript"))
            {
                filename = ReplaceEx(filename, "javascript", "JavaScript");
            }

            if (filename.ToLowerInvariant().Contains("jquery"))
            {
                filename = ReplaceEx(filename, "jquery", "jQuery");
            }

            if (filename.ToLowerInvariant().Contains("csharp"))
            {
                filename = ReplaceEx(filename, "csharp", "C#");
            }

            if (filename.ToLowerInvariant().Contains("docbook"))
            {
                filename = ReplaceEx(filename, "docbook", "DocBook");
            }

            if (filename.ToLowerInvariant().Contains("powershell"))
            {
                filename = ReplaceEx(filename, "powershell", "PowerShell");
            }

            if (filename.ToLowerInvariant().Contains("couchdb"))
            {
                filename = ReplaceEx(filename, "couchdb", "CouchDB");
            }

            if (filename.ToLowerInvariant().Contains("node js"))
            {
                filename = ReplaceEx(filename, "node js", "Node.js");
            }

            if (filename.ToLowerInvariant().Contains("node.js"))
            {
                filename = ReplaceEx(filename, "node.js", "Node.js");
            }

            if (filename.ToLowerInvariant().Contains(".js"))
            {
                filename = ReplaceEx(filename, ".js", ".js");
            }

            if (filename.ToLowerInvariant().Contains("asp.net"))
            {
                filename = ReplaceEx(filename, "asp.net", "ASP.Net");
            }

            if (filename.ToLowerInvariant().Contains("ibook"))
            {
                filename = ReplaceEx(filename, "ibook", "iBook");
            }

            if (filename.ToLowerInvariant().Contains("cplusplus"))
            {
                filename = ReplaceEx(filename, "cplusplus", "C++");
            }

            if (filename.ToLowerInvariant().Contains(" ios "))
            {
                filename = ReplaceEx(filename, " ios ", " IOS ");
            }

            if (filename.ToLowerInvariant().Contains("l i n q"))
            {
                filename = ReplaceEx(filename, "l i n q", "LINQ");
            }

            if (filename.ToLowerInvariant().Contains("s q l"))
            {
                filename = ReplaceEx(filename, "s q l", "SQL");
            }

            if (filename.ToLowerInvariant().Contains("r e a d m e"))
            {
                filename = ReplaceEx(filename, "r e a d m e", "README");
            }

            if (filename.ToLowerInvariant().Contains("u k "))
            {
                filename = ReplaceEx(filename, "u k ", "UK ");
            }

            if (filename.ToLowerInvariant().Contains("p c "))
            {
                filename = ReplaceEx(filename, "p c ", "PC ");
            }

            if (filename.ToLowerInvariant().Contains("oreilly"))
            {
                filename = ReplaceEx(filename, "oreilly", "O'Reilly");
            }
            
            if (filename.ToLowerInvariant().Contains("a novel"))
            {
                filename = filename.ToLowerInvariant().Replace("a novel", string.Empty);
            }

            return filename;
        }
    }
}
