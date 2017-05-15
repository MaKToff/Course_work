/*
 * Dataset generator for the coursework.
 * 
 * Author: Mikhail Kita, 371
 */

namespace Course_work
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Xml;
    using ICSharpCode.Decompiler;
    using ICSharpCode.Decompiler.Ast;
    using ICSharpCode.NRefactory;
    using ICSharpCode.NRefactory.CSharp;
    using Mono.Cecil;

    /// <summary>
    /// Generates data
    /// </summary>
    internal class DatasetGenerator
    {
        private const string Path = @"..\..\..\data\";

        private static readonly object LockObj = new object();

        private DefaultAssemblyResolver Resolver { get; } = new DefaultAssemblyResolver();
        private Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();
        private List<string> ApiList { get; } = new List<string>();

        /// <summary>
        /// Initializes data extraction.
        /// </summary>
        /// <param name="filenames">List of .dll files, which should be processed.</param>
        public void Start(List<string> filenames)
        {
            //Resolver.AddSearchDirectory(Path);

            foreach (string file in filenames)
            {
                string name = file.Replace(".dll", string.Empty);

                lock (LockObj)
                {
                    using (var writer = new StreamWriter(Path + "used.txt", true))
                    {
                        writer.WriteLine(file);
                    }

                    Console.WriteLine(name);
                }

                Thread m = new Thread(Extract, 2147483647);
                m.Start(name);

                int currentResultSize = 0;
                Result.Clear();

                for (int i = 0; i < 60; ++i)
                {
                    Thread.Sleep(1000);

                    if (!m.IsAlive)
                    {
                        break;
                    }

                    if (currentResultSize < Result.Size())
                    {
                        i = 0;
                        currentResultSize = Result.Size();
                    }
                }

                Console.WriteLine("Collected: " + Result.Size());

                m.Abort();
                SaveToFile();
            }
        }

        /// <summary>
        /// Extracts summary for given method from specified XML file.
        /// </summary>
        /// <param name="fileName">Name of XML documentation file.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <returns>String, which contains summary for method.</returns>
        private string ExtractComment(string fileName, string methodName)
        {
            string summary = string.Empty;
            string path = Path + "0\\" + fileName + ".xml";

            if (!File.Exists(path))
            {
                return summary;
            }

            var xmlDoc = new XmlDocument();
            xmlDoc.Load(path);
            XmlNodeList comments = xmlDoc.GetElementsByTagName("member");

            foreach (XmlNode node in comments)
            {
                if (node.Attributes != null)
                {
                    string name = node.Attributes.GetNamedItem("name").Value;

                    if (name == "M:" + methodName && node.HasChildNodes)
                    {
                        summary = node.ChildNodes[0].InnerText;
                        break;
                    }
                }
            }

            string[] forReplace = { "\n", "\r", "\t", "  " };

            foreach (string oldValue in forReplace)
            {
                summary = summary.Replace(oldValue, " ");
            }

            return summary.Trim();
        }

        /// <summary>
        /// Creates summary for given method using its name.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <returns>String, which contains summary for given method.</returns>
        private string CreateComment(string methodName)
        {
            methodName = methodName.Remove(methodName.IndexOf('('));
            methodName = methodName.Replace("_", string.Empty);

            string[] array = methodName.Split('.');
            int n = array.Length;

            // Splits strings on separate words.
            for (int i = 0; i < n; ++i)
            {
                string processed = string.Empty;

                foreach (char character in array[i])
                {
                    if (character >= 'A' && character <= 'Z')
                    {
                        if (i == n - 1)
                        {
                            processed += " ";
                        }

                        processed += character;
                    }
                    else if (character >= 'a' && character <= 'z')
                    {
                        processed += character;
                    }
                }

                array[i] = processed;
            }

            string summary = array[n - 1];
            summary = summary.Replace("\n", " ").Replace("\r", " ").Trim().ToLower();

            if (summary != string.Empty)
            {
                string firstWord = summary.Split(' ')[0];

                if (n > 2)
                {
                    if (firstWord == "to")
                    {
                        summary = "Converts " + array[n - 2] + " " + summary;
                    }
                    else if (firstWord == "is")
                    {
                        summary = "Checks that " + array[n - 2] + " " + summary;
                    }
                    else
                    {
                        summary += summary.Count(x => x == ' ') == 0 ? " " : " for ";
                        summary += array[n - 2];
                    }
                }

                // Converts a first letter to upper.
                summary = char.ToUpper(summary[0]) + summary.Substring(1) + ".";
            }

            return summary;
        }

        /// <summary>
        /// Finds and returns first AST node with specified role.
        /// </summary>
        /// <param name="node">Parent AST node.</param>
        /// <param name="role">Node role.</param>
        /// <returns>First node with specified role.</returns>
        private AstNode FindNode(AstNode node, Role role)
        {
            return node.Children.FirstOrDefault(child => child.Role == role);
        }

        /// <summary>
        /// Removes incorrect characters in given string.
        /// </summary>
        /// <param name="input">Input string.</param>
        /// <returns>Correct string.</returns>
        private string RemoveIncorrectCharacters(string input)
        {
            string incorrect = "<>{}()\"\'?!";

            foreach (char symbol in incorrect)
            {
                if (input.Contains(symbol))
                {
                    input = input.Remove(input.IndexOf(symbol));
                }
            }

            if (input.Contains('['))
            {
                input = input.Remove(input.IndexOf('['));
                input += "[]";
            }

            return input;
        }

        /// <summary>
        /// Processes given expression.
        /// </summary>
        /// <param name="expression">AST node, which contains expression.</param>
        /// <param name="keyword"></param>
        private void ProcessExpression(AstNode expression, string keyword)
        {
            if (expression == null)
            {
                return;
            }

            string[] splittedExpr = expression.ToString().Split(' ');
            string target = splittedExpr[0];

            if (target == "new")
            {
                foreach (AstNode node in expression.Children)
                {
                    if (node.Role == Roles.Argument)
                    {
                        ProcessExpression(node, keyword);
                    }
                }

                string result = RemoveIncorrectCharacters(splittedExpr[1]);

                if (result != string.Empty)
                {
                    ApiList.Add(result + ".new");
                }

                return;
            }

            if (target.Contains("."))
            {
                string[] splittedTarget = target.Split('.');
                string name = splittedTarget[0] == "this" ? splittedTarget[0] : splittedTarget[1];
                bool hasArgs = false;

                if (Variables.ContainsKey(name))
                {
                    target = target.Replace(name, Variables[name]);
                }

                foreach (AstNode node in expression.Children)
                {
                    if (node.Role == Roles.Argument)
                    {
                        ProcessExpression(node, keyword);
                        hasArgs = true;
                    }
                }

                splittedTarget = target.Split('.');
                target = string.Empty;

                foreach (string item in splittedTarget)
                {
                    string result = RemoveIncorrectCharacters(item);

                    if (result != string.Empty)
                    {
                        if (target != string.Empty)
                        {
                            target += ".";
                        }

                        target += result;
                    }
                }

                if (hasArgs)
                {
                    target = "#args " + target;
                }
                
                if (keyword != string.Empty)
                {
                    target = "#" + keyword + " " + target;
                }

                ApiList.Add(target);
            }
        }

        /// <summary>
        /// Extracts API usage sequences from given source code.
        /// </summary>
        /// <param name="currentNode">Current node.</param>
        /// <param name="currentKeyword">Current control element.</param>
        private void VisitChildren(IEnumerable<AstNode> currentNode, string currentKeyword)
        {
            string[] keywords = { "if", "while", "for", "foreach" };

            foreach (AstNode child in currentNode)
            {
                string keyword = child.ToString().Split(' ')[0];

                if (!keywords.Contains(keyword))
                {
                    keyword = currentKeyword;
                }

                if (child.NodeType == NodeType.Statement)
                {
                    AstNode variable = FindNode(child, Roles.Variable);
                    AstNode expression = FindNode(child, Roles.Expression);

                    if (variable != null)
                    {
                        string type = FindNode(child, Roles.Type)?.ToString();
                        string name = FindNode(variable, Roles.Identifier)?.ToString();

                        if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(name) && !Variables.ContainsKey(name))
                        {
                            Variables.Add(name, type);
                        }

                        AstNode expr = FindNode(variable, Roles.Expression);

                        ProcessExpression(expr, keyword);
                    }
                    else
                    {
                        ProcessExpression(expression, keyword);
                    }
                }
                else if (child.Role == Roles.Parameter)
                {
                    string parameter = child.ToString();

                    if (parameter.Contains(" "))
                    {
                        var splittedParameter = parameter.Split(' ');

                        string type = splittedParameter.Length >= 2
                            ? splittedParameter[splittedParameter.Length - 2]
                            : string.Empty;

                        string name = splittedParameter.Length >= 2
                            ? splittedParameter[splittedParameter.Length - 1]
                            : string.Empty;

                        if (type != string.Empty && name != string.Empty && !Variables.ContainsKey(name))
                        {
                            Variables.Add(name, type);
                        }
                    }
                }

                VisitChildren(child.Children, keyword);
            }
        }

        /// <summary>
        /// Extracts source code from assembly.
        /// </summary>
        /// <param name="fileName">Assembly file name.</param>
        private void Extract(object fileName)
        {
            // Sets the resolver for dependencies.
            var parameters = new ReaderParameters
            {
                AssemblyResolver = Resolver
            };

            AssemblyDefinition assembly;

            try
            {
                assembly = AssemblyDefinition.ReadAssembly(Path + "0\\" + (string)fileName + ".dll", parameters);
            }
            catch (Exception)
            {
                return;
            }

            foreach (var type in assembly.MainModule.Types)
            {
                var fields = new Dictionary<string, string>();

                foreach (var field in type.Fields)
                {
                    if (!fields.ContainsKey(field.Name))
                    {
                        fields.Add(field.Name, field.FieldType.ToString());
                    }
                }

                foreach (var method in type.Methods)
                {
                    // Ignores nonfunctional methods.
                    if (method.IsGetter || method.IsSetter || method.IsConstructor)
                    {
                        continue;
                    }

                    // Creates builder which contains code from a single method.
                    var astBuilder = new AstBuilder(new DecompilerContext(assembly.MainModule)
                    {
                        CurrentType = type
                    });

                    try
                    {
                        astBuilder.AddMethod(method);
                        astBuilder.GenerateCode(new PlainTextOutput());
                    }
                    catch (Exception)
                    {
                       continue;
                    }

                    var tree = astBuilder.SyntaxTree;

                    Variables.Clear();
                    ApiList.Clear();

                    foreach (var field in fields)
                    {
                        Variables.Add(field.Key, field.Value);
                    }

                    VisitChildren(tree.Children, string.Empty);

                    string methodName =
                        method.FullName.Remove(0, method.FullName.IndexOf(' ') + 1).Replace("::", ".");

                    string summary = ExtractComment((string)fileName, methodName);

                    if (summary == string.Empty)
                    {
                        summary = CreateComment(methodName);
                    }

                    if (ApiList.Count != 0)
                    {
                        string result = string.Empty;

                        foreach (string item in ApiList)
                        {
                            if (item != string.Empty)
                            {
                                result += item.Replace("this", type.ToString()) + "\t";
                            }
                        }

                        result = result.Replace("\n", " ").Replace("\r", " ");

                        if (summary != string.Empty && result != string.Empty)
                        {
                            Result.Comments += summary + "\n";
                            Result.Apis += result + "\n";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves collected data into the file.
        /// </summary>
        private void SaveToFile()
        {
            lock (LockObj)
            {
                using (var writer = new StreamWriter(Path + "comments.txt", true))
                {
                    writer.Write(Result.Comments);
                }

                using (var writer = new StreamWriter(Path + "apis.txt", true))
                {
                    writer.Write(Result.Apis);
                }
            }
        }

        /// <summary>
        /// Class for collected data.
        /// </summary>
        internal static class Result
        {
            public static string Comments { get; set; } = string.Empty;
            public static string Apis { get; set; } = string.Empty;

            public static void Clear()
            {
                Comments = string.Empty;
                Apis = string.Empty;
            }

            public static int Size()
            {
                return Apis.Count(x => x == '\n');
            }
        }
    }
}