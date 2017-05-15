/*
 * Coursework main file.
 * 
 * Author: Mikhail Kita, 371
 */

namespace Course_work
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using NuGet;

    /// <summary>
    /// Main class.
    /// </summary>
    internal class Program
    {
        private const string WorkPath = @"..\..\..\data\";
        private const string PackagesPath = @"..\..\..\packages\";

        private static readonly List<string> List = new List<string>();
        private static readonly List<string> VisitedPaths = new List<string>();
        private static readonly List<List<string[]>> Dataset = new List<List<string[]>>();

        private static void Main()
        {
            string[] files = { "0", "dll", "xml", "used", "apis" };

            foreach (var file in files)
            {
                string path = WorkPath + file + ".txt";

                if (!File.Exists(path))
                {
                    File.Create(path);
                }
            }

            Directory.CreateDirectory(WorkPath + "0\\");

            using (var reader = new StreamReader(WorkPath + "0.txt", true))
            {
                while (!reader.EndOfStream)
                {
                    VisitedPaths.Add(reader.ReadLine());
                }
            }

            //DownloadPackages();
            //Walk(PackagesPath);
            //GenerateDataset();
            LoadDataset();

            List<string> list = new List<string>();

            using (var reader = new StreamReader(@"..\..\input.txt", true))
            {
                while (!reader.EndOfStream)
                {
                    list.Add(reader.ReadLine());
                }
            }

            using (var writer = new StreamWriter(@"..\..\output.txt", true))
            {
                writer.WriteLine(GenerateCode(list));
            }

            Console.WriteLine("Finished.");
            Console.ReadLine();
        }
        
        /// <summary>
        /// Downloads NuGet packages.
        /// </summary>
        private static void DownloadPackages()
        {
            Console.WriteLine("Downloading packages...");

            // Connect to the official package repository.
            IPackageRepository repo =
                PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");

            using (var reader = new StreamReader(WorkPath + "packages.txt", true))
            {
                while (!reader.EndOfStream)
                {
                    try
                    {
                        string package = reader.ReadLine();

                        Console.WriteLine(package);

                        PackageManager packageManager = new PackageManager(repo, PackagesPath);
                        packageManager.InstallPackage(package);
                    }
                    catch (Exception)
                    {
                        // Ignored.
                    }
                }
            }
        }

        /// <summary>
        /// Copies required files from given path to work path.
        /// </summary>
        /// <param name="path">Path to files.</param>
        /// <param name="type">Type of required files.</param>
        private static void CopyFiles(string path, string type)
        {
            var di = new DirectoryInfo(path);
            FileInfo[] files = di.GetFiles("*." + type);

            foreach (FileInfo file in files)
            {
                string lowerCaseFileName = file.Name.ToLower();

                if (!List.Contains(lowerCaseFileName))
                {
                    File.Copy(path + file.Name, WorkPath + "0\\" + lowerCaseFileName, true);
                    List.Add(lowerCaseFileName);

                    using (var writer = new StreamWriter(WorkPath + type + ".txt", true))
                    {
                        writer.WriteLine(lowerCaseFileName);
                    }
                }
                else
                {
                    string existedVersion = FileVersionInfo.GetVersionInfo(WorkPath + "0\\" + file.Name).FileVersion;
                    string newVersion = FileVersionInfo.GetVersionInfo(path + file.Name).FileVersion;

                    if (string.CompareOrdinal(existedVersion, newVersion) < 0)
                    {
                        File.Copy(path + file.Name, WorkPath + "0\\" + lowerCaseFileName, true);
                    }
                }
            }
        }

        /// <summary>
        /// Finds all .dll and .xml files located in given path.
        /// </summary>
        /// <param name="path">Current path.</param>
        private static void Walk(string path)
        {
            if (VisitedPaths.Contains(path))
            {
                return;
            }

            CopyFiles(path, "dll");
            CopyFiles(path, "xml");

            using (var writer = new StreamWriter(WorkPath + "0.txt", true))
            {
                writer.WriteLine(path);
            }

            VisitedPaths.Add(path);

            // Performs Walk() to subdirectories.
            var di = new DirectoryInfo(path);
            var dirs = di.GetDirectories();

            foreach (DirectoryInfo directory in dirs)
            {
                string newPath = path + directory.Name + "\\";

                if (!VisitedPaths.Contains(newPath))
                {
                    Walk(newPath);
                }
            }
        }

        /// <summary>
        /// Creates dataset using collected files.
        /// </summary>
        private static void GenerateDataset()
        {
            Console.WriteLine("Generating data...");

            VisitedPaths.Clear();
            List.Clear();

            using (var reader = new StreamReader(WorkPath + "used.txt", true))
            {
                while (!reader.EndOfStream)
                {
                    string temp = reader.ReadLine();

                    if (temp != null)
                    {
                        VisitedPaths.Add(temp);
                    }
                }
            }

            using (var reader = new StreamReader(WorkPath + "dll.txt", true))
            {
                while (!reader.EndOfStream)
                {
                    string filename = reader.ReadLine();

                    if (filename != null && !VisitedPaths.Contains(filename))
                    {
                        List.Add(filename);
                    }
                }
            }

            DatasetGenerator gen = new DatasetGenerator();
            gen.Start(List);
        }

        /// <summary>
        /// Loads dataset from file.
        /// </summary>
        private static void LoadDataset()
        {
            using (var reader = new StreamReader(WorkPath + "apis.txt", true))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (line != null)
                    {
                        string[] array = line.Split('\t');
                        List<string[]> list = new List<string[]>();

                        foreach (string elem in array)
                        {
                            list.Add(elem.Split(' '));
                        }

                        Dataset.Add(list);
                    }
                }
            }
        }

        /// <summary>
        /// Generates code snippet by given API sequence.
        /// </summary>
        /// <param name="list">API usage sequence.</param>
        /// <returns>String, which contains generated snippet.</returns>
        private static string GenerateCode(List<string> list)
        {
            Groum groum = new Groum(list, Dataset);
            return groum.GenerateCode();
        }
    }
}