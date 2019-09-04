﻿using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace Xamarin.Forms.DesignTimeData.BuildTasks
{
    public class DesignTimeDataTask : Task
    {
        public override bool Execute()
        {
            try
            {
                GetReferences();
                GetAssembliesToInstrument();
                Instrument();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Log.LogErrorFromException(ex);
                return false;
            }

            return true;
        }

        [Required]
        public string Assembly { get; set; }
        [Required]
        public string OutputPath { get; set; }
        public string ReferencePath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public string Enabled { get; set; }

        List<string> refpaths;

        void GetReferences()
        {
            refpaths = new List<string>();
            refpaths.Add(Path.GetFullPath(Assembly));
            var references = ReferencePath.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            foreach (var r in references)
            {
                refpaths.Add(r);
            }
        }

        List<string> toinstrument;

        void GetAssembliesToInstrument()
        {
            bool ShouldInstrument(string path)
            {                
                var name = Path.GetFileNameWithoutExtension(path);
                return name == "Xamarin.Forms";
            }

            toinstrument = new List<string>(refpaths.Where(ShouldInstrument));
        }

        LinkerAssemblyResolver asmResolver;
        AssemblyDefinition niasm;

        void Instrument()
        {
            var asmParameters = new ReaderParameters
            {
                AssemblyResolver = asmResolver,
                ReadSymbols = true,
                ReadWrite = true,
            };
            var asms = toinstrument.Select(path => {
                var guessName = new AssemblyNameReference(Path.GetFileNameWithoutExtension(path), new Version(0, 0));
                asmParameters.ReadSymbols = File.Exists(Path.ChangeExtension(path, ".pdb"));
                var asm = asmResolver.Resolve(guessName, asmParameters);
                return (asm, path);
            }).ToList();
            System.Threading.Tasks.Parallel.ForEach(asms, x => InstrumentAssembly(x.asm, x.path));
        }

        void InstrumentAssembly(AssemblyDefinition asm, string path)
        {
            // Log.LogMessage (asm.FullName);

            var changed = false;
            foreach (var m in asm.Modules)
            {
                if (m.AssemblyReferences.Any(x => x == niasm.Name))
                {
                    // Log.LogMessage ("Skipping module " + m);
                    break;
                }
                InstrumentModule(m);
                changed = true;
            }

            if (changed)
            {
                var wps = new WriterParameters
                {
                    WriteSymbols = File.Exists(Path.ChangeExtension(path, ".pdb")),
                };
                asm.Write(wps);
                Log.LogMessage($"Fixed \"{path}\"");
                asm.Dispose();
            }
        }

        void InstrumentModule(ModuleDefinition xf)
        {
            bool ShouldInstrument(string path)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name == "Xamarin.Forms.DesignTimeData";
            }

            var xfdPath = refpaths.First(ShouldInstrument);

            using (var xfd = ModuleDefinition.ReadModule(xfdPath))
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
            }         
        }

        
        class LinkerAssemblyResolver : BaseAssemblyResolver
        {
            DesignTimeDataTask task;

            Dictionary<string, AssemblyDefinition> AssemblyCache = new Dictionary<string, AssemblyDefinition>();

            public LinkerAssemblyResolver(DesignTimeDataTask buildDistTask)
            {
                task = buildDistTask;
            }

            void CacheAssembly(AssemblyDefinition a) => AssemblyCache[a.Name.Name] = a;

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                if (!AssemblyCache.TryGetValue(name.Name, out var asm))
                {
                    //task.Log.LogMessage ("Looking for " + name.Name);
                    var path = task.refpaths.FirstOrDefault(x => {
                        var rname = Path.GetFileNameWithoutExtension(x);
                        //task.Log.LogMessage ("? " + rname);
                        var eq = rname.Equals(name.Name, StringComparison.InvariantCultureIgnoreCase);
                        return eq;
                    });
                    if (path != null)
                    {
                        //task.Log.LogMessage ($"SUCCESS {path}");
                        //var stream = new MemoryStream (File.ReadAllBytes (path));
                        //var symbolPath = Path.ChangeExtension (path, ".pdb");
                        //if (File.Exists (symbolPath))
                        //parameters.SymbolStream = new MemoryStream (File.ReadAllBytes (symbolPath));
                        asm = AssemblyDefinition.ReadAssembly(path, parameters);
                        CacheAssembly(asm);
                        return asm;
                    }
                    return base.Resolve(name, parameters);
                }
                return asm;
            }
        }

    }
}
