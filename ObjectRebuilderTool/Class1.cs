using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.SqlServer.Server;
using Newtonsoft.Json;

namespace ObjectRebuilderTool
{
    public class SchemaObject
    {
        public string Type { get; set; }
        public IEnumerable<(Type, string)> MemberInfo { get; set; } = new List<(Type, string)>();
    }

    public static class Program
    {
        private static IEnumerable<(Type, string)> generator(IEnumerable<MemberInfo> info)
        {
            foreach (var val in info)
            {
                var prop = val as PropertyInfo;
                var field = val as FieldInfo;

                yield return (prop?.PropertyType ?? field?.FieldType ?? typeof(void), val.Name);
            }
        }

        static void Main(string[] args)
        {
            var assembly =
                Assembly.LoadFile(
                    @".\SpaceEngineers.ObjectBuilders.dll");

            var stream = new FileStream("./output.log", FileMode.Create);
            Console.SetOut(new StreamWriter(stream));

            var objectList = new List<SchemaObject>();
            foreach (var type in assembly.GetTypes().Where(s => s.HasAttribute("MyObjectBuilderDefinitionAttribute")))
            {
                Console.WriteLine($"Type: {type.Name}");
                Console.WriteLine($"Ancestor: {type.BaseType}");
                Console.WriteLine("Members:");

                foreach (var nestedType in type.GetNestedTypes(BindingFlags.NonPublic).Where(s => !s.Name.Contains("m_") && s.GetInterfaces().Any(i => i.Name.Contains("IMemberAccessor"))))
                {
                    var fmtStr = FormatType(nestedType);
                    if (!String.IsNullOrWhiteSpace(fmtStr))
                    {
                        Console.WriteLine($"    {fmtStr}");
                    }
                }

                Console.WriteLine("---------------\n");
            }

            //Console.WriteLine(JsonConvert.SerializeObject(objectList, Formatting.Indented));
        }

        private static Regex nameExtractor = new Regex(@"(?<TypeName>MyObjectBuilder_[a-zA-Z]+)<>(?<MemberName>[a-zA-Z-_]+)<>Accessor");
        private static string FormatType(Type nestedType)
        {
            // Name
            var match = nameExtractor.Match(nestedType.Name);

            if (match.Success)
            {
                var name = match?.Groups["MemberName"] ??
                           throw new Exception($"Unable to extract member name from {nestedType.Name}");

                var method = nestedType.GetMethods().Single(s =>
                    s.Name == "Set" &&
                    s.GetParameters()[0].ParameterType.Name == $"{match?.Groups["TypeName"].Value}&");
                var paramType = method.GetParameters()[1];

                return $"{name} : {paramType}";
            }
            else
            {
                return "";
            }
        }
    }

    public static class TypeExtensions {
        public static bool HasAttribute<T>(this Type self) where T: Attribute
        {
            return self.GetCustomAttribute<T>() != null;
        }

        public static bool HasAttribute<T>(this MemberInfo self) where T : Attribute
        {
            return self.GetCustomAttribute<T>() != null;
        }

        public static bool HasAttribute(this Type self, string typeName)
        {
            return self.CustomAttributes.Any(s => s.AttributeType.Name == typeName);
        }
    }
}
