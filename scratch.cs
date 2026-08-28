using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        try {
            Assembly asm = Assembly.LoadFrom(@"C:\Users\redbi\AppData\Roaming\Vintagestory\VintagestoryAPI.dll");
            Type t = asm.GetType("Vintagestory.API.Client.GuiComposerHelpers");
            if (t != null) {
                foreach (var m in t.GetMethods()) {
                    if (m.Name == "AddSmallButton") {
                        Console.WriteLine("AddSmallButton: " + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)));
                    }
                }
            } else { Console.WriteLine("GuiComposerHelpers not found"); }
        } catch (Exception e) { Console.WriteLine(e.Message); }
    }
}
