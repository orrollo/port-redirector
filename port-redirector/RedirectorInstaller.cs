using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace port_redirector
{
	[RunInstaller(true)]
	public class RedirectorInstaller : Installer
	{
		public RedirectorInstaller()
		{
			var processInstaller = new ServiceProcessInstaller();
			var serviceInstaller = new ServiceInstaller();
			processInstaller.Account = ServiceAccount.LocalSystem;
			serviceInstaller.DisplayName = Program.RedirectorServiceName;
			serviceInstaller.StartType = ServiceStartMode.Automatic;
			serviceInstaller.ServiceName = Program.RedirectorServiceName;
			Installers.Add(processInstaller);
			Installers.Add(serviceInstaller);
		}
	}
}