using System;
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
            Assembly assembly;
            if (!args.Name.Contains("System.ComponentModel.Annotations"))
            {
                AssemblyName assemblyName = new AssemblyName(args.Name);
                Assembly assembly1 = null;
                AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                try
                {
                    assembly1 = Assembly.Load(assemblyName.Name);
                }
                catch
                {
                }
                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                assembly = assembly1;
            }
            else
            {
                AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                try
                {
                    Assembly assembly2 = Assembly.LoadWithPartialName("System.ComponentModel.Annotations");
                    AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                    assembly = assembly2;
                }
                catch (Exception exception1)
                {
                    Exception exception = exception1;
                    AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                    //MessageBox.Show("Failed to Load", exception.Message);
                    throw exception;
                }
            }
            return assembly;
        }
    }
}
