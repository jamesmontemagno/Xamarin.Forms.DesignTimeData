﻿using Mono.Cecil;
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
                using (var yourassembly = ModuleDefinition.ReadModule("Xamarin.Forms.DesignTimeData.dll"))
                {
                    var dtd = yourassembly.GetType("DesignTimeData");
                    var is_enabled_def = dtd.Methods.First(m => m.Name == "get_IsEnabled");

                    var is_enabled = xf.ImportMethod(is_enabled_def);
                    var dm = xf.GetType("DesignMode");

                    var dm_enabled = dm.Methods.First(m => m.Name == "get_IsDesignModeEnabled");

                    var il = dm_enabled.Body = new MethodBody(dm).GetILProcessor();
                    il.Emit(OpCodes.Call, is_enabled);
                    il.Emit(OpCodes.Ret);
                    xf.Write("Xamarin.Forms.dll");
                }
            }
        }
    }
}
