using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        try {
            Assembly asm = Assembly.LoadFrom(@"C:\Users\redbi\AppData\Roaming\Vintagestory\Mods\VSEssentials.dll");
            Type t = asm.GetType("Vintagestory.GameContent.WaypointMapLayer");
            if (t == null) {
                Console.WriteLine("Type not found.");
                return;
            }
            Console.WriteLine("Fields:");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                Console.WriteLine(" - " + f.FieldType.Name + " " + f.Name);
            }
            Console.WriteLine("Properties:");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                Console.WriteLine(" - " + p.PropertyType.Name + " " + p.Name);
            }
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }
    }
}
