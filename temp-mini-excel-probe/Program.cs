using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        var asm = Assembly.Load("MiniExcel");
        var type = asm.GetType("MiniExcelLibs.MiniExcel");
        if (type == null) { Console.WriteLine("MiniExcel type not found"); return; }
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
        {
            Console.WriteLine($"{m.Name}{(m.IsGenericMethod ? "`" + m.GetGenericArguments().Length : "")}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) [generic={m.IsGenericMethod}]");
        }
    }
}
