using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;


namespace HyparIrizer
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Get assembly name
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";

            // Get resource name
            var resourceName = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(x => x.EndsWith(".dll")).ToArray().FirstOrDefault(x => x.EndsWith(assemblyName));
            if (resourceName == null)
            {
                return null;
            }

            // Load assembly from resource
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
            }
           
        }
    }
}
