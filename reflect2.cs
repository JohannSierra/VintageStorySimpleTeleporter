using System;
using System.Reflection;
using System.IO;

class Program {
    static void Main() {
        string[] dlls = {
            @"C:\Users\redbi\AppData\Roaming\Vintagestory\Mods\VSEssentials.dll",
            @"C:\Users\redbi\AppData\Roaming\Vintagestory\Mods\VSSurvivalMod.dll",
            @"C:\Users\redbi\AppData\Roaming\Vintagestory\VintagestoryAPI.dll",
            @"C:\Users\redbi\AppData\Roaming\Vintagestory\VintagestoryLib.dll"
        };
        foreach (var dll in dlls) {
            try {
                if (!File.Exists(dll)) continue;
                Assembly asm = Assembly.LoadFrom(dll);
                foreach (var type in asm.GetTypes()) {
                    if (type.Name.Contains("Waypoint")) {
                        Console.WriteLine(dll + " -> " + type.FullName);
                        if (type.Name == "WaypointMapLayer") {
                            Console.WriteLine("FOUND IT!");
                            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                                Console.WriteLine(" Field: " + f.FieldType.Name + " " + f.Name);
                            }
                            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                                Console.WriteLine(" Prop: " + p.PropertyType.Name + " " + p.Name);
                            }
                        }
                    }
                }
            } catch {}
        }
    }
}
