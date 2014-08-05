using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace port_redirector
{
	class Program : ServiceBase
	{
		public const string RedirectorServiceName = "port_redirector";

		public Program()
		{
			ServiceName = RedirectorServiceName;
		}

		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += excHandler;

			if (args.Length > 0)
			{
				foreach (var arg in args)
				{
					var upper = arg.ToUpper();
					if (upper == "-I" || upper == "/I")
					{
						Install(); 
						return;
					}
					if (upper == "-U" || upper == "/U")
					{
						Uninstall();
						return;
					}
				}
				Redirector.CommandLine = args;
			}
			if (System.Environment.UserInteractive)
			{
				Console.WriteLine("press enter to stop...");
				Console.CancelKeyPress += Console_CancelKeyPress;
				Redirector.Start();
				Console.ReadLine();
				DoStop();
			}
			else
			{
				Run(new Program());
			}
		}

		protected override void OnStart(string[] args)
		{
			Redirector.CommandLine = args;
			Redirector.Start();
			base.OnStart(args);
		}

		protected override void OnStop()
		{
			DoStop();
			base.OnStop();
		}

		private static void Uninstall()
		{
			ManagedInstallerClass.InstallHelper(new[] { "/u", GetLocation() });
		}

		private static string GetLocation()
		{
			return Assembly.GetExecutingAssembly().Location;
		}

		private static void Install()
		{
			ManagedInstallerClass.InstallHelper(new[] { GetLocation() });
		}

		private static void excHandler(object sender, UnhandledExceptionEventArgs e)
		{
			Debug.WriteLine(string.Format("unhandled exception: {0}", e.ExceptionObject));
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			DoStop();
		}

		private static void DoStop()
		{
			Redirector.Stop();
			Thread.Sleep(1500);
		}
	}
}
