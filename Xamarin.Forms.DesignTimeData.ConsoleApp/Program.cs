using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace Xamarin.Forms.DesignTimeData.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var xf = ModuleDefinition.ReadModule("Xamarin.Forms.dll"))
            {
                using (var xfd = ModuleDefinition.ReadModule("Xamarin.Forms.DesignTimeData.dll"))
                {
                    var dtd = xfd.GetType("Xamarin.Forms.DesignTimeData");
                    var is_enabled_def = dtd.Methods.First(m => m.Name == "get_IsEnabled");

                    var is_enabled = xf.ImportReference(is_enabled_def);
                    var dm = xf.GetType("Xamarin.Forms.DesignMode");

                    var dm_enabled = dm.Methods.First(m => m.Name == "get_IsDesignModeEnabled");

                    dm_enabled.Body = new MethodBody(dm_enabled);
                    var il = dm_enabled.Body.GetILProcessor();
                    il.Emit(OpCodes.Call, is_enabled);
                    il.Emit(OpCodes.Ret);
                    xf.Write("Xamarin.Forms.dll");
                }
            }
        }
    }
}
